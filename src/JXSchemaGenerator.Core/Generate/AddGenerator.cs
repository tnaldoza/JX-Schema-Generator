using System.Text.Json.Nodes;
using System.Xml;

using JXSchemaGenerator.Expand;
using JXSchemaGenerator.Generate.Models;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Generate;

/// <summary>
/// Generates add elements JSON by discovering all *AddRq_MType complex types.
/// For each add operation it emits:
///   requestSchema  - the fields needed to create the entity (MsgRqHdr stripped)
///   responseSchema - the response envelope fields (MsgRsHdr stripped)
///                    derived by swapping Rq_MType -> Rs_MType
/// Add operations do not use the x_* extended element mechanism.
/// </summary>
public sealed class AddGenerator
{
	private static readonly HashSet<string> SkippedRqElements =
		new(StringComparer.OrdinalIgnoreCase) { "MsgRqHdr" };

	private static readonly HashSet<string> SkippedRsElements =
		new(StringComparer.OrdinalIgnoreCase) { "MsgRsHdr" };

	public AddElementsRoot Generate(SchemaIndex index, ExpansionOptions options)
	{
		var expander = new TypeExpander(index, options);
		var root = new AddElementsRoot();

		foreach (var kvp in index.ComplexTypes)
		{
			var typeName = kvp.Key.Name;

			// Only process *AddRq_MType types
			if (!typeName.EndsWith("AddRq_MType", StringComparison.Ordinal)) continue;

			// Derive element name: CustAddRq_MType -> CustAdd  (strip "Rq_MType")
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

			root.addOperations[elementName] = new AddOperation
			{
				name = elementName,
				requestSchema = requestSchema,
				responseSchema = responseSchema,
			};
		}

		// Sort alphabetically
		root.addOperations = root.addOperations
			.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

		return root;
	}
}
