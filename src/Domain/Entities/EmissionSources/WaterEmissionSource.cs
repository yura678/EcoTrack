namespace Domain.Entities.EmissionSources;

public class WaterEmissionSource : EmissionSource
{
    public string Receiver { get; private set; }
    public double DesignFlowRate { get; private set; }

    private WaterEmissionSource(Guid id, Guid installationId, string code,
        string receiver, double designFlowRate,
        DateTime createdAt, DateTime? updatedAt) : base(id, installationId, code, createdAt,
        updatedAt)
    {
        Receiver = receiver;
        DesignFlowRate = designFlowRate;
    }

    public static WaterEmissionSource New(Guid id, Guid installationId, string code,
        string receiver,
        double designFlowRate) => new(id, installationId, code, receiver, designFlowRate, DateTime.UtcNow, null);

    public void UpdateDetails(string receiver, double designFlowRate)
    {
        Receiver = receiver;
        DesignFlowRate = designFlowRate;

        UpdatedAt = DateTime.UtcNow;
    }
}