# XKantor Local Hardware Agent

Lokalny Agent dla systemu **xkantor.app** (który działa centralnie na VPS) - bezpieczny most
między aplikacją webową (otwartą w przeglądarce na stanowisku kantoru) a lokalnym sprzętem
(drukarki, monitory, wyświetlacz kursów walut).

```
                    INTERNET
                       |
                       | HTTPS
                       v
              XKANTOR.APP (VPS)
                       |
                       v
                 PRZEGLADARKA
              NA STANOWISKU
                       |
                       | LOCALHOST
                       v
       XKANTOR LOCAL HARDWARE AGENT
                      |
        +-------------+--------------+
        |             |              |
        v             v              v
     DRUKARKI      WYSWIETLACZE    MONITORY
     LPT/USB       KURSOW

```

Pełny opis architektury: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Wymagania

- Windows 10/11 lub Windows Server 2019+ (x64) - Agent jest z definicji Windows-only.
- .NET 10 SDK (build/testy) / .NET 10 Desktop Runtime (uruchomienie).

## Struktura projektu

```
src/
  XKantor.LocalAgent.Core/            - modele, konfiguracja, agregacja statusu, command whitelist
  XKantor.LocalAgent.Security/        - tożsamość (ECDSA), parowanie, odnowienie, tokeny sesji
  XKantor.LocalAgent.Printing/        - IPrinter (Windows/RAW/LPT), wykrywanie drukarek
  XKantor.LocalAgent.Devices/         - wykrywanie urządzeń/portów
  XKantor.LocalAgent.Monitors/        - wykrywanie monitorów + IPC do komponentu sesji użytkownika
  XKantor.LocalAgent.CurrencyDisplay/ - framework adapterów wyświetlaczy kursów walut
  XKantor.LocalAgent.Api/             - Local API (minimal API), middleware, DTO
  XKantor.LocalAgent.Service/         - Windows Service + hosting Kestrel (proces główny)
  XKantor.LocalAgent.UserSession/     - komponent w sesji zalogowanego operatora (tray icon)
tests/
  XKantor.LocalAgent.Tests/           - xUnit (Security, Core, walidacja DTO)
installer/                            - skrypt Inno Setup
docs/                                 - ARCHITECTURE, SECURITY, PAIRING, INSTALLATION, DECISIONS, QUESTIONS.csv
```

## Build

```powershell
dotnet build xkantor-local-agent.slnx
```

## Testy

```powershell
dotnet test tests\XKantor.LocalAgent.Tests\XKantor.LocalAgent.Tests.csproj
```

44 testy (Security: generowanie/ładowanie tożsamości, podpisywanie, parowanie, polityka
odnowienia/grace period, tokeny sesji; Core: agregacja statusu, command whitelist; Api:
walidacja DTO drukowania).

## Uruchomienie lokalne (development, bez instalatora)

```powershell
cd src\XKantor.LocalAgent.Service
dotnet run
```

Domyślnie nasłuchuje na `http://127.0.0.1:47311` (wyłącznie loopback - patrz
[`docs/SECURITY.md`](docs/SECURITY.md)).

```powershell
Invoke-RestMethod http://127.0.0.1:47311/health   # -> OFFLINE (jeszcze niesparowany, oczekiwane)
```

## Konfiguracja

`%ProgramData%\XKantorLocalAgent\config\agent.json` - tworzony automatycznie z wartościami
domyślnymi przy pierwszym starcie. Szczegóły: [`docs/INSTALLATION.md`](docs/INSTALLATION.md).

## Bezpieczeństwo

Żadnego dowolnego wykonania poleceń - tylko jawnie zdefiniowana whitelist operacji
(`Core/CommandWhitelist.cs`). Pełny opis modelu bezpieczeństwa (tożsamość, parowanie,
krótkoterminowe tokeny sesji, Origin validation): [`docs/SECURITY.md`](docs/SECURITY.md) i
[`docs/PAIRING.md`](docs/PAIRING.md).

## Instalator

[`docs/INSTALLATION.md`](docs/INSTALLATION.md) - skrypt Inno Setup gotowy, nieskompilowany w
tym środowisku (brak Inno Setup na maszynie deweloperskiej - patrz
[`docs/DECISIONS.md`](docs/DECISIONS.md) #13).

## Zakres tego etapu / czego świadomie brakuje

Patrz [`docs/QUESTIONS.csv`](docs/QUESTIONS.csv) dla pełnej listy otwartych pytań (głównie:
dokładny kontrakt parowania/odnowienia z backendem xkantor.app, konkretny sterownik
wyświetlacza kursów - żaden producent nie był podany). Zgodnie z etapem 2 (sekcja 34-35):
zaimplementowano kompletny, działający fundament (uruchamianie, lokalne API, health check,
status, wykrywanie monitorów/drukarek, drukowanie, whitelist, para kluczy, bezpieczne
przechowywanie, parowanie, odnawialna tożsamość, logowanie, autostart, testy, dokumentacja,
przygotowanie instalatora) - celowo NIE zaimplementowano konkretnych sterowników nieznanych
wyświetlaczy kursów ani żadnej integracji internetowej Agenta.
