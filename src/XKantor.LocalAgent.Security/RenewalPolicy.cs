namespace XKantor.LocalAgent.Security;

// Parametry odnawiania tożsamości - patrz etap 2, sekcje 12-13 (CERTIFICATE VALIDITY: 30 DAYS,
// RENEWAL WINDOW: 7 DAYS BEFORE EXPIRATION, GRACE PERIOD: 7 DAYS). Klasa (nie stałe statyczne),
// żeby testy mogły podstawić inne wartości bez czekania dni w rzeczywistości.
public sealed class RenewalPolicy
{
    public TimeSpan WaznoscCertyfikatu { get; init; } = TimeSpan.FromDays(30);
    public TimeSpan OknoOdnowienia { get; init; } = TimeSpan.FromDays(7);
    public TimeSpan OkresAwaryjny { get; init; } = TimeSpan.FromDays(7);

    // Prawda gdy certyfikat wchodzi w okno odnowienia (albo już wygasł, ale wciąż w grace) -
    // Agent powinien wtedy przy najbliższej okazji poprosić przeglądarkę o zainicjowanie
    // odnowienia (patrz RenewalService.CreateRenewalRequest, Api/Endpoints/IdentityEndpoints.cs).
    public bool WymagaOdnowienia(DateTimeOffset wygasaUtc, DateTimeOffset? teraz = null)
    {
        var chwilaOceny = teraz ?? DateTimeOffset.UtcNow;
        return chwilaOceny >= wygasaUtc - OknoOdnowienia;
    }

    public bool WOkresieAwaryjnym(DateTimeOffset wygasaUtc, DateTimeOffset? teraz = null)
    {
        var chwilaOceny = teraz ?? DateTimeOffset.UtcNow;
        return chwilaOceny >= wygasaUtc && chwilaOceny < wygasaUtc + OkresAwaryjny;
    }

    public bool WygaslPoOkresieAwaryjnym(DateTimeOffset wygasaUtc, DateTimeOffset? teraz = null)
    {
        var chwilaOceny = teraz ?? DateTimeOffset.UtcNow;
        return chwilaOceny >= wygasaUtc + OkresAwaryjny;
    }
}
