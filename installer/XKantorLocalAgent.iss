; XKantor Local Hardware Agent - instalator (Inno Setup)
; Patrz docs/INSTALLATION.md dla pełnej instrukcji budowania i docs/DECISIONS.md #13 dla
; uzasadnienia wyboru Inno Setup. NIE skompilowane w środowisku deweloperskim (brak ISCC.exe) -
; wymaga: 1) dotnet publish (Service + UserSession, win-x64, self-contained) do publish\service
; i publish\usersession, 2) Inno Setup (https://jrsoftware.org/isinfo.php) na maszynie budującej.

#define MyAppName "XKantor Local Hardware Agent"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "AS-Soft"
#define MyServiceName "XKantorLocalAgent"
#define MyTaskName "XKantorLocalAgentUserSession"
#define MyServiceExeName "XKantor.LocalAgent.Service.exe"
#define MyUserSessionExeName "XKantor.LocalAgent.UserSession.exe"

[Setup]
AppId={{8F1B7B7E-2B7B-4E6B-9B3E-XKANTORAGENT1}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\XKantorLocalAgent
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputBaseFilename=XKantor-Local-Agent-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Files]
; Publikacja self-contained - patrz docs/INSTALLATION.md, krok "dotnet publish".
Source: "..\publish\service\*"; DestDir: "{app}\Service"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\usersession\*"; DestDir: "{app}\UserSession"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; %ProgramData%\XKantorLocalAgent - patrz Core/Configuration/ConfigPaths.cs. ACL: konto usługi
; (SYSTEM) + administratorzy - operatorzy stanowiska NIE powinni mieć bezpośredniego dostępu do
; secure\ (klucz prywatny zaszyfrowany DPAPI, ale defense-in-depth).
Name: "{commonappdata}\XKantorLocalAgent"; Permissions: system-full admins-full
Name: "{commonappdata}\XKantorLocalAgent\config"; Permissions: system-full admins-full
Name: "{commonappdata}\XKantorLocalAgent\secure"; Permissions: system-full admins-full
Name: "{commonappdata}\XKantorLocalAgent\logs"; Permissions: system-full admins-modify users-readexec

[Run]
; Rejestracja i start usługi Windows - patrz docs/INSTALLATION.md.
Filename: "sc.exe"; Parameters: "create {#MyServiceName} binPath= ""{app}\Service\{#MyServiceExeName}"" start= auto DisplayName= ""{#MyAppName}"""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "description {#MyServiceName} ""Lokalny most miedzy xkantor.app a sprzetem stanowiska (drukarki, monitory, wyswietlacz kursow). Patrz docs/ARCHITECTURE.md."""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden waituntilterminated

; Komponent sesji użytkownika - uruchamiany przy logowaniu KAŻDEGO użytkownika (patrz
; docs/DECISIONS.md #9 - detekcja monitorow wymaga interaktywnej sesji).
Filename: "schtasks.exe"; Parameters: "/create /tn ""{#MyTaskName}"" /tr ""\""{app}\UserSession\{#MyUserSessionExeName}\"""" /sc onlogon /rl limited /f"; Flags: runhidden waituntilterminated

; Zadanie "/sc onlogon" uruchomi UserSession.exe dopiero przy NASTĘPNYM logowaniu - jeśli
; operator jest zalogowany JUŻ TERAZ (typowy przypadek: instalator odpalony w jego własnej
; sesji), komponent bez tego wpisu w ogóle by nie wystartował aż do wylogowania/restartu, a
; usługa w Session 0 zwraca wtedy mylący "FALLBACK:MONITOR1" 1024x768 zamiast prawdziwych
; monitorów operatora (patrz MonitorsModule.cs). "runasoriginaluser" zdejmuje podniesienie UAC
; instalatora (PrivilegesRequired=admin) - uruchamiamy jako zwykły, niepodniesiony operator.
Filename: "{app}\UserSession\{#MyUserSessionExeName}"; Flags: nowait runasoriginaluser

[UninstallRun]
; Zamknij komponent sesji uzytkownika PRZED zatrzymaniem uslugi/usunieciem zadania - inaczej
; proces moze wciaz trzymac otwarty uchwyt do UserSession.exe podczas usuwania plikow (patrz
; [Code] StopRunningAgent nizej - ten sam problem co przy instalacji nowej wersji).
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyUserSessionExeName} /T"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden waituntilterminated
Filename: "schtasks.exe"; Parameters: "/delete /tn ""{#MyTaskName}"" /f"; Flags: runhidden waituntilterminated

; Uwaga: {commonappdata}\XKantorLocalAgent\secure NIE jest usuwane automatycznie przy
; odinstalowaniu - patrz docs/INSTALLATION.md ("nie unieważniamy parowania przez przypadke").

[Code]
// Problem: przy instalacji nowej wersji NA ISTNIEJACEJ (usluga XKantorLocalAgent dziala,
// UserSession.exe jest uruchomiony w sesji operatora) [Files] probuje nadpisac zablokowane
// pliki .exe/.dll -> Inno Setup pyta Retry/Abort/Ignore albo cicho pomija plik. StopRunningAgent
// jest wywolywane w PrepareToInstall - to jedyny hook ktory biegnie PRZED krokiem kopiowania
// plikow (ssInstall), wiec zamyka proces/usluge ZANIM instalator dotknie plikow, nie w trakcie.
procedure StopRunningAgent();
var
  ResultCode: Integer;
begin
  // Zamknij komponent sesji uzytkownika (moze byc uruchomiony w kilku sesjach naraz - /T zabija
  // takze procesy potomne). Kod bledu ignorowany celowo - "process not found" jest OK (np. przy
  // pierwszej instalacji albo gdy operator akurat nie jest zalogowany).
  Exec('taskkill.exe', '/F /IM {#MyUserSessionExeName} /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Zatrzymaj usluge Windows i POCZEKAJ az faktycznie przejdzie w stan Stopped (sc.exe stop
  // tylko wysyla zadanie zatrzymania i wraca od razu - proces moze jeszcze przez chwile trzymac
  // otwarte pliki .exe, stad aktywne czekanie do 15s zamiast stalego sleep). Brak uslugi (pierwsza
  // instalacja) jest ciche dzieki -ErrorAction SilentlyContinue.
  Exec('powershell.exe',
    '-NoProfile -ExecutionPolicy Bypass -Command "Stop-Service -Name ''{#MyServiceName}'' -Force -ErrorAction SilentlyContinue; ' +
    '$sw = [Diagnostics.Stopwatch]::StartNew(); ' +
    'while ((Get-Service -Name ''{#MyServiceName}'' -ErrorAction SilentlyContinue).Status -eq ''Running'' -and $sw.Elapsed.TotalSeconds -lt 15) { Start-Sleep -Milliseconds 200 }"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningAgent();
  Result := '';
end;
