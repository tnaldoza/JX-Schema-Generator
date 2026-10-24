using System.Xml;
using System.Xml.Schema;

namespace JXSchemaGenerator.Schema;

/// <summary>
/// Looks up the global elements and named types of a compiled schema set by qualified name
/// </summary>
public sealed class SchemaIndex
{
	public Dictionary<XmlQualifiedName, XmlSchemaComplexType> ComplexTypes { get; } = new();
	public Dictionary<XmlQualifiedName, XmlSchemaElement> GlobalElements { get; } = new();
	public Dictionary<XmlQualifiedName, XmlSchemaSimpleType> SimpleTypes { get; } = new();

	/// <summary>
	/// Indexes the global declarations of every document in a compiled set, included and imported documents among them
	/// </summary>
	/// <param name="set">A compiled schema set</param>
	/// <returns>The index, without the built-in XSD types</returns>
	public static SchemaIndex Build(XmlSchemaSet set)
	{
		var index = new SchemaIndex();

		foreach (XmlSchemaElement element in set.GlobalElements.Values)
			index.GlobalElements[element.QualifiedName] = element;

		foreach (XmlSchemaType type in set.GlobalTypes.Values)
		{
			if (type.QualifiedName.Namespace == XmlSchema.Namespace)
				continue;

			switch (type)
			{
				case XmlSchemaComplexType complex:
					index.ComplexTypes[complex.QualifiedName] = complex;
					break;
				case XmlSchemaSimpleType simple:
					index.SimpleTypes[simple.QualifiedName] = simple;
					break;
			}
		}

		return index;
	}

	public XmlSchemaComplexType GetComplexTypeOrThrow(XmlQualifiedName name) =>
		ComplexTypes.TryGetValue(name, out var ct) ? ct : throw new KeyNotFoundException($"ComplexType not found: {name}");

	public XmlSchemaElement GetElementOrThrow(XmlQualifiedName name) =>
		GlobalElements.TryGetValue(name, out var el) ? el : throw new KeyNotFoundException($"GlobalElement not found: {name}");

	public override string ToString() =>
		$"SchemaIndex: ComplexTypes={ComplexTypes.Count}, SimpleTypes={SimpleTypes.Count}, GlobalElements={GlobalElements.Count}";
}
