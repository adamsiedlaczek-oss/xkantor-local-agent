using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Tests.SecurityTests;

// Używa realnego DPAPI (Windows) na tymczasowym katalogu %TEMP% zamiast %ProgramData% -
// patrz TestConfigPaths - żeby testy nie dotykały prawdziwej instalacji Agenta na maszynie CI/dev.
public sealed class IdentityServiceTests : IDisposable
{
    private readonly TestIdentityEnvironment _env = new();

    [Fact]
    public void EnsureKeyPair_GenerujeParaKluczyIJestIdempotentne()
    {
        var identity = _env.NowyIdentityService();
        identity.EnsureKeyPair();
        var info1 = identity.GetPublicKeyInfo();

        identity.EnsureKeyPair(); // drugie wywołanie nie powinno nadpisać klucza
        var info2 = identity.GetPublicKeyInfo();

        Assert.Equal(info1.Thumbprint, info2.Thumbprint);
    }

    [Fact]
    public void EnsureKeyPair_PoRestarcieProcesuLadujeTenSamKlucz()
    {
        var identity1 = _env.NowyIdentityService();
        identity1.EnsureKeyPair();
        var thumbprint1 = identity1.GetPublicKeyInfo().Thumbprint;

        var identity2 = _env.NowyIdentityService(); // symuluje restart usługi - nowy proces, ten sam magazyn
        identity2.EnsureKeyPair();
        var thumbprint2 = identity2.GetPublicKeyInfo().Thumbprint;

        Assert.Equal(thumbprint1, thumbprint2);
    }

    [Fact]
    public void Sign_PodpisJestWeryfikowalnyKluczemPublicznym()
    {
        var identity = _env.NowyIdentityService();
        identity.EnsureKeyPair();

        var dane = "test-payload"u8.ToArray();
        var podpis = identity.Sign(dane);

        var info = identity.GetPublicKeyInfo();
        using var ecdsa = System.Security.Cryptography.ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(info.PublicKeySpkiBase64), out _);

        Assert.True(ecdsa.VerifyData(dane, podpis, System.Security.Cryptography.HashAlgorithmName.SHA256));
    }

    [Fact]
    public void GetIdentityStatus_PrzedSparowaniem_CzySparowanyFalse()
    {
        var identity = _env.NowyIdentityService();
        identity.EnsureKeyPair();

        var status = identity.GetIdentityStatus(new RenewalPolicy());

        Assert.False(status.CzySparowany);
    }

    public void Dispose() => _env.Dispose();
}
