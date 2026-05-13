# Collection Manager

An [Emby](https://emby.media/) plugin that automatically curates collections and binge-watch playlists from your library metadata, plus per-user smart playlists that each user configures themselves.

## Features

### Streaming Service Collections (shared)
Detects movies and shows from major streaming services in your library's `studio` metadata and groups them into named collections with native logos.

Supported services: Netflix, Amazon Prime Video, Disney+, Hulu, HBO Max, Apple TV+, Paramount+, MGM+, YouTube TV, Sling TV, Discovery+, ESPN+.

Item with multiple studios (e.g. an HBO Max original distributed through Netflix) appears in every matching collection.

### Movie Franchise Binge Playlists (per user, private)
Creates a `<Franchise> Franchise` playlist for each user with all movies in a franchise, sorted in release order (e.g. _The Hobbit Franchise_, _Pirates of the Caribbean Franchise_).

Two detection modes:
- **With a free [TMDB](https://themoviedb.org) API key (recommended)** — uses TMDB's `belongs_to_collection` data for the most accurate grouping. Get one at _Settings → API → Request an API Key → Developer_.
- **Without a key** — falls back to shared name-prefix matching (e.g. `Avatar` + `Avatar: The Way of Water` → `Avatar Franchise`).

### TV Universe Binge Playlists (per user, private)
Creates a `<Universe> Universe` playlist for each user, grouping shows with franchise relationships scraped from [TVMaze](https://tvmaze.com) (e.g. _Game of Thrones Universe_ groups Game of Thrones + House of the Dragon + A Knight of the Seven Kingdoms).

Only `Franchise / Prequel / Sequel / Spin-off / Companion Series` relationships are included — After Shows, talk shows, and reunion specials are excluded.

### Dynamic User Playlists (per user)
Each user gets a **Dynamic Playlists** entry in their user menu (top-right) where they can define unlimited smart playlists with these filters:

- Content type (Movies / TV Shows / Both)
- Genres, Studios, Years, Parental Ratings, Tags (multi-select from your library)
- Play state (Any / Watched / Unwatched)
- Favorites (Any / Favorites only / Non-favorites)
- Series Status (Any / Continuing / Ended)
- Max items

Playlists are rebuilt at every scheduled-task run and immediately after each user save. Disabling a playlist removes it on the next run while keeping its configuration so it can be re-enabled later.

From the same page each user can also opt out of receiving the binge (Franchise / Universe) playlists.

## Installation

1. Build the project (see below) or download the latest `Emby.CollectionManager.Plugin.dll`.
2. Drop the DLL into `<Emby programdata>/plugins/`.
3. Restart Emby Server.
4. Open _Dashboard → Plugins → Collection Manager_ and configure.

## How It Works

A scheduled task named **Collection Manager** runs every 24 hours and after any save to the admin or user configuration. Phase order:

1. **Dynamic User Playlists** — read each user's config, rebuild enabled playlists, delete disabled-or-removed ones.
2. **Movie Franchises** — detect franchises (TMDB or name-prefix), build a private playlist per user who has opted in.
3. **TV Universes** — detect universes via TVMaze, build a private playlist per user who has opted in.
4. **Streaming Service Collections** — scan studios and build shared collections with logos.

Disabling any feature at the admin level removes the matching playlists / collections for every user on the next run.

## Configuration

### Admin (server)
_Dashboard → Plugins → Collection Manager_

- **Dynamic Playlists** — Enable per-user smart playlists.
- **Movie Franchises** — Enable franchise binge playlists + TMDB API key.
- **TV Universes** — Enable universe binge playlists + Playlists library image.
- **Streaming Collections** — Enable streaming collections, include movies/TV, Collections library image.
- **Diagnostics** — Verbose debug logging.

Saving triggers an immediate task run.

### Per-user (Dynamic Playlists page)
_User menu (top-right) → Dynamic Playlists_

- Receive Movie Franchise binge playlists (toggle)
- Receive TV Universe binge playlists (toggle)
- Add / edit / delete dynamic playlists (unlimited)

Saving triggers an immediate task run.

## Build

Requires .NET SDK and the [Emby plugin SDK NuGet packages](https://www.nuget.org/packages/MediaBrowser.Server.Core/).

```powershell
dotnet build
```

The post-build step copies the DLL to `%AppData%\Emby-Server\programdata\plugins\` for local testing.

## Project Layout

```
Configuration/         Admin + user HTML/JS pages, POCO config classes
Helpers/               CollectionHelper, PlaylistHelper, DynamicPlaylistHelper, LibraryScanner
ScheduledTasks/        CollectionManagerTask (the 4-phase task)
Services/              UserPlaylistsService (per-user IService REST endpoint)
logos/                 Embedded streaming service / playlists logos
```

## License

[MIT](LICENSE) © Flawkee
