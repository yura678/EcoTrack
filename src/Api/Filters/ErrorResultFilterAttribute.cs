using Api.Modules.Errors;
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

            // Normalize every error body into (message, data) so the envelope is consistent:
            // `message` is always the human-readable reason, `data` is structured detail only
            // (field-error dict, rich domain object) or null. See Normalize below.
            var (message, data) = Normalize(objectResult.Value);

            var apiResult = new ApiResult<object?>(
                isSuccess: false,
                statusCode: apiStatusCode,
                data: data,
                message: !string.IsNullOrWhiteSpace(message) ? message : apiStatusCode.ToDisplay()
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

    // Collapses the various error body shapes into the standard (message, data) pair:
    //   • ApiError              → its own Message + Data (the canonical path)
    //   • plain string          → the message; no structured data
    //   • field-error dict      → first message for the toast; the dict as data
    //   • anything else         → no message (caller falls back to the status display); no data,
    //                             so arbitrary/anonymous objects never leak into the envelope.
    private static (string? Message, object? Data) Normalize(object? value) => value switch
    {
        ApiError e => (e.Message, e.Data),
        string s => (s, null),
        IDictionary<string, string[]> errors => (FirstMessage(errors.Values), errors),
        IDictionary<string, List<string>> errors => (FirstMessage(errors.Values), errors),
        _ => (null, null)
    };

    private static string? FirstMessage(IEnumerable<IEnumerable<string>> messageGroups) =>
        messageGroups.SelectMany(g => g).FirstOrDefault();

    private ApiResultStatusCode MapToApiResultStatusCode(int httpStatusCode)
    {
        return httpStatusCode switch
        {
            StatusCodes.Status400BadRequest => ApiResultStatusCode.BadRequest,
            StatusCodes.Status401Unauthorized => ApiResultStatusCode.UnAuthorized,
            StatusCodes.Status403Forbidden => ApiResultStatusCode.Forbidden,
            StatusCodes.Status404NotFound => ApiResultStatusCode.NotFound,
            StatusCodes.Status409Conflict => ApiResultStatusCode.Conflict,
            StatusCodes.Status422UnprocessableEntity => ApiResultStatusCode.EntityProcessError,
            StatusCodes.Status500InternalServerError => ApiResultStatusCode.ServerError,
            _ => ApiResultStatusCode.BadRequest
        };
    }
}
