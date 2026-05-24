namespace Application.Common.Settings;

public class AdminSettings
{
    /// <summary>
    /// Mailbox that receives notifications about new enterprise registrations awaiting
    /// SuperAdmin verification. Empty string disables the notification (development /
    /// tests where you don't want to litter the outbox).
    /// </summary>
    public string NotificationEmail { get; set; } = string.Empty;
}
