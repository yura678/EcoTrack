using Domain.Common;

namespace Domain.Entities.Enterprises;

public class Sector : BaseEntity
{
    public string Name { get; private set; }
    public string Code { get; }

    private Sector(Guid id, string name, string code)
    {
        Id = id;
        Name = name;
        Code = code;
    }

    public static Sector New(
        Guid id,
        string name,
        string code)
        => new(id, name, code);

    public ICollection<Enterprise>? Enterprises { get; } = [];
}