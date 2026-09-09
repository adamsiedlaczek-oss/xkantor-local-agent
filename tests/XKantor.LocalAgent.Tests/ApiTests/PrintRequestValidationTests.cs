using System.ComponentModel.DataAnnotations;
using XKantor.LocalAgent.Api.Dto;

namespace XKantor.LocalAgent.Tests.ApiTests;

// Patrz etap 2, sekcja 8 - każda operacja whitelisted musi mieć jasno określony model danych
// i walidację.
public sealed class PrintRequestValidationTests
{
    private static List<ValidationResult> Waliduj(PrintRequestDto dto)
    {
        var wyniki = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), wyniki, validateAllProperties: true);
        return wyniki;
    }

    [Fact]
    public void PoprawneZadanie_BrakBledowWalidacji()
    {
        var dto = new PrintRequestDto { PrinterName = "Microsoft Print to PDF", ContentBase64 = Convert.ToBase64String("test"u8.ToArray()), DocumentName = "Paragon 1" };
        Assert.Empty(Waliduj(dto));
    }

    [Fact]
    public void PustaNazwaDrukarki_BladWalidacji()
    {
        var dto = new PrintRequestDto { PrinterName = "", ContentBase64 = "AA==", DocumentName = "x" };
        Assert.NotEmpty(Waliduj(dto));
    }

    [Fact]
    public void PustaTresc_BladWalidacji()
    {
        var dto = new PrintRequestDto { PrinterName = "Test", ContentBase64 = "", DocumentName = "x" };
        Assert.NotEmpty(Waliduj(dto));
    }

    [Fact]
    public void ZaDlugaNazwaDokumentu_BladWalidacji()
    {
        var dto = new PrintRequestDto { PrinterName = "Test", ContentBase64 = "AA==", DocumentName = new string('x', 500) };
        Assert.NotEmpty(Waliduj(dto));
    }
}
