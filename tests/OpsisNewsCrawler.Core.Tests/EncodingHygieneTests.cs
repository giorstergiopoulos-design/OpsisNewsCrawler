using System.Text;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>
/// Source files must not contain double-encoded text ("mojibake": UTF-8 bytes read as a Windows code page and saved as UTF-8 again, e.g. a dash shown as
/// "β€”" or Greek shown as "ΞΞ΄Ξ·Ξ³ΟΟ‚"). It happened once (0.15.0, a script that read the file with the system code page) and nothing noticed it, because
/// the text is valid UTF-8 and compiles. The check: a line whose characters can all be written in cp1253 or cp1252 and whose bytes then form valid UTF-8
/// that decodes to different text is double-encoded.
/// </summary>
public class EncodingHygieneTests
{
    private static readonly string[] Extensions = { ".cs", ".json", ".iss", ".md", ".ps1", ".yml", ".txt", ".csproj", ".bat" };
    private static readonly string[] SkippedFolders = { "bin", "obj", ".git", "release", "Output", "node_modules", ".vs" };

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any()) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => Path.GetFileName(f) != "EncodingHygieneTests.cs")   // its own examples are deliberately broken
            .Where(f => !f.Substring(root.Length).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => SkippedFolders.Contains(p, StringComparer.OrdinalIgnoreCase)));

    /// <summary>The repaired text when the line is double-encoded; null when it is fine.</summary>
    internal static string? Repair(string line)
    {
        if (line.All(c => c < 0x80)) return null;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        foreach (var cp in new[] { 1253, 1252 })
        {
            try
            {
                var strict = Encoding.GetEncoding(cp, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                var bytes = strict.GetBytes(line);
                var back = new UTF8Encoding(false, true).GetString(bytes);
                if (back != line && back.Any(c => c >= 0x80)) return back;
            }
            catch (Exception ex) when (ex is EncoderFallbackException or DecoderFallbackException or ArgumentException) { }
        }
        // Often the damage is lossy (bytes that the code page does not define were dropped), so the text cannot be repaired by a round trip:
        // recognise the signature instead (a dash "β€”", or Greek capital Xi / Omicron followed by a symbol, accent or punctuation mark of the code page).
        if (Signature.IsMatch(line)) return "(lossy double encoding)";
        return null;
    }

    private static readonly System.Text.RegularExpressions.Regex Signature = new("β€|[ΞΟ][\u0384-\u038F\u00A3-\u00A9\u00AC\u00B1-\u00B6\u00BD\u201A\u201E\u2020\u2021\u2030\u2039\u203A\u20AC\u2122\u0192]", System.Text.RegularExpressions.RegexOptions.Compiled);

    [Theory]
    [InlineData("News Crawler β€” Welcome guide", "News Crawler — Welcome guide")]
    public void TheDetectorRepairsDoubleEncodedText(string broken, string fixedText) => Assert.Equal(fixedText, Repair(broken));

    [Fact]
    public void TheDetectorRecognisesTheLossyGreekCase() =>
        Assert.NotNull(Repair("News Crawler β€” ΞΞ΄Ξ·Ξ³ΟΟ‚ ΞΊΞ±Ξ»Ο‰ΟƒΞΏΟΞ―ΟƒΞΌΞ±Ο„ΞΏΟ‚"));

    [Theory]
    [InlineData("News Crawler — Οδηγός καλωσορίσματος")]
    [InlineData("Ελληνικά / English")]
    [InlineData("Ünlü · café · ß · “quotes” · 日本語 · العربية · हिन्दी")]
    [InlineData("plain ASCII")]
    [InlineData("Ξενοδοχείο Ορφέας — Οικονομία Ξηρασία")]
    public void TheDetectorLeavesCorrectTextAlone(string text) => Assert.Null(Repair(text));

    [Fact]
    public void NoFileOfTheRepositoryContainsDoubleEncodedText()
    {
        var root = Root();
        var found = new List<string>();
        foreach (var file in SourceFiles(root))
        {
            var number = 0;
            foreach (var line in File.ReadLines(file, Encoding.UTF8))
            {
                number++;
                if (Repair(line) is { } repaired)
                    found.Add($"{Path.GetRelativePath(root, file)}:{number}: {line.Trim()[..Math.Min(80, line.Trim().Length)]}  =>  {repaired.Trim()[..Math.Min(80, repaired.Trim().Length)]}");
            }
        }
        Assert.True(found.Count == 0, "double-encoded text:\n" + string.Join("\n", found.Take(40)));
    }
}
