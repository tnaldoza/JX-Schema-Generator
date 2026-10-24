namespace JXSchemaGenerator.Model;

/// <summary>
/// Whether a value list is the complete set of legal values
/// </summary>
public enum EnumKind
{
	Closed,
	Open,
}

/// <summary>
/// One attribute of a type
/// </summary>
/// <param name="BuiltInType">The built-in XSD type name of the attribute value</param>
/// <param name="TypeName">The local name of the attribute's named, non-built-in type</param>
/// <param name="Required">The attribute is declared <c>use="required"</c></param>
public sealed record AttributeInfo(string? BuiltInType, string? TypeName, bool Required);

/// <summary>
/// One named schema type of a Master, shared by every element that references it
/// </summary>
/// <param name="Name">The type's local name</param>
public sealed record TypeEntry(string Name)
{
	/// <summary>
	/// The built-in XSD type name at the root of the base chain, or <see langword="null"/> for element-only types
	/// </summary>
	public string? BuiltInBase { get; init; }

	/// <summary>
	/// The local name of the nearest named, non-built-in base type
	/// </summary>
	public string? BaseTypeName { get; init; }

	/// <summary>
	/// <see langword="null"/> when the type has neither an enumeration marker nor listed values
	/// </summary>
	public EnumKind? Enumeration { get; init; }

	/// <summary>
	/// The first non-empty value list along the base chain, starting with the type itself
	/// </summary>
	public IReadOnlyList<string> Values { get; init; } = [];

	/// <summary>
	/// The type's own annotation, without anything inherited
	/// </summary>
	public Annotation? Annotation { get; init; }

	/// <summary>
	/// The attributes keyed by name and sorted with <see cref="StringComparer.Ordinal"/>
	/// </summary>
	public IReadOnlyDictionary<string, AttributeInfo> Attributes { get; init; } = new SortedDictionary<string, AttributeInfo>(StringComparer.Ordinal);
}
