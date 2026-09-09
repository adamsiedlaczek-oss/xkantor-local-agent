using XKantor.LocalAgent.Security;

namespace XKantor.LocalAgent.Service;

// Pilnuje okna odnowienia tożsamości w tle (etap 2, sekcja 12: "Przed wygaśnięciem Agent
// powinien automatycznie próbować odnowić swoje poświadczenie"). Agent sam nie łączy się z
// Internetem (patrz Security/RenewalService.cs) - ten serwis tylko LOGUJE, że zbliża się okno
// odnowienia / że tożsamość jest w grace period, żeby było to widoczne w logach/statusie
// jeszcze zanim przeglądarka odwiedzi stanowisko i faktycznie zainicjuje odnowienie przez
// POST /api/v1/identity/renewal-request.
public sealed class RenewalWatcherHostedService : BackgroundService
{
    private static readonly TimeSpan OdstepSprawdzania = TimeSpan.FromHours(1);

    private readonly IdentityService _identity;
    private readonly RenewalPolicy _polityka;
    private readonly ILogger<RenewalWatcherHostedService> _logger;

    public RenewalWatcherHostedService(IdentityService identity, RenewalPolicy polityka, ILogger<RenewalWatcherHostedService> logger)
    {
        _identity = identity;
        _polityka = polityka;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var status = _identity.GetIdentityStatus(_polityka);
                if (status.CzySparowany && status.WygasaUtc is { } wygasa)
                {
                    if (!status.CertyfikatWazny && status.WOkresieAwaryjnym)
                    {
                        _logger.LogWarning("Certyfikat stanowiska {StationId} wygasł - Agent działa w okresie awaryjnym do {Koniec:u}.",
                            status.StationId, wygasa + _polityka.OkresAwaryjny);
                    }
                    else if (!status.CertyfikatWazny)
                    {
                        _logger.LogError("Certyfikat stanowiska {StationId} wygasł POZA okresem awaryjnym - Agent w stanie OFFLINE.", status.StationId);
                    }
                    else if (_polityka.WymagaOdnowienia(wygasa))
                    {
                        _logger.LogInformation("Certyfikat stanowiska {StationId} wchodzi w okno odnowienia (wygasa {Wygasa:u}).", status.StationId, wygasa);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania okna odnowienia tożsamości.");
            }

            await Task.Delay(OdstepSprawdzania, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }
}
