# Bezpieczeństwo

## Zasada nadrzędna: żadnego dowolnego wykonania poleceń

Agent NIE implementuje `executeCommand()`/`runCMD()`/`runPowerShell()` ani żadnego ich
odpowiednika. Wszystkie operacje osiągalne z przeglądarki są jawnie wymienione w
`Core/CommandWhitelist.cs` i zamapowane na konkretne, typowane endpointy
(`Api/Endpoints/*.cs`) z walidacją danych wejściowych (`DataAnnotations` na DTO -
`Api/Dto/*.cs`). Patrz `tests/.../CoreTests/CommandWhitelistTests.cs` - test pilnuje, że
whitelist nie rozrośnie się o coś, co "brzmi" jak ogólne wykonanie polecenia.

## Sieć: wyłącznie loopback

Kestrel (`Service/Program.cs`) jest jawnie skonfigurowany na `IPAddress.Loopback` -
nigdy `0.0.0.0`, nigdy adres w sieci lokalnej. Port jest konfigurowalny (`agent.json`,
domyślnie 47311).

## Origin validation - localhost to za mało

Sam fakt nasłuchu na `127.0.0.1` NIE chroni przed złośliwą stroną otwartą w tej samej
przeglądarce, która próbuje wywołać `fetch("http://127.0.0.1:47311/...")`. Dlatego
`Api/Middleware/OriginValidationMiddleware.cs` odrzuca (`403`) każde żądanie (poza `/health`) z
nagłówkiem `Origin` spoza jawnej listy w `AgentConfig.AllowedOrigins`.

## Tożsamość Agenta

- Para kluczy **ECDSA P-256** generowana automatycznie przy pierwszym uruchomieniu
  (`Security/IdentityService.EnsureKeyPair`).
- Klucz prywatny nigdy nie opuszcza procesu Agenta w postaci jawnej - przechowywany
  zaszyfrowany DPAPI (`Security/SecureKeyStore.cs`, `DataProtectionScope.LocalMachine`) w
  `%ProgramData%\XKantorLocalAgent\secure\identity.bin`.
- Klucz prywatny nigdy nie jest logowany (patrz komentarze w `Security/IdentityModels.cs`).
- Klucz publiczny jest jedyną rzeczą przekazywaną na zewnątrz (parowanie, weryfikacja podpisów
  żądań odnowienia).

## Parowanie i krótkoterminowe autoryzacje

Patrz `docs/PAIRING.md` dla pełnego przepływu. Skrót zasad bezpieczeństwa:

- Kod parowania: 8 znaków z alfabetu bez znaków mylących (`0/O`, `1/I/L`), losowany
  `RandomNumberGenerator` (kryptograficzny RNG, nie `Random`), ważny **10 minut**,
  **jednorazowy** (`PairingService.ZweryfikujIZuzyjKod` usuwa go z puli przy pierwszym użyciu).
- `StationSecret`: 256-bitowy sekret losowany przy zakończeniu parowania, przekazywany
  przeglądarce **dokładnie raz**. Używany WYŁĄCZNIE do uzyskiwania krótkoterminowych tokenów
  sesji (`SessionTokenService.WystawToken`), nigdy bezpośrednio jako nagłówek autoryzacji
  operacji.
- Token sesji: ważny domyślnie **2 minuty**, podpisany HMAC-SHA256 kluczem losowanym **przy
  każdym starcie procesu Agenta** (istnieje wyłącznie w pamięci) - restart Agenta unieważnia
  wszystkie wcześniej wydane tokeny niezależnie od `ExpiresAtUtc`. Token niesie `StationId` i
  jawną listę `Scopes` (`read`/`print`/`display`/`renew`), zweryfikowaną per-endpoint
  (`Api/Auth/SessionAuthFilter.cs`).
- Porównania sekretów/podpisów używają `CryptographicOperations.FixedTimeEquals` (obrona przed
  atakami czasowymi).

## Odnawianie tożsamości i grace period

- Ważność certyfikatu (lekkiego, podpisanego JSON - patrz `docs/DECISIONS.md` #6): **30 dni**.
- Okno odnowienia: **7 dni** przed wygaśnięciem - `RenewalPolicy.WymagaOdnowienia`.
- Okres awaryjny (grace period): **7 dni** PO wygaśnięciu - Agent zgłasza stan `LIMITED`
  (`Core/Modules/AgentStatusService.cs`), ale wciąż działa. Po tym okresie: `OFFLINE` - Agent
  nie udaje ważnej tożsamości bezterminowo.
- Instalacja nowego certyfikatu (`RenewalService.InstallRenewedCertificate`) weryfikuje, że
  dotyczy TEGO SAMEGO `StationId` i TEGO SAMEGO klucza publicznego - Agent nigdy nie
  przyjmuje certyfikatu "obcego".

## Minimalne uprawnienia

Usługa jest zarejestrowana bez wymogu interaktywnego logowania. Dokładne konto usługi
(LocalSystem vs NetworkService vs dedykowane konto) jest odnotowane jako otwarta decyzja do
zawężenia przy wdrożeniu produkcyjnym - patrz `docs/QUESTIONS.csv` (PERMISSIONS). Moduł
`Printing`/`Devices` wymaga dostępu do kolejki drukarek i portów LPT/COM - w praktyce to
implikuje pełne uprawnienia lokalnego konta usługi; izolacja przez dedykowane konto z jawnymi
ACL-ami na te zasoby jest możliwym usprawnieniem.

## Logowanie

Serilog, rotacja dzienna, limit 30 plików (`Service/Program.cs`). Nigdy nie logujemy: kluczy
prywatnych, `StationSecret`, tokenów sesji, pełnej zawartości `IdentityRecord`. Loguje się:
start/stop, błędy, status modułów, wynik operacji drukowania, wykrywanie urządzeń, próby
autoryzacji odrzucone przez Origin validation/token sesji.
