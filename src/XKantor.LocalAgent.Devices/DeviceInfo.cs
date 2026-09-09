namespace XKantor.LocalAgent.Devices;

public enum DeviceKind
{
    PrinterWindows,
    PrinterLpt,
    SerialPort
}

public sealed record DeviceInfo(string Name, DeviceKind Kind, bool IsAvailable);
