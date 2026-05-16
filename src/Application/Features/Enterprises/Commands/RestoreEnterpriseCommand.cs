using Application.Features.Enterprises.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Enterprises.Commands;

public record RestoreEnterpriseCommand(Guid Id) : IRequest<Either<EnterpriseException, Enterprise>>;
