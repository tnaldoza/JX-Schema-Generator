using System.Text.Json.Nodes;
using System.Xml;

using JXSchemaGenerator.Expand;
using JXSchemaGenerator.Generate.Models;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Generate;

/// <summary>
/// Generates inquiry elements JSON by discovering all *InqRq_MType complex types.
/// For each inquiry operation it emits:
///   requestSchema  - the input parameters (MsgRqHdr header stripped)
///   responseSchema - the response envelope fields (MsgRsHdr header stripped)
///                    derived by swapping Rq_MType -> Rs_MType
/// </summary>
public sealed class InquiryGenerator
{
	private static readonly HashSet<string> SkippedRqElements =
		new(StringComparer.OrdinalIgnoreCase) { "MsgRqHdr" };

	private static readonly HashSet<string> SkippedRsElements =
		new(StringComparer.OrdinalIgnoreCase) { "MsgRsHdr" };

	public InquiryElementsRoot Generate(SchemaIndex index, ExpansionOptions options)
	{
		var expander = new TypeExpander(index, options);
		var root = new InquiryElementsRoot();

		foreach (var kvp in index.ComplexTypes)
		{
			var typeName = kvp.Key.Name;

			// Only process *InqRq_MType types
			if (!typeName.EndsWith("InqRq_MType", StringComparison.Ordinal)) continue;

			// Derive element name: AcctSweepInqRq_MType -> AcctSweepInq  (strip "Rq_MType")
			var elementName = typeName[..^"Rq_MType".Length];
			var targetNs = kvp.Key.Namespace;

			// Request schema: expand and remove the boilerplate header
			JsonNode? requestSchema = null;
			var reqTree = expander.ExpandToTree(kvp.Key, 0, []) as JsonObject;
			if (reqTree is not null)
			{
				foreach (var skip in SkippedRqElements)
					reqTree.Remove(skip);

				requestSchema = reqTree.Count > 0 ? reqTree : null;
			}

			// Response schema: JXChange naming convention Rq_MType -> Rs_MType
			// (every *InqRq_MType has a matching *InqRs_MType for the response envelope)
			JsonNode? responseSchema = null;
			var rsTypeName = new XmlQualifiedName(
				typeName[..^"Rq_MType".Length] + "Rs_MType", targetNs);

			if (index.ComplexTypes.ContainsKey(rsTypeName))
			{
				var rsTree = expander.ExpandToTree(rsTypeName, 0, []) as JsonObject;
				if (rsTree is not null)
				{
					foreach (var skip in SkippedRsElements)
						rsTree.Remove(skip);

					responseSchema = rsTree.Count > 0 ? rsTree : null;
				}
			}

			// Patch IncXtendElemArray in the request: replace XtendElem: null with the
			// actual list of x_* element names from the response. These are the valid
			// values you pass in IncXtendElemInfo/XtendElem to get that data back.
			if (requestSchema is JsonObject reqObj
				&& reqObj["IncXtendElemArray"] is JsonObject incXtendObj
				&& incXtendObj["IncXtendElemInfo"] is JsonObject elemInfoObj)
			{
				var xNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
				CollectXNames(responseSchema, xNames);

				if (xNames.Count > 0)
				{
					var namesArray = new JsonArray();
					foreach (var n in xNames) namesArray.Add(JsonValue.Create(n));
					elemInfoObj["XtendElem"] = namesArray;
				}
			}

			root.inquiryOperations[elementName] = new InquiryOperation
			{
				name = elementName,
				requestSchema = requestSchema,
				responseSchema = responseSchema,
			};
		}

		// Sort alphabetically
		root.inquiryOperations = root.inquiryOperations
			.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

		return root;
	}

	/// <summary>
	/// Recursively collects all x_* element names from a response schema tree.
	/// These are the valid values for IncXtendElemInfo/XtendElem in the request.
	/// Does not recurse inside x_* nodes (their children are field names, not extended-element names).
	/// </summary>
	private static void CollectXNames(JsonNode? node, SortedSet<string> names)
	{
		if (node is not JsonObject obj) return;
		foreach (var (key, value) in obj)
		{
			if (key.Equals("isArray", StringComparison.OrdinalIgnoreCase)) continue;
			if (key.StartsWith("x_", StringComparison.OrdinalIgnoreCase))
				names.Add(key);
			else
				CollectXNames(value, names); // recurse into non-x_ nodes to find nested x_ elements
		}
	}
}
