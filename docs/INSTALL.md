# Manual install and testing

This guide is for users who want to test a release or a pull-request build before it is available through the Emby Plugin Catalog.

## Compatibility

- Emby Server 4.8.x or newer is recommended.
- The plugin targets `netstandard2.0` and is intended to run anywhere Emby Server runs.
- Builds are checked on Windows, macOS, and Linux in CI.

## Build from source

Install the [.NET SDK](https://dotnet.microsoft.com/download), then run:

```bash
dotnet restore Emby.CollectionManager.Plugin.csproj
dotnet build Emby.CollectionManager.Plugin.csproj --configuration Release /p:SkipLocalPluginCopy=true
```

The DLL will be created at:

```text
bin/Release/netstandard2.0/Emby.CollectionManager.Plugin.dll
```

If a test project is present on your branch, run:

```bash
dotnet test CollectionManager.Plugin.Tests/CollectionManager.Plugin.Tests.csproj --configuration Release
```

## Find the Emby plugins folder

The exact plugin path depends on how Emby Server was installed. In the Emby dashboard, open:

```text
Dashboard → Logs
```

The server startup log usually shows the program data path. The plugin folder is named `plugins` under that program data directory.

Common examples:

| Platform | Common plugin folder |
|---|---|
| Windows portable / standard install | `%AppData%\Emby-Server\programdata\plugins` |
| Linux package install | `/var/lib/emby/plugins` |
| Linux user install | `~/.config/emby-server/plugins` |
| macOS | `~/.config/emby-server/plugins` or the program data path shown in logs |
| Docker / containers | The `plugins` folder inside the mounted Emby config/programdata volume |

If these examples do not match your setup, use the path shown in your Emby logs rather than guessing.

## Install or update the plugin manually

1. Stop Emby Server.
2. Back up any existing `Emby.CollectionManager.Plugin.dll` from the plugins folder.
3. Copy the new `Emby.CollectionManager.Plugin.dll` into the plugins folder.
4. Start Emby Server.
5. Open `Dashboard → Plugins` and confirm **Collection Manager** is listed.
6. Open `Dashboard → Plugins → Collection Manager` and review the configuration.
7. Run the **Collection Manager** scheduled task manually once for testing.

## Uninstall or roll back

1. Stop Emby Server.
2. Remove `Emby.CollectionManager.Plugin.dll` from the plugins folder, or replace it with the backed-up DLL.
3. Start Emby Server.
4. Check the Emby server log for plugin load errors.

## Troubleshooting

### The plugin does not appear

- Confirm the DLL is directly inside the `plugins` folder, not inside a nested folder.
- Restart Emby Server after copying the DLL.
- Check the Emby server log for plugin load or dependency errors.

### The build succeeds but the DLL is not copied automatically

Automatic local copy is only for Windows developer installs. For portable cross-platform builds, use:

```bash
dotnet build Emby.CollectionManager.Plugin.csproj --configuration Release /p:SkipLocalPluginCopy=true
```

Then copy the DLL manually from `bin/Release/netstandard2.0/`.

### Dynamic Playlists page is not visible in an app

The per-user Dynamic Playlists page is available from the Emby web interface. Some Emby apps do not expose plugin configuration pages in their native UI.

### Collections or playlists are not created

- Confirm the relevant feature is enabled in the plugin admin page.
- Run the **Collection Manager** scheduled task manually.
- Check the Emby server log for Collection Manager messages.
- For TMDB-powered franchise matching, confirm the TMDB API key is present and valid.
- For metadata-dependent matches, refresh metadata for a small library sample and try again.
