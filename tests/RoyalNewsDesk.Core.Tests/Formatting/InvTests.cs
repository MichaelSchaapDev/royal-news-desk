using System.Globalization;
using RoyalNewsDesk.Core.Formatting;

namespace RoyalNewsDesk.Core.Tests.Formatting;

public class InvTests
{
    [Fact]
    public void FormatsWithDotsUnderDutchCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
            CultureInfo.CurrentUICulture = new CultureInfo("nl-NL");

            Assert.Equal("0.500", Inv.N3(0.5));
            Assert.Equal("1234.042", Inv.N3(1234.042));
            Assert.Equal("12345", Inv.I(12345));
            Assert.Equal("t=1.500", Inv.F($"t={1.5:0.000}"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
