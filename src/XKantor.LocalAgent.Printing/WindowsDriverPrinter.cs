using System.Drawing;
using System.Drawing.Printing;
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
    private const float RozmiarCzcionki = 10f;
    private const float MarginesPunkty = 40f;

    public string Nazwa { get; }

    public WindowsDriverPrinter(string nazwaDrukarkiWindows)
    {
        Nazwa = nazwaDrukarkiWindows;
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

            using var czcionka = new Font(FontFamily.GenericMonospace, RozmiarCzcionki);
            Exception? bladStrony = null;

            dokument.PrintPage += (_, e) =>
            {
                try
                {
                    var g = e.Graphics!;
                    var wysokoscLinii = czcionka.GetHeight(g);
                    var y = MarginesPunkty;
                    var dolnaGranica = e.MarginBounds.Bottom;

                    while (pozycja < linie.Length && y + wysokoscLinii <= dolnaGranica)
                    {
                        g.DrawString(linie[pozycja], czcionka, Brushes.Black, MarginesPunkty, y);
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
