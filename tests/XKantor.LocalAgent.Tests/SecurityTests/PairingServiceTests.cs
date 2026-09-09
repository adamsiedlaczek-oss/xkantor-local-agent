using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Tests.SecurityTests;

public sealed class PairingServiceTests : IDisposable
{
    private readonly TestIdentityEnvironment _env = new();

    private (IdentityService Identity, PairingService Pairing) Zbuduj()
    {
        var identity = _env.NowyIdentityService();
        var pairing = new PairingService(identity, new RenewalPolicy());
        return (identity, pairing);
    }

    [Fact]
    public void StartPairing_ZwracaKodOSensownejDlugosciIKluczPubliczny()
    {
        var (_, pairing) = Zbuduj();

        var wynik = pairing.StartPairing();

        Assert.Equal(8, wynik.PairingCode.Length);
        Assert.False(string.IsNullOrWhiteSpace(wynik.PublicKey.PublicKeySpkiBase64));
        Assert.True(wynik.ExpiresAtUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ZweryfikujIZuzyjKod_KodJestJednorazowy()
    {
        var (_, pairing) = Zbuduj();
        var wynik = pairing.StartPairing();

        Assert.True(pairing.ZweryfikujIZuzyjKod(wynik.PairingCode));
        Assert.False(pairing.ZweryfikujIZuzyjKod(wynik.PairingCode)); // drugie użycie tego samego kodu musi się nie udać
    }

    [Fact]
    public void ZweryfikujIZuzyjKod_NieznanyKodOdrzucony()
    {
        var (_, pairing) = Zbuduj();

        Assert.False(pairing.ZweryfikujIZuzyjKod("NIEZNANY1"));
    }

    [Fact]
    public void CompletePairing_ZapisujeStationIdICertyfikatWIdentityService()
    {
        var (identity, pairing) = Zbuduj();
        var start = pairing.StartPairing();
        Assert.True(pairing.ZweryfikujIZuzyjKod(start.PairingCode));

        var wynik = pairing.CompletePairing("KANTOR-01", "opaque-issuer-token");

        Assert.True(identity.CzySparowany);
        Assert.Equal("KANTOR-01", identity.StationId);
        Assert.Equal("KANTOR-01", wynik.Certificate.StationId);
        Assert.True(identity.ZweryfikujStationSecret(wynik.StationSecretBase64));
    }

    [Fact]
    public void CompletePairing_ZlySekretJestOdrzucany()
    {
        var (identity, pairing) = Zbuduj();
        var start = pairing.StartPairing();
        pairing.ZweryfikujIZuzyjKod(start.PairingCode);
        pairing.CompletePairing("KANTOR-01", "opaque-issuer-token");

        Assert.False(identity.ZweryfikujStationSecret(Convert.ToBase64String(new byte[32])));
    }

    public void Dispose() => _env.Dispose();
}
