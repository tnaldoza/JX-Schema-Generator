
using System.Text.Json.Nodes;

namespace JXSchemaGenerator.Generate.Models;

public sealed class ModificationElementsRoot
{
	public string Version { get; set; } = "1.0.0";
	public string LastUpdated { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
	public Dictionary<string, ModificationEntityType> entityTypes { get; set; } = new();
}

public sealed class ModificationEntityType
{
	public string name { get; set; } = "";
	public List<string> aliases { get; set; } = new();
	public string modificationStructure { get; set; } = "";
	public List<ModificationSection> modificationSections { get; set; } = new();
}

public sealed class ModificationSection
{
	public string name { get; set; } = "";
	public string? description { get; set; }
	public JsonNode? schema { get; set; }
}
