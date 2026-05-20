using Domain.Entities.User;

namespace Application.Models.Profile;

public record LoginHistoryEntry(
    Guid Id,
    DateTime OccurredAt,
    LoginMethod Method,
    LoginOutcome Outcome,
    string? IpAddress,
    string? UserAgent);
