using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class QuoteInstrumentTests
{
    [Fact]
    public void Clone_PreservesMarketAndAllEditableFields()
    {
        var original = new QuoteInstrument(QuoteSource.Euronext, "NL0000009165", "HEIA", "Heineken", "Heineken", "€", false)
        {
            Market = "XAMS"
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.Source, clone.Source);
        Assert.Equal(original.Code, clone.Code);
        Assert.Equal(original.Market, clone.Market);
        Assert.Equal(original.Symbol, clone.Symbol);
        Assert.Equal(original.NameEl, clone.NameEl);
        Assert.Equal(original.NameEn, clone.NameEn);
        Assert.Equal(original.Unit, clone.Unit);
        Assert.Equal(original.Enabled, clone.Enabled);
    }

    [Fact]
    public void Defaults_UseAthensMarketForAthensInstruments()
    {
        Assert.All(QuoteInstrument.Defaults().Where(x => x.Source == QuoteSource.Athens),
            instrument => Assert.Equal("XATH", instrument.Market));
    }
}
