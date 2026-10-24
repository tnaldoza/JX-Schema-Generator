using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using JXSchemaGenerator.Model;

namespace JXSchemaGenerator.Build;

/// <summary>
/// Turns <c>xsd:documentation</c> markup into an <see cref="Annotation"/>
/// </summary>
/// <remarks>
/// Element names are matched without regard to case, and note and provider keys keep the spelling of the XSD
/// Top-level text and the own text of a <c>Jx</c> element are description parts, in document order with the <c>ElemDesc</c> fields
/// The fields of a <c>Jx</c> element and the top-level elements without child elements share one set of field rules
/// A top-level element with child elements, other than <c>Jx</c> and <c>Deprecate</c>, is a provider block
/// ! Text that is empty after whitespace collapsing is never stored, so a null result means the writer has nothing to write
/// </remarks>
public static partial class AnnotationReader
{
	private const string JxElement = "Jx";
	private const string DeprecateElement = "Deprecate";
	private const string DescriptionField = "ElemDesc";
	private const string ValuesField = "CanonicalVal";
	private const string DefaultField = "ElemDefault";
	private const string DateField = "Dt";
	private const string CommentField = "Cmnt";

	/// <summary>
	/// Reads the documentation of a schema item and ignores <c>xsd:appinfo</c>
	/// </summary>
	/// <param name="item">The schema item, or <see langword="null"/></param>
	/// <returns>The annotation, or <see langword="null"/> when the item carries no documentation content</returns>
	public static Annotation? Read(XmlSchemaAnnotated? item)
	{
		if (item?.Annotation is not { } annotation)
			return null;

		var fields = new Fields();
		var providers = new SortedDictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
		Deprecation? deprecated = null;

		foreach (var node in annotation.Items.OfType<XmlSchemaDocumentation>().SelectMany(d => d.Markup ?? []).OfType<XmlNode>())
		{
			if (node is not XmlElement element)
				fields.AddDescription(TextOf(node));
			else if (Is(element, JxElement))
				ReadJx(element, fields);
			else if (Is(element, DeprecateElement))
				deprecated ??= ReadDeprecation(element);
			else if (element.ChildNodes.OfType<XmlElement>().Any())
				AddProvider(element, providers);
			else
				fields.Add(element);
		}

		return fields.ToAnnotation(deprecated, providers);
	}

	private static void ReadJx(XmlElement jx, Fields fields)
	{
		foreach (XmlNode child in jx.ChildNodes)
		{
			if (child is XmlElement field)
				fields.Add(field);
			else
				fields.AddDescription(TextOf(child));
		}
	}

	private static Deprecation ReadDeprecation(XmlElement element)
	{
		string? date = null;
		string? comment = null;

		foreach (var child in element.ChildNodes.OfType<XmlElement>())
		{
			var text = TextOf(child);

			if (text.Length == 0)
				continue;

			if (Is(child, DateField))
				date ??= text;
			else if (Is(child, CommentField))
				comment ??= text;
		}

		return new Deprecation(date, comment);
	}

	private static void AddProvider(XmlElement block, SortedDictionary<string, IReadOnlyDictionary<string, string>> providers)
	{
		var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);

		foreach (var child in block.ChildNodes.OfType<XmlElement>())
		{
			var text = TextOf(child);

			if (text.Length > 0)
				fields.TryAdd(child.LocalName, text);
		}

		if (fields.Count > 0)
			providers.TryAdd(block.LocalName, fields);
	}

	private static bool Is(XmlElement element, string name) => string.Equals(element.LocalName, name, StringComparison.OrdinalIgnoreCase);

	private static string TextOf(XmlNode node)
	{
		var pieces = new List<string>();
		CollectText(node, pieces);

		return Whitespace().Replace(string.Join(' ', pieces), " ").Trim();
	}

	private static void CollectText(XmlNode node, List<string> pieces)
	{
		switch (node)
		{
			case XmlText or XmlCDataSection:
				pieces.Add(node.Value ?? string.Empty);
				break;
			case XmlElement:
				foreach (XmlNode child in node.ChildNodes)
					CollectText(child, pieces);
				break;
		}
	}

	[GeneratedRegex(@"\s+")]
	private static partial Regex Whitespace();

	/// <summary>
	/// Collects the description parts, values, default and notes of one annotation, first value wins for the default and each note
	/// </summary>
	private sealed class Fields
	{
		private readonly List<string> _parts = [];
		private readonly List<string> _values = [];
		private readonly SortedDictionary<string, string> _notes = new(StringComparer.Ordinal);
		private string? _default;

		public void AddDescription(string text)
		{
			if (text.Length > 0)
				_parts.Add(text);
		}

		public void Add(XmlElement field)
		{
			var text = TextOf(field);

			if (text.Length == 0)
				return;

			if (Is(field, DescriptionField))
				_parts.Add(text);
			else if (Is(field, ValuesField))
				_values.AddRange(text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
			else if (Is(field, DefaultField))
				_default ??= text;
			else
				_notes.TryAdd(field.LocalName, text);
		}

		public Annotation? ToAnnotation(Deprecation? deprecated, SortedDictionary<string, IReadOnlyDictionary<string, string>> providers)
		{
			var description = _parts.Count == 0 ? null : string.Join(' ', _parts);
			var values = _values.Distinct(StringComparer.Ordinal).ToArray();

			return description is null && values.Length == 0 && _default is null && deprecated is null && providers.Count == 0 && _notes.Count == 0
				? null
				: new Annotation(description, values, _default, deprecated, providers, _notes);
		}
	}
}
