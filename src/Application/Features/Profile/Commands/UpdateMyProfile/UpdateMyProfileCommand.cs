using Application.Features.Users.Exceptions;
using Application.Models.Profile;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Profile.Commands.UpdateMyProfile;

public class UpdateMyProfileCommand : IRequest<Either<UserException, MyProfileInfo>>,
    IValidatableModel<UpdateMyProfileCommand>
{
    public string? Name { get; init; }
    public string? FamilyName { get; init; }

    public IValidator<UpdateMyProfileCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateMyProfileCommand> validator)
    {
        validator.RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => x.Name is not null);

        validator.RuleFor(x => x.FamilyName)
            .MaximumLength(200)
            .When(x => x.FamilyName is not null);

        return validator;
    }
}
