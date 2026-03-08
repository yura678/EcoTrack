using Application.Features.Installations.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Installations.Commands;

public class UpdateInstallationCommand : IRequest<Either<InstallationException, Installation>>,
    IValidatableModel<UpdateInstallationCommand>
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid IedCategoryId { get; init; }

    public IValidator<UpdateInstallationCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateInstallationCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        validator.RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        validator.RuleFor(x => x.IedCategoryId)
            .NotEmpty();

        return validator;
    }
}