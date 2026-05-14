using Domain.Common;

namespace Domain.Entities.Enterprises;

public class IedCategory : BaseEntity, ISoftDeletable
{
    // Код категорії "1.1", "2.3.b"
    public string Code { get; private set; }

    public string? Description { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private IedCategory(Guid id, string code, string? description)
    {
        Id = id;
        Code = code;
        Description = description;
    }

    public static IedCategory New(
        Guid id,
        string name,
        string? description)
        => new(id, name, description);

    public ICollection<Installation>? Installations { get; private set; } = [];

    public void MarkDeleted() => DeletedAt = DateTime.UtcNow;
    public void Restore() => DeletedAt = null;
}
