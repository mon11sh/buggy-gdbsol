using System.Text.Json.Serialization;

namespace Gdb.Common.DTOs;

public class HTTPValidationError
{
    [JsonPropertyName("detail")]
    public ValidationError[] Detail { get; set; } = Array.Empty<ValidationError>();
}
