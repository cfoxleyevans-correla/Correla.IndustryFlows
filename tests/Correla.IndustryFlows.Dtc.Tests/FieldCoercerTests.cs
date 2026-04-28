using Correla.IndustryFlows.Dtc.Schema;

namespace Correla.IndustryFlows.Dtc.Tests;

/// <summary>
/// Unit tests for <see cref="FieldCoercer"/>. One happy + one failure test per
/// DTC type variant, plus MPAN-as-string and enum-validation paths.
/// </summary>
public sealed class FieldCoercerTests
{
    // DI-free helper — builds a minimal DataItem for coercion tests.
    private static DataItem Item(string format, string validSetKind = "none",
        IReadOnlyList<EnumValue>? enumValues = null) =>
        new()
        {
            Ref = "J0000",
            Name = "Test",
            Domain = "Test",
            LogicalFormat = format,
            PhysicalLength = "0",
            Notes = string.Empty,
            ValidSet = new ValidSet
            {
                Kind = validSetKind,
                EnumValues = enumValues,
            },
        };

    // ---- NUM (integer, stored as string) ----

    [Fact]
    public void Coerce_NumInteger_ReturnsStringValue()
    {
        var (value, error) = FieldCoercer.Coerce("00042", Item("NUM(5)"));

        Assert.Null(error);
        Assert.Equal("00042", value); // Leading zeros preserved.
    }

    [Fact]
    public void Coerce_NumInteger_NonDigits_ReturnsError()
    {
        var (_, error) = FieldCoercer.Coerce("12A45", Item("NUM(5)"));

        Assert.NotNull(error);
    }

    [Fact]
    public void Coerce_NumInteger_ExceedsMaxLength_ReturnsError()
    {
        var (_, error) = FieldCoercer.Coerce("123456", Item("NUM(5)"));

        Assert.NotNull(error);
    }

    // ---- NUM (decimal) ----

    [Fact]
    public void Coerce_NumDecimal_ReturnsDecimalValue()
    {
        // NUM(9 ,1) means 9 total digits, 1 decimal place; value 045231 = 4523.1
        var (value, error) = FieldCoercer.Coerce("045231", Item("NUM(9 ,1)"));

        Assert.Null(error);
        Assert.Equal(4523.1m, value);
    }

    [Fact]
    public void Coerce_NumDecimal_NonDigits_ReturnsError()
    {
        var (_, error) = FieldCoercer.Coerce("45.3X1", Item("NUM(9 ,1)"));

        Assert.NotNull(error);
    }

    // ---- CHAR ----

    [Fact]
    public void Coerce_Char_ReturnsStringValue()
    {
        var (value, error) = FieldCoercer.Coerce("V", Item("CHAR(1)"));

        Assert.Null(error);
        Assert.Equal("V", value);
    }

    [Fact]
    public void Coerce_Char_ExceedsMaxLength_ReturnsError()
    {
        var (_, error) = FieldCoercer.Coerce("TOOLONG", Item("CHAR(3)"));

        Assert.NotNull(error);
    }

    [Fact]
    public void Coerce_Char_WithEnum_ValidValue_ReturnsValue()
    {
        var enumVals = new List<EnumValue> { new("A", "Alpha"), new("B", "Beta") };
        var (value, error) = FieldCoercer.Coerce("A", Item("CHAR(1)", "enum", enumVals));

        Assert.Null(error);
        Assert.Equal("A", value);
    }

    [Fact]
    public void Coerce_Char_WithEnum_InvalidValue_ReturnsError()
    {
        var enumVals = new List<EnumValue> { new("A", "Alpha"), new("B", "Beta") };
        var (_, error) = FieldCoercer.Coerce("X", Item("CHAR(1)", "enum", enumVals));

        Assert.NotNull(error);
    }

    // ---- DATE ----

    [Fact]
    public void Coerce_Date_ValidValue_ReturnsDateOnly()
    {
        var (value, error) = FieldCoercer.Coerce("20260415", Item("DATE"));

        Assert.Null(error);
        Assert.Equal(new DateOnly(2026, 4, 15), value);
    }

    [Fact]
    public void Coerce_Date_InvalidValue_ReturnsError()
    {
        var (_, error) = FieldCoercer.Coerce("20261332", Item("DATE"));

        Assert.NotNull(error);
    }

    // ---- DATETIME ----

    [Fact]
    public void Coerce_DateTime_ValidValue_ReturnsDateTime()
    {
        var (value, error) = FieldCoercer.Coerce("20260415093000", Item("DATETIME(14)"));

        Assert.Null(error);
        Assert.Equal(new DateTime(2026, 4, 15, 9, 30, 0), value);
    }

    [Fact]
    public void Coerce_DateTime_TooShort_ReturnsError()
    {
        var (_, error) = FieldCoercer.Coerce("202604150930", Item("DATETIME(14)"));

        Assert.NotNull(error);
    }

    // ---- BOOLEAN ----

    [Fact]
    public void Coerce_Boolean_T_ReturnsTrue()
    {
        var (value, error) = FieldCoercer.Coerce("T", Item("BOOLEAN"));

        Assert.Null(error);
        Assert.Equal(true, value);
    }

    [Fact]
    public void Coerce_Boolean_F_ReturnsFalse()
    {
        var (value, error) = FieldCoercer.Coerce("F", Item("BOOLEAN"));

        Assert.Null(error);
        Assert.Equal(false, value);
    }

    [Fact]
    public void Coerce_Boolean_InvalidChar_ReturnsError()
    {
        var (_, error) = FieldCoercer.Coerce("Y", Item("BOOLEAN"));

        Assert.NotNull(error);
    }

    // ---- INT ----

    [Fact]
    public void Coerce_Int_ValidValue_ReturnsInt()
    {
        var (value, error) = FieldCoercer.Coerce("42", Item("INT(3)"));

        Assert.Null(error);
        Assert.Equal(42, value);
    }

    [Fact]
    public void Coerce_Int_NonNumeric_ReturnsError()
    {
        var (_, error) = FieldCoercer.Coerce("XY", Item("INT(3)"));

        Assert.NotNull(error);
    }

    // ---- MPAN stored as string even though NUM ----

    [Fact]
    public void Coerce_Mpan_LeadingZerosPreserved()
    {
        // A 13-digit NUM must be stored as a string, not parsed as a number.
        var (value, error) = FieldCoercer.Coerce("0012345678901", Item("NUM(13)"));

        Assert.Null(error);
        Assert.IsType<string>(value);
        Assert.Equal("0012345678901", (string)value!);
    }

    // ---- TIME ----

    [Fact]
    public void Coerce_Time_ValidValue_ReturnsTimeOnly()
    {
        var (value, error) = FieldCoercer.Coerce("143000", Item("TIME"));

        Assert.Null(error);
        Assert.Equal(new TimeOnly(14, 30, 0), value);
    }

    [Fact]
    public void Coerce_Time_Invalid_ReturnsError()
    {
        var (_, error) = FieldCoercer.Coerce("256161", Item("TIME"));

        Assert.NotNull(error);
    }
}

