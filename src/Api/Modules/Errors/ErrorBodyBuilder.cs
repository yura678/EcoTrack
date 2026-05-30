using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

// Shared helper for the *ErrorFactory family. Each factory's switch returns a
// (StatusCode, FieldName) tuple; this builder turns that into the wire response.
//
// Every error funnels through an ApiError carrier so the envelope is consistent:
//   • `message` always holds the human-readable reason.
//   • `data` holds structured detail only — a `{ field: [message] }` dictionary when the error
//     is tied to a specific input, or null for a global error. Never a bare-string duplicate of
//     the message.
// ErrorResultFilterAttribute unwraps the ApiError into the ApiResult envelope.
internal static class ErrorBodyBuilder
{
    public static ObjectResult Build(int statusCode, string message, string? fieldName)
    {
        var data = fieldName is null
            ? null
            : new Dictionary<string, string[]> { [fieldName] = [message] };

        return new ObjectResult(new ApiError(message, data)) { StatusCode = statusCode };
    }
}
