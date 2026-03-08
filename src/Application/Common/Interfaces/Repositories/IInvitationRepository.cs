using Domain.Entities.User;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories;

public interface IInvitationRepository
{
    Task<EnterpriseInvitation> AddAsync(EnterpriseInvitation entity, CancellationToken cancellationToken);
    EnterpriseInvitation Update(EnterpriseInvitation entity);
    Task<Option<EnterpriseInvitation>> GetValidInvitation(string token, CancellationToken cancellationToken);
}