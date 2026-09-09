using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Tests.SecurityTests;

// Patrz etap 2, sekcja 12-13 - CERTIFICATE VALIDITY 30 DNI, RENEWAL WINDOW 7 DNI PRZED
// WYGAŚNIĘCIEM, GRACE PERIOD 7 DNI.
public sealed class RenewalPolicyTests
{
    private readonly RenewalPolicy _polityka = new();
    private static readonly DateTimeOffset Teraz = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WymagaOdnowienia_PozaOknem_False()
    {
        var wygasa = Teraz + TimeSpan.FromDays(20);
        Assert.False(_polityka.WymagaOdnowienia(wygasa, Teraz));
    }

    [Fact]
    public void WymagaOdnowienia_WOknie7Dni_True()
    {
        var wygasa = Teraz + TimeSpan.FromDays(5);
        Assert.True(_polityka.WymagaOdnowienia(wygasa, Teraz));
    }

    [Fact]
    public void WOkresieAwaryjnym_TuzPoWygasnieciu_True()
    {
        var wygasa = Teraz - TimeSpan.FromDays(1);
        Assert.True(_polityka.WOkresieAwaryjnym(wygasa, Teraz));
    }

    [Fact]
    public void WOkresieAwaryjnym_PoUplywieGrace_False()
    {
        var wygasa = Teraz - TimeSpan.FromDays(8);
        Assert.False(_polityka.WOkresieAwaryjnym(wygasa, Teraz));
    }

    [Fact]
    public void WygaslPoOkresieAwaryjnym_Po8Dniach_True()
    {
        var wygasa = Teraz - TimeSpan.FromDays(8);
        Assert.True(_polityka.WygaslPoOkresieAwaryjnym(wygasa, Teraz));
    }

    [Fact]
    public void WygaslPoOkresieAwaryjnym_W6Dniu_FalseWciazGrace()
    {
        var wygasa = Teraz - TimeSpan.FromDays(6);
        Assert.False(_polityka.WygaslPoOkresieAwaryjnym(wygasa, Teraz));
    }
}
