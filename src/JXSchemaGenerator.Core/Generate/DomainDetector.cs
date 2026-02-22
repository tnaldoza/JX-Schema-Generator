using System.Xml;
using System.Xml.Schema;

using JXSchemaGenerator.Expand;
using JXSchemaGenerator.Schema;
using JXSchemaGenerator.Generate.Models;

namespace JXSchemaGenerator.Generate;

/// <summary>
/// Analyses a compiled SchemaIndex and produces a deterministic detection report
/// describing what domain configs (if any) should be added for that XSD.
/// Validation is performed by running the real TypeExpander against each detected
/// element and section, so tag counts and edge cases are exact - not estimates.
/// </summary>
public sealed class DomainDetector
{
	private readonly SchemaIndex _index;
	private readonly string _xsdFileName;
	private TypeExpander _expander = null!;

	// Infrastructure elements that appear in every Rq/Rs type - not domain content
	private static readonly HashSet<string> InfraElements = new(StringComparer.OrdinalIgnoreCase)
	{
		"MsgRqHdr", "MsgRsHdr", "ErrOvrRdInfoArray", "AccountId", "Dlt",
		"Custom", "ActIntentKey", "ActIntent", "RsStat",
	};

	public DomainDetector(SchemaIndex index, string xsdFileName)
	{
		_index = index;
		_xsdFileName = xsdFileName;
	}

	public DetectionReport Detect(ExpansionOptions options)
	{
		_expander = new TypeExpander(_index, options);

		var report = new DetectionReport(_xsdFileName);

		// Collect all *InqRec_CType and *InqRs_MType that contain x_ elements
		var inquiryContainers = FindContainersMatching(
			name => name.EndsWith("AcctInqRec_CType", StringComparison.OrdinalIgnoreCase)
				 || name.EndsWith("InqRs_MType", StringComparison.OrdinalIgnoreCase),
			requiresXChildren: true);

		// Collect all *ModRq_MType that contain a *Mod_CType child with sections
		var modContainers = FindContainersMatching(
			name => name.EndsWith("ModRq_MType", StringComparison.OrdinalIgnoreCase)
				 || name.EndsWith("AcctModRq_MType", StringComparison.OrdinalIgnoreCase),
			requiresXChildren: false,
			requiresModChild: true);

		// Inquiry analysis + validation
		foreach (var c in inquiryContainers)
		{
			var entry = report.GetOrAddInquiry(c.ContainerName);
			entry.XElements.AddRange(c.XChildren);
			entry.ContainerNames.Add(c.ContainerName);

			if (c.ContainerName.EndsWith("AcctInqRec_CType", StringComparison.OrdinalIgnoreCase))
			{
				var prefix = c.ContainerName[..^"AcctInqRec_CType".Length];
				entry.DetectedPrefix = prefix;
				entry.Pattern = ContainerPattern.MultiAcctInqRec;
			}
			else
			{
				var prefix = c.ContainerName[..^"InqRs_MType".Length];
				entry.DetectedPrefix = prefix;
				entry.Pattern = ContainerPattern.SingleInqRs;
			}

			// Run the real expansion pipeline for each x_ element
			foreach (var xEl in c.XChildren)
				entry.Validations.Add(ValidateElement(xEl, c.Namespace, isXElement: true));
		}

		// Modification analysis + validation
		foreach (var c in modContainers)
		{
			var entry = report.GetOrAddModification(c.ContainerName);
			entry.ContainerNames.Add(c.ContainerName);
			entry.ModStructureName = c.ModStructureName;

			if (c.ContainerName.EndsWith("AcctModRq_MType", StringComparison.OrdinalIgnoreCase))
			{
				var prefix = c.ContainerName[..^"AcctModRq_MType".Length];
				entry.DetectedPrefix = prefix;
				entry.Pattern = ContainerPattern.MultiAcctModRq;
			}
			else
			{
				var prefix = c.ContainerName[..^"ModRq_MType".Length];
				entry.DetectedPrefix = prefix;
				entry.Pattern = ContainerPattern.SingleModRq;
			}

			// Run the real expansion pipeline for each section element
			foreach (var sectionEl in c.ModSectionElements)
			{
				var v = ValidateElement(sectionEl, c.Namespace, isXElement: false);
				entry.SectionValidations.Add(new ModSectionValidation
				{
					SectionName = v.ElementName,
					Strategy = v.Strategy,
					ResolvedTypeName = v.ResolvedTypeName,
					TagCount = v.TagCount,
				});
			}
		}

		// Detect if already registered
		report.AlreadyRegisteredInquiry = DomainConfigs.FromRootXsd(_xsdFileName) is { Length: > 0 };
		report.AlreadyRegisteredMod = ModificationDomainConfigs.FromRootXsd(_xsdFileName) is not null;

		return report;
	}

	// Run the real three-strategy TypeExpander expansion used by the generators.
	// Returns exact tag count, which strategy worked, and what type was resolved.
	private XElementValidation ValidateElement(XmlSchemaElement el, string ns, bool isXElement)
	{
		var name = el.Name ?? el.QualifiedName.Name;
		var v = new XElementValidation { ElementName = name };

		// Strategy 1: ElementSchemaType already resolved by the XML schema compiler
		if (el.ElementSchemaType is XmlSchemaComplexType directCt && !directCt.QualifiedName.IsEmpty)
		{
			var tags = _expander.ExpandComplexType(directCt.QualifiedName, 0, new HashSet<XmlQualifiedName>());
			if (tags.Count > 0)
			{
				v.Strategy = TagResolutionStrategy.DirectType;
				v.ResolvedTypeName = directCt.QualifiedName.Name;
				v.TagCount = tags.Count;
				return v;
			}
		}

		// Strategy 2: SchemaTypeName attribute (backfill namespace from enclosing type if missing)
		var tn = el.SchemaTypeName;
		if (!tn.IsEmpty)
		{
			if (string.IsNullOrEmpty(tn.Namespace))
				tn = new XmlQualifiedName(tn.Name, ns);

			if (_index.ComplexTypes.ContainsKey(tn))
			{
				var tags = _expander.ExpandComplexType(tn, 0, new HashSet<XmlQualifiedName>());
				if (tags.Count > 0)
				{
					v.Strategy = TagResolutionStrategy.SchemaTypeName;
					v.ResolvedTypeName = tn.Name;
					v.TagCount = tags.Count;
					return v;
				}
			}
		}

		// Strategy 3: JXChange naming convention - try Foo_CType then Foo_AType
		foreach (var suffix in new[] { "_CType", "_AType" })
		{
			var q = new XmlQualifiedName(name + suffix, ns);
			if (_index.ComplexTypes.ContainsKey(q))
			{
				var tags = _expander.ExpandComplexType(q, 0, new HashSet<XmlQualifiedName>());
				if (tags.Count > 0)
				{
					v.Strategy = TagResolutionStrategy.Convention;
					v.ResolvedTypeName = name + suffix;
					v.TagCount = tags.Count;
					return v;
				}
			}
		}

		// None of the strategies produced tags
		v.Strategy = TagResolutionStrategy.None;
		v.TagCount = 0;
		return v;
	}

	private List<ContainerResult> FindContainersMatching(
		Func<string, bool> nameFilter,
		bool requiresXChildren,
		bool requiresModChild = false)
	{
		var results = new List<ContainerResult>();

		foreach (var kvp in _index.ComplexTypes)
		{
			var typeName = kvp.Key.Name;
			if (!nameFilter(typeName)) continue;

			var ct = kvp.Value;
			var all = GetAllChildElements(ct);

			var xChildren = all
				.Where(e => (e.Name ?? "").StartsWith("x_", StringComparison.OrdinalIgnoreCase))
				.ToList();

			if (requiresXChildren && xChildren.Count == 0) continue;

			// For mod: find the *Mod child and its section elements
			string? modStructure = null;
			var modSectionElements = new List<XmlSchemaElement>();

			if (requiresModChild)
			{
				// Nested pattern: look for an explicit *Mod element (name ends in "Mod")
				var modEl = all.FirstOrDefault(e =>
				{
					var n = e.Name ?? "";
					return !InfraElements.Contains(n)
						&& !n.StartsWith("Ver_", StringComparison.OrdinalIgnoreCase)
						&& n.EndsWith("Mod", StringComparison.OrdinalIgnoreCase)
						&& (e.SchemaTypeName.Name.EndsWith("_CType") || e.ElementSchemaType is XmlSchemaComplexType);
				});

				if (modEl is not null)
				{
					// Nested: use the *Mod_CType's children as sections
					modStructure = modEl.Name;
					var modCt = ResolveType(modEl, kvp.Key.Namespace);
					if (modCt is null) continue;

					modSectionElements = GetAllChildElements(modCt)
						.Where(e =>
						{
							var n = e.Name ?? "";
							return !InfraElements.Contains(n)
								&& !n.StartsWith("Ver_", StringComparison.OrdinalIgnoreCase)
								&& !string.Equals(n, "Custom", StringComparison.OrdinalIgnoreCase)
								&& !string.IsNullOrEmpty(n);
						})
						.ToList();
				}
				else
				{
					// Flat pattern: sections are direct non-infra children (e.g. CustModRq -> CustDetail, RegDetail)
					modStructure = "";
					modSectionElements = all
						.Where(e =>
						{
							var n = e.Name ?? "";
							if (InfraElements.Contains(n)) return false;
							if (n.StartsWith("Ver_", StringComparison.OrdinalIgnoreCase)) return false;
							if (string.Equals(n, "Custom", StringComparison.OrdinalIgnoreCase)) return false;
							if (string.IsNullOrEmpty(n)) return false;
							// Skip identifier/scalar elements (no child elements in their type)
							var ct = ResolveType(e, kvp.Key.Namespace);
							return ct is not null && GetAllChildElements(ct).Count > 0;
						})
						.ToList();
				}

				if (modSectionElements.Count == 0) continue;
			}

			results.Add(new ContainerResult
			{
				ContainerName = typeName,
				Namespace = kvp.Key.Namespace,
				XChildren = xChildren,
				ModStructureName = modStructure,
				ModSectionElements = modSectionElements,
			});
		}

		return results;
	}

	private XmlSchemaComplexType? ResolveType(XmlSchemaElement el, string ns)
	{
		if (el.ElementSchemaType is XmlSchemaComplexType direct) return direct;

		var tn = el.SchemaTypeName;
		if (!tn.IsEmpty)
		{
			if (string.IsNullOrEmpty(tn.Namespace)) tn = new XmlQualifiedName(tn.Name, ns);
			if (_index.ComplexTypes.TryGetValue(tn, out var ct)) return ct;
		}

		foreach (var suffix in new[] { "_CType", "_AType" })
		{
			var q = new XmlQualifiedName((el.Name ?? "") + suffix, ns);
			if (_index.ComplexTypes.TryGetValue(q, out var ct2)) return ct2;
		}

		return null;
	}

	private static List<XmlSchemaElement> GetAllChildElements(XmlSchemaComplexType ct)
	{
		var list = new List<XmlSchemaElement>();

		void WalkParticle(XmlSchemaParticle? p)
		{
			if (p is null) return;
			switch (p)
			{
				case XmlSchemaSequence seq: foreach (var i in seq.Items) WalkObject(i); break;
				case XmlSchemaChoice choice: foreach (var i in choice.Items) WalkObject(i); break;
				case XmlSchemaAll all: foreach (var i in all.Items) WalkObject(i); break;
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
}
