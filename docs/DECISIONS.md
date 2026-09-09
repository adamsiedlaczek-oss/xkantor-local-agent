# Decyzje architektoniczne

Zapisane na bieżąco podczas autonomicznej implementacji (etap 2, sekcja 4) - żadna z poniższych
decyzji nie zatrzymała pracy; tam, gdzie decyzja wymaga dalszego doprecyzowania z zespołem
xkantor.app (zwłaszcza formatu integracji z backendem centrali), odpowiadająca pozycja czeka też
w `docs/QUESTIONS.csv`.

## 1. .NET 10, `net10.0-windows`, cała solucja

Repozytorium xkantor.app (siostrzany projekt) już używa `net10.0` (aktualnej, stabilnej wersji
LTS-owej linii .NET dostępnej w środowisku - `dotnet --list-sdks` pokazuje `10.0.103`). Agent
celowo dziedziczy tę samą wersję dla spójności toolchainu, i dokłada `-windows` do TFM
wszystkich projektów, bo Agent z definicji działa wyłącznie na Windows (stanowisko kantoru) -
żaden projekt nie udaje przenośności, której nie ma (dostęp do DPAPI, WinForms
`Screen.AllScreens`, `winspool.drv`, portów LPT/COM).

## 2. Osobne repozytorium, poza `E:\kantorApp`

Etap 2 (sekcja 2/3) wprost wymaga osobnego projektu/repozytorium i zakazuje zmiany istniejącego
xkantor.app. Repozytorium żyje w `E:\agentApp` (katalog wskazany przez użytkownika w trakcie
pracy jako zaufany do tego zadania) - osobny `.git`, nie jest podfolderem/submodułem
`E:\kantorApp`.

## 3. Modularna struktura solucji: 7 bibliotek + Service + UserSession + Tests

`src/XKantor.LocalAgent.{Core,Security,Printing,Devices,Monitors,CurrencyDisplay,Api}` jako
osobne biblioteki klas, plus dwa uruchamialne: `Service` (Windows Service + lokalne API) i
`UserSession` (komponent działający w sesji zalogowanego operatora - patrz decyzja 9). Podział
zgodny z przykładową strukturą z etapu 2 (sekcja 3), z jedną zmianą: `Api` jest osobnym
projektem (nie częścią `Service`), żeby endpointy dało się w przyszłości przetestować/hostować
niezależnie od modelu Windows Service.

## 4. Dane w `%ProgramData%\XKantorLocalAgent`, nie w folderze instalacyjnym

Instalator (etap 2, sekcja 26/27) musi umożliwiać aktualizację bez utraty tożsamości Agenta.
`%ProgramData%` przetrwa reinstalację/upgrade pliku `.exe` w `Program Files`. Patrz
`Core/Configuration/ConfigPaths.cs`.

## 5. DPAPI (`ProtectedData`, `DataProtectionScope.LocalMachine`) do klucza prywatnego

Etap 2 (sekcja 10) wymaga "odpowiednich mechanizmów Windows/.NET" do przechowywania sekretów -
DPAPI to standardowy, wbudowany mechanizm .NET/Windows do tego celu (bez własnej kryptografii
"na kolanie"). Zakres `LocalMachine` (nie `CurrentUser`) - usługa Windows zwykle działa jako
`LocalSystem`/`NetworkService`, konto bez załadowanego profilu użytkownika, więc `CurrentUser`
byłby zawodny. Świadomy kompromis: ochrona DPAPI `LocalMachine` chroni przed odczytem pliku POZA
tą maszyną (np. skopiowanym na inny komputer), nie przed innym procesem na TEJ SAMEJ maszynie z
uprawnieniami administratora - dodatkowa warstwa to ACL katalogu `secure\` (patrz
`docs/INSTALLATION.md`).

## 6. Lekki podpisany JSON zamiast pełnego X.509/PKI

"Certyfikat" Agenta (`Security/IdentityModels.cs` - `StationCertificate`) to podpisany JSON, nie
prawdziwy certyfikat X.509. Pełne X.509 wymagałoby CA (własnego lub zewnętrznego) po stronie
xkantor.app, którego kontrakt/istnienie jest poza zakresem tego repozytorium na tym etapie.
Lekki format jest wystarczający do spełnienia wymagań etapu 2 (stała para kluczy + odnawialne
poświadczenie, okno odnowienia, grace period) i łatwy do zastąpienia prawdziwym X.509/mTLS w
przyszłości bez zmiany reszty architektury (interfejs `RenewalService`/`IdentityService` by się
nie zmienił).

## 7. Agent nigdy nie łączy się z Internetem - przeglądarka jako przekaźnik

Diagram architektury (etap 2, sekcja 1) pokazuje jednoznacznie: `XKANTOR.APP (VPS) --HTTPS-->
PRZEGLĄDARKA --LOCALHOST--> AGENT`. Nie ma strzałki Agent -> Internet. Konsekwentnie: parowanie
(`PairingService`), odnowienie tożsamości (`RenewalService`) i wszystkie inne operacje, które w
naiwnym projekcie wymagałyby wywołania do centrali, są zaprojektowane tak, żeby Agent
przygotowywał/konsumował dane, a WYŁĄCZNIE przeglądarka wykonywała rzeczywiste żądania HTTPS do
xkantor.app. Patrz `docs/PAIRING.md`.

## 8. Krótkoterminowe tokeny sesji podpisane kluczem losowanym przy starcie procesu

Etap 2 (sekcja 14) zakazuje jednego stałego sekretu w JavaScript i wymaga autoryzacji
ograniczonej czasowo/zakresem/stanowiskiem. `SessionTokenService` generuje losowy klucz HMAC
WYŁĄCZNIE w pamięci przy starcie procesu (nigdy nie trafia na dysk) - token traci ważność nie
tylko po `ExpiresAtUtc`, ale też przy każdym restarcie Agenta. Wystawienie tokenu wymaga
uprzedniej znajomości `StationSecret` (ustalonego raz przy parowaniu), więc przeglądarka nigdy
nie trzyma jednego stałego sekretu używanego bezpośrednio do autoryzacji operacji - tylko do
uzyskania kolejnych, krótkotrwałych tokenów.

## 9. Architektura Service + UserSession + IPC (named pipe)

Windows Service działa w Session 0, bez dostępu do interaktywnej sesji zalogowanego operatora -
`System.Windows.Forms.Screen.AllScreens` (wykrywanie monitorów) w Session 0 może zwracać
puste/nieaktualne dane (udokumentowane ograniczenie Windows, nie błąd). Etap 2 (sekcja 24-25)
wprost przewiduje taką architekturę. `XKantor.LocalAgent.UserSession` to lekka aplikacja z
ikoną w zasobniku systemowym (bez głównego okna), uruchamiana w sesji operatora (autostart -
patrz `docs/INSTALLATION.md`), która wykonuje detekcję monitorów i przesyła wynik do usługi
przez named pipe (`Monitors/Ipc/MonitorPipeServer.cs`/`MonitorPipeClient.cs`). Usługa, gdy
UserSession nie jest (jeszcze) podłączony, best-effort próbuje wykryć monitory sama - w Session 0
prawdopodobnie zwróci 0, co jest jawnie raportowane jako `NotConfigured`, nie fałszywie jako
`Ready`.

## 10. `IPrinter`: trzy implementacje, żadna nie wykonuje poleceń systemowych

`RawWinspoolPrinter` (P/Invoke `winspool.drv`, tryb RAW - ten sam sprawdzony wzorzec co
`xkantor.app`'s `Data/Wydruki/RawPrinterService.cs`, ale własna kopia w tym repo, bo etap 2 nie
pozwala dotykać drugiego projektu), `LptPrinter` (bezpośredni zapis do `\\.\LPTn`),
`WindowsDriverPrinter` (druk tekstu przez sterownik Windows/GDI, `System.Drawing.Printing`).
Każda przyjmuje wyłącznie gotowe bajty/nazwę drukarki z whitelisty aktualnie wykrytych urządzeń
(patrz `Api/Endpoints/PrintEndpoints.cs`) - nigdy dowolne polecenie.

## 11. Command whitelist jako statyczny rejestr + testy pilnujące jego zawartości

`Core/CommandWhitelist.cs` to jawny, mały słownik komenda -> opis. Testy
(`tests/.../CoreTests/CommandWhitelistTests.cs`) porównują go z oczekiwanym zbiorem i
sprawdzają, że żadna nazwa nie przypomina ogólnego wykonania polecenia (`EXECUTE`/`CMD`/
`SHELL`/...) - świadoma bariera przed przypadkowym rozszerzeniem API w niebezpiecznym kierunku
w przyszłości.

## 12. Serilog zamiast własnej rotacji logów

Etap 2 (sekcja 22) wymaga profesjonalnego logowania z rotacją. Zamiast pisać własny
`RollingFileLogger`, użyto `Serilog.Sinks.File` z `rollingInterval: Day` i
`retainedFileCountLimit: 30` - standardowe, przetestowane narzędzie, mniej kodu do utrzymania.
Nigdy nie logujemy `IdentityRecord` w całości (patrz komentarz w `IdentityModels.cs`) ani
tokenów/sekretów.

## 13. Instalator: Inno Setup (skrypt), nie WiX/MSIX

Na maszynie deweloperskiej nie było zainstalowanego ani WiX Toolset, ani Inno Setup (`ISCC.exe`)
- `docs/INSTALLATION.md` i `installer/README.md` to jawnie odnotowują. Wybrano Inno Setup jako
docelowe narzędzie (prostszy, jeden plik `.iss`, szeroko używany do instalatorów usług Windows)
zamiast WiX (bardziej złożony, wymaga osobnego SDK) - `installer/XKantorLocalAgent.iss` jest
gotowy do skompilowania na maszynie z zainstalowanym Inno Setup (`iscc.exe`), ale NIE został (i
nie mógł zostać) skompilowany do `.exe` w tym środowisku - patrz etap 2, sekcja 33 ("nie udawaj
implementacji").

## 14. `UserSession`: WinForms + `NotifyIcon`, bez okna głównego

Etap 2 (sekcja 25) wymaga "LOCAL UI/STATUS" dla komponentu sesji użytkownika. Zamiast pełnego
okna (niepotrzebne dla stanowiska kasjera), zastosowano minimalną ikonę w zasobniku systemowym
z podpowiedzią statusu i menu kontekstowym (`Zakończ`) - wystarczające dla operatora, żeby
zobaczyć "czy komponent działa", bez rozpraszającego interfejsu.
