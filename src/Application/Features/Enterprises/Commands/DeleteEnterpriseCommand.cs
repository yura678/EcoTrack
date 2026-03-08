using Application.Features.Enterprises.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Enterprises.Commands;

public class DeleteEnterpriseCommand : IRequest<Either<EnterpriseException, Enterprise>>,
    IValidatableModel<DeleteEnterpriseCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeleteEnterpriseCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeleteEnterpriseCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();
        
        return validator;
    }
}