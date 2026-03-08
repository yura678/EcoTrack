using Domain.Common;
using Domain.Entities.Enterprises;

namespace Domain.Entities.Monitoring;

public class MeasureUnit : BaseEntity
{
    public string Symbol { get; private set; }
    public MeasureUnitDimension Dimension { get; private set; }
    public decimal ToBaseFactor { get; private set; }
    public DateTime CreatedAt { get; }

    public ICollection<Measurement>? Measurements { get; private set; } = [];
    public ICollection<EmissionLimit>? EmissionLimits { get; private set; } = [];

    private MeasureUnit(Guid id, string symbol, MeasureUnitDimension dimension,
        decimal toBaseFactor,
        DateTime createdAt)
    {
        Id = id;
        Symbol = symbol;
        Dimension = dimension;
        ToBaseFactor = toBaseFactor;

        CreatedAt = createdAt;
    }

    public static MeasureUnit New(Guid id, string symbol, MeasureUnitDimension dimension,
        decimal toBaseFactor) =>
        new(id, symbol, dimension, toBaseFactor, DateTime.UtcNow);
}

public enum MeasureUnitDimension
{
    // Маса (кг, г, т)
    Mass = 0,

    // Час (с, хв, год)
    Time = 1,

    // Довжина (м, км)
    Length = 2,

    // Температура (C, K)
    Temperature = 3,

    // Об'єм (м³, л)
    Volume = 4,

    // Об'ємна витрата (м³/год, л/с)
    VolumetricFlow = 5,

    // Масова концентрація (мг/м³, г/м³)
    MassConcentration = 6,

    // Масова витрата (кг/год, т/рік)
    MassFlow = 7,

    // Тиск (Па, бар)
    Pressure = 8,

    // Безрозмірні (ppm, %, шт)
    Dimensionless = 9
}