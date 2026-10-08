using System.ComponentModel.DataAnnotations;

namespace RegistryService.DTOs;

public class RegistrationRequest
{
    [Required]
    [MinLength(1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string Url { get; set; } = string.Empty;

    public double Ttl { get; set; } = 30.0;
}
