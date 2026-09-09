using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Tests.SecurityTests;

public sealed class SessionTokenServiceTests : IDisposable
{
    private readonly TestIdentityEnvironment _env = new();

    private (IdentityService Identity, SessionTokenService Tokens, string StationSecret) Zbuduj()
    {
        var identity = _env.NowyIdentityService();
        var polityka = new RenewalPolicy();
        var pairing = new PairingService(identity, polityka);
        var start = pairing.StartPairing();
        pairing.ZweryfikujIZuzyjKod(start.PairingCode);
        var wynikParowania = pairing.CompletePairing("KANTOR-01", "token");

        return (identity, new SessionTokenService(identity), wynikParowania.StationSecretBase64);
    }

    [Fact]
    public void WystawToken_ZlySekretZwracaNull()
    {
        var (_, tokens, _) = Zbuduj();
        var token = tokens.WystawToken(Convert.ToBase64String(new byte[32]), new[] { "read" });
        Assert.Null(token);
    }

    [Fact]
    public void WystawTokenIWaliduj_PoprawnySekretDajeWaznyToken()
    {
        var (_, tokens, sekret) = Zbuduj();
        var token = tokens.WystawToken(sekret, new[] { "read", "print" });

        Assert.NotNull(token);
        var (wynik, payload) = tokens.Waliduj(token, "read");
        Assert.Equal(SessionTokenValidationResult.Valid, wynik);
        Assert.Contains("print", payload!.Scopes);
    }

    [Fact]
    public void Waliduj_BrakujacyZakresOdrzucony()
    {
        var (_, tokens, sekret) = Zbuduj();
        var token = tokens.WystawToken(sekret, new[] { "read" });

        var (wynik, _) = tokens.Waliduj(token, "print");
        Assert.Equal(SessionTokenValidationResult.MissingScope, wynik);
    }

    [Fact]
    public void Waliduj_WygasleTokenOdrzucony()
    {
        var (_, tokens, sekret) = Zbuduj();
        var token = tokens.WystawToken(sekret, new[] { "read" }, ttl: TimeSpan.FromMilliseconds(1));

        Thread.Sleep(20);

        var (wynik, _) = tokens.Waliduj(token, "read");
        Assert.Equal(SessionTokenValidationResult.Expired, wynik);
    }

    [Fact]
    public void Waliduj_ZmanipulowanyTokenOdrzucony()
    {
        var (_, tokens, sekret) = Zbuduj();
        var token = tokens.WystawToken(sekret, new[] { "read" })!;

        var zmanipulowany = token[..^2] + (token[^2] == 'A' ? "B" : "A") + token[^1];

        var (wynik, _) = tokens.Waliduj(zmanipulowany, "read");
        Assert.Equal(SessionTokenValidationResult.Invalid, wynik);
    }

    [Fact]
    public void Waliduj_InnaInstancjaSerwisu_TokenNiewazny()
    {
        // Klucz podpisujący jest losowany PRZY KAŻDYM STARCIE procesu (patrz
        // SessionTokenService.cs) - druga instancja symuluje restart Agenta, po którym
        // wszystkie wcześniej wydane tokeny muszą przestać działać.
        var (identity, tokens, sekret) = Zbuduj();
        var token = tokens.WystawToken(sekret, new[] { "read" });

        var innaInstancja = new SessionTokenService(identity);
        var (wynik, _) = innaInstancja.Waliduj(token, "read");

        Assert.Equal(SessionTokenValidationResult.Invalid, wynik);
    }

    public void Dispose() => _env.Dispose();
}
