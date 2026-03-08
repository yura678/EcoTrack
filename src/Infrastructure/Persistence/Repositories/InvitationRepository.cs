using Application.Common.Interfaces.Repositories;
using Domain.Entities.User;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;


namespace Infrastructure.Persistence.Repositories;

internal class InvitationRepository(ApplicationDbContext dbContext)
    : BaseAsyncRepository<EnterpriseInvitation>(dbContext), IInvitationRepository
{
    public async Task<Option<EnterpriseInvitation>> GetValidInvitation(string token,
        CancellationToken cancellationToken)
    {
        return await base.TableNoTracking.FirstOrDefaultAsync(
            i => i.Token == token && !i.IsUsed && i.ExpiresAt > DateTime.UtcNow, cancellationToken);
    }
}