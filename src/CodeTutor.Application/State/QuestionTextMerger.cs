using CodeTutor.Application.Ocr;
using CodeTutor.Application.Abstractions;
using CodeTutor.Domain.Ocr;

namespace CodeTutor.Application.State;

/// <summary>
/// 行级首尾模糊重叠 + 字符级兜底拼接。
/// 无可靠重叠时保守追加，不静默删字。
/// </summary>
public sealed class QuestionTextMerger : IQuestionTextMerger
{
    private const int MaxLineWindow = 12;
    private const int CharWindow = 400;
    private const double LineOverlapThreshold = 0.86;
    private const double CharOverlapThreshold = 0.90;
    private const int MinCharOverlap = 15;

    public MergeResult Merge(string existingText, OcrResult incoming)
    {
        var incomingText = OcrTextNormalizer.Flatten(incoming.FullText).Trim();

        if (string.IsNullOrWhiteSpace(existingText))
        {
            return new MergeResult(
                incomingText,
                new MergeDecision(MergeStrategy.First, 0, 0, 1.0, true));
        }

        var existingLines = NormalizeToLines(existingText);
        var incomingLines = NormalizeToLines(incomingText);

        var bestLine = FindBestLineOverlap(
            existingLines.TakeLast(MaxLineWindow).ToList(),
            incomingLines.Take(MaxLineWindow).ToList());

        if (bestLine.IsReliable)
        {
            var merged = AppendWithoutFirstKLines(existingText, incomingLines, bestLine.OverlapLineCount);
            return new MergeResult(merged, bestLine);
        }

        var tail = Tail(existingText, CharWindow);
        var head = Head(incomingText, CharWindow);
        var charOverlap = FindCharacterOverlap(tail, head);

        if (charOverlap.IsReliable)
        {
            var merged = existingText + incomingText[charOverlap.OverlapCharCount..];
            return new MergeResult(merged.TrimEnd(), charOverlap);
        }

        var separator = "\n\n--- 新截图：未检测到可靠重叠，请检查 ---\n";
        return new MergeResult(
            existingText.TrimEnd() + separator + incomingText,
            new MergeDecision(MergeStrategy.NoOverlapWithWarning, 0, 0, 0, false));
    }

    private static List<string> NormalizeToLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.None)
            .Select(l => l.TrimEnd())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

    private static string Tail(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[^maxChars..];

    private static string Head(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[..maxChars];

    private static MergeDecision FindBestLineOverlap(IReadOnlyList<string> tailLines, IReadOnlyList<string> headLines)
    {
        var bestScore = 0.0;
        var bestK = 0;

        var maxK = Math.Min(tailLines.Count, headLines.Count);
        for (var k = 1; k <= maxK; k++)
        {
            var score = 0.0;
            var minSim = 1.0;
            var charCount = 0;

            for (var i = 0; i < k; i++)
            {
                var a = NormalizeLine(tailLines[tailLines.Count - k + i]);
                var b = NormalizeLine(headLines[i]);
                var sim = LineSimilarity(a, b);
                minSim = Math.Min(minSim, sim);
                charCount += Math.Max(a.Length, b.Length);
                score += sim * Math.Max(a.Length, b.Length);
            }

            score = charCount > 0 ? score / charCount + Math.Min(k, 5) * 0.01 : 0;
            var reliable = score >= LineOverlapThreshold
                           && (charCount >= 12 || k >= 2)
                           && minSim >= 0.72;

            if (reliable && score > bestScore)
            {
                bestScore = score;
                bestK = k;
            }
        }

        return new MergeDecision(
            MergeStrategy.LineOverlap,
            bestK,
            0,
            bestScore,
            bestK > 0 && bestScore >= LineOverlapThreshold);
    }

    private static MergeDecision FindCharacterOverlap(string tail, string head)
    {
        var maxLen = Math.Min(tail.Length, head.Length);
        var bestLen = 0;
        var bestScore = 0.0;

        for (var len = MinCharOverlap; len <= maxLen; len++)
        {
            var suffix = tail[^len..];
            var prefix = head[..len];
            var score = LineSimilarity(NormalizeLine(suffix), NormalizeLine(prefix));
            if (score >= CharOverlapThreshold && score > bestScore)
            {
                bestScore = score;
                bestLen = len;
            }
        }

        return new MergeDecision(
            MergeStrategy.CharacterOverlap,
            0,
            bestLen,
            bestScore,
            bestLen >= MinCharOverlap && bestScore >= CharOverlapThreshold);
    }

    private static string AppendWithoutFirstKLines(string existing, IReadOnlyList<string> incomingLines, int k)
    {
        var rest = string.Join('\n', incomingLines.Skip(k));
        if (string.IsNullOrWhiteSpace(rest))
            return existing.TrimEnd();

        return existing.TrimEnd() + rest;
    }

    private static string NormalizeLine(string line) =>
        string.Concat(line.Where(c => !char.IsWhiteSpace(c) || c == ' ')).Trim();

    private static double LineSimilarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1.0;
        var dist = Levenshtein(a, b);
        return 1.0 - (double)dist / Math.Max(Math.Max(a.Length, b.Length), 1);
    }

    private static int Levenshtein(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            dp[i, j] = Math.Min(
                Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                dp[i - 1, j - 1] + cost);
        }

        return dp[a.Length, b.Length];
    }
}
