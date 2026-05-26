using Application.Models.ApiResult;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Extensions;

namespace Api.Filters;

public class ErrorResultFilterAttribute : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult objectResult && objectResult.StatusCode >= 400)
        {
            var apiStatusCode = MapToApiResultStatusCode(objectResult.StatusCode.Value);

            // Prefer the real exception message that handlers set via `new ObjectResult(error.Message)`
            // over the generic enum display name, so SPA toasts show "Role is in use" rather than
            // "Bad Request Error". Falls back to the display name when no message was supplied
            // (e.g. handler returned only a status code).
            var realMessage = ExtractMessage(objectResult.Value);

            var apiResult = new ApiResult<object?>(
                isSuccess: false,
                statusCode: apiStatusCode,
                data: objectResult.Value,
                message: !string.IsNullOrWhiteSpace(realMessage) ? realMessage : apiStatusCode.ToDisplay()
            );

            context.Result = new JsonResult(apiResult) { StatusCode = objectResult.StatusCode };
        }
        else if (context.Result is StatusCodeResult statusCodeResult && statusCodeResult.StatusCode >= 400)
        {
            var apiStatusCode = MapToApiResultStatusCode(statusCodeResult.StatusCode);

            var apiResult = new ApiResult(
                isSuccess: false,
                statusCode: apiStatusCode,
                message: apiStatusCode.ToDisplay()
            );

            context.Result = new JsonResult(apiResult) { StatusCode = statusCodeResult.StatusCode };
        }
    }

    private static string? ExtractMessage(object? value) => value switch
    {
        string s => s,
        // FluentValidation hooks throw a Dictionary<string, List<string>> via ModelStateValidationAttribute
        // — picking the first message keeps the toast non-empty until field-level mapping kicks in.
        IDictionary<string, List<string>> errors => errors.Values.SelectMany(v => v).FirstOrDefault(),
        _ => null
    };

    private ApiResultStatusCode MapToApiResultStatusCode(int httpStatusCode)
    {
        return httpStatusCode switch
        {
            StatusCodes.Status400BadRequest => ApiResultStatusCode.BadRequest,
            StatusCodes.Status401Unauthorized => ApiResultStatusCode.UnAuthorized,
            StatusCodes.Status403Forbidden => ApiResultStatusCode.Forbidden,
            StatusCodes.Status404NotFound => ApiResultStatusCode.NotFound,
            StatusCodes.Status409Conflict => ApiResultStatusCode.Conflict,
            StatusCodes.Status500InternalServerError => ApiResultStatusCode.ServerError,
            _ => ApiResultStatusCode.BadRequest
        };
    }
}
