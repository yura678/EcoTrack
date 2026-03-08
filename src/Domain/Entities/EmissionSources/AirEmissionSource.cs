namespace Domain.Entities.EmissionSources;

public class AirEmissionSource : EmissionSource
{
    public double Height { get; private set; }
    public double Diameter { get; private set; }
    public double DesignFlowRate { get; private set; }

    private AirEmissionSource(Guid id, Guid installationId, string code, double height,
        double diameter, double designFlowRate,
        DateTime createdAt, DateTime? updatedAt) : base(id, installationId, code,
        createdAt,
        updatedAt)
    {
        Height = height;
        Diameter = diameter;
        DesignFlowRate = designFlowRate;
    }

    public static AirEmissionSource New(Guid id, Guid installationId, string code, double height,
        double diameter,
        double designFlowRate) =>
        new(id, installationId, code, height, diameter, designFlowRate, DateTime.UtcNow, null);

    public void UpdateDetails(double height, double diameter, double designFlowRate)
    {
        Height = height;
        Diameter = diameter;
        DesignFlowRate = designFlowRate;

        UpdatedAt = DateTime.UtcNow;
    }
}