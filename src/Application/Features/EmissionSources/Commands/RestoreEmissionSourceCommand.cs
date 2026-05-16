using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using LanguageExt;
using MediatR;

namespace Application.Features.EmissionSources.Commands;

public record RestoreEmissionSourceCommand(Guid Id) : IRequest<Either<EmissionSourceException, EmissionSource>>;
