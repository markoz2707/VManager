using System.Text;
using System.Text.Json;
using HyperV.CentralManagement.Models;
using MailKit.Net.Smtp;
using MimeKit;

namespace HyperV.CentralManagement.Services;

public class NotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IHttpClientFactory httpClientFactory, ILogger<NotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(NotificationChannel channel, string subject, string message)
    {
        try
        {
            switch (channel.Type)
            {
                case NotificationChannelType.Email:
                    await SendEmailAsync(channel.Configuration, subject, message);
                    break;
                case NotificationChannelType.Webhook:
                    await SendWebhookAsync(channel.Configuration, subject, message);
                    break;
                case NotificationChannelType.Slack:
                    await SendSlackAsync(channel.Configuration, subject, message);
                    break;
                case NotificationChannelType.MsTeams:
                    await SendTeamsAsync(channel.Configuration, subject, message);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification via channel '{Channel}' ({Type})", channel.Name, channel.Type);
        }
    }

    private async Task SendEmailAsync(string configJson, string subject, string body)
    {
        var config = JsonSerializer.Deserialize<EmailConfig>(configJson);
        if (config == null) return;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config.FromName ?? "VManager", config.FromAddress));
        message.To.Add(new MailboxAddress("", config.ToAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(config.SmtpHost, config.SmtpPort, config.UseSsl);

        if (!string.IsNullOrEmpty(config.Username))
        {
            await client.AuthenticateAsync(config.Username, config.Password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    private async Task SendWebhookAsync(string configJson, string subject, string body)
    {
        var config = JsonSerializer.Deserialize<WebhookConfig>(configJson);
        if (config?.Url == null) return;

        var client = _httpClientFactory.CreateClient();
        var payload = JsonSerializer.Serialize(new { subject, message = body, timestamp = DateTimeOffset.UtcNow });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        await client.PostAsync(config.Url, content);
    }

    private async Task SendSlackAsync(string configJson, string subject, string body)
    {
        var config = JsonSerializer.Deserialize<SlackConfig>(configJson);
        if (config?.WebhookUrl == null) return;

        var client = _httpClientFactory.CreateClient();
        var payload = JsonSerializer.Serialize(new { text = $"*{subject}*\n{body}" });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        await client.PostAsync(config.WebhookUrl, content);
    }

    private async Task SendTeamsAsync(string configJson, string subject, string body)
    {
        var config = JsonSerializer.Deserialize<TeamsConfig>(configJson);
        if (config?.WebhookUrl == null) return;

        var client = _httpClientFactory.CreateClient();
        var payload = JsonSerializer.Serialize(new
        {
            @type = "MessageCard",
            summary = subject,
            sections = new[]
            {
                new { activityTitle = subject, text = body }
            }
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        await client.PostAsync(config.WebhookUrl, content);
    }
}

public class EmailConfig
{
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "vmanager@localhost";
    public string? FromName { get; set; }
    public string ToAddress { get; set; } = "";
}

public class WebhookConfig
{
    public string Url { get; set; } = "";
}

public class SlackConfig
{
    public string WebhookUrl { get; set; } = "";
}

public class TeamsConfig
{
    public string WebhookUrl { get; set; } = "";
}
