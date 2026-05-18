namespace Application.Common.Interfaces.Notifications;

/// <summary>
/// Posts a signed JSON payload to a subscriber's webhook URL. The implementation owns the
/// HMAC scheme and headers; the dispatcher just supplies (url, secret, body).
/// </summary>
public interface IWebhookSender
{
    Task SendAsync(
        string url,
        string secret,
        string payloadJson,
        CancellationToken cancellationToken);
}
