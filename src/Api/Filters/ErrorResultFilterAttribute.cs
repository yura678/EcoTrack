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

            var apiResult = new ApiResult<object?>(
                isSuccess: false,
                statusCode: apiStatusCode,
                data: objectResult.Value,
                message: apiStatusCode.ToDisplay()
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

    private ApiResultStatusCode MapToApiResultStatusCode(int httpStatusCode)
    {
        return httpStatusCode switch
        {
            StatusCodes.Status400BadRequest => ApiResultStatusCode.BadRequest,
            StatusCodes.Status401Unauthorized => ApiResultStatusCode.UnAuthorized,
            StatusCodes.Status403Forbidden => ApiResultStatusCode.Forbidden,
            StatusCodes.Status404NotFound => ApiResultStatusCode.NotFound,
            StatusCodes.Status500InternalServerError => ApiResultStatusCode.ServerError,
            _ => ApiResultStatusCode.BadRequest
        };
    }
}