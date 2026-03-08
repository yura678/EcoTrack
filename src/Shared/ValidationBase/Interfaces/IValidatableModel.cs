using FluentValidation;

namespace Shared.ValidationBase.Interfaces;

public interface IValidatableModel<TApplicationModel> where TApplicationModel:class
{
    IValidator<TApplicationModel> ValidateApplicationModel(ApplicationBaseValidationModelProvider<TApplicationModel> validator);
}