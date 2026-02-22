using System.Text.Json.Nodes;

namespace JXSchemaGenerator.Generate.Models;

public sealed class SearchElementsRoot
{
	public string Version { get; set; } = "1.0.0";
	public string LastUpdated { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
	public Dictionary<string, SearchOperation> searchOperations { get; set; } = new();
}

public sealed class SearchOperation
{
	public string name { get; set; } = "";
	public JsonNode? requestSchema { get; set; }
	public JsonNode? responseRecordSchema { get; set; }
}
