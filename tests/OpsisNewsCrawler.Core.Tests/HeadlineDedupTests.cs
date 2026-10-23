using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class HeadlineDedupTests
{
    private static bool Same(string a, string b) => HeadlineDedup.AreSameStory(HeadlineDedup.Words(a), HeadlineDedup.Words(b));

    [Fact]
    public void ReorderedWordingIsTheSameStory() =>
        Assert.True(Same("Eurogroup: no change in the fiscal path", "No change in the fiscal path, says Eurogroup"));

    [Fact]
    public void GreekAccentsAndFinalSigmaDoNotMatter() =>
        Assert.True(Same("Ο Γιούνκερ μιλά για τη νέα συμφωνία των υπουργών", "Ο ΓΙΟΥΝΚΕΡ ΜΙΛΑ ΓΙΑ ΤΗ ΝΕΑ ΣΥΜΦΩΝΙΑ ΤΩΝ ΥΠΟΥΡΓΩΝ"));

    [Fact]
    public void DifferentStoriesAreKept() =>
        Assert.False(Same("Athens stocks close higher on banks", "Defence ministers meet in Brussels today"));

    [Fact]
    public void ShortHeadlinesAreNeverMerged() =>
        Assert.False(Same("Goal in Athens", "Goal in Athens"));

    [Fact]
    public void SharedTopicButDifferentFactsIsNotMerged() =>
        Assert.False(Same("Bank of Greece raises growth forecast for 2027", "Bank of Greece warns about rising loan defaults this winter"));

    [Fact]
    public void EmptyTitleGivesNoWords() => Assert.Empty(HeadlineDedup.Words(null));
}
