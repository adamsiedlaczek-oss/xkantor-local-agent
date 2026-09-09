# Instalator

`XKantorLocalAgent.iss` to skrypt **Inno Setup**. Pełna instrukcja budowania:
`../docs/INSTALLATION.md`.

**Status na koniec tego etapu:** skrypt jest kompletny i gotowy do kompilacji, ale NIE został
skompilowany do `.exe` w środowisku deweloperskim - `ISCC.exe` (kompilator Inno Setup) nie był
zainstalowany na tej maszynie. Patrz `../docs/DECISIONS.md` (decyzja #13) - świadomie
odnotowane, zgodnie z etapem 2, sekcja 33 ("nie udawaj implementacji, jawnie oznacz brak
konkretnego narzędzia").

Aby zbudować `XKantor-Local-Agent-Setup.exe`:

1. Zainstaluj Inno Setup: <https://jrsoftware.org/isinfo.php>
2. `dotnet publish` obu uruchamialnych projektów (Service, UserSession) - patrz komendy w
   `../docs/INSTALLATION.md`.
3. `iscc XKantorLocalAgent.iss`
