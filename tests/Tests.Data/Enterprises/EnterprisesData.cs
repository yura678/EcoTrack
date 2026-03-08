using Domain;
using Domain.Entities.Enterprises;
using System.Threading;

namespace Tests.Data.Enterprises;

public static class EnterprisesData
{
    private static int edrpouCounter = 10000000;

    private static string GetNextEdrpou()
    {
        return Interlocked.Increment(ref edrpouCounter).ToString();
    }

    public static Enterprise FirstTestEquipment(Guid sectorId) => Enterprise.New(
        Guid.NewGuid(),
        "First test enterprise", 
        GetNextEdrpou(),
        "Address", 
        RiskGroup.Average, 
        sectorId);

    public static Enterprise SecondTestEquipment(Guid sectorId) => Enterprise.New(
        Guid.NewGuid(),
        "Second test enterprise", 
        GetNextEdrpou(), 
        "Address2", 
        RiskGroup.High, 
        sectorId);

    public static Enterprise ThirdTestEquipment(Guid sectorId) => Enterprise.New(
        Guid.NewGuid(),
        "Third test enterprise", 
        GetNextEdrpou(), 
        "Address3", 
        RiskGroup.Minor, 
        sectorId);
}