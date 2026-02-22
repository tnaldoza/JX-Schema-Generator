using System.Text.Json.Nodes;
using System.Xml;

using JXSchemaGenerator.Expand;
using JXSchemaGenerator.Generate.Models;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Generate;

/// <summary>
/// Generates searchElements.json by discovering all *SrchRq_MType complex types
/// in the schema index. For each search operation it emits:
///   requestSchema       - the filterable fields (SrchMsgRqHdr header stripped)
///   responseRecordSchema - the result record fields ({Name}Rec_CType convention)
/// </summary>
public sealed class SearchGenerator
{
	// Standard boilerplate header present in every search request - not a filter field
	private static readonly HashSet<string> SkippedRqElements =
		new(StringComparer.OrdinalIgnoreCase) { "SrchMsgRqHdr" };

	public SearchElementsRoot Generate(SchemaIndex index, ExpansionOptions options)
	{
		var expander = new TypeExpander(index, options);
		var root = new SearchElementsRoot();

		foreach (var kvp in index.ComplexTypes)
		{
			var typeName = kvp.Key.Name;

			// Only process *SrchRq_MType types
			if (!typeName.EndsWith("SrchRq_MType", StringComparison.Ordinal)) continue;

			// Derive element name: XferSrchRq_MType -> XferSrch  (strip "Rq_MType")
			var elementName = typeName[..^"Rq_MType".Length];
			var targetNs = kvp.Key.Namespace;

			// Request schema: expand the full request type, then remove the header
			JsonNode? requestSchema = null;
			var tree = expander.ExpandToTree(kvp.Key, 0, []) as JsonObject;
			if (tree is not null)
			{
				foreach (var skip in SkippedRqElements)
					tree.Remove(skip);

				requestSchema = tree.Count > 0 ? tree : null;
			}

			// Response record schema: convention {elementName}Rec_CType
			// e.g. XferSrch -> XferSrchRec_CType
			JsonNode? responseRecordSchema = null;
			var recTypeName = new XmlQualifiedName(elementName + "Rec_CType", targetNs);
			if (index.ComplexTypes.ContainsKey(recTypeName))
				responseRecordSchema = expander.ExpandToTree(recTypeName, 0, []);

			root.searchOperations[elementName] = new SearchOperation
			{
				name = elementName,
				requestSchema = requestSchema,
				responseRecordSchema = responseRecordSchema,
			};
		}

		// Sort alphabetically
		root.searchOperations = root.searchOperations
			.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

		return root;
	}
}
