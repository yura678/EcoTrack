using FluentValidation;

namespace Shared.ValidationBase;

public class ApplicationBaseValidationModelProvider<TApplicationModel> 
    : AbstractValidator<TApplicationModel>;