using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NotificationService.DTOs;

public enum NotificationType
{
    INFO,
    SUCCESS,
    WARNING,
    ERROR
}

public class NotificationRequest
{
    [JsonPropertyName("recipient")]
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Recipient { get; set; } = null!;

    [JsonPropertyName("message")]
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Message { get; set; } = null!;

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationType Type { get; set; } = NotificationType.INFO;

    [JsonPropertyName("title")]
    [StringLength(255)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("mode")]
    [StringLength(50)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; set; }
}

public class NotificationModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("recipient")]
    public string Recipient { get; set; } = null!;

    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = null!;

    [JsonPropertyName("read")]
    public bool Read { get; set; }
}
