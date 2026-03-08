using Application.Features.Enterprises.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Enterprises.Commands;

public class CreateEnterpriseCommand : IRequest<Either<EnterpriseException, Enterprise>>,
    IValidatableModel<CreateEnterpriseCommand>
{
    public required string Name { get; init; }
    public required string Edrpou { get; init; }
    public required RiskGroup RiskGroup { get; init; }
    public required string Address { get; init; }
    public required Guid SectorId { get; init; }

    public IValidator<CreateEnterpriseCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreateEnterpriseCommand> validator)
    {
        validator.RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        validator.RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(500);

        validator.RuleFor(x => x.RiskGroup)
            .IsInEnum();

        validator.RuleFor(x => x.Edrpou)
            .NotEmpty()
            .MaximumLength(8)
            .Matches(@"^\d{8}$");

        validator.RuleFor(x => x.SectorId)
            .NotEmpty();

        return validator;
    }
}