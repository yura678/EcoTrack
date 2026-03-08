using Application.Features.Sites.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Sites.Commands;

public class DeleteSiteCommand : IRequest<Either<SiteException, Site>>,
    IValidatableModel<DeleteSiteCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeleteSiteCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeleteSiteCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}