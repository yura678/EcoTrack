using Domain.Common;

namespace Domain.Entities.Enterprises;

public class Sector : BaseEntity, ISoftDeletable
{
    public string Name { get; private set; }
    public string Code { get; }
    public DateTime? DeletedAt { get; private set; }

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

    public void MarkDeleted() => DeletedAt = DateTime.UtcNow;
    public void Restore() => DeletedAt = null;
}
