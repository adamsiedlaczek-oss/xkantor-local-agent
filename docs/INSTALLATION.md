# Instalacja

## Wymagania

- Windows 10/11 lub Windows Server 2019+ (x64).
- .NET 10 Desktop Runtime (instalator go dołącza/sprawdza - patrz `installer/XKantorLocalAgent.iss`).
- Uprawnienia administratora (rejestracja usługi Windows).

## Docelowy proces (po skompilowaniu instalatora - patrz niżej)

```
URUCHOM XKantor-Local-Agent-Setup.exe
        |
        v
INSTALUJ (usługa + komponent sesji użytkownika)
        |
        v
AGENT START (usługa startuje automatycznie)
        |
        v
DEVICE DETECTION (pierwszy /api/v1/status pokazuje wykryte drukarki/monitory)
        |
        v
READY (do sparowania - patrz docs/PAIRING.md)
```

Administrator nie kopiuje plików ręcznie, nie konfiguruje autostartu, nie odpala CMD/usług
ręcznie - wszystko robi instalator.

## Budowanie instalatora

Instalator (`installer/XKantorLocalAgent.iss`) używa **Inno Setup** - patrz uzasadnienie w
`docs/DECISIONS.md` #13. Na maszynie deweloperskiej, na której powstał ten projekt, Inno Setup
NIE był zainstalowany (`ISCC.exe` nie znaleziono w `PATH`), więc finalny `Setup.exe` nie został
(i nie mógł zostać) skompilowany w tym środowisku - to jawnie odnotowana granica tego etapu
(etap 2, sekcja 33: "nie udawaj implementacji").

Aby zbudować `Setup.exe` na maszynie z Inno Setup:

```powershell
# 1. Publikacja self-contained (Service + UserSession), win-x64
dotnet publish src/XKantor.LocalAgent.Service -c Release -r win-x64 --self-contained true -o publish/service
dotnet publish src/XKantor.LocalAgent.UserSession -c Release -r win-x64 --self-contained true -o publish/usersession

# 2. Kompilacja instalatora (wymaga Inno Setup: https://jrsoftware.org/isinfo.php)
iscc installer\XKantorLocalAgent.iss
```

Wynik: `installer\Output\XKantor-Local-Agent-Setup.exe`.

## Co robi instalator (`installer/XKantorLocalAgent.iss`)

1. Kopiuje pliki opublikowane w kroku 1 do `{autopf}\XKantorLocalAgent`.
2. Rejestruje `XKantor.LocalAgent.Service.exe` jako usługę Windows (`XKantorLocalAgent`,
   autostart) - `sc.exe create` w sekcji `[Run]`.
3. Rejestruje `XKantor.LocalAgent.UserSession.exe` w Harmonogramie zadań, uruchamianie przy
   logowaniu KAŻDEGO użytkownika (`schtasks /create ... /sc onlogon`) - patrz
   `docs/DECISIONS.md` #9 (dlaczego osobny komponent w sesji użytkownika).
4. Tworzy `%ProgramData%\XKantorLocalAgent\{config,secure,logs}` z ACL-ami ograniczającymi
   dostęp do konta usługi i administratorów (patrz `docs/SECURITY.md`).
5. Startuje usługę.
6. Odinstalowanie (`[UninstallRun]`) zatrzymuje/usuwa usługę i zadanie harmonogramu; **NIE**
   usuwa `%ProgramData%\XKantorLocalAgent\secure` domyślnie (żeby przypadkowe odinstalowanie nie
   unieważniło parowania) - użytkownik może usunąć ręcznie, jeśli chce pełny reset tożsamości.

## Konfiguracja stanowiska

`%ProgramData%\XKantorLocalAgent\config\agent.json` (tworzony automatycznie przy pierwszym
starcie z wartościami domyślnymi - patrz `Core/Configuration/ConfigStore.cs`):

```json
{
  "AgentVersion": "1.0.0",
  "ListenPort": 47311,
  "AllowedOrigins": ["https://xkantor.app", "https://www.xkantor.app"],
  "EnabledModules": { "Printing": true, "Devices": true, "Monitors": true, "CurrencyDisplay": true }
}
```

Zmiana portu/Originów wymaga restartu usługi (`Restart-Service XKantorLocalAgent`).

## Ręczna weryfikacja po instalacji (bez czekania na parowanie z xkantor.app)

```powershell
Get-Service XKantorLocalAgent
Invoke-RestMethod http://127.0.0.1:47311/health
```

Powinno zwrócić `OFFLINE` (stanowisko jeszcze nie sparowane - to oczekiwane, nie błąd).
