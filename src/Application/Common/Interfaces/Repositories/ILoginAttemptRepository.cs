using Application.Common.Models;
using Domain.Entities.User;

namespace Application.Common.Interfaces.Repositories;

public interface ILoginAttemptRepository
{
    Task AddAsync(LoginAttempt entity, CancellationToken cancellationToken);

    /// <summary>
    /// Paged history of login attempts for the user, newest-first. Optional date filters; if
    /// both null the most recent page is returned.
    /// </summary>
    Task<PageResult<LoginAttempt>> GetForUserPagedAsync(
        Guid userId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
