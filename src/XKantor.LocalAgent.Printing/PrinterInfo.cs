namespace XKantor.LocalAgent.Printing;

public enum PrinterKind
{
    Windows,
    Raw,
    Lpt
}

public sealed record PrinterInfo(string Name, PrinterKind Kind, bool IsDefault, bool IsAvailable);
