using System.Xml;
using System.Xml.Schema;
using JXSchemaGenerator.Model;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Build;

/// <summary>
/// Collects the named types an element forest depends on into one table per Master
/// </summary>
public static class TypeTableBuilder
{
	private const string ClosedEnumName = "ClosedEnum_Type";
	private const string OpenEnumName = "OpenEnum_Type";

	/// <summary>
	/// Builds the table of every type referenced by the nodes, by their attributes or by their base chains
	/// </summary>
	/// <param name="roots">The top-level nodes of every request and response tree</param>
	/// <param name="index">The compiled schema index the types are resolved from</param>
	/// <returns>The entries keyed by local name and sorted with <see cref="StringComparer.Ordinal"/></returns>
	/// <exception cref="InvalidOperationException">A type is missing from the index, or two namespaces define the same local name</exception>
	public static IReadOnlyDictionary<string, TypeEntry> Build(IEnumerable<ElementNode> roots, SchemaIndex index)
	{
		var table = new SortedDictionary<string, TypeEntry>(StringComparer.Ordinal);
		var seen = new Dictionary<string, XmlQualifiedName>(StringComparer.Ordinal);
		var pending = new Stack<XmlQualifiedName>();

		void Enqueue(XmlQualifiedName? name)
		{
			if (name is null || name.IsEmpty || BuiltInTypes.IsBuiltIn(name))
				return;

			if (seen.TryGetValue(name.Name, out var existing))
			{
				if (existing != name)
					throw new InvalidOperationException($"Types named {name.Name} exist in both {existing.Namespace} and {name.Namespace}");

				return;
			}

			seen[name.Name] = name;
			pending.Push(name);
		}

		foreach (var root in roots)
			EnqueueNode(root, Enqueue);

		while (pending.Count > 0)
		{
			var name = pending.Pop();
			var type = Resolve(name, index);
			var chain = ChainOf(type);

			foreach (var member in chain)
				Enqueue(member.QualifiedName);

			var attributes = AttributesOf(type, Enqueue);
			var values = chain.Select(AnnotationReader.Read).FirstOrDefault(a => a is { Values.Count: > 0 })?.Values ?? [];

			table[name.Name] = new TypeEntry(name.Name)
			{
				BuiltInBase = BuiltInTypes.NameOf(type),
				BaseTypeName = chain.Count > 1 ? chain[1].QualifiedName.Name : null,
				Enumeration = EnumerationOf(chain, values),
				Values = values,
				Annotation = AnnotationReader.Read(type),
				Attributes = attributes,
			};
		}

		return table;
	}

	private static void EnqueueNode(ElementNode node, Action<XmlQualifiedName?> enqueue)
	{
		enqueue(node.TypeName);

		foreach (var child in node.Children)
			EnqueueNode(child, enqueue);
	}

	private static XmlSchemaType Resolve(XmlQualifiedName name, SchemaIndex index)
	{
		if (index.ComplexTypes.TryGetValue(name, out var complex))
			return complex;

		if (index.SimpleTypes.TryGetValue(name, out var simple))
			return simple;

		throw new InvalidOperationException($"Type {name.Name} is not defined in the schema index");
	}

	private static List<XmlSchemaType> ChainOf(XmlSchemaType type)
	{
		var chain = new List<XmlSchemaType> { type };

		for (
			var next = type.BaseXmlSchemaType;
			next is not null && !next.QualifiedName.IsEmpty && !BuiltInTypes.IsBuiltIn(next.QualifiedName);
			next = next.BaseXmlSchemaType
		)
			chain.Add(next);

		return chain;
	}

	private static EnumKind? EnumerationOf(List<XmlSchemaType> chain, IReadOnlyList<string> values)
	{
		if (chain.Exists(t => t.QualifiedName.Name == ClosedEnumName))
			return EnumKind.Closed;

		return chain.Exists(t => t.QualifiedName.Name == OpenEnumName) || values.Count > 0 ? EnumKind.Open : null;
	}

	private static SortedDictionary<string, AttributeInfo> AttributesOf(XmlSchemaType type, Action<XmlQualifiedName?> enqueue)
	{
		var attributes = new SortedDictionary<string, AttributeInfo>(StringComparer.Ordinal);

		if (type is not XmlSchemaComplexType complex)
			return attributes;

		foreach (var use in complex.AttributeUses.Values.Cast<XmlSchemaAttribute>())
		{
			var attributeType = use.AttributeSchemaType;
			var named = attributeType is { QualifiedName.IsEmpty: false } && !BuiltInTypes.IsBuiltIn(attributeType.QualifiedName);

			if (named)
				enqueue(attributeType!.QualifiedName);

			attributes[use.QualifiedName.Name] = new AttributeInfo(
				attributeType is null ? null : BuiltInTypes.NameOf(attributeType),
				named ? attributeType!.QualifiedName.Name : null,
				use.Use == XmlSchemaUse.Required
			);
		}

		return attributes;
	}
}
