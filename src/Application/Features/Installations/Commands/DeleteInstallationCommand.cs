using Application.Features.Installations.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Installations.Commands;

public class DeleteInstallationCommand : IRequest<Either<InstallationException, Installation>>,
    IValidatableModel<DeleteInstallationCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeleteInstallationCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeleteInstallationCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}