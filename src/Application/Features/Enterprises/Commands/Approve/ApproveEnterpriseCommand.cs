using Application.Features.Enterprises.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Enterprises.Commands.Approve;

public record ApproveEnterpriseCommand(Guid EnterpriseId)
    : IRequest<Either<EnterpriseException, Enterprise>>;
