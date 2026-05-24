using Application.Features.Enterprises.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Enterprises.Commands.Reject;

public record RejectEnterpriseCommand(Guid EnterpriseId, string Reason)
    : IRequest<Either<EnterpriseException, Enterprise>>;
