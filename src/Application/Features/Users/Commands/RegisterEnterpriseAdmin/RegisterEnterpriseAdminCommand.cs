using System.Text.RegularExpressions;
using Application.Features.Users.Commands.Create;
using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Commands.RegisterEnterpriseAdmin;

public record RegisterEnterpriseAdminCommand(
    string UserName,
    string Name,
    string FamilyName,
    string PhoneNumber,
    string Email,
    string Password,
    string RepeatPassword,
    string EnterpriseName,
    string Edrpou,
    string Address,
    Guid SectorId)
    : IRequest<Either<UserException, UserCreateCommandResult>>,
        IValidatableModel<RegisterEnterpriseAdminCommand>
{
    public IValidator<RegisterEnterpriseAdminCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RegisterEnterpriseAdminCommand> validator)
    {
        validator.RuleFor(c => c.Name).NotEmpty();
        validator.RuleFor(c => c.UserName).NotEmpty();
        validator.RuleFor(c => c.FamilyName).NotEmpty();
        
        validator.RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Email is required.")
            .NotNull()
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email format is invalid");
        
        validator.RuleFor(c => c.PhoneNumber).NotEmpty()
            .Matches(new Regex(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$"));
        
        validator.RuleFor(c => c.Password)
            .Matches(c => c.RepeatPassword).WithMessage("passwords do not match");

        validator.RuleFor(x => x.EnterpriseName)
            .NotEmpty()
            .MaximumLength(255);

        validator.RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(500);

        validator.RuleFor(x => x.Edrpou)
            .NotEmpty()
            .MaximumLength(8)
            .Matches(@"^\d{8}$");

        validator.RuleFor(x => x.SectorId)
            .NotEmpty();


        return validator;
    }
}