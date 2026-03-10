# Game Trainer Launcher

A Windows Desktop Application to manage game trainers from FlingTrainer.

## Prerequisites

- .NET 8 SDK
- Windows 10/11

## Project Structure

- **GameTrainerLauncher.Core**: Domain entities and interfaces.
- **GameTrainerLauncher.Infrastructure**: Implementation of services (Scraping, Scanning, DB).
- **GameTrainerLauncher.UI**: WPF User Interface (MVVM).

## How to Run

1. Open the solution in Visual Studio or VS Code.
2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```
3. Build the project:
   ```bash
   dotnet build
   ```
4. Run the UI project:
   ```bash
   dotnet run --project GameTrainerLauncher.UI
   ```

## Features

- **Popular Games**: Fetches latest trainers from FlingTrainer.
- **My Games**: Scans local Steam/Epic games and matches them with trainers.
- **Search**: Search for any trainer on FlingTrainer.
- **Auto-Download**: Downloads and extracts trainers automatically.

## Notes

- The application uses `HtmlAgilityPack` to scrape FlingTrainer. If the site layout changes, the scraper might need updates.
- Trainer ZIPs are downloaded to `%LOCALAPPDATA%\GameTrainerLauncher\Trainers`.
- Logs are stored in `%LOCALAPPDATA%\GameTrainerLauncher\logs`.
