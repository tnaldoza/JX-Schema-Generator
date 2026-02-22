using System.Text.Json.Nodes;

namespace JXSchemaGenerator.Generate.Models;

public sealed class AddElementsRoot
{
	public string Version { get; set; } = "1.0.0";
	public string LastUpdated { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
	public Dictionary<string, AddOperation> addOperations { get; set; } = new();
}

public sealed class AddOperation
{
	public string name { get; set; } = "";
	public JsonNode? requestSchema { get; set; }
	public JsonNode? responseSchema { get; set; }
}
