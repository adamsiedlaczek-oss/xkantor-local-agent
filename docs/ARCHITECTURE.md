# Architektura

## Kontekst systemowy

```
                    INTERNET
                       |
                       | HTTPS
                       v
              +------------------+
              |   XKANTOR.APP    |
              |       VPS        |
              +--------+---------+
                       |
                       v
                 PRZEGLADARKA
              NA STANOWISKU
                       |
                       | LOCALHOST (127.0.0.1, HTTP)
                       v
       +-----------------------------+
       | XKANTOR LOCAL HARDWARE AGENT|
       +--------------+--------------+
                      |
        +-------------+--------------+
        |             |              |
        v             v              v
     DRUKARKI      WYSWIETLACZE   MONITORY
     LPT/USB       KURSOW         / EKRANY
```

Agent **nigdy** nie łączy się z Internetem (patrz `docs/DECISIONS.md` #7). Wszystkie operacje,
które logicznie wymagają centrali (parowanie, odnowienie tożsamości), są zaprojektowane tak, że
przeglądarka jest jedynym pośrednikiem HTTPS <-> localhost.

## Solucja

```
xkantor-local-agent/
├── src/
│   ├── XKantor.LocalAgent.Core/           - modele, konfiguracja, agregacja statusu, whitelist
│   ├── XKantor.LocalAgent.Security/       - tożsamość, parowanie, odnowienie, tokeny sesji
│   ├── XKantor.LocalAgent.Printing/       - IPrinter (Windows/RAW/LPT), wykrywanie drukarek
│   ├── XKantor.LocalAgent.Devices/        - wykrywanie urządzeń/portów (agreguje Printing + COM)
│   ├── XKantor.LocalAgent.Monitors/       - wykrywanie monitorów + IPC (named pipe) do UserSession
│   ├── XKantor.LocalAgent.CurrencyDisplay/- framework adapterów wyświetlaczy kursów
│   ├── XKantor.LocalAgent.Api/            - endpointy Local API (minimal API), middleware, DTOs
│   ├── XKantor.LocalAgent.Service/        - Windows Service + hosting Kestrel (proces główny)
│   └── XKantor.LocalAgent.UserSession/    - komponent w sesji operatora (tray icon + IPC klient)
├── tests/
│   └── XKantor.LocalAgent.Tests/          - xUnit: Security, Core, walidacja DTO
├── installer/                             - skrypt Inno Setup
└── docs/
```

Zależności między projektami (strzałka = "zależy od"):

```
Service --> Api --> {Security, Printing, Devices, Monitors, CurrencyDisplay} --> Core
UserSession --> Monitors --> Core
Devices --> Printing (reużywa PrinterDiscovery)
```

## Proces: Service + UserSession + IPC

Windows Service (`XKantor.LocalAgent.Service`) działa w Session 0 - bez dostępu do interaktywnej
sesji zalogowanego operatora. Detekcja monitorów wymaga tej sesji
(`System.Windows.Forms.Screen.AllScreens`), więc:

```
XKANTOR AGENT SERVICE (Session 0)
        |
        | named pipe "XKantorLocalAgent.Monitors"
        v
XKANTOR USER SESSION AGENT (Session N - operator)
        |
        +-- MONITORS (Screen.AllScreens)
        +-- LOCAL UI/STATUS (NotifyIcon w zasobniku)
```

`UserSession` wysyła raport monitorów co ~20s (`TrayApplicationContext.cs`). Usługa cachuje
ostatni świeży raport (`Monitors/Ipc/MonitorCache.cs`, świeżość 60s) i go zwraca przez
`GET /api/v1/monitors`; jeśli UserSession nie jest podłączony, usługa best-effort próbuje
wykryć monitory sama (może zwrócić 0 - jawnie oznaczone jako `NotConfigured`, nie fałszywy
`Ready`).

## Local API

Kestrel nasłuchuje WYŁĄCZNIE na `127.0.0.1:<port>` (domyślnie 47311, konfigurowalny w
`agent.json`) - nigdy `0.0.0.0` (patrz `docs/SECURITY.md`). Trzy warstwy ochrony przed
nieautoryzowanym dostępem:

1. **Sieciowa** - loopback only (żadne urządzenie spoza maszyny nie dosięgnie portu).
2. **Origin validation** (`Api/Middleware/OriginValidationMiddleware.cs`) - odrzuca żądania z
   Originów spoza jawnej listy (`AgentConfig.AllowedOrigins`), broniąc przed złośliwą stroną
   otwartą w tej samej przeglądarce.
3. **Autoryzacja operacji** - krótkoterminowy token sesji (`Api/Auth/SessionAuthFilter.cs`,
   `Security/SessionTokenService.cs`) wymagany na każdym whitelisted endpointcie poza
   `/health`, `GET /api/v1/status` i parowaniem.

Endpointy - patrz `Core/CommandWhitelist.cs` i `Api/Endpoints/*.cs` dla pełnej listy.

## Status i health check

`AgentStatusService` (`Core/Modules/AgentStatusService.cs`) agreguje status wszystkich modułów
(`IAgentModule`) + tożsamości w jeden `AgentStatusReport` (`ONLINE`/`LIMITED`/`OFFLINE`/`ERROR`,
patrz `Core/Models/AgentOverallState.cs`). `/health` zwraca sam ten stan jako czysty tekst, do
szybkiego, częstego pollingu z xkantor.app; `/api/v1/status` zwraca pełny raport.
