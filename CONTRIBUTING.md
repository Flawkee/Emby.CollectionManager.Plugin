# Contributing

Thanks for helping improve Collection Manager. This project is an Emby plugin, so changes should be safe for normal Emby users and not depend on one maintainer's local server layout.

## Before opening a pull request

1. Keep the PR focused on one problem or feature.
2. Avoid local/private environment assumptions in code, docs, screenshots, and PR text.
3. Do not include private hostnames, local absolute paths, tokens, backup paths, or deployment scripts that only work on your server.
4. Prefer generic Emby instructions that work across Windows, macOS, Linux, and container installs.
5. If UI changes affect users, include screenshots or clear before/after notes when practical.

## Local verification

Run the core checks before submitting:

```bash
dotnet restore Emby.CollectionManager.Plugin.csproj
dotnet build Emby.CollectionManager.Plugin.csproj --configuration Release /p:SkipLocalPluginCopy=true
node --check Configuration/adminconfigpage.js
node --check Configuration/configurationpage.js
```

If your branch includes the test project, also run:

```bash
dotnet test CollectionManager.Plugin.Tests/CollectionManager.Plugin.Tests.csproj --configuration Release
```

## Cross-platform expectations

- Windows local-development copy behavior should remain Windows-only.
- macOS and Linux builds should not require `copy`, `xcopy`, PowerShell-only commands, hard-coded user directories, or container-specific paths.
- CI should pass on Windows, macOS, and Linux.
- Documentation should point users to their Emby program data path rather than assuming a specific NAS, Docker, or host layout.

## PR description checklist

Include:

- Summary of the user-visible change.
- Verification commands you actually ran.
- Any known limitations.
- Whether the change affects existing collections/playlists or only future runs.

Avoid:

- Private server names or domains.
- Local filesystem paths from your own machine or server.
- Secrets, tokens, screenshots with identifying details, or deployment-only evidence.
- AI/model/tool attribution sections unless the maintainer asks for them.
