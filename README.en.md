# Game Trainer Launcher

[中文版](README.md)

A Windows desktop launcher focused on improving the `FlingTrainer` workflow for searching, downloading, organizing, and launching game trainers.

## Core Features

- Search by Chinese or English game title with a local title index plus background backfill.
- Browse popular trainers and add them to the local library.
- Choose trainer versions before download to reduce version mismatch problems.
- Manage a local trainer library with ordering, launch, removal, cover backfill, and status feedback.
- Switch language and theme, and check for in-app updates.

## Tech Stack and Structure

- Runtime: `.NET 8`, `WPF`, Windows only
- UI: `WPF-UI` + `CommunityToolkit.Mvvm`
- Data: `SQLite` + `Entity Framework Core 8`
- Scraping and integrations: `HtmlAgilityPack`, local scanning, download, and launch services

Main directories:

- `GameTrainerLauncher.Core`: domain entities and interfaces
- `GameTrainerLauncher.Infrastructure`: data, scraping, scanning, download, and launch implementations
- `GameTrainerLauncher.UI`: WPF UI and application entry point
- `Docs/site`: website source
- `installer`: local packaging and release helper scripts

## Quick Start

Requirements: Windows 10/11, `.NET 8 SDK`

```powershell
dotnet restore
dotnet build GameTrainerLauncher.UI/GameTrainerLauncher.UI.csproj
dotnet run --project GameTrainerLauncher.UI
```

Application data is stored under `%LocalAppData%\GameTrainerLauncher\Data` by default.

## Commit Convention

This repository uses [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/).

After the first clone, install commitlint and the Husky hook:

```powershell
npm install
```

Validate the current history manually:

```powershell
npm run commitlint:history -- HEAD
```

## Documentation Map

- [AGENTS.md](AGENTS.md): repository guidance for coding agents
- [README.md](README.md): Chinese project overview
- [Docs/README.md](Docs/README.md): documentation map and boundaries
- `Docs/site`: website source, not a development manual
- `designs/`: local design-archive directory, not committed by default

## License

This project is licensed under the [GNU General Public License v3.0 (GPL-3.0)](LICENSE).
