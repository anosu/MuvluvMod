using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MuvluvMod.Services;

/// <summary>
/// Describes hashes for translation files published by the translation repository.
/// </summary>
internal sealed class TranslationManifest
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("names")]
    public string Names { get; set; }

    [JsonPropertyName("scenes")]
    public Dictionary<string, string> Scenes { get; set; }

    [JsonPropertyName("static")]
    public string Static { get; set; }
}
