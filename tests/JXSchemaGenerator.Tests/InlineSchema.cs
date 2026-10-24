using System.Xml;
using System.Xml.Schema;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Tests;

internal static class InlineSchema
{
	public const string Namespace = "urn:jxschemagenerator:inline";
	public const string RootFile = "TPG_InlineMaster.xsd";

	public static SchemaLoader.LoadResult Compile(string body, string? version = "1.0")
	{
		var versionAttribute = version is null ? "" : $" version=\"{version}\"";
		var text =
			$"""<xsd:schema xmlns:xsd="{XmlSchema.Namespace}" xmlns="{Namespace}" targetNamespace="{Namespace}" elementFormDefault="qualified"{versionAttribute}>{body}</xsd:schema>""";

		using var source = new StringReader(text);
		using var reader = XmlReader.Create(source);
		var schema = XmlSchema.Read(reader, null) ?? throw new InvalidOperationException($"XmlSchema.Read returned null for {RootFile}");
		var set = new XmlSchemaSet { XmlResolver = null };

		set.Add(schema);
		set.Compile();

		return new SchemaLoader.LoadResult(set, schema, RootFile);
	}
}
