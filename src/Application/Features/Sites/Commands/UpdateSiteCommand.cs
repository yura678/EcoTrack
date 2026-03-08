using Application.Features.Sites.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Sites.Commands;

public class UpdateSiteCommand : IRequest<Either<SiteException, Site>>,
    IValidatableModel<UpdateSiteCommand>
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Address { get; init; }
    public required int? SanitaryZoneRadius { get; init; }

    public IValidator<UpdateSiteCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateSiteCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        validator.RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        validator.RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(500);

        validator.RuleFor(x => x.SanitaryZoneRadius)
            .GreaterThan(0);

        return validator;
    }
}