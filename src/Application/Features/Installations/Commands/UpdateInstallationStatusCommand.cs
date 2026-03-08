using Application.Features.Installations.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Installations.Commands;

public class UpdateInstallationStatusCommand : IRequest<Either<InstallationException, Installation>>,
    IValidatableModel<UpdateInstallationStatusCommand>
{
    public required Guid Id { get; init; }
    public required InstallationStatus Status { get; init; }

    public IValidator<UpdateInstallationStatusCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateInstallationStatusCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        validator.RuleFor(x => x.Status)
            .IsInEnum();

        return validator;
    }
}