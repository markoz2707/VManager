using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum NotificationChannelType
{
    Email = 0,
    Webhook = 1,
    Slack = 2,
    MsTeams = 3
}

public class NotificationChannel
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public NotificationChannelType Type { get; set; }

    /// <summary>
    /// JSON configuration for the channel (e.g. SMTP settings, webhook URL, Slack token)
    /// </summary>
    [Required]
    public string Configuration { get; set; } = "{}";

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<AlertNotificationChannel> AlertDefinitions { get; set; } = new();
}
