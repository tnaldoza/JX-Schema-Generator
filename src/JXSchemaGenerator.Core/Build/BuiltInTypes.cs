using System.Xml;
using System.Xml.Schema;

namespace JXSchemaGenerator.Build;

/// <summary>
/// Names the built-in XSD type at the root of a schema type's base chain
/// </summary>
public static class BuiltInTypes
{
	/// <summary>
	/// Returns the built-in type name, such as <c>string</c>, <c>decimal</c> or <c>dateTime</c>
	/// </summary>
	/// <param name="type">A simple type, or a complex type of any content</param>
	/// <returns>The name for simple types and text-only complex types, otherwise <see langword="null"/></returns>
	public static string? NameOf(XmlSchemaType type) =>
		type is XmlSchemaSimpleType or XmlSchemaComplexType { ContentType: XmlSchemaContentType.TextOnly } && type.Datatype is { } datatype
			? XmlSchemaType.GetBuiltInSimpleType(datatype.TypeCode)?.QualifiedName.Name
			: null;

	/// <summary>
	/// Tells whether a qualified name belongs to the XSD namespace itself
	/// </summary>
	/// <param name="name">A schema type name</param>
	/// <returns><see langword="true"/> for names in <see cref="XmlSchema.Namespace"/></returns>
	public static bool IsBuiltIn(XmlQualifiedName name) => name.Namespace == XmlSchema.Namespace;
}
