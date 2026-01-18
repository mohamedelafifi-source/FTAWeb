using System.Text.Json.Serialization;

namespace FTAWeb.Models;

public class Person
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("parents")]
    public List<string> Parents { get; set; } = new();

    [JsonPropertyName("spouses")]
    public List<string> Spouses { get; set; } = new();

    [JsonPropertyName("children")]
    public List<string> Children { get; set; } = new();

    [JsonPropertyName("siblings")]
    public List<string> Siblings { get; set; } = new();
}
