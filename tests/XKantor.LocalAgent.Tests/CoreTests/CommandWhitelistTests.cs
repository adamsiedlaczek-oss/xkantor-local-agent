using XKantor.LocalAgent.Core;

namespace XKantor.LocalAgent.Tests.CoreTests;

// Patrz etap 2, sekcja 7-8 - Agent może wykonywać WYŁĄCZNIE jawnie zdefiniowane operacje.
// Ten test pilnuje, że whitelist nie "obrasta" niebezpiecznymi/ogólnymi komendami bez
// świadomej decyzji (aktualizacja tej listy w teście = review w code review).
public sealed class CommandWhitelistTests
{
    private static readonly string[] DozwoloneKomendy =
    {
        "GET_AGENT_STATUS", "GET_DEVICE_STATUS", "GET_MONITORS", "GET_PRINTERS",
        "PRINT_TRANSACTION", "PRINT_DOCUMENT", "SAVE_FILE", "UPDATE_CURRENCY_DISPLAY",
        "GET_BOARD_CONFIG", "UPDATE_BOARD_CONFIG"
    };

    private static readonly string[] ZakazaneWzorce =
    {
        "EXECUTE", "RUN_CMD", "RUNPOWERSHELL", "RUN_POWERSHELL", "SHELL", "CMD", "EVAL"
    };

    [Fact]
    public void ZawieraDokladnieOczekiwanyZbiorKomend()
    {
        Assert.Equal(DozwoloneKomendy.OrderBy(x => x), CommandWhitelist.Komendy.Keys.OrderBy(x => x));
    }

    [Fact]
    public void KazdaKomendaMaNiepustyOpis()
    {
        Assert.All(CommandWhitelist.Komendy.Values, opis => Assert.False(string.IsNullOrWhiteSpace(opis)));
    }

    [Fact]
    public void ZadnaKomendaNieBrzmiJakDowolneWykonanieCommand()
    {
        foreach (var komenda in CommandWhitelist.Komendy.Keys)
        {
            foreach (var zakazany in ZakazaneWzorce)
            {
                Assert.DoesNotContain(zakazany, komenda, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [InlineData("EXECUTE_COMMAND")]
    [InlineData("RUN_CMD")]
    [InlineData("RUN_POWERSHELL")]
    [InlineData("")]
    [InlineData("PRINT_TRANSACTION ")] // spacja - nie powinna "przejść" jako ta sama komenda
    public void JestDozwolona_OdrzucaCokolwiekSpozaWhitelist(string komenda)
    {
        Assert.False(CommandWhitelist.JestDozwolona(komenda));
    }
}
