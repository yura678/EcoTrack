namespace Application.Features.Role.Exceptions;

public abstract class RoleException(
    Guid roleId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid RoleId { get; } = roleId;
}

public class RoleNotFoundException(
    Guid userId,
    Guid roleId)
    : RoleException(userId, "Role not found.")
{
    public Guid TargetRoleId { get; } = roleId;
}

public class RoleCreationException(
    Guid roleId,
    string errors)
    : RoleException(roleId, $"Failed to create role: {errors}.")
{
    public string Errors { get; } = errors;
}

public class RoleClaimsUpdateException(
    Guid roleId)
    : RoleException(roleId, "Failed to update role permissions.");

/// <summary>
/// Refusal to delete a role that other rows still reference. We pre-check active memberships
/// and pending invitations and surface the counts so the operator knows what to clean up
/// before retrying — otherwise the delete would crash deep in EF with a foreign-key violation.
/// </summary>
public class RoleInUseException(
    Guid roleId,
    int activeMemberships,
    int historicalMemberships,
    int pendingInvitations)
    : RoleException(roleId,
        BuildMessage(activeMemberships, historicalMemberships, pendingInvitations))
{
    public int ActiveMemberships { get; } = activeMemberships;
    public int HistoricalMemberships { get; } = historicalMemberships;
    public int PendingInvitations { get; } = pendingInvitations;

    private static string BuildMessage(int active, int historical, int pending)
    {
        var parts = new List<string>();
        if (active > 0) parts.Add($"active members: {active}");
        if (pending > 0) parts.Add($"pending invitations: {pending}");
        if (historical > 0) parts.Add($"historical memberships: {historical}");
        var detail = parts.Count > 0 ? string.Join(", ", parts) : "dependent rows";
        return $"Role cannot be deleted because it is in use ({detail}).";
    }
}

public class UnhandledRoleException(
    Guid roleId,
    Exception? innerException = null)
    : RoleException(roleId, "Unexpected error occurred.", innerException);