using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Models;
using GameTrainerLauncher.Core.Utilities;

namespace GameTrainerLauncher.UI.Services;

internal static class TrainerTitleMatchResolver
{
    private const double StrictSimilarityThreshold = 0.88;
    private const double RelaxedSimilarityThreshold = 0.84;
    private const double MinScoreGap = 0.08;

    public static TitleMatchResult<SteamStoreSearchCandidate>? TrySelectBestSteamCandidate(
        string flingTitle,
        IReadOnlyList<SteamStoreSearchCandidate> candidates)
    {
        return TrySelectBestMatch(
            flingTitle,
            candidates,
            candidate => candidate.Title,
            requireTopResultForSimilarity: true);
    }

    public static TitleMatchResult<GameTitleMetadata>? TrySelectBestMetadata(
        string flingTitle,
        IReadOnlyList<GameTitleMetadata> metadataEntries)
    {
        return TrySelectBestMatch(
            flingTitle,
            metadataEntries,
            entry => entry.EnglishName,
            requireTopResultForSimilarity: false);
    }

    public static Trainer? TrySelectBestTrainer(string englishTitle, IReadOnlyList<Trainer> trainers)
    {
        var normalizedEnglishTitle = TitleSearchNormalizer.NormalizeEnglishTitle(englishTitle);
        if (string.IsNullOrWhiteSpace(normalizedEnglishTitle))
        {
            return null;
        }

        var targetTokens = TitleSearchNormalizer.TokenizeEnglish(normalizedEnglishTitle);
        var normalizedTrainers = trainers
            .Select((trainer, index) => new NormalizedTrainer(
                trainer,
                index,
                TitleSearchNormalizer.NormalizeFlingTitle(trainer.Title),
                TitleSearchNormalizer.TokenizeEnglish(trainer.Title)))
            .Where(trainer => !string.IsNullOrWhiteSpace(trainer.NormalizedTitle))
            .ToList();

        var exactMatches = normalizedTrainers
            .Where(trainer => trainer.NormalizedTitle == normalizedEnglishTitle)
            .ToList();
        if (exactMatches.Count == 1)
        {
            return exactMatches[0].Trainer;
        }

        var tokenMatches = normalizedTrainers
            .Where(trainer => trainer.Tokens.SetEquals(targetTokens))
            .ToList();
        if (tokenMatches.Count == 1)
        {
            return tokenMatches[0].Trainer;
        }

        var scored = normalizedTrainers
            .Select(trainer => new
            {
                trainer.Trainer,
                trainer.Index,
                Score = CalculateSimilarity(normalizedEnglishTitle, targetTokens, trainer.NormalizedTitle, trainer.Tokens)
            })
            .OrderByDescending(trainer => trainer.Score)
            .ThenBy(trainer => trainer.Index)
            .ToList();

        if (scored.Count == 0)
        {
            return null;
        }

        var top = scored[0];
        var second = scored.Count > 1 ? scored[1].Score : 0d;
        return top.Score >= 0.88 && top.Score - second >= 0.08
            ? top.Trainer
            : null;
    }

    private static TitleMatchResult<T>? TrySelectBestMatch<T>(
        string flingTitle,
        IReadOnlyList<T> candidates,
        Func<T, string?> titleSelector,
        bool requireTopResultForSimilarity)
    {
        var normalizedFlingTitle = TitleSearchNormalizer.NormalizeFlingTitle(flingTitle);
        if (string.IsNullOrWhiteSpace(normalizedFlingTitle))
        {
            return null;
        }

        var targetTokens = TitleSearchNormalizer.TokenizeEnglish(normalizedFlingTitle);
        var normalizedCandidates = candidates
            .Select((candidate, index) => new NormalizedCandidate<T>(
                candidate,
                index,
                TitleSearchNormalizer.NormalizeEnglishTitle(titleSelector(candidate)),
                TitleSearchNormalizer.TokenizeEnglish(titleSelector(candidate))))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.NormalizedTitle))
            .ToList();

        var exactMatches = normalizedCandidates
            .Where(candidate => candidate.NormalizedTitle == normalizedFlingTitle)
            .ToList();
        if (exactMatches.Count == 1)
        {
            return new TitleMatchResult<T>(exactMatches[0].Candidate, 1d);
        }

        var tokenMatches = normalizedCandidates
            .Where(candidate => candidate.Tokens.SetEquals(targetTokens))
            .ToList();
        if (tokenMatches.Count == 1)
        {
            return new TitleMatchResult<T>(tokenMatches[0].Candidate, 0.96d);
        }

        var normalizedMainTitle = TitleSearchNormalizer.NormalizeEnglishMainTitle(flingTitle);
        var mainTitleMatches = normalizedCandidates
            .Where(candidate =>
                TitleSearchNormalizer.NormalizeEnglishMainTitle(titleSelector(candidate.Candidate)) == normalizedMainTitle)
            .ToList();
        if (!string.IsNullOrWhiteSpace(normalizedMainTitle) && mainTitleMatches.Count == 1)
        {
            return new TitleMatchResult<T>(mainTitleMatches[0].Candidate, 0.92d);
        }

        var scored = normalizedCandidates
            .Select(candidate => new
            {
                candidate.Candidate,
                candidate.Index,
                Score = CalculateSimilarity(normalizedFlingTitle, targetTokens, candidate.NormalizedTitle, candidate.Tokens)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Index)
            .ToList();

        if (scored.Count == 0)
        {
            return null;
        }

        var top = scored[0];
        var second = scored.Count > 1 ? scored[1].Score : 0d;
        var passesIndexRule = !requireTopResultForSimilarity || top.Index == 0;
        var topTitle = titleSelector(top.Candidate);
        var threshold = TitleSearchNormalizer.IsVersionOnlyDifference(flingTitle, topTitle)
            ? RelaxedSimilarityThreshold
            : StrictSimilarityThreshold;
        return passesIndexRule && top.Score >= threshold && top.Score - second >= MinScoreGap
            ? new TitleMatchResult<T>(top.Candidate, top.Score)
            : null;
    }

    private static double CalculateSimilarity(
        string normalizedTarget,
        HashSet<string> targetTokens,
        string normalizedCandidate,
        HashSet<string> candidateTokens)
    {
        if (targetTokens.Count == 0 || candidateTokens.Count == 0)
        {
            return 0;
        }

        var overlap = targetTokens.Intersect(candidateTokens).Count();
        var union = targetTokens.Union(candidateTokens).Count();
        var jaccard = union == 0 ? 0 : (double)overlap / union;
        var prefixBonus = normalizedCandidate.StartsWith(normalizedTarget, StringComparison.Ordinal) ||
                          normalizedTarget.StartsWith(normalizedCandidate, StringComparison.Ordinal)
            ? 0.08
            : 0;
        var lengthBonus = Math.Abs(normalizedCandidate.Length - normalizedTarget.Length) <= 3 ? 0.04 : 0;
        return Math.Min(1d, jaccard + prefixBonus + lengthBonus);
    }

    private sealed record NormalizedCandidate<T>(
        T Candidate,
        int Index,
        string NormalizedTitle,
        HashSet<string> Tokens);

    private sealed record NormalizedTrainer(
        Trainer Trainer,
        int Index,
        string NormalizedTitle,
        HashSet<string> Tokens);
}

internal sealed record TitleMatchResult<T>(T Candidate, double Confidence);
