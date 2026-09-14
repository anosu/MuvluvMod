using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MuvluvMod.Services;

/// <summary>
/// Describes hashes for translation files published by the translation repository.
/// </summary>
internal sealed class TranslationManifest
{
    [JsonPropertyName("hash")]
    public string ContentHash { get; set; }

    [JsonPropertyName("names")]
    public string NamesHash { get; set; }

    [JsonPropertyName("scenes")]
    public Dictionary<string, string> SceneHashes { get; set; }

    [JsonPropertyName("static")]
    public string MasterDataHash { get; set; }
}
