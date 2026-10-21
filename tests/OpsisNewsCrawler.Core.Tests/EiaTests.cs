using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class EiaTests
{
    private const string Html = "<table> <tr> <td class='B6'>&nbsp;&nbsp;2026 Oct- 5 to Oct- 9</td> <td class='B3'>130.00</td> <td class='B3'>131.50</td> <td class='B3'>NA</td> <td class='B3'>129.00</td> <td class='B3'>128.00</td> </tr>"
        + " <tr> <td class='B6'>&nbsp;&nbsp;2026 Oct-12 to Oct-16</td> <td class='B3'>129.95</td> <td class='B3'>125.78</td> <td class='B3'>126.55</td> <td class='B3'></td> <td class='B3'></td> </tr> </table>";

    [Fact]
    public void Parse_ReadsWeeklyRows_IntoDailyValues()
    {
        var points = EiaDaily.Parse(Html);
        Assert.Equal(7, points.Count);
        Assert.Equal(new DateTime(2026, 10, 5), points[0].Day);
        Assert.Equal(new DateTime(2026, 10, 9), points[3].Day);          // the "NA" cell is skipped, Thursday keeps its place
        Assert.Equal(new DateTime(2026, 10, 14), points[^1].Day);
        Assert.Equal(126.55m, points[^1].Value);
    }

    [Fact]
    public void Latest_GivesNewestPriceAndChange()
    {
        var q = EiaDaily.Latest(new QuoteInstrument(QuoteSource.Energy, "DCOILBRENTEU", "BRENT", "", "Brent", "$"), Html)!;
        Assert.Equal(126.55m, q.Price);
        Assert.Equal(Math.Round((126.55m - 125.78m) / 125.78m * 100, 2), q.ChangePercent);
    }

    [Fact]
    public void Latest_OnGarbage_IsNull() =>
        Assert.Null(EiaDaily.Latest(new QuoteInstrument(QuoteSource.Energy, "X", "X", "", "X", "$"), "<html>bot check</html>"));
}
