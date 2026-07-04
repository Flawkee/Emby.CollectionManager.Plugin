#!/usr/bin/env python3
"""Build and stage the Collection Manager DLL onto an Emby TrueNAS/HexOS app.

This intentionally does not store credentials. By default it reads the TrueNAS API
key from Abdiel's approved Vaultwarden helper:

    ~/.local/bin/vw-secret hexos-truenas api_key

Environment overrides:
    TRUENAS_BASE       https://hexos/api/v2.0
    TRUENAS_API_KEY    explicit API key, if not using the helper
    REMOTE_DLL         live plugin DLL path
    REMOTE_CONFIG      optional plugin config XML path to back up

The script backs up the live DLL/config, uploads the local Release DLL, and
verifies the remote SHA256 matches. Restart/redeploy Emby once after this script
finishes, then run the Collection Manager scheduled task and live checks.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import ssl
import subprocess
import sys
import time
import urllib.request
from datetime import datetime
from pathlib import Path

DEFAULT_REMOTE_DLL = "/mnt/.ix-apps/app_mounts/emby/config/plugins/Emby.CollectionManager.Plugin.dll"
DEFAULT_REMOTE_CONFIG = "/mnt/.ix-apps/app_mounts/emby/config/plugins/configurations/Emby.CollectionManager.Plugin.xml"


def run(cmd: list[str], *, env: dict[str, str] | None = None) -> None:
    subprocess.run(cmd, check=True, env=env)


def output(cmd: list[str]) -> str:
    return subprocess.check_output(cmd, text=True).strip()


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


class TrueNasClient:
    def __init__(self, base: str, token: str) -> None:
        self.base = base.rstrip("/")
        self.headers = {"Authorization": f"Bearer {token}"}
        self.ssl_context = ssl._create_unverified_context()

    def request(self, method: str, path: str, data: object | None = None) -> object | None:
        body = None
        headers = dict(self.headers)
        if data is not None:
            body = json.dumps(data).encode()
            headers["Content-Type"] = "application/json"
        req = urllib.request.Request(self.base + path, data=body, headers=headers, method=method)
        with urllib.request.urlopen(req, context=self.ssl_context, timeout=120) as resp:
            payload = resp.read()
        return json.loads(payload.decode()) if payload else None

    def poll_job(self, job_id: int, label: str) -> None:
        deadline = time.time() + 180
        last = None
        while time.time() < deadline:
            job_result = self.request("GET", f"/core/get_jobs?id={job_id}")
            job = job_result[0] if isinstance(job_result, list) and job_result else job_result
            last = job
            state = job.get("state") if isinstance(job, dict) else None
            if state in ("SUCCESS", "FAILED", "ABORTED"):
                if state != "SUCCESS":
                    raise RuntimeError(f"{label} job {job_id} ended {state}: {job}")
                return
            time.sleep(1)
        raise TimeoutError(f"{label} job {job_id} timed out; last={last}")

    def get_file(self, remote_path: str) -> bytes:
        result = self.request(
            "POST",
            "/core/download",
            {"method": "filesystem.get", "args": [remote_path], "filename": Path(remote_path).name},
        )
        if not (isinstance(result, list) and len(result) >= 2 and isinstance(result[0], int)):
            raise RuntimeError(f"Unexpected core/download response: {result!r}")
        job_id = result[0]
        url = str(result[1])
        if url.startswith("/"):
            url = self.base.split("/api/")[0] + url
        req = urllib.request.Request(url, headers=self.headers, method="GET")
        with urllib.request.urlopen(req, context=self.ssl_context, timeout=180) as resp:
            payload = resp.read()
        self.poll_job(job_id, f"download {remote_path}")
        return payload

    def put_file(self, local_path: Path, remote_path: str) -> object:
        data = json.dumps({"path": remote_path, "options": {"append": False, "mode": 420}})
        cmd = [
            "curl",
            "-ksS",
            "-H",
            "Authorization: " + self.headers["Authorization"],
            "-F",
            f"data={data};type=application/json",
            "-F",
            f"file=@{local_path};filename={Path(remote_path).name}",
            self.base + "/filesystem/put",
        ]
        proc = subprocess.run(cmd, text=True, capture_output=True, timeout=180)
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr.strip() or proc.stdout.strip())
        text = proc.stdout.strip()
        parsed: object = True if not text else json.loads(text)
        if isinstance(parsed, int):
            self.poll_job(parsed, f"upload {remote_path}")
        elif isinstance(parsed, dict) and isinstance(parsed.get("job_id"), int):
            self.poll_job(parsed["job_id"], f"upload {remote_path}")
        return parsed


def api_key() -> str:
    if os.environ.get("TRUENAS_API_KEY"):
        return os.environ["TRUENAS_API_KEY"].strip()
    return output([str(Path.home() / ".local/bin/vw-secret"), "hexos-truenas", "api_key"])


def build_dll(repo: Path, skip_build: bool) -> Path:
    env = os.environ.copy()
    dotnet_root = str(Path.home() / ".dotnet")
    env["DOTNET_ROOT"] = env.get("DOTNET_ROOT", dotnet_root)
    env["PATH"] = dotnet_root + os.pathsep + env.get("PATH", "")
    if not skip_build:
        run(["dotnet", "restore", "Emby.CollectionManager.Plugin.csproj", "-r", "linux-x64"], env=env)
        run(["dotnet", "build", "Emby.CollectionManager.Plugin.csproj", "--no-restore", "-c", "Release", "-r", "linux-x64", "-v:minimal"], env=env)
    runtime_dll = repo / "bin/Release/netstandard2.0/linux-x64/Emby.CollectionManager.Plugin.dll"
    plain_dll = repo / "bin/Release/netstandard2.0/Emby.CollectionManager.Plugin.dll"
    if runtime_dll.exists():
        return runtime_dll
    if plain_dll.exists():
        return plain_dll
    raise FileNotFoundError("Release DLL not found after build")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--skip-build", action="store_true", help="upload the existing Release DLL without rebuilding")
    parser.add_argument("--remote-dll", default=os.environ.get("REMOTE_DLL", DEFAULT_REMOTE_DLL))
    parser.add_argument("--remote-config", default=os.environ.get("REMOTE_CONFIG", DEFAULT_REMOTE_CONFIG))
    args = parser.parse_args()

    repo = Path(__file__).resolve().parents[1]
    dll = build_dll(repo, args.skip_build)
    local_sha = sha256_file(dll)
    timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    work = Path("/tmp") / f"emby-plugin-hotpatch-{timestamp}"
    work.mkdir(parents=True, exist_ok=True)

    client = TrueNasClient(os.environ.get("TRUENAS_BASE", "https://hexos/api/v2.0"), api_key())
    info = client.request("GET", "/system/info")
    if isinstance(info, dict):
        print(f"TrueNAS: {info.get('hostname')} {info.get('version')}")
    print(f"Local DLL: {dll}")
    print(f"Local SHA256: {local_sha}")

    old_dll = client.get_file(args.remote_dll)
    old_dll_path = work / "Emby.CollectionManager.Plugin.dll.live-before"
    old_dll_path.write_bytes(old_dll)
    dll_backup = args.remote_dll + ".bak-" + timestamp
    client.put_file(old_dll_path, dll_backup)
    print(f"Backed up DLL: {dll_backup} ({sha256_bytes(old_dll)})")

    if args.remote_config:
        try:
            old_config = client.get_file(args.remote_config)
            old_config_path = work / "Emby.CollectionManager.Plugin.xml.live-before"
            old_config_path.write_bytes(old_config)
            config_backup = args.remote_config + ".bak-" + timestamp
            client.put_file(old_config_path, config_backup)
            print(f"Backed up config: {config_backup}")
        except Exception as exc:  # config can be missing in fresh installs
            print(f"Config backup skipped: {exc}")

    client.put_file(dll, args.remote_dll)
    remote_sha = sha256_bytes(client.get_file(args.remote_dll))
    print(f"Remote SHA256: {remote_sha}")
    if remote_sha != local_sha:
        raise RuntimeError(f"Remote hash mismatch: local={local_sha} remote={remote_sha}")
    print("Upload verified. Restart/redeploy Emby once, then run live checks.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
