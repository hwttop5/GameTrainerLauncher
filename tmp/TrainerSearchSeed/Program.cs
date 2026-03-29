using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure;
using GameTrainerLauncher.Infrastructure.Data;
using GameTrainerLauncher.Infrastructure.Services;
using GameTrainerLauncher.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

ProxyEnvironmentBootstrapper.Configure();

var command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "search";
var value = args.Length > 1 ? args[1].Trim() : "怪物猎人";
int? maxRows = null;
if (args.Length > 2 && int.TryParse(args[2], out var parsedMaxRows) && parsedMaxRows > 0)
{
    maxRows = parsedMaxRows;
}

var services = new ServiceCollection();
services.AddDbContext<AppDbContext>();
services.AddSingleton<IScraperService, FlingScraperService>();
services.AddSingleton<IGameTitleMetadataService, GamerskyMetadataService>();
services.AddSingleton<ISteamStoreMetadataService, SteamStoreMetadataService>();
services.AddSingleton<IAppSettingsService, AppSettingsService>();
services.AddSingleton<ITrainerTitleSnapshotService, TrainerTitleSnapshotService>();
services.AddSingleton<ITrainerTitleSyncService, TrainerTitleSyncService>();
services.AddSingleton<ITrainerSearchService, TrainerSearchService>();

await using var provider = services.BuildServiceProvider();

return command switch
{
    "stats" => await PrintStatsAsync(provider),
    "sync" => await RunSyncAsync(provider),
    "search" => await RunSearchAsync(provider, value),
    "keyword-sync" => await RunKeywordSyncAsync(provider, value),
    "steam-backfill" => await RunSteamBackfillAsync(provider, maxRows),
    "gamersky-search" => await RunGamerskySearchAsync(provider, value),
    "gamersky-detail" => await RunGamerskyDetailAsync(provider, value),
    "snapshot-export" => await RunSnapshotExportAsync(provider),
    "snapshot-export-seed" => await RunSnapshotExportSeedAsync(provider),
    "snapshot-import-seed-if-needed" => await RunSnapshotImportSeedIfNeededAsync(provider),
    "snapshot-import" => await RunSnapshotImportAsync(provider),
    "snapshot-verify" => await RunSnapshotVerifyAsync(provider),
    _ => await ShowUsageAsync(command)
};

static async Task<int> PrintStatsAsync(ServiceProvider provider)
{
    await using var scope = provider.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.EnsureTrainerTitleIndexSchemaAsync();

    var total = await db.TrainerTitleIndexEntries.CountAsync();
    var active = await db.TrainerTitleIndexEntries.CountAsync(row => row.IsActive);
    var matched = await db.TrainerTitleIndexEntries.CountAsync(row => row.IsActive && row.MatchStatus == "Matched");
    var chinese = await db.TrainerTitleIndexEntries.CountAsync(row => row.IsActive && row.ChineseName != null && row.ChineseName != string.Empty);
    var gamersky = await db.TrainerTitleIndexEntries.CountAsync(row => row.IsActive && row.MetadataSource == "Gamersky");
    var steam = await db.TrainerTitleIndexEntries.CountAsync(row => row.IsActive && row.MetadataSource == "Steam");

    Console.WriteLine($"total={total}; active={active}; matched={matched}; chinese={chinese}; gamersky={gamersky}; steam={steam}");

    var samples = await db.TrainerTitleIndexEntries
        .AsNoTracking()
        .Where(row => row.IsActive && row.ChineseName != null && row.ChineseName != string.Empty)
        .OrderBy(row => row.ChineseName)
        .Take(10)
        .Select(row => new
        {
            row.FlingTitle,
            row.EnglishName,
            row.ChineseName,
            row.MetadataSource,
            row.MatchConfidence
        })
        .ToListAsync();

    foreach (var sample in samples)
    {
        Console.WriteLine($"{sample.FlingTitle} || {sample.EnglishName} || {sample.ChineseName} || {sample.MetadataSource} || {sample.MatchConfidence}");
    }

    return 0;
}

static async Task<int> RunSyncAsync(ServiceProvider provider)
{
    var syncService = provider.GetRequiredService<ITrainerTitleSyncService>();
    await syncService.EnsureSynchronizedAsync(force: true);
    return await PrintStatsAsync(provider);
}

static async Task<int> RunSearchAsync(ServiceProvider provider, string keyword)
{
    if (string.IsNullOrWhiteSpace(keyword))
    {
        Console.Error.WriteLine("Keyword is required.");
        return 2;
    }

    var searchService = provider.GetRequiredService<ITrainerSearchService>();
    var result = await searchService.SearchAsync(keyword);

    Console.WriteLine($"keyword={keyword}; count={result.Trainers.Count}; incomplete={result.IndexMayBeIncomplete}; updating={result.IsIndexUpdating}");
    foreach (var trainer in result.Trainers.Take(10))
    {
        Console.WriteLine($"{trainer.DisplayTitle} || {trainer.SecondaryDisplayTitle} || {trainer.Title} || {trainer.PageUrl}");
    }

    return result.Trainers.Count > 0 ? 0 : 3;
}

static async Task<int> RunKeywordSyncAsync(ServiceProvider provider, string keyword)
{
    if (string.IsNullOrWhiteSpace(keyword))
    {
        Console.Error.WriteLine("Keyword is required.");
        return 2;
    }

    var syncService = provider.GetRequiredService<ITrainerTitleSyncService>();
    await syncService.EnsureKeywordSynchronizedAsync(keyword);
    Console.WriteLine($"keyword-sync=ok; keyword={keyword}");
    return 0;
}

static async Task<int> RunSteamBackfillAsync(ServiceProvider provider, int? maxRows)
{
    var syncService = provider.GetRequiredService<ITrainerTitleSyncService>();
    var report = await syncService.EnsureSteamBackfillUntilErrorAsync(maxRows);
    Console.WriteLine(
        "steam-backfill=done; processed={0}; matched={1}; errors={2}; consecutiveErrors={3}; stoppedByConsecutiveErrors={4}",
        report.ProcessedRows,
        report.MatchedRows,
        report.ErrorCount,
        report.ConsecutiveErrorCount,
        report.StoppedByConsecutiveErrors);
    return 0;
}

static async Task<int> RunGamerskySearchAsync(ServiceProvider provider, string query)
{
    var metadataService = provider.GetRequiredService<IGameTitleMetadataService>();
    var candidates = await metadataService.SearchCandidatesAsync(query);

    Console.WriteLine($"query={query}; candidates={candidates.Count}");
    foreach (var candidate in candidates.Take(10))
    {
        Console.WriteLine($"{candidate.Title} || {candidate.DetailUrl}");
    }

    return candidates.Count > 0 ? 0 : 3;
}

static async Task<int> RunGamerskyDetailAsync(ServiceProvider provider, string url)
{
    var metadataService = provider.GetRequiredService<IGameTitleMetadataService>();
    var metadata = await metadataService.GetMetadataAsync(url);
    if (metadata == null)
    {
        Console.WriteLine("metadata=<null>");
        return 3;
    }

    Console.WriteLine($"{metadata.Source} || {metadata.SourceUrl} || {metadata.EnglishName} || {metadata.ChineseName}");
    return 0;
}

static async Task<int> RunSnapshotExportAsync(ServiceProvider provider)
{
    await using var scope = provider.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var snapshotService = scope.ServiceProvider.GetRequiredService<ITrainerTitleSnapshotService>();
    var ok = await snapshotService.SaveSnapshotFromDatabaseAsync(db);
    Console.WriteLine(ok
        ? $"snapshot-export=ok; path={snapshotService.SnapshotPath}"
        : $"snapshot-export=failed; path={snapshotService.SnapshotPath}");
    return ok ? 0 : 4;
}

static async Task<int> RunSnapshotExportSeedAsync(ServiceProvider provider)
{
    await using var scope = provider.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var snapshotService = scope.ServiceProvider.GetRequiredService<ITrainerTitleSnapshotService>();
    var ok = await snapshotService.SaveSeedSnapshotFromDatabaseAsync(db);
    Console.WriteLine(ok
        ? $"snapshot-export-seed=ok; path={snapshotService.SeedSnapshotPath}"
        : $"snapshot-export-seed=failed; path={snapshotService.SeedSnapshotPath}");
    return ok ? 0 : 4;
}

static async Task<int> RunSnapshotImportAsync(ServiceProvider provider)
{
    await using var scope = provider.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var snapshotService = scope.ServiceProvider.GetRequiredService<ITrainerTitleSnapshotService>();
    var changed = await snapshotService.ImportSnapshotAsync(db, overwriteExisting: false);
    Console.WriteLine($"snapshot-import=ok; changed={changed}; path={snapshotService.SnapshotPath}");
    return changed > 0 ? 0 : 3;
}

static async Task<int> RunSnapshotImportSeedIfNeededAsync(ServiceProvider provider)
{
    await using var scope = provider.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var snapshotService = scope.ServiceProvider.GetRequiredService<ITrainerTitleSnapshotService>();
    var changed = await snapshotService.ImportSeedSnapshotIfNeededAsync(db);
    Console.WriteLine($"snapshot-import-seed-if-needed=ok; changed={changed}; seedPath={snapshotService.SeedSnapshotPath}; localPath={snapshotService.SnapshotPath}");
    return 0;
}

static async Task<int> RunSnapshotVerifyAsync(ServiceProvider provider)
{
    await using var scope = provider.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.EnsureTrainerTitleIndexSchemaAsync();
    var snapshotPath = Path.Combine(AppPaths.DataFolder, "title-index.snapshot.json");
    var active = await db.TrainerTitleIndexEntries.CountAsync(row => row.IsActive);
    var chinese = await db.TrainerTitleIndexEntries.CountAsync(row => row.IsActive && row.NormalizedChineseName != null && row.NormalizedChineseName != string.Empty);
    Console.WriteLine($"snapshot-path={snapshotPath}; exists={File.Exists(snapshotPath)}; active={active}; chinese={chinese}");
    return 0;
}

static Task<int> ShowUsageAsync(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Console.Error.WriteLine("Usage: stats | sync | search <keyword> | keyword-sync <keyword> | steam-backfill [maxRows] | gamersky-search <query> | gamersky-detail <url> | snapshot-export | snapshot-export-seed | snapshot-import | snapshot-import-seed-if-needed | snapshot-verify");
    return Task.FromResult(1);
}
