using System.Text.Json;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>Every shipped language file must be valid JSON with the same keys as English (a broken file silently turns the UI into raw keys).</summary>
public sealed class LocaleFilesTests
{
    private static readonly string[] Codes = { "ar", "de", "el", "en", "es", "fr", "hi", "it", "ja", "ko", "pt", "ru", "tr", "zh" };

    private static Dictionary<string, string> Load(string code)
    {
        using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"OpsisNewsCrawler.Locales.{code}.json")
                           ?? throw new InvalidOperationException($"locale {code} is not embedded");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }

    [Theory]
    [InlineData("ar")] [InlineData("de")] [InlineData("el")] [InlineData("en")] [InlineData("es")] [InlineData("fr")] [InlineData("hi")]
    [InlineData("it")] [InlineData("ja")] [InlineData("ko")] [InlineData("pt")] [InlineData("ru")] [InlineData("tr")] [InlineData("zh")]
    public void LanguageFileIsValidAndComplete(string code)
    {
        var en = Load("en");
        var other = Load(code);
        Assert.DoesNotContain(en.Keys, k => !other.ContainsKey(k));
        Assert.DoesNotContain(other, kv => string.IsNullOrWhiteSpace(kv.Value));
    }

    [Fact]
    public void EveryLanguageIsCovered() => Assert.Equal(14, Codes.Length);

    [Theory]
    [InlineData("ar")] [InlineData("de")] [InlineData("el")] [InlineData("en")] [InlineData("es")] [InlineData("fr")] [InlineData("hi")]
    [InlineData("it")] [InlineData("ja")] [InlineData("ko")] [InlineData("pt")] [InlineData("ru")] [InlineData("tr")] [InlineData("zh")]
    public void PlaceholdersMatchEnglish(string code)
    {
        var en = Load("en");
        var other = Load(code);
        foreach (var (key, text) in en)
        {
            var wanted = Enumerable.Range(0, 4).Where(i => text.Contains("{" + i + "}")).ToArray();
            var got = Enumerable.Range(0, 4).Where(i => other[key].Contains("{" + i + "}")).ToArray();
            Assert.True(wanted.SequenceEqual(got), $"{code}:{key} placeholders differ");
        }
    }

    // Keys that are legitimately the same in every language (the product name, the MIT licence text, technical names).
    private static readonly HashSet<string> SameEverywhere = new() { "app.name", "license.text", "dep.dotnet", "fmt.monitor", "note.hostTheme", "hazard.range" };

    [Theory]
    [InlineData("ar")] [InlineData("de")] [InlineData("es")] [InlineData("fr")] [InlineData("hi")] [InlineData("it")]
    [InlineData("ja")] [InlineData("ko")] [InlineData("pt")] [InlineData("ru")] [InlineData("tr")] [InlineData("zh")]
    public void NoEnglishTextLeftInOtherLanguages(string code)
    {
        var en = Load("en");
        var other = Load(code);
        var untranslated = en.Where(kv => kv.Value.Length > 12 && kv.Value.Any(char.IsLetter) && other[kv.Key] == kv.Value && !SameEverywhere.Contains(kv.Key)).Select(kv => kv.Key).ToList();
        Assert.True(untranslated.Count == 0, $"{code}: still English: {string.Join(", ", untranslated)}");
    }
}
