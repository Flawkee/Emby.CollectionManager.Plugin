## Summary

-

## Verification

- [ ] `dotnet restore Emby.CollectionManager.Plugin.csproj`
- [ ] `dotnet build Emby.CollectionManager.Plugin.csproj --configuration Release /p:SkipLocalPluginCopy=true`
- [ ] `node --check Configuration/adminconfigpage.js`
- [ ] `node --check Configuration/configurationpage.js`
- [ ] `dotnet test CollectionManager.Plugin.Tests/CollectionManager.Plugin.Tests.csproj --configuration Release` if this branch includes tests

## User impact

Describe whether this changes existing collections/playlists, only future scheduled runs, or only documentation/build behavior.

## Public-safety checklist

- [ ] This PR does not include private hostnames, domains, tokens, backup paths, or local-only deployment scripts.
- [ ] Docs and defaults are useful for general Emby users across supported environments.
- [ ] Any screenshots/logs have private information removed.
