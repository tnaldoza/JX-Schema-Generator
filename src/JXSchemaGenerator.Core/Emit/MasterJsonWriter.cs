using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using JXSchemaGenerator.Model;

namespace JXSchemaGenerator.Emit;

/// <summary>
/// Writes a <see cref="MasterDocument"/> as format 2 JSON and ships the format's JSON Schema
/// </summary>
/// <remarks>
/// Key names, key order and omission rules are the contract downstream clients generate their types from
/// Dictionaries are written in <see cref="StringComparer.Ordinal"/> key order and element children keep XSD sequence order
/// Optional keys are omitted when their value would be null, false or empty, and the shipped JSON Schema marks the keys that are always written
/// </remarks>
public static class MasterJsonWriter
{
	public const int FormatVersion = 2;

	public const string SchemaFileName = "jxchange-schemas.schema.json";

	private static readonly JsonWriterOptions Options = new()
	{
		Indented = true,
		IndentCharacter = ' ',
		IndentSize = 2,
		NewLine = "\n",
		// ! Output is read as JSON and never embedded in HTML
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	/// <summary>
	/// Serializes a Master document in the format 2 key order
	/// </summary>
	/// <param name="document">The built Master</param>
	/// <returns>Two-space indented JSON with <c>\n</c> line endings and one trailing newline</returns>
	public static string ToJson(MasterDocument document)
	{
		var buffer = new ArrayBufferWriter<byte>();

		using (var writer = new Utf8JsonWriter(buffer, Options))
			WriteDocument(writer, document);

		return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
	}

	/// <summary>
	/// Reads the format's JSON Schema from the embedded resource
	/// </summary>
	/// <returns>The schema text</returns>
	/// <exception cref="InvalidOperationException">The resource is missing from the assembly</exception>
	public static string ReadSchema()
	{
		var assembly = typeof(MasterJsonWriter).Assembly;

		using var stream =
			assembly.GetManifestResourceStream(SchemaFileName)
			?? throw new InvalidOperationException($"Embedded resource {SchemaFileName} is missing from {assembly.GetName().Name}");
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}

	/// <summary>
	/// Writes the format's JSON Schema next to the Master files, where their <c>$schema</c> key points
	/// </summary>
	/// <param name="outputFolder">An existing folder</param>
	public static void WriteSchema(string outputFolder) => File.WriteAllText(Path.Combine(outputFolder, SchemaFileName), ReadSchema());

	private static void WriteDocument(Utf8JsonWriter writer, MasterDocument document)
	{
		writer.WriteStartObject();
		writer.WriteString("$schema", "./" + SchemaFileName);
		writer.WriteNumber("formatVersion", FormatVersion);

		if (document.TpgVersion is null)
			writer.WriteNull("tpgVersion");
		else
			writer.WriteString("tpgVersion", document.TpgVersion);

		writer.WriteString("master", document.Master);
		writer.WriteStartObject("operations");

		foreach (var operation in document.Operations.OrderBy(o => o.Name, StringComparer.Ordinal))
		{
			writer.WritePropertyName(operation.Name);
			WriteOperation(writer, operation);
		}

		writer.WriteEndObject();
		writer.WriteStartObject("types");

		foreach (var (name, entry) in Ordered(document.Types))
		{
			writer.WritePropertyName(name);
			WriteTypeEntry(writer, entry);
		}

		writer.WriteEndObject();
		writer.WriteEndObject();
	}

	private static void WriteOperation(Utf8JsonWriter writer, OperationModel operation)
	{
		writer.WriteStartObject();
		writer.WriteString("kind", operation.Kind.ToString().ToLowerInvariant());
		writer.WriteString("requestElement", operation.RequestElement);
		WriteIfPresent(writer, "responseElement", operation.ResponseElement);
		WriteAnnotation(writer, "annotation", operation.Annotation);
		WriteIfPresent(writer, "extendedElements", operation.ExtendedElements);
		WriteIfPresent(writer, "records", operation.Records);
		WriteModify(writer, operation.ModifyContainers);

		writer.WriteStartObject("request");
		WriteNodes(writer, operation.Request);
		writer.WriteEndObject();

		if (operation.Response is null)
		{
			writer.WriteNull("response");
		}
		else
		{
			writer.WriteStartObject("response");
			WriteNodes(writer, operation.Response);
			writer.WriteEndObject();
		}

		writer.WriteEndObject();
	}

	private static void WriteModify(Utf8JsonWriter writer, IReadOnlyList<ModifyContainer> containers)
	{
		if (containers.Count == 0)
			return;

		writer.WriteStartObject("modify");
		writer.WriteStartArray("containers");

		foreach (var container in containers)
		{
			writer.WriteStartObject();

			if (container.Name is null)
				writer.WriteNull("name");
			else
				writer.WriteString("name", container.Name);

			writer.WriteStartArray("sections");

			foreach (var section in container.Sections)
				writer.WriteStringValue(section);

			writer.WriteEndArray();
			writer.WriteEndObject();
		}

		writer.WriteEndArray();
		writer.WriteEndObject();
	}

	private static void WriteNodes(Utf8JsonWriter writer, IReadOnlyList<ElementNode> nodes)
	{
		foreach (var node in nodes)
		{
			writer.WriteStartObject(node.Name);
			WriteIfPresent(writer, "$type", node.BuiltInType);
			WriteIfPresent(writer, "$typeName", node.TypeName?.Name);
			WriteIfTrue(writer, "$required", node.Required);
			WriteIfTrue(writer, "$array", node.Repeats);
			WriteIfTrue(writer, "$recursive", node.Recursive);
			WriteIfPresent(writer, "$attrs", node.Attributes);
			WriteAnnotation(writer, "$annotation", node.Annotation);
			WriteNodes(writer, node.Children);
			writer.WriteEndObject();
		}
	}

	private static void WriteAnnotation(Utf8JsonWriter writer, string propertyName, Annotation? annotation)
	{
		if (annotation is null || !HasContent(annotation))
			return;

		writer.WriteStartObject(propertyName);
		WriteIfPresent(writer, "description", annotation.Description);
		WriteIfPresent(writer, "values", annotation.Values);
		WriteAnnotationDetails(writer, annotation);
		writer.WriteEndObject();
	}

	private static void WriteTypeEntry(Utf8JsonWriter writer, TypeEntry entry)
	{
		writer.WriteStartObject();
		WriteIfPresent(writer, "base", entry.BuiltInBase);
		WriteIfPresent(writer, "baseTypeName", entry.BaseTypeName);
		WriteIfPresent(writer, "enum", entry.Enumeration?.ToString().ToLowerInvariant());
		WriteIfPresent(writer, "values", entry.Values);

		if (entry.Annotation is { } annotation)
		{
			WriteIfPresent(writer, "description", annotation.Description);
			WriteAnnotationDetails(writer, annotation);
		}

		WriteAttributes(writer, entry.Attributes);
		writer.WriteEndObject();
	}

	/// <summary>
	/// Writes the annotation fields that follow <c>description</c> and <c>values</c> in both the annotation and the type entry layouts
	/// </summary>
	private static void WriteAnnotationDetails(Utf8JsonWriter writer, Annotation annotation)
	{
		WriteIfPresent(writer, "default", annotation.Default);

		// ! An empty deprecated object still marks the item as deprecated, so only a missing Deprecation omits the key
		if (annotation.Deprecated is { } deprecated)
		{
			writer.WriteStartObject("deprecated");
			WriteIfPresent(writer, "date", deprecated.Date);
			WriteIfPresent(writer, "comment", deprecated.Comment);
			writer.WriteEndObject();
		}

		if (annotation.Providers.Values.Any(fields => fields.Count > 0))
		{
			writer.WriteStartObject("providers");

			foreach (var (provider, fields) in Ordered(annotation.Providers))
				WriteIfPresent(writer, provider, fields);

			writer.WriteEndObject();
		}

		WriteIfPresent(writer, "notes", annotation.Notes);
	}

	private static void WriteAttributes(Utf8JsonWriter writer, IReadOnlyDictionary<string, AttributeInfo> attributes)
	{
		if (attributes.Count == 0)
			return;

		writer.WriteStartObject("attributes");

		foreach (var (name, attribute) in Ordered(attributes))
		{
			writer.WriteStartObject(name);
			WriteIfPresent(writer, "type", attribute.BuiltInType);
			WriteIfPresent(writer, "typeName", attribute.TypeName);
			WriteIfTrue(writer, "required", attribute.Required);
			writer.WriteEndObject();
		}

		writer.WriteEndObject();
	}

	private static bool HasContent(Annotation annotation) =>
		!string.IsNullOrEmpty(annotation.Description)
		|| annotation.Values.Count > 0
		|| !string.IsNullOrEmpty(annotation.Default)
		|| annotation.Deprecated is not null
		|| annotation.Providers.Values.Any(fields => fields.Count > 0)
		|| annotation.Notes.Count > 0;

	private static void WriteIfPresent(Utf8JsonWriter writer, string propertyName, string? value)
	{
		if (!string.IsNullOrEmpty(value))
			writer.WriteString(propertyName, value);
	}

	private static void WriteIfPresent(Utf8JsonWriter writer, string propertyName, IReadOnlyList<string> values)
	{
		if (values.Count == 0)
			return;

		writer.WriteStartArray(propertyName);

		foreach (var value in values)
			writer.WriteStringValue(value);

		writer.WriteEndArray();
	}

	private static void WriteIfPresent(Utf8JsonWriter writer, string propertyName, IReadOnlyDictionary<string, string> fields)
	{
		if (fields.Count == 0)
			return;

		writer.WriteStartObject(propertyName);

		foreach (var (name, text) in Ordered(fields))
			writer.WriteString(name, text);

		writer.WriteEndObject();
	}

	private static void WriteIfTrue(Utf8JsonWriter writer, string propertyName, bool value)
	{
		if (value)
			writer.WriteBoolean(propertyName, true);
	}

	private static IOrderedEnumerable<KeyValuePair<string, T>> Ordered<T>(IReadOnlyDictionary<string, T> map) =>
		map.OrderBy(pair => pair.Key, StringComparer.Ordinal);
}
