using System.Text.Json.Serialization;

namespace WebApplication.Dto;

public sealed class AccessMatrixSaveRequestDto
{
    [JsonPropertyName("entries")]
    public List<AccessMatrixSaveEntryDto> Entries { get; set; } = [];
}

public sealed class AccessMatrixSaveEntryDto
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("item")]
    public string? Item { get; set; }

    [JsonPropertyName("granted")]
    public bool Granted { get; set; }
}
