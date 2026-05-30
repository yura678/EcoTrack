using Application.Models.ApiResult;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Extensions;
using StatusCodes = Microsoft.AspNetCore.Http.StatusCodes;

namespace Api.Filters;

public class ModelStateValidationAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var contextActionArgument in context.ActionArguments)
        {
            if (contextActionArgument.Value == null)
            {
                continue;
            }

            var viewModelValidator =
                context.HttpContext.RequestServices.GetService(
                    typeof(IValidator<>).MakeGenericType(contextActionArgument.Value.GetType()));

            if (viewModelValidator is IValidator validator)
            {
                var validationResult =
                    await validator.ValidateAsync(new ValidationContext<object>(contextActionArgument.Value));

                if (!validationResult.IsValid)
                {
                    foreach (var validationResultError in validationResult.Errors)
                    {
                        context.ModelState.AddModelError(validationResultError.PropertyName,
                            validationResultError.ErrorMessage);
                    }
                }
            }
        }


        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    k => k.Key,
                    v => v.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            // `message` carries the first field error (not the generic "Bad Request Error") so a
            // validation failure toasts something actionable; `data` keeps the full field dict
            // for per-input rendering — matching the standard error contract.
            var firstMessage = errors.Values.SelectMany(m => m).FirstOrDefault()
                ?? ApiResultStatusCode.BadRequest.ToDisplay();

            var apiResult = new ApiResult<IDictionary<string, string[]>>(
                false,
                ApiResultStatusCode.BadRequest,
                errors,
                firstMessage);

            context.Result = new JsonResult(apiResult)
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

            return;
        }

        await base.OnActionExecutionAsync(context, next);
    }
}