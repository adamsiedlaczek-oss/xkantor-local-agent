using XKantor.LocalAgent.CurrencyDisplay;
using Xunit;

namespace XKantor.LocalAgent.Tests.CurrencyDisplayTests;

public class Wysw8PezetAdapterTests
{
    [Fact]
    public void IsConfigured_PustyPort_ZwracaFalse()
    {
        var adapter = new Wysw8PezetAdapter("", "C:\\nieistniejacy");
        Assert.False(adapter.IsConfigured);
    }

    [Fact]
    public void IsConfigured_PodanyPort_ZwracaTrue()
    {
        var adapter = new Wysw8PezetAdapter("3", "C:\\nieistniejacy");
        Assert.True(adapter.IsConfigured);
    }

    [Fact]
    public async Task UpdateAsync_WszystkiePozycjeBezDisplayRow_NicNieProbujeWyslacIZwracaSukces()
    {
        // DisplayRow <= 0 = waluta bez przypisanego wiersza PEZET (zadanie sekcja 17/18) -
        // adapter ma je pominąć, więc katalog drivera (celowo nieistniejący) nigdy nie jest
        // dotykany i wynik jest sukcesem (nic do wysłania != błąd).
        var adapter = new Wysw8PezetAdapter("3", "C:\\ten-katalog-na-pewno-nie-istnieje-xyz");
        var rates = new List<CurrencyRate>
        {
            new("EUR", 4.20m, 4.34m, DisplayRow: 0),
            new("USD", 3.56m, 3.74m, DisplayRow: -1),
        };

        var wynik = await adapter.UpdateAsync(rates);

        Assert.True(wynik.CzySukces);
        Assert.Null(wynik.Blad);
    }

    [Fact]
    public async Task UpdateAsync_BrakSterownika_ZwracaCzytelnyBlad()
    {
        var katalog = "C:\\ten-katalog-na-pewno-nie-istnieje-xyz";
        var adapter = new Wysw8PezetAdapter("3", katalog);
        var rates = new List<CurrencyRate> { new("EUR", 4.20m, 4.34m, DisplayRow: 1) };

        var wynik = await adapter.UpdateAsync(rates);

        Assert.False(wynik.CzySukces);
        Assert.Contains("wysw8.exe", wynik.Blad);
        Assert.Contains(katalog, wynik.Blad);
    }

    [Fact]
    public void Name_ZawieraPort()
    {
        var adapter = new Wysw8PezetAdapter("USB", "C:\\x");
        Assert.Contains("USB", adapter.Name);
    }
}
