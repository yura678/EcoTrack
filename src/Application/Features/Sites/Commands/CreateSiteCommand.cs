using Application.Features.Sites.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Sites.Commands;

public class CreateSiteCommand : IRequest<Either<SiteException, Site>>,
    IValidatableModel<CreateSiteCommand>
{
    public required string Name { get; init; }
    public required string Address { get; init; }
    public required int? SanitaryZoneRadius { get; init; }
    public required Guid EnterpriseId { get; init; }

    public IValidator<CreateSiteCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreateSiteCommand> validator)
    {
        validator.RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        validator.RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(500);

        validator.RuleFor(x => x.SanitaryZoneRadius)
            .GreaterThan(0);

        validator.RuleFor(x => x.EnterpriseId)
            .NotEmpty();

        return validator;
    }
}