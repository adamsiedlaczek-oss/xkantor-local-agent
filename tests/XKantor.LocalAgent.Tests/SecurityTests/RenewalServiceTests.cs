using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Tests.SecurityTests;

public sealed class RenewalServiceTests : IDisposable
{
    private readonly TestIdentityEnvironment _env = new();

    private (IdentityService Identity, RenewalService Renewal, PairingService Pairing) Zbuduj()
    {
        var identity = _env.NowyIdentityService();
        var polityka = new RenewalPolicy();
        var pairing = new PairingService(identity, polityka);
        var renewal = new RenewalService(identity, polityka);
        return (identity, renewal, pairing);
    }

    [Fact]
    public void CreateRenewalRequest_BezSparowaniaRzucaWyjatek()
    {
        var (_, renewal, _) = Zbuduj();
        Assert.Throws<InvalidOperationException>(() => renewal.CreateRenewalRequest());
    }

    [Fact]
    public void CreateRenewalRequest_PoSparowaniuZwracaPodpisanyPayload()
    {
        var (identity, renewal, pairing) = Zbuduj();
        var start = pairing.StartPairing();
        pairing.ZweryfikujIZuzyjKod(start.PairingCode);
        pairing.CompletePairing("KANTOR-01", "token");

        var zadanie = renewal.CreateRenewalRequest();

        Assert.Contains("KANTOR-01", zadanie.PayloadJson);
        Assert.False(string.IsNullOrEmpty(zadanie.SignatureBase64));
    }

    [Fact]
    public void InstallRenewedCertificate_OdrzucaInneStationId()
    {
        var (identity, renewal, pairing) = Zbuduj();
        var start = pairing.StartPairing();
        pairing.ZweryfikujIZuzyjKod(start.PairingCode);
        pairing.CompletePairing("KANTOR-01", "token");

        var falszywy = new StationCertificate
        {
            StationId = "KANTOR-99",
            PublicKeyThumbprint = identity.GetPublicKeyInfo().Thumbprint,
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            IssuerToken = "token"
        };

        Assert.Throws<InvalidOperationException>(() => renewal.InstallRenewedCertificate(falszywy));
    }

    [Fact]
    public void InstallRenewedCertificate_OdrzucaInnyKluczPubliczny()
    {
        var (identity, renewal, pairing) = Zbuduj();
        var start = pairing.StartPairing();
        pairing.ZweryfikujIZuzyjKod(start.PairingCode);
        pairing.CompletePairing("KANTOR-01", "token");

        var falszywy = new StationCertificate
        {
            StationId = "KANTOR-01",
            PublicKeyThumbprint = "0000000000000000000000000000000000000000000000000000000000000000",
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            IssuerToken = "token"
        };

        Assert.Throws<InvalidOperationException>(() => renewal.InstallRenewedCertificate(falszywy));
    }

    [Fact]
    public void InstallRenewedCertificate_PoprawnyCertyfikatAktualizujeWygasniecie()
    {
        var (identity, renewal, pairing) = Zbuduj();
        var start = pairing.StartPairing();
        pairing.ZweryfikujIZuzyjKod(start.PairingCode);
        pairing.CompletePairing("KANTOR-01", "token");

        var nowaData = DateTimeOffset.UtcNow.AddDays(60);
        var nowy = new StationCertificate
        {
            StationId = "KANTOR-01",
            PublicKeyThumbprint = identity.GetPublicKeyInfo().Thumbprint,
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = nowaData,
            IssuerToken = "token-v2"
        };

        renewal.InstallRenewedCertificate(nowy);

        Assert.Equal(nowaData, identity.Certificate!.ExpiresAtUtc);
    }

    public void Dispose() => _env.Dispose();
}
