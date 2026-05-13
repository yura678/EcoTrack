using NetTopologySuite.Geometries;

namespace Domain.Entities.EmissionSources;

public class WaterEmissionSource : EmissionSource
{
    public string Receiver { get; private set; }
    public double DesignFlowRate { get; private set; }

    private WaterEmissionSource(Guid id, Guid installationId, string code, Point location,
        string receiver, double designFlowRate,
        DateTime createdAt, DateTime? updatedAt) : base(id, installationId, code, location, createdAt,
        updatedAt)
    {
        Receiver = receiver;
        DesignFlowRate = designFlowRate;
    }

    public static WaterEmissionSource New(Guid id, Guid installationId, string code,
        double latitude, double longitude,
        string receiver, double designFlowRate) =>
        new(id, installationId, code, BuildPoint(latitude, longitude),
            receiver, designFlowRate, DateTime.UtcNow, null);

    public void UpdateDetails(string receiver, double designFlowRate)
    {
        Receiver = receiver;
        DesignFlowRate = designFlowRate;

        UpdatedAt = DateTime.UtcNow;
    }
}