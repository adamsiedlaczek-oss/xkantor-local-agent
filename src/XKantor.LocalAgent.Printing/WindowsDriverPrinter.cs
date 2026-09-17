using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace XKantor.LocalAgent.Printing;

// Drukowanie przez sterownik Windows (w odróżnieniu od RawWinspoolPrinter/LptPrinter, które
// omijają sterownik) - dla zwykłych drukarek laserowych/atramentowych bez trybu RAW/ESC-POS.
// `dane` jest interpretowane jako zwykły tekst (UTF-8, linie \n) - formatowanie/łamanie stron
// robi sterownik/PCL, Agent tylko renderuje linie przez GDI (System.Drawing.Printing), zgodnie
// z etap 2 sekcja 17 ("rozróżnij logicznie: drukowanie przez system Windows, RAW, LPT").
[SupportedOSPlatform("windows")]
public sealed class WindowsDriverPrinter : IPrinter
{
    private const float RozmiarCzcionkiDomyslny = 10f;
    private const float MarginesPunktyDomyslny = 40f;

    // Wąska rolka (57/58mm, 80mm) zainstalowana jako zwykła drukarka Windows (Standard "bez
    // kodów sterujących" - patrz xkantor.app HardwareAgentService.CelParagonu) - sterownik sam
    // formatuje tekst, ale strona domyślnie ma rozmiar A4/Letter (nie wie, że fizycznie to wąska
    // rolka), więc bez jawnego PaperSize/Margins tekst ląduje w lewym górnym rogu ogromnej
    // strony zamiast wypełniać całą szerokość rolki (zgłoszenie użytkownika 2026-09-17). Margines
    // ~2mm z każdej strony - tyle realnie mają typowe rolki paragonowe, nie margines A4.
    private const float SetnychCalaNaMm = 100f / 25.4f;
    private const int MarginesWaskiSetne = 8; // ~2mm
    private const int WysokoscCiagliSetne = 3000; // ~76cm - jedna "strona" starcza na cały paragon
    private const float MinRozmiarCzcionki = 5f;
    private const float MaxRozmiarCzcionki = 14f;

    public string Nazwa { get; }
    private readonly int? _szerokoscRolkiMm;

    public WindowsDriverPrinter(string nazwaDrukarkiWindows, int? szerokoscRolkiMm = null)
    {
        Nazwa = nazwaDrukarkiWindows;
        _szerokoscRolkiMm = szerokoscRolkiMm;
    }

    public Task<PrintResult> DrukujAsync(byte[] dane, string nazwaDokumentu, CancellationToken ct = default)
    {
        try
        {
            var linie = Encoding.UTF8.GetString(dane).Replace("\r\n", "\n").Split('\n');
            var pozycja = 0;

            using var dokument = new PrintDocument();
            dokument.PrinterSettings.PrinterName = Nazwa;
            if (!dokument.PrinterSettings.IsValid)
            {
                return Task.FromResult(new PrintResult(false, $"Drukarka '{Nazwa}' nie jest prawidłowa/dostępna w systemie."));
            }
            dokument.DocumentName = nazwaDokumentu;

            var marginesPunkty = MarginesPunktyDomyslny;
            if (_szerokoscRolkiMm is int mm && mm > 0)
            {
                var szerokoscSetne = (int)Math.Round(mm * SetnychCalaNaMm);
                dokument.DefaultPageSettings.PaperSize = new PaperSize("Rolka", szerokoscSetne, WysokoscCiagliSetne);
                dokument.DefaultPageSettings.Margins = new Margins(MarginesWaskiSetne, MarginesWaskiSetne, MarginesWaskiSetne, MarginesWaskiSetne);
                marginesPunkty = MarginesWaskiSetne / 100f * 72f; // setne cala -> punkty (1 cal = 72 pkt)
            }

            // Czcionka monospace (font stały - patrz komentarz w xkantor.app) w domyślnym
            // rozmiarze mogłaby być szersza niż fizyczna rolka (10pt Courier ≈ 2mm/znak, a
            // paragon bywa formatowany na 32-48 znaków w linii) - dobieramy rozmiar tak, żeby
            // NAJDŁUŻSZA faktyczna linia treści zmieściła się w szerokości do druku, zamiast na
            // sztywno zakładać konkretną liczbę znaków (odporne na każdą zawartość, nie tylko
            // paragon walutowy).
            using var bitmapPomiarowa = new Bitmap(1, 1);
            using var gPomiar = Graphics.FromImage(bitmapPomiarowa);
            var rozmiarCzcionki = RozmiarCzcionkiDomyslny;
            if (_szerokoscRolkiMm is int szerRolkiMm2 && szerRolkiMm2 > 0)
            {
                var najdluzszaLinia = linie.Length > 0 ? linie.Max(l => l.Length) : 0;
                if (najdluzszaLinia > 0)
                {
                    var szerokoscDoDrukuPunkty = (szerRolkiMm2 * SetnychCalaNaMm - 2 * MarginesWaskiSetne) / 100f * 72f;
                    using var probna = new Font(FontFamily.GenericMonospace, RozmiarCzcionkiDomyslny);
                    var zmierzonaSzerokosc = gPomiar.MeasureString(new string('0', najdluzszaLinia), probna).Width;
                    if (zmierzonaSzerokosc > 0)
                    {
                        var skala = szerokoscDoDrukuPunkty / zmierzonaSzerokosc;
                        rozmiarCzcionki = Math.Clamp(RozmiarCzcionkiDomyslny * skala, MinRozmiarCzcionki, MaxRozmiarCzcionki);
                    }
                }
            }

            using var czcionka = new Font(FontFamily.GenericMonospace, rozmiarCzcionki);
            Exception? bladStrony = null;

            dokument.PrintPage += (_, e) =>
            {
                try
                {
                    var g = e.Graphics!;
                    var wysokoscLinii = czcionka.GetHeight(g);
                    var y = marginesPunkty;
                    var dolnaGranica = e.MarginBounds.Bottom;

                    while (pozycja < linie.Length && y + wysokoscLinii <= dolnaGranica)
                    {
                        g.DrawString(linie[pozycja], czcionka, Brushes.Black, marginesPunkty, y);
                        y += wysokoscLinii;
                        pozycja++;
                    }

                    e.HasMorePages = pozycja < linie.Length;
                }
                catch (Exception ex)
                {
                    bladStrony = ex;
                    e.HasMorePages = false;
                }
            };

            dokument.Print();

            return Task.FromResult(bladStrony is null
                ? new PrintResult(true, null)
                : new PrintResult(false, bladStrony.Message));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new PrintResult(false, ex.Message));
        }
    }
}
