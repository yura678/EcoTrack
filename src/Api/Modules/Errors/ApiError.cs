namespace Api.Modules.Errors;

// Canonical carrier for error responses. ErrorResultFilterAttribute recognises it and unwraps
// it into the ApiResult envelope: Message becomes the envelope's `message` (always the
// human-readable reason) and Data becomes the envelope's `data` (structured detail only — a
// field-error dictionary for validation, a domain object for rich cases, or null for a global
// error). The ApiError instance itself is never serialized.
internal sealed class ApiError(string message, object? data = null)
{
    public string Message { get; } = message;
    public object? Data { get; } = data;
}
