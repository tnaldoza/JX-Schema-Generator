using System.Xml;

namespace JXSchemaGenerator.Model;

/// <summary>
/// Which message of an operation a tree describes
/// </summary>
public enum TreeSide
{
	Request,
	Response,
}

/// <summary>
/// One element of a request or response tree, with its children in XSD sequence order
/// </summary>
/// <param name="Name">The element's local name</param>
public sealed record ElementNode(string Name)
{
	/// <summary>
	/// The built-in XSD type name, such as <c>string</c> or <c>date</c>, for elements with text content
	/// </summary>
	public string? BuiltInType { get; init; }

	/// <summary>
	/// The named, non-built-in schema type, or <see langword="null"/> for anonymous and built-in types
	/// </summary>
	public XmlQualifiedName? TypeName { get; init; }

	/// <summary>
	/// The element must appear whenever its parent does; always <see langword="false"/> on the response side
	/// </summary>
	public bool Required { get; init; }

	/// <summary>
	/// The element itself may occur more than once
	/// </summary>
	public bool Repeats { get; init; }

	/// <summary>
	/// The most occurrences allowed when the element repeats, or <see langword="null"/> when there is no limit
	/// </summary>
	public int? MaxOccurs { get; init; }

	/// <summary>
	/// The element's type is already being expanded higher up the tree, so its children are omitted
	/// </summary>
	public bool Recursive { get; init; }

	/// <summary>
	/// The attribute names, sorted with <see cref="StringComparer.Ordinal"/>
	/// </summary>
	public IReadOnlyList<string> Attributes { get; init; } = [];

	public Annotation? Annotation { get; init; }

	public IReadOnlyList<ElementNode> Children { get; init; } = [];
}
