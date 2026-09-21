using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace XKantor.LocalAgent.Monitors;

// Stabilna identyfikacja monitora - patrz zadanie "tablica kursów", sekcja 7 ("NIE
// IDENTYFIKUJ MONITORA JAKO MONITOR 2"). WinForms Screen.DeviceName ("\\.\DISPLAY1") NIE jest
// stabilny (zmienia się po zmianie portu/kabla/kolejności wykrywania) - tu budujemy identyfikator
// oparty o EDID (producent/kod produktu/numer seryjny), z jawnym, nigdy-nie-rzucającym
// łańcuchem fallbacków, bo nie każdy monitor/sterownik/maszyna wirtualna udostępnia EDID.
//
// Technika korelacji (nie istnieje jedno wywołanie Win32, które zwraca to razem):
// 1. EnumDisplayDevicesW - z nazwy adaptera (\\.\DISPLAYn, ta sama co Screen.DeviceName)
//    pobieramy interfejs PODŁĄCZONEGO monitora (flaga EDD_GET_DEVICE_INTERFACE_NAME) - ścieżka
//    postaci "\\?\DISPLAY#<PNPID>#<instancja z UID####>#{guid}".
// 2. WMI root\wmi, klasa WmiMonitorID - ManufacturerName/ProductCodeID/SerialNumberID/
//    UserFriendlyName jako ushort[] (kody znaków EDID), z InstanceName postaci
//    "DISPLAY\<PNPID>\<instancja>_<n>".
// 3. Korelacja przez wspólny PNPID + UID#### (numer instancji z EnumDisplayDevices bywa inny niż
//    prefiks w WMI InstanceName, ale fragment "UID####" jest wspólny w obu).
[SupportedOSPlatform("windows")]
public static class MonitorStableIdResolver
{
    public sealed record Wynik(string StableId, string? DisplayLabel);

    private static readonly Regex UidRegex = new(@"UID(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Wynik Rozpoznaj(string deviceName, int indeks)
    {
        try
        {
            var sciezkaInterfejsu = PobierzSciezkeInterfejsuMonitora(deviceName);
            if (sciezkaInterfejsu is not null)
            {
                var (pnpId, uid) = WyciagnijPnpIUid(sciezkaInterfejsu);
                if (pnpId is not null)
                {
                    var edid = SpróbujOdczytacEdid(pnpId, uid);
                    if (edid is not null)
                    {
                        var klucz = uid ?? pnpId;
                        var stabilne = $"EDID:{edid.Value.Manufacturer}:{edid.Value.ProductCode}:{edid.Value.Serial}:{klucz}";
                        return new Wynik(stabilne, edid.Value.DisplayLabel);
                    }
                }

                // EDID niedostępne (sterownik/maszyna wirtualna/RDP) - ścieżka interfejsu wciąż
                // jest o wiele stabilniejsza niż numer kolejności Screen.AllScreens, ale NIE jest
                // odporna na zmianę portu fizycznego - jawnie oznaczone prefiksem, żeby wywołujący
                // (BoardEndpoints/UI) mógł to zakomunikować operatorowi zamiast udawać pewność.
                return new Wynik($"DEVPATH:{sciezkaInterfejsu}", null);
            }
        }
        catch
        {
            // Wykrywanie monitorów nigdy nie może wywalić się przez ten dodatek - w razie
            // jakiegokolwiek błędu P/Invoke/WMI spadamy do najsłabszego, ale zawsze dostępnego
            // identyfikatora.
        }

        return new Wynik($"FALLBACK:MONITOR{indeks + 1}", null);
    }

    private static string? PobierzSciezkeInterfejsuMonitora(string deviceName)
    {
        for (uint i = 0; ; i++)
        {
            var adapter = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!EnumDisplayDevicesW(null, i, ref adapter, 0)) break;
            if (!string.Equals(adapter.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)) continue;

            for (uint m = 0; ; m++)
            {
                var monitor = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                if (!EnumDisplayDevicesW(adapter.DeviceName, m, ref monitor, EDD_GET_DEVICE_INTERFACE_NAME)) break;

                const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
                if ((monitor.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0 && !string.IsNullOrWhiteSpace(monitor.DeviceID))
                {
                    return monitor.DeviceID;
                }
            }

            return null;
        }

        return null;
    }

    private static (string? PnpId, string? Uid) WyciagnijPnpIUid(string sciezkaInterfejsu)
    {
        // "\\?\DISPLAY#ACM071A#5&1a2b3c4d&0&UID4352#{guid}" -> segmenty ["", "?", "DISPLAY",
        // "ACM071A", "5&1a2b3c4d&0&UID4352", "{guid}"].
        var segmenty = sciezkaInterfejsu.Split('#', StringSplitOptions.RemoveEmptyEntries);
        if (segmenty.Length < 3) return (null, null);

        var pnpId = segmenty[1];
        var instancja = segmenty[2];
        var uidMatch = UidRegex.Match(instancja);
        var uid = uidMatch.Success ? $"UID{uidMatch.Groups[1].Value}" : null;
        return (pnpId, uid);
    }

    private readonly record struct EdidInfo(string Manufacturer, string ProductCode, string Serial, string? DisplayLabel);

    private static EdidInfo? SpróbujOdczytacEdid(string pnpId, string? uid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorID");
            using var wyniki = searcher.Get();

            foreach (ManagementObject mo in wyniki)
            {
                using (mo)
                {
                    var instanceName = mo["InstanceName"] as string ?? "";
                    if (!instanceName.Contains(pnpId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (uid is not null && !instanceName.Contains(uid, StringComparison.OrdinalIgnoreCase)) continue;

                    var manufacturer = DekodujEdidTekst(mo["ManufacturerName"] as ushort[]);
                    var productCode = DekodujEdidTekst(mo["ProductCodeID"] as ushort[]);
                    var serial = DekodujEdidTekst(mo["SerialNumberID"] as ushort[]);
                    var friendly = DekodujEdidTekst(mo["UserFriendlyName"] as ushort[]);

                    if (string.IsNullOrWhiteSpace(manufacturer) && string.IsNullOrWhiteSpace(productCode))
                    {
                        continue;
                    }

                    var etykieta = !string.IsNullOrWhiteSpace(friendly)
                        ? friendly
                        : !string.IsNullOrWhiteSpace(manufacturer) || !string.IsNullOrWhiteSpace(productCode)
                            ? $"{manufacturer} {productCode}".Trim()
                            : null;

                    return new EdidInfo(manufacturer, productCode, serial, etykieta);
                }
            }
        }
        catch
        {
            // WMI root\wmi bywa niedostępne (maszyna wirtualna/RDP/sterownik bez EDID) - to nie
            // jest błąd krytyczny, tylko powód do fallbacku na DEVPATH (patrz Rozpoznaj powyżej).
        }

        return null;
    }

    private static string DekodujEdidTekst(ushort[]? surowe)
    {
        if (surowe is null || surowe.Length == 0) return "";
        var znaki = surowe.TakeWhile(v => v != 0 && v != 0xFFFF).Select(v => (char)v).ToArray();
        return new string(znaki).Trim();
    }

    private const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumDisplayDevicesW(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);
}
