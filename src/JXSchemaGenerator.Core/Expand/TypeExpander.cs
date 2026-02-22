using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Schema;

using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Expand;

public sealed class TypeExpander
{
	private readonly SchemaIndex _index;
	private readonly ExpansionOptions _opt;

	// Memoize flat tag expansions by complexType QName
	private readonly Dictionary<XmlQualifiedName, HashSet<string>> _memo = new();

	public TypeExpander(SchemaIndex index, ExpansionOptions options)
	{
		_index = index;
		_opt = options;
	}

	// ─── Flat tag expansion (existing) ───────────────────────────────────────

	public List<string> ExpandElementTags(XmlSchemaElement element)
	{
		var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// IMPORTANT: compiled schemas often have SchemaTypeName empty for local elements,
		// but ElementSchemaType is populated.
		if (element.ElementSchemaType is XmlSchemaComplexType ct)
		{
			ExpandComplexTypeObject(ct, tags, 0, new HashSet<XmlSchemaObject>());
		}
		else if (!element.SchemaTypeName.IsEmpty)
		{
			foreach (var t in ExpandComplexType(element.SchemaTypeName, 0, new HashSet<System.Xml.XmlQualifiedName>()))
				tags.Add(t);
		}
		else
		{
			// leaf/simple
			tags.Add(element.Name ?? element.QualifiedName.Name);
		}

		return tags.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
	}

	public HashSet<string> ExpandComplexType(XmlQualifiedName ctName, int depth, HashSet<XmlQualifiedName> stack)
	{
		if (depth > _opt.MaxDepth) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		if (_memo.TryGetValue(ctName, out var cached))
			return new HashSet<string>(cached, StringComparer.OrdinalIgnoreCase);

		if (stack.Contains(ctName))
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase); // cycle break

		stack.Add(ctName);

		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		if (!_index.ComplexTypes.TryGetValue(ctName, out var ct))
		{
			stack.Remove(ctName);
			return result;
		}

		// Handle complexContent extension/restriction
		if (ct.ContentModel is XmlSchemaComplexContent complexContent &&
			complexContent.Content is XmlSchemaComplexContentExtension ext &&
			!ext.BaseTypeName.IsEmpty)
		{
			// expand base first
			foreach (var t in ExpandComplexType(ext.BaseTypeName, depth + 1, stack))
				result.Add(t);

			// then expand extension particle
			if (ext.Particle is not null)
				ExpandParticleIntoTags(ext.Particle, result, depth + 1, stack);
		}
		else
		{
			// Normal particle
			if (ct.Particle is not null)
				ExpandParticleIntoTags(ct.Particle, result, depth + 1, stack);
		}

		stack.Remove(ctName);

		_memo[ctName] = new HashSet<string>(result, StringComparer.OrdinalIgnoreCase);
		return result;
	}

	private void ExpandComplexTypeObject(
	XmlSchemaComplexType ct,
	HashSet<string> tags,
	int depth,
	HashSet<XmlSchemaObject> stack)
	{
		if (depth > _opt.MaxDepth) return;
		if (stack.Contains(ct)) return; // cycle break for anonymous types

		stack.Add(ct);

		// complexContent extension support
		if (ct.ContentModel is XmlSchemaComplexContent complexContent &&
			complexContent.Content is XmlSchemaComplexContentExtension ext)
		{
			// Expand base if it's a named type we can find
			if (!ext.BaseTypeName.IsEmpty)
			{
				foreach (var t in ExpandComplexType(ext.BaseTypeName, depth + 1, new HashSet<System.Xml.XmlQualifiedName>()))
					tags.Add(t);
			}

			if (ext.Particle is not null)
				ExpandParticleIntoTags(ext.Particle, tags, depth + 1, new HashSet<System.Xml.XmlQualifiedName>());
		}
		else
		{
			if (ct.Particle is not null)
				ExpandParticleIntoTags(ct.Particle, tags, depth + 1, new HashSet<System.Xml.XmlQualifiedName>());
		}

		stack.Remove(ct);
	}

	private void ExpandParticleIntoTags(XmlSchemaParticle particle, HashSet<string> tags, int depth, HashSet<XmlQualifiedName> stack)
	{
		switch (particle)
		{
			case XmlSchemaSequence seq:
				foreach (var item in seq.Items)
					ExpandObjectIntoTags(item, tags, depth + 1, stack);
				break;

			case XmlSchemaChoice choice:
				foreach (var item in choice.Items)
					ExpandObjectIntoTags(item, tags, depth + 1, stack);
				break;

			case XmlSchemaAll all:
				foreach (var item in all.Items)
					ExpandObjectIntoTags(item, tags, depth + 1, stack);
				break;

			default:
				// other particle types are rare here
				break;
		}
	}

	private void ExpandObjectIntoTags(XmlSchemaObject obj, HashSet<string> tags, int depth, HashSet<XmlQualifiedName> stack)
	{
		if (obj is XmlSchemaElement el)
		{
			if (!el.RefName.IsEmpty && _index.GlobalElements.TryGetValue(el.RefName, out var resolved))
			{
				el = resolved;
			}

			var name = !el.RefName.IsEmpty ? el.RefName.Name : (el.Name ?? el.QualifiedName.Name);

			if (_opt.SkipVersionContainers && name.StartsWith("Ver_", StringComparison.OrdinalIgnoreCase))
			{
				// Traverse its resolved type (ElementSchemaType) if possible
				if (el.ElementSchemaType is XmlSchemaComplexType verCt)
					ExpandComplexTypeObject(verCt, tags, depth + 1, new HashSet<XmlSchemaObject>());
				else if (!el.SchemaTypeName.IsEmpty)
					foreach (var t in ExpandComplexType(el.SchemaTypeName, depth + 1, stack)) tags.Add(t);

				return;
			}

			if (el.ElementSchemaType is XmlSchemaComplexType childCt)
			{
				if (!_opt.LeafTagsOnly)
					tags.Add(name); // optional; keep off if you want leaf-only

				ExpandComplexTypeObject(childCt, tags, depth + 1, new HashSet<XmlSchemaObject>());
				return;
			}

			tags.Add(name);

			return;
		}

		if (obj is XmlSchemaSequence seq)
		{
			ExpandParticleIntoTags(seq, tags, depth + 1, stack);
			return;
		}

		if (obj is XmlSchemaChoice choice)
		{
			ExpandParticleIntoTags(choice, tags, depth + 1, stack);
			return;
		}

		if (obj is XmlSchemaAll all)
		{
			ExpandParticleIntoTags(all, tags, depth + 1, stack);
			return;
		}

		if (obj is XmlSchemaGroupRef gr)
		{
			if (gr.Particle is not null)
				ExpandParticleIntoTags(gr.Particle, tags, depth + 1, stack);

			return;
		}

		if (obj is XmlSchemaAny any)
		{
			if (_opt.SkipAny) return;
			tags.Add("*");
			return;
		}
	}

	// ─── Hierarchical schema tree expansion ──────────────────────────────────

	/// <summary>
	/// Builds a hierarchical JSON schema tree for the type of the given element.
	/// Returns null for simple/leaf elements with no complex type.
	/// </summary>
	public JsonNode? ExpandElementTree(XmlSchemaElement element)
	{
		if (element.ElementSchemaType is XmlSchemaComplexType ct)
		{
			return ct.QualifiedName.IsEmpty
				? ExpandComplexTypeToTree(ct, 0, [])
				: ExpandToTree(ct.QualifiedName, 0, []);
		}
		if (!element.SchemaTypeName.IsEmpty)
			return ExpandToTree(element.SchemaTypeName, 0, []);
		return null;
	}

	/// <summary>
	/// Builds a hierarchical JSON schema tree for a named complex type.
	/// Each key is an element name; the value is either:
	///   null        – leaf (simple type)
	///   JsonObject  – complex element with children (may include "isArray": true)
	/// Returns null on cycle, depth limit, or type not found.
	/// </summary>
	public JsonNode? ExpandToTree(XmlQualifiedName ctName, int depth, HashSet<XmlQualifiedName> stack)
	{
		if (depth > _opt.MaxDepth) return null;
		if (stack.Contains(ctName)) return null;
		if (!_index.ComplexTypes.TryGetValue(ctName, out var ct)) return null;

		stack.Add(ctName);
		var result = ExpandComplexTypeToTree(ct, depth, stack);
		stack.Remove(ctName);
		return result;
	}

	private JsonNode? ExpandComplexTypeToTree(XmlSchemaComplexType ct, int depth, HashSet<XmlQualifiedName> stack)
	{
		if (depth > _opt.MaxDepth) return null;

		var obj = new JsonObject();

		if (ct.ContentModel is XmlSchemaComplexContent complexContent &&
			complexContent.Content is XmlSchemaComplexContentExtension ext)
		{
			if (!ext.BaseTypeName.IsEmpty)
			{
				var baseTree = ExpandToTree(ext.BaseTypeName, depth + 1, stack);
				if (baseTree is JsonObject baseObj)
					foreach (var prop in baseObj)
						obj[prop.Key] = prop.Value?.DeepClone();
			}
			if (ext.Particle is not null)
				ExpandParticleIntoTree(ext.Particle, obj, depth + 1, stack);
		}
		else
		{
			if (ct.Particle is not null)
				ExpandParticleIntoTree(ct.Particle, obj, depth + 1, stack);
		}

		return obj.Count > 0 ? obj : null;
	}

	private void ExpandParticleIntoTree(XmlSchemaParticle particle, JsonObject obj, int depth, HashSet<XmlQualifiedName> stack)
	{
		switch (particle)
		{
			case XmlSchemaSequence seq:
				foreach (var item in seq.Items)
					ExpandObjectIntoTree(item, obj, depth + 1, stack);
				break;
			case XmlSchemaChoice choice:
				foreach (var item in choice.Items)
					ExpandObjectIntoTree(item, obj, depth + 1, stack);
				break;
			case XmlSchemaAll all:
				foreach (var item in all.Items)
					ExpandObjectIntoTree(item, obj, depth + 1, stack);
				break;
		}
	}

	private void ExpandObjectIntoTree(XmlSchemaObject schemaObj, JsonObject obj, int depth, HashSet<XmlQualifiedName> stack)
	{
		if (schemaObj is XmlSchemaElement el)
		{
			if (!el.RefName.IsEmpty && _index.GlobalElements.TryGetValue(el.RefName, out var resolved))
				el = resolved;

			var name = !el.RefName.IsEmpty ? el.RefName.Name : (el.Name ?? el.QualifiedName.Name);

			// Ver_* transparent descent: skip the container, merge its children into current level
			if (_opt.SkipVersionContainers && name.StartsWith("Ver_", StringComparison.OrdinalIgnoreCase))
			{
				JsonNode? verTree = null;
				if (el.ElementSchemaType is XmlSchemaComplexType verCt)
					verTree = ExpandComplexTypeToTree(verCt, depth + 1, stack);
				else if (!el.SchemaTypeName.IsEmpty)
					verTree = ExpandToTree(el.SchemaTypeName, depth + 1, stack);

				if (verTree is JsonObject verObj)
					foreach (var prop in verObj)
						obj[prop.Key] = prop.Value?.DeepClone();
				return;
			}

			// Resolve child complex type -> build child tree
			JsonNode? childTree = null;
			if (el.ElementSchemaType is XmlSchemaComplexType childCt)
			{
				childTree = childCt.QualifiedName.IsEmpty
					? ExpandComplexTypeToTree(childCt, depth + 1, stack)
					: ExpandToTree(childCt.QualifiedName, depth + 1, stack);
			}
			else if (!el.SchemaTypeName.IsEmpty && _index.ComplexTypes.ContainsKey(el.SchemaTypeName))
			{
				childTree = ExpandToTree(el.SchemaTypeName, depth + 1, stack);
			}

			if (childTree is not null)
			{
				// Elements whose name contains "Array" are repeating array containers
				if (name.Contains("Array", StringComparison.OrdinalIgnoreCase))
				{
					var arrayObj = new JsonObject { ["isArray"] = JsonValue.Create(true) };
					if (childTree is JsonObject childJsonObj)
						foreach (var prop in childJsonObj)
							arrayObj[prop.Key] = prop.Value?.DeepClone();
					obj[name] = arrayObj;
				}
				else
				{
					obj[name] = childTree;
				}
				return;
			}

			// Simple/leaf element
			obj[name] = null;
			return;
		}

		if (schemaObj is XmlSchemaSequence seq2) { ExpandParticleIntoTree(seq2, obj, depth + 1, stack); return; }
		if (schemaObj is XmlSchemaChoice choice2) { ExpandParticleIntoTree(choice2, obj, depth + 1, stack); return; }
		if (schemaObj is XmlSchemaAll all2) { ExpandParticleIntoTree(all2, obj, depth + 1, stack); return; }
		if (schemaObj is XmlSchemaGroupRef gr)
		{
			if (gr.Particle is not null) ExpandParticleIntoTree(gr.Particle, obj, depth + 1, stack);
			return;
		}
		if (schemaObj is XmlSchemaAny)
		{
			if (!_opt.SkipAny) obj["*"] = null;
			return;
		}
	}
}
