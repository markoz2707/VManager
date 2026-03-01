namespace HyperV.CentralManagement.Models;

public class AlertNotificationChannel
{
    public Guid AlertDefinitionId { get; set; }
    public AlertDefinition? AlertDefinition { get; set; }

    public Guid NotificationChannelId { get; set; }
    public NotificationChannel? NotificationChannel { get; set; }
}
