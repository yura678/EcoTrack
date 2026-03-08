using Application.Features.Installations.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Installations.Commands;

public class CreateInstallationCommand : IRequest<Either<InstallationException, Installation>>,
    IValidatableModel<CreateInstallationCommand>
{
    public required string Name { get; init; }
    public required Guid IedCategoryId { get; init; }
    public required Guid SiteId { get; init; }
    public InstallationStatus Status { get; init; }

    public IValidator<CreateInstallationCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreateInstallationCommand> validator)
    {
        validator.RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        validator.RuleFor(x => x.IedCategoryId)
            .NotEmpty();

        validator.RuleFor(x => x.SiteId)
            .NotEmpty();

        validator.RuleFor(x => x.Status)
            .IsInEnum();

        return validator;
    }
}