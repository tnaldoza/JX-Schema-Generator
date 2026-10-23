using System.Globalization;
using System.Text.Json.Nodes;

namespace JXSchemaGenerator.Generate.Models;

public sealed class InquiryElementsRoot
{
	public string Version { get; set; } = "1.0.0";
	public string LastUpdated { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
	public Dictionary<string, InquiryOperation> inquiryOperations { get; set; } = new();
}

public sealed class InquiryOperation
{
	public string name { get; set; } = "";
	public JsonNode? requestSchema { get; set; }
	public JsonNode? responseSchema { get; set; }
}
