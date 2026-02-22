
using System.Text.Json.Nodes;

namespace JXSchemaGenerator.Generate.Models;

public sealed class InquiryElementsRoot
{
	public string Version { get; set; } = "1.0.0";
	public string LastUpdated { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
	public Dictionary<string, InquiryAccountType> accountTypes { get; set; } = new();
}

public sealed class InquiryAccountType
{
	public string name { get; set; } = "";
	public List<string> aliases { get; set; } = new();
	public List<InquiryExtendedElement> extendedElements { get; set; } = new();
}

public sealed class InquiryExtendedElement
{
	public string name { get; set; } = "";
	public string? description { get; set; }
	public JsonNode? schema { get; set; }
}
