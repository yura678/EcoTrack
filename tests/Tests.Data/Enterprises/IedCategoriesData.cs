using Domain.Entities.Enterprises;

namespace Tests.Data.Enterprises;

public static class IedCategoriesData
{
    public static IedCategory FirstTestIedCategory() =>
        IedCategory.New(Guid.NewGuid(), Guid.NewGuid().ToString("N")[..8], null);
}