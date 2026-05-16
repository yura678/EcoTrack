using Application.Features.Sites.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Sites.Commands;

public record RestoreSiteCommand(Guid Id) : IRequest<Either<SiteException, Site>>;
