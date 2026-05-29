using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

// Shared helper for the *ErrorFactory family. Each factory's switch returns a
// (StatusCode, FieldName) tuple; this builder turns that into the wire response.
//
// When `fieldName` is non-null the body is a Dictionary<string, List<string>> with the
// property name as the key, which the SPA recognises and renders under the matching form
// control. When `fieldName` is null the body falls back to the raw message string for
// global errors that aren't tied to a specific input.
//
// `ErrorResultFilterAttribute.ExtractMessage` already recognises
// `IDictionary<string, List<string>>`, so the toast summary line keeps working unchanged.
internal static class ErrorBodyBuilder
{
    public static ObjectResult Build(int statusCode, string message, string? fieldName)
    {
        object body = fieldName is null
            ? message
            : new Dictionary<string, List<string>> { { fieldName, new List<string> { message } } };

        return new ObjectResult(body) { StatusCode = statusCode };
    }
}
