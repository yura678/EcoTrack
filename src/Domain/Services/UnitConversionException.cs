namespace Domain.Services;

public class UnitConversionException : Exception
{
    public UnitConversionException(string message) : base(message) { }
}
