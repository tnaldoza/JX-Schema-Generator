using System.Xml.Schema;
using JXSchemaGenerator.Model;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Build;

/// <summary>
/// Walks compiled XSD content models into <see cref="ElementNode"/> trees
/// </summary>
/// <remarks>
/// Supports sequences, group references, element references, wildcards and complexContent extensions
/// Any other construct throws <see cref="NotSupportedException"/> so a Master never yields a silently wrong tree
/// Message headers are skipped and <c>Ver_N</c> markers are replaced by their type's children
/// </remarks>
/// <param name="index">The compiled schema index, used to resolve element references</param>
/// <param name="warn">Receives one message per recursive type and per duplicate element, every time either occurs</param>
public sealed class ElementTreeBuilder(SchemaIndex index, Action<string> warn)
{
	public const int MaxDepth = 64;

	private const string VersionPrefix = "Ver_";

	private static readonly HashSet<string> Headers = new(["MsgRqHdr", "SrchMsgRqHdr", "MsgRsHdr", "SrchMsgRsHdr"], StringComparer.Ordinal);

	private readonly List<XmlSchemaComplexType> _expanding = [];
	private readonly List<string> _path = [];

	/// <summary>
	/// Builds the element tree of one complex type's content
	/// </summary>
	/// <param name="type">The type whose content becomes the top level of the tree</param>
	/// <param name="side">The message side, which decides whether <see cref="ElementNode.Required"/> is set</param>
	/// <returns>The top-level nodes in XSD sequence order</returns>
	/// <exception cref="NotSupportedException">The content uses a construct outside the supported subset</exception>
	/// <exception cref="InvalidOperationException">Elements nest deeper than <see cref="MaxDepth"/></exception>
	public IReadOnlyList<ElementNode> BuildChildren(XmlSchemaComplexType type, TreeSide side)
	{
		// ! Discards the expansion state of an earlier call that threw
		_expanding.Clear();
		_path.Clear();

		return ChildrenOf(type, LabelOf(type, null), side);
	}

	private List<ElementNode> ChildrenOf(XmlSchemaComplexType type, string label, TreeSide side)
	{
		var siblings = new Siblings(label);

		_expanding.Add(type);
		AddContent(type, label, siblings, side, enclosingRequired: true);
		_expanding.RemoveAt(_expanding.Count - 1);

		return siblings.Nodes;
	}

	private void AddContent(XmlSchemaComplexType type, string label, Siblings siblings, TreeSide side, bool enclosingRequired)
	{
		switch (type.ContentModel)
		{
			case XmlSchemaSimpleContent:
				return;
			case XmlSchemaComplexContent { Content: XmlSchemaComplexContentExtension extension }:
				if (type.BaseXmlSchemaType is XmlSchemaComplexType baseType)
					AddContent(baseType, LabelOf(baseType, null), siblings, side, enclosingRequired);

				AddParticle(extension.Particle, label, siblings, side, enclosingRequired);
				return;
			case XmlSchemaComplexContent:
				throw new NotSupportedException($"Type {label} derives by complexContent restriction, which is outside the supported XSD subset");
			default:
				AddParticle(type.Particle, label, siblings, side, enclosingRequired);
				return;
		}
	}

	private void AddParticle(XmlSchemaParticle? particle, string label, Siblings siblings, TreeSide side, bool enclosingRequired)
	{
		switch (particle)
		{
			case null or XmlSchemaAny:
				return;
			case XmlSchemaElement element:
				AddElement(element, siblings, side, enclosingRequired);
				return;
			case XmlSchemaSequence { MaxOccurs: <= 1 } sequence:
				foreach (var item in sequence.Items.OfType<XmlSchemaParticle>())
					AddParticle(item, label, siblings, side, enclosingRequired && sequence.MinOccurs >= 1);
				return;
			case XmlSchemaGroupRef { MaxOccurs: <= 1 } groupRef:
				AddParticle(groupRef.Particle, label, siblings, side, enclosingRequired && groupRef.MinOccurs >= 1);
				return;
			default:
				throw new NotSupportedException($"Type {label} uses {ConstructOf(particle)}, which is outside the supported XSD subset");
		}
	}

	private void AddElement(XmlSchemaElement element, Siblings siblings, TreeSide side, bool enclosingRequired)
	{
		var name = element.QualifiedName.Name;

		if (Headers.Contains(name))
			return;

		var type = element.ElementSchemaType ?? throw new InvalidOperationException($"Element {name} has no compiled schema type");
		var required = side == TreeSide.Request && enclosingRequired && element.MinOccurs >= 1;

		if (IsVersionMarker(name))
		{
			if (type is XmlSchemaComplexType marker)
				AddContent(marker, LabelOf(marker, name), siblings, side, required);

			return;
		}

		if (!siblings.Names.Add(name))
		{
			warn($"Duplicate element {name} in {siblings.Label}; the first declaration is kept");
			return;
		}

		siblings.Nodes.Add(BuildNode(element, name, type, side, required));
	}

	private ElementNode BuildNode(XmlSchemaElement element, string name, XmlSchemaType type, TreeSide side, bool required)
	{
		var complex = type as XmlSchemaComplexType;
		var anonymous = type.QualifiedName.IsEmpty;

		if (anonymous && complex is { AttributeUses.Count: > 0 })
			throw new NotSupportedException($"Element {name} has an anonymous complex type with attributes, which is outside the supported XSD subset");

		_path.Add(name);

		if (_path.Count > MaxDepth)
			throw new InvalidOperationException($"Elements nest deeper than {MaxDepth} levels at {string.Join('/', _path)}");

		var recursive = false;
		IReadOnlyList<ElementNode> children = [];

		if (complex is { ContentType: not XmlSchemaContentType.TextOnly })
		{
			var label = LabelOf(complex, name);

			if (IsExpanding(complex))
			{
				recursive = true;
				warn($"Recursive type {label} at element {name}; children omitted");
			}
			else
			{
				children = ChildrenOf(complex, label, side);
			}
		}

		_path.RemoveAt(_path.Count - 1);

		return new ElementNode(name)
		{
			BuiltInType = BuiltInTypes.NameOf(type),
			TypeName = anonymous || BuiltInTypes.IsBuiltIn(type.QualifiedName) ? null : type.QualifiedName,
			Required = required,
			Repeats = element.MaxOccurs > 1,
			// ! unbounded compiles to decimal.MaxValue, so it and any limit beyond int range are written as no limit
			MaxOccurs = element.MaxOccurs > 1 && element.MaxOccurs <= int.MaxValue ? (int)element.MaxOccurs : null,
			Recursive = recursive,
			Attributes = AttributeNames(complex),
			Annotation = AnnotationOf(element, type, anonymous),
			Children = children,
		};
	}

	private Annotation? AnnotationOf(XmlSchemaElement element, XmlSchemaType type, bool anonymous)
	{
		var annotation = AnnotationReader.Read(element);

		if (annotation is null && !element.RefName.IsEmpty && index.GlobalElements.TryGetValue(element.RefName, out var referenced))
			annotation = AnnotationReader.Read(referenced);

		if (annotation is null && anonymous)
			annotation = AnnotationReader.Read(type);

		return annotation;
	}

	private bool IsExpanding(XmlSchemaComplexType type) =>
		_expanding.Exists(t => ReferenceEquals(t, type) || (!type.QualifiedName.IsEmpty && t.QualifiedName == type.QualifiedName));

	private static string[] AttributeNames(XmlSchemaComplexType? type) =>
		type is null ? [] : [.. type.AttributeUses.Values.Cast<XmlSchemaAttribute>().Select(a => a.QualifiedName.Name).Order(StringComparer.Ordinal)];

	private static bool IsVersionMarker(string name) =>
		name.Length > VersionPrefix.Length && name.StartsWith(VersionPrefix, StringComparison.Ordinal) && name[VersionPrefix.Length..].All(char.IsAsciiDigit);

	private static string LabelOf(XmlSchemaComplexType type, string? element) =>
		type.QualifiedName.IsEmpty
			? element is null
				? "(anonymous type)"
				: $"(anonymous type of {element})"
			: type.QualifiedName.Name;

	private static string ConstructOf(XmlSchemaParticle particle) =>
		particle switch
		{
			XmlSchemaChoice => "xsd:choice",
			XmlSchemaAll => "xsd:all",
			XmlSchemaSequence => "a repeating xsd:sequence",
			XmlSchemaGroupRef groupRef => $"a repeating reference to group {groupRef.RefName.Name}",
			_ => particle.GetType().Name,
		};

	private sealed class Siblings(string label)
	{
		public string Label { get; } = label;

		public List<ElementNode> Nodes { get; } = [];

		public HashSet<string> Names { get; } = new(StringComparer.Ordinal);
	}
}
