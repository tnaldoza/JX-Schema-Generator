using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Schema;

using JXSchemaGenerator.Expand;
using JXSchemaGenerator.Generate.Models;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Generate;

/// <summary>
/// Generic x_ extended-element generator driven by a <see cref="DomainConfig"/>.
/// Works for any JXChange XSD domain: Inquiry, Customer, and future domains.
/// </summary>
public sealed class XElementGenerator
{
	private readonly DomainConfig _config;

	public XElementGenerator(DomainConfig config) => _config = config;

	public InquiryElementsRoot Generate(SchemaIndex index, ExpansionOptions options)
	{
		var expander = new TypeExpander(index, options);
		var root = new InquiryElementsRoot();

		foreach (var kvp in index.ComplexTypes)
		{
			var match = _config.ContainerTypeRegex.Match(kvp.Key.Name);
			if (!match.Success) continue;

			var capturedKey = match.Groups["key"].Value;
			var mapping = _config.TypeToEntry(capturedKey);
			if (mapping is null) continue;

			var targetNs = kvp.Key.Namespace;

			var xElements = FindChildElements(kvp.Value)
				.Where(e => (e.Name ?? e.QualifiedName.Name)
					.StartsWith("x_", StringComparison.OrdinalIgnoreCase))
				.ToList();

			if (xElements.Count == 0) continue;

			if (!root.accountTypes.TryGetValue(mapping.Key, out var entity))
			{
				entity = new InquiryAccountType
				{
					name = mapping.DisplayName,
					aliases = [.. mapping.Aliases],
				};
				root.accountTypes[mapping.Key] = entity;
			}

			foreach (var x in xElements)
			{
				var xName = x.Name ?? x.QualifiedName.Name;
				if (entity.extendedElements.Any(e => e.name == xName)) continue;

				entity.extendedElements.Add(new InquiryExtendedElement
				{
					name = xName,
					description = TryGetDoc(x),
					schema = ResolveSchemaForElement(x, xName, targetNs, index, expander),
				});
			}

			entity.extendedElements = [.. entity.extendedElements
				.OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase)];
		}

		// Apply configured output order; append any unlisted keys at the end
		var ordered = _config.OutputOrder
			.Where(root.accountTypes.ContainsKey)
			.Concat(root.accountTypes.Keys.Except(_config.OutputOrder))
			.ToList();

		root.accountTypes = ordered.ToDictionary(k => k, k => root.accountTypes[k]);

		return root;
	}

	// Build the hierarchical schema tree for an x_ extended element
	private static JsonNode? ResolveSchemaForElement(
		XmlSchemaElement x,
		string xName,
		string targetNs,
		SchemaIndex index,
		TypeExpander expander)
	{
		// 1. ElementSchemaType already resolved by the compiler
		if (x.ElementSchemaType is XmlSchemaComplexType directCt &&
			!directCt.QualifiedName.IsEmpty)
		{
			var t1 = expander.ExpandToTree(directCt.QualifiedName, 0, []);
			if (t1 is not null) return t1;
		}

		// 2. SchemaTypeName - backfill missing namespace from enclosing type's ns
		var typeName = x.SchemaTypeName;
		if (!typeName.IsEmpty)
		{
			if (string.IsNullOrEmpty(typeName.Namespace))
				typeName = new XmlQualifiedName(typeName.Name, targetNs);

			if (index.ComplexTypes.ContainsKey(typeName))
			{
				var t2 = expander.ExpandToTree(typeName, 0, []);
				if (t2 is not null) return t2;
			}
		}

		// 3. JXChange naming convention: x_Foo -> x_Foo_CType / x_Foo_AType
		foreach (var suffix in (string[])["_CType", "_AType"])
		{
			var q = new XmlQualifiedName(xName + suffix, targetNs);
			if (index.ComplexTypes.ContainsKey(q))
			{
				var t3 = expander.ExpandToTree(q, 0, []);
				if (t3 is not null) return t3;
			}
		}

		return null;
	}

	// Walk ALL child elements, descending into Ver_ sequences
	private static IEnumerable<XmlSchemaElement> FindChildElements(XmlSchemaComplexType ct)
	{
		var list = new List<XmlSchemaElement>();

		void WalkParticle(XmlSchemaParticle? p)
		{
			if (p is null) return;
			switch (p)
			{
				case XmlSchemaSequence seq:
					foreach (var item in seq.Items) WalkObject(item);
					break;
				case XmlSchemaChoice choice:
					foreach (var item in choice.Items) WalkObject(item);
					break;
				case XmlSchemaAll all:
					foreach (var item in all.Items) WalkObject(item);
					break;
			}
		}

		void WalkObject(XmlSchemaObject obj)
		{
			if (obj is XmlSchemaElement el) { list.Add(el); return; }
			if (obj is XmlSchemaSequence s) { WalkParticle(s); return; }
			if (obj is XmlSchemaChoice c) { WalkParticle(c); return; }
			if (obj is XmlSchemaAll a) { WalkParticle(a); return; }
		}

		if (ct.ContentModel is XmlSchemaComplexContent cc &&
			cc.Content is XmlSchemaComplexContentExtension ext)
			WalkParticle(ext.Particle);
		else
			WalkParticle(ct.Particle);

		return list;
	}

	private static string? TryGetDoc(XmlSchemaAnnotated annotated)
	{
		if (annotated.Annotation is null) return null;
		foreach (var item in annotated.Annotation.Items)
		{
			if (item is XmlSchemaDocumentation doc && doc.Markup is not null)
			{
				var text = string.Concat(doc.Markup.Select(m => m?.Value)).Trim();
				if (!string.IsNullOrWhiteSpace(text)) return text;
			}
		}
		return null;
	}
}
