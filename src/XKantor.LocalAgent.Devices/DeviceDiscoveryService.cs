using System.IO.Ports;
using System.Runtime.Versioning;
using XKantor.LocalAgent.Printing;

namespace XKantor.LocalAgent.Devices;

// Wykrywanie urządzeń/portów - patrz etap 2, sekcja 6 (moduł DEVICES: "wykrywanie urządzeń
// drukujących, portów, urządzeń obsługiwanych przez przyszłe moduły"). Odpowiada za
// GET_DEVICE_STATUS; wykrywanie drukarek Windows/LPT deleguje do Printing.PrinterDiscovery
// (jedno źródło prawdy, żeby /api/v1/printers i /api/v1/devices nigdy się nie rozjechały).
[SupportedOSPlatform("windows")]
public sealed class DeviceDiscoveryService
{
    public IReadOnlyList<DeviceInfo> WykryjWszystkie()
    {
        var wynik = new List<DeviceInfo>();

        foreach (var d in PrinterDiscovery.WykryjDrukarkiWindows())
        {
            wynik.Add(new DeviceInfo(d.Name, DeviceKind.PrinterWindows, d.IsAvailable));
        }

        foreach (var lpt in PrinterDiscovery.WykryjPortyLpt())
        {
            wynik.Add(new DeviceInfo(lpt.Name, DeviceKind.PrinterLpt, lpt.IsAvailable));
        }

        foreach (var com in SerialPort.GetPortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            wynik.Add(new DeviceInfo(com, DeviceKind.SerialPort, IsAvailable: true));
        }

        return wynik;
    }
}
