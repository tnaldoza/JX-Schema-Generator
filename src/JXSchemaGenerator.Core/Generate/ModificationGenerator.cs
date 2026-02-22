using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Schema;

using JXSchemaGenerator.Expand;
using JXSchemaGenerator.Generate.Models;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Generate;

/// <summary>
/// Generates modification element mappings from JXChange *ModRq_MType complex types.
///
/// Two structural patterns exist in JXChange:
///
/// Nested pattern (e.g. Loan):
///   LnModRq_MType
///     -> LnMod (type LnMod_CType)     = modificationStructure
///          -> LnInfoRec, LnAcctInfo… = modificationSections
///
/// Flat pattern (e.g. Customer):
///   CustModRq_MType
///     -> CustDetail, RegDetail, BusDetail, TaxInfo… = modificationSections directly
///   (no intermediate *Mod_CType wrapper)
///
/// The nested pattern is detected when a direct child element's name ends in "Mod"
/// (e.g. "LnMod", "DepMod"). When no such element is found the flat pattern is used
/// and the non-infrastructure direct children become the sections.
/// </summary>
public sealed class ModificationGenerator
{
	private readonly ModificationDomainConfig _config;

	public ModificationGenerator(ModificationDomainConfig config) => _config = config;

	public ModificationElementsRoot Generate(SchemaIndex index, ExpansionOptions options)
	{
		var expander = new TypeExpander(index, options);
		var root = new ModificationElementsRoot();

		foreach (var kvp in index.ComplexTypes)
		{
			var match = _config.ContainerTypeRegex.Match(kvp.Key.Name);
			if (!match.Success) continue;

			var capturedKey = match.Groups["key"].Value;
			var mapping = _config.TypeToEntry(capturedKey);
			if (mapping is null) continue;

			var targetNs = kvp.Key.Namespace;

			string modStructureName;
			List<XmlSchemaElement> sectionEls;

			var modElement = FindModElement(kvp.Value);
			if (modElement is not null)
			{
				// Nested pattern: *ModRq -> *Mod element -> sections inside *Mod_CType
				modStructureName = modElement.Name ?? modElement.QualifiedName.Name;
				var modCType = ResolveComplexType(modElement, modStructureName, targetNs, index);
				if (modCType is null) continue;
				sectionEls = [.. FindSectionElements(modCType)];
			}
			else
			{
				// Flat pattern: sections are direct non-infrastructure children of the request type
				// e.g. CustModRq_MType -> CustDetail, RegDetail, BusDetail, TaxInfo
				modStructureName = "";
				sectionEls = [.. FindFlatSectionElements(kvp.Value, targetNs, index)];
			}

			if (sectionEls.Count == 0) continue;

			if (!root.entityTypes.TryGetValue(mapping.Key, out var entity))
			{
				entity = new ModificationEntityType
				{
					name = mapping.DisplayName,
					aliases = [.. mapping.Aliases],
					modificationStructure = modStructureName,
				};
				root.entityTypes[mapping.Key] = entity;
			}

			// Each section element maps to a modificationSection entry
			foreach (var sectionEl in sectionEls)
			{
				var sectionName = sectionEl.Name ?? sectionEl.QualifiedName.Name;
				if (entity.modificationSections.Any(s => s.name == sectionName)) continue;

				entity.modificationSections.Add(new ModificationSection
				{
					name = sectionName,
					description = TryGetDoc(sectionEl),
					schema = ResolveSchemaForElement(sectionEl, sectionName, targetNs, index, expander),
				});
			}
		}

		// Apply output order
		var ordered = _config.OutputOrder
			.Where(root.entityTypes.ContainsKey)
			.Concat(root.entityTypes.Keys.Except(_config.OutputOrder))
			.ToList();

		root.entityTypes = ordered.ToDictionary(k => k, k => root.entityTypes[k]);

		return root;
	}

	/// <summary>
	/// Finds the explicit *Mod wrapper element inside a *ModRq type.
	/// Identified by convention: element name ends in "Mod" (e.g. LnMod, DepMod, CustMod).
	/// Returns null when no such element exists -> caller uses flat-pattern fallback.
	/// </summary>
	private static XmlSchemaElement? FindModElement(XmlSchemaComplexType rqType)
	{
		return FindChildElements(rqType)
			.FirstOrDefault(e =>
			{
				var name = e.Name ?? e.QualifiedName.Name;
				return !ModificationDomainConfig.SkippedRqElements.Contains(name)
					&& !name.StartsWith("Ver_", StringComparison.OrdinalIgnoreCase)
					&& name.EndsWith("Mod", StringComparison.OrdinalIgnoreCase)
					&& (e.SchemaTypeName.Name.EndsWith("_CType", StringComparison.OrdinalIgnoreCase)
						|| e.ElementSchemaType is XmlSchemaComplexType);
			});
	}

	/// <summary>
	/// Find section elements inside an explicit *Mod_CType (nested pattern).
	/// Skips Custom and Ver_* infrastructure.
	/// </summary>
	private static IEnumerable<XmlSchemaElement> FindSectionElements(XmlSchemaComplexType modType)
	{
		return FindChildElements(modType)
			.Where(e =>
			{
				var name = e.Name ?? e.QualifiedName.Name;
				return !string.Equals(name, "Custom", StringComparison.OrdinalIgnoreCase)
					&& !name.StartsWith("Ver_", StringComparison.OrdinalIgnoreCase);
			});
	}

	/// <summary>
	/// Find section elements for the flat pattern where the request type's direct children
	/// ARE the sections (e.g. CustModRq_MType -> CustDetail, RegDetail, BusDetail, TaxInfo).
	/// Filters out infrastructure, Ver_ elements, and simple-content identifiers (CustId etc.)
	/// by requiring the resolved type to have at least one child element.
	/// </summary>
	private static IEnumerable<XmlSchemaElement> FindFlatSectionElements(
		XmlSchemaComplexType rqType, string targetNs, SchemaIndex index)
	{
		return FindChildElements(rqType)
			.Where(e =>
			{
				var name = e.Name ?? e.QualifiedName.Name;
				if (ModificationDomainConfig.SkippedRqElements.Contains(name)) return false;
				if (name.StartsWith("Ver_", StringComparison.OrdinalIgnoreCase)) return false;
				if (string.Equals(name, "Custom", StringComparison.OrdinalIgnoreCase)) return false;
				// Skip identifier/scalar fields (complex types with simple content have no child elements)
				var ct = ResolveComplexType(e, name, targetNs, index);
				return ct is not null && FindChildElements(ct).Any();
			});
	}

	// Resolve the complex type for a given element using the same three strategies
	// as XElementGenerator
	private static XmlSchemaComplexType? ResolveComplexType(
		XmlSchemaElement el,
		string elName,
		string targetNs,
		SchemaIndex index)
	{
		if (el.ElementSchemaType is XmlSchemaComplexType direct)
			return direct;

		var typeName = el.SchemaTypeName;
		if (!typeName.IsEmpty)
		{
			if (string.IsNullOrEmpty(typeName.Namespace))
				typeName = new XmlQualifiedName(typeName.Name, targetNs);
			if (index.ComplexTypes.TryGetValue(typeName, out var ct)) return ct;
		}

		foreach (var suffix in (string[])["_CType", "_AType"])
		{
			var q = new XmlQualifiedName(elName + suffix, targetNs);
			if (index.ComplexTypes.TryGetValue(q, out var ct2)) return ct2;
		}

		return null;
	}

	// Build the hierarchical schema tree for a section element
	private static JsonNode? ResolveSchemaForElement(
		XmlSchemaElement el,
		string elName,
		string targetNs,
		SchemaIndex index,
		TypeExpander expander)
	{
		// 1. ElementSchemaType (compiler-resolved named type)
		if (el.ElementSchemaType is XmlSchemaComplexType directCt &&
			!directCt.QualifiedName.IsEmpty)
		{
			var t1 = expander.ExpandToTree(directCt.QualifiedName, 0, []);
			if (t1 is not null) return t1;
		}

		// 2. SchemaTypeName - backfill missing namespace from enclosing type's ns
		var typeName = el.SchemaTypeName;
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

		// 3. JXChange naming convention: SectionName -> SectionName_CType / SectionName_AType
		foreach (var suffix in (string[])["_CType", "_AType"])
		{
			var q = new XmlQualifiedName(elName + suffix, targetNs);
			if (index.ComplexTypes.ContainsKey(q))
			{
				var t3 = expander.ExpandToTree(q, 0, []);
				if (t3 is not null) return t3;
			}
		}

		return null;
	}

	// Shared particle walker - identical to XElementGenerator
	private static List<XmlSchemaElement> FindChildElements(XmlSchemaComplexType ct)
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
