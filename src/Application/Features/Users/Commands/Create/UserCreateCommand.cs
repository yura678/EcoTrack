using System.Text.RegularExpressions;
using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Commands.Create;

public record UserCreateCommand(
    string UserName,
    string Name,
    string FamilyName,
    string Email,
    string PhoneNumber,
    string Password,
    string RepeatPassword)
    : IRequest<Either<UserException, UserCreateCommandResult>>
        , IValidatableModel<UserCreateCommand>
{
    public IValidator<UserCreateCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UserCreateCommand> validator)
    {
        validator
            .RuleFor(c => c.Name)
            .NotEmpty()
            .NotNull()
            .WithMessage("User must have first name");

        validator.RuleFor(c => c.UserName)
            .NotEmpty()
            .NotNull()
            .WithMessage("Please enter your username");

        validator
            .RuleFor(c => c.FamilyName)
            .NotEmpty()
            .NotNull()
            .WithMessage("User must have last name");


        validator.RuleFor(c => c.PhoneNumber).NotEmpty()
            .NotNull().WithMessage("Phone Number is required.")
            .MinimumLength(10).WithMessage("PhoneNumber must not be less than 10 characters.")
            .MaximumLength(20).WithMessage("PhoneNumber must not exceed 50 characters.")
            .Matches(new Regex(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$"))
            .WithMessage("Phone number is not valid");
        
        validator.RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Email is required.")
            .NotNull()
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email format is invalid");

        validator.RuleFor(c => c.Password)
            .Matches(c => c.RepeatPassword)
            .WithMessage("Passwords do not match");

        return validator;
    }
}