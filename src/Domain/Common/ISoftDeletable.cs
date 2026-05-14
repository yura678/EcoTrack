namespace Domain.Common;

public interface ISoftDeletable
{
    DateTime? DeletedAt { get; }
    void MarkDeleted();
    void Restore();
}
