
using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record MeasureUnitDto(
    Guid Id,
    string Symbol,
    MeasureUnitDimension Dimension,
    decimal ToBaseFactor,
    DateTime CreatedAt)
{
    public static MeasureUnitDto FromDomainModel(MeasureUnit unit)
    {
        return new MeasureUnitDto(
            unit.Id,
            unit.Symbol,
            unit.Dimension,
            unit.ToBaseFactor,
            unit.CreatedAt
        );
    }
}