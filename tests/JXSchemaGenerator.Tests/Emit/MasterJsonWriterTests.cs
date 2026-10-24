using System.Text.Json;
using Json.Schema;
using JXSchemaGenerator.Build;
using JXSchemaGenerator.Emit;

namespace JXSchemaGenerator.Tests.Emit;

public sealed class MasterJsonWriterTests
{
	private const string SmallestOperation = """{"kind":"inquiry","requestElement":"AInq","request":{"A":{}},"response":null}""";

	private static readonly Lazy<JsonSchema> SchemaValue = new(() => JsonSchema.FromText(MasterJsonWriter.ReadSchema()));

	private static string SampleJson() => MasterJsonWriter.ToJson(MasterBuilder.Build(SampleMaster.Loaded).Document);

	private static bool IsValidDocument(string operation, string type)
	{
		using var document = JsonDocument.Parse(
			$$$"""{"$schema":"./jxchange-schemas.schema.json","formatVersion":2,"tpgVersion":null,"master":"M.xsd","operations":{"AInq": {{{operation}}} },"fault":null,"types":{"T": {{{type}}} }}"""
		);

		return SchemaValue.Value.Evaluate(document.RootElement).IsValid;
	}

	private static JsonElement RequestNode(JsonDocument document, string operation, params string[] path) =>
		path.Aggregate(document.RootElement.GetProperty("operations").GetProperty(operation).GetProperty("request"), (node, name) => node.GetProperty(name));

	[Fact]
	public void SampleMasterMatchesTheGoldenFile()
	{
		var json = SampleJson();
		var goldenPath = Path.Combine(SampleMaster.Folder, "sample.golden.json");
		var golden = File.Exists(goldenPath) ? File.ReadAllText(goldenPath) : "";

		if (json != golden)
		{
			var actualPath = Path.Combine(SampleMaster.Folder, "sample.actual.json");
			File.WriteAllText(actualPath, json);
			Assert.Fail(
				$"Output differs from sample.golden.json; review {actualPath} and copy it over tests/JXSchemaGenerator.Tests/Fixtures/sample.golden.json when the change is intended"
			);
		}
	}

	[Fact]
	public void OutputIsIdenticalAcrossLoadsAndUsesLineFeeds()
	{
		var first = MasterJsonWriter.ToJson(MasterBuilder.Build(SampleMaster.Load()).Document);
		var second = MasterJsonWriter.ToJson(MasterBuilder.Build(SampleMaster.Load()).Document);

		Assert.Equal(first, second);
		Assert.DoesNotContain('\r', first);
		Assert.EndsWith("}\n", first, StringComparison.Ordinal);
	}

	[Fact]
	public void OutputValidatesAgainstTheShippedSchema()
	{
		var schema = JsonSchema.FromText(MasterJsonWriter.ReadSchema());
		using var document = JsonDocument.Parse(SampleJson());

		var result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

		var errors = (result.Details ?? []).Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation} {e.Key}: {e.Value}"));
		Assert.True(result.IsValid, string.Join('\n', errors));
	}

	[Fact]
	public void SchemaAcceptsTheSmallestDocument() => Assert.True(IsValidDocument(SmallestOperation, "{}"));

	[Theory]
	[InlineData("""{"kind":"query","requestElement":"AInq","request":{},"response":null}""", "{}")]
	[InlineData("""{"kind":"inquiry","requestElement":"AInq","request":{}}""", "{}")]
	[InlineData("""{"kind":"inquiry","requestElement":"AInq","request":{"A":{"$required":false}},"response":null}""", "{}")]
	[InlineData("""{"kind":"inquiry","requestElement":"AInq","request":{"A":{"$unknown":true}},"response":null}""", "{}")]
	[InlineData("""{"kind":"inquiry","requestElement":"AInq","request":{"$type":"string"},"response":null}""", "{}")]
	[InlineData("""{"kind":"inquiry","requestElement":"AInq","annotation":{},"request":{},"response":null}""", "{}")]
	[InlineData("""{"kind":"inquiry","requestElement":"AInq","annotation":{"summary":"x"},"request":{},"response":null}""", "{}")]
	[InlineData("""{"kind":"modify","requestElement":"AInq","modify":{"containers":[{"name":"X"}]},"request":{},"response":null}""", "{}")]
	[InlineData("""{"kind":"inquiry","requestElement":"AInq","request":{"A":{"$array":true}},"response":null}""", "{}")]
	[InlineData("""{"kind":"inquiry","requestElement":"AInq","request":{"A":{"$maxOccurs":null}},"response":null}""", "{}")]
	[InlineData("""{"kind":"inquiry","requestElement":"AInq","request":{"A":{"$array":true,"$maxOccurs":1}},"response":null}""", "{}")]
	[InlineData(SmallestOperation, """{"enum":"partial"}""")]
	[InlineData(SmallestOperation, """{"base":"string","label":"x"}""")]
	[InlineData(SmallestOperation, """{"providers":{"SilverLake":{}}}""")]
	[InlineData(SmallestOperation, """{"attributes":{"A":{"type":"string","use":"optional"}}}""")]
	public void SchemaRejectsKeysAndValuesOutsideTheFormat(string operation, string type) => Assert.False(IsValidDocument(operation, type));

	[Fact]
	public void SchemaAcceptsAnArrayWithALimit() =>
		Assert.True(IsValidDocument("""{"kind":"inquiry","requestElement":"AInq","request":{"A":{"$array":true,"$maxOccurs":11}},"response":null}""", "{}"));

	[Fact]
	public void SchemaRequiresTheFaultKey()
	{
		using var document = JsonDocument.Parse(
			$$$"""{"$schema":"./jxchange-schemas.schema.json","formatVersion":2,"tpgVersion":null,"master":"M.xsd","operations":{"AInq": {{{SmallestOperation}}} },"types":{}}"""
		);

		Assert.False(SchemaValue.Value.Evaluate(document.RootElement).IsValid);
	}

	[Fact]
	public void DocumentWritesTheFaultBetweenOperationsAndTypes()
	{
		using var document = JsonDocument.Parse(SampleJson());

		string[] keys = ["$schema", "formatVersion", "tpgVersion", "master", "operations", "fault", "types"];
		Assert.Equal(keys, document.RootElement.EnumerateObject().Select(p => p.Name));
		string[] fault = ["FaultHdr", "FaultRecInfoArray"];
		Assert.Equal(fault, document.RootElement.GetProperty("fault").EnumerateObject().Select(p => p.Name));
	}

	[Fact]
	public void BoundedArraysWriteTheirMaxOccurs()
	{
		using var document = JsonDocument.Parse(SampleJson());

		Assert.Equal(3, RequestNode(document, "CustMod", "CustDetail", "NickName").GetProperty("$maxOccurs").GetInt32());
	}

	[Fact]
	public void UnboundedArraysWriteANullMaxOccurs()
	{
		using var document = JsonDocument.Parse(SampleJson());

		Assert.Equal(JsonValueKind.Null, RequestNode(document, "AcctAdd", "EmailArray", "EmailInfo").GetProperty("$maxOccurs").ValueKind);
	}

	[Fact]
	public void NonRepeatingNodesWriteNoMaxOccurs()
	{
		using var document = JsonDocument.Parse(SampleJson());

		Assert.False(RequestNode(document, "AcctAdd", "EmailArray").TryGetProperty("$maxOccurs", out _));
	}

	[Fact]
	public void NodesWriteMetadataBeforeChildElements()
	{
		using var document = JsonDocument.Parse(SampleJson());
		var node = document.RootElement.GetProperty("operations").GetProperty("AcctInq").GetProperty("request").GetProperty("InAcctId");

		string[] expected = ["$typeName", "$required", "AcctId", "AcctType"];
		Assert.Equal(expected, node.EnumerateObject().Select(p => p.Name));
	}

	[Fact]
	public void DescriptionsKeepSpecialCharactersAndWriteNonAsciiRaw()
	{
		var json = SampleJson();
		using var document = JsonDocument.Parse(json);

		Assert.Equal(
			"Fees & \"charges\" for caf\u00e9 accounts",
			document.RootElement.GetProperty("types").GetProperty("Misc_CType").GetProperty("description").GetString()
		);
		Assert.Contains("caf\u00e9", json, StringComparison.Ordinal);
	}

	[Fact]
	public void TpgVersionIsNullWhenTheMasterHasNoVersion()
	{
		var loaded = InlineSchema.Compile(
			"""
			<xsd:element name="SoloInq" type="SoloInqRq_MType" />
			<xsd:complexType name="SoloInqRq_MType"><xsd:sequence><xsd:element name="Note" type="xsd:string" minOccurs="0" /></xsd:sequence></xsd:complexType>
			<xsd:element name="SoloInqResponse" type="SoloInqRs_MType" />
			<xsd:complexType name="SoloInqRs_MType"><xsd:sequence><xsd:element name="Note" type="xsd:string" minOccurs="0" /></xsd:sequence></xsd:complexType>
			""",
			version: null
		);

		var document = MasterBuilder.Build(loaded).Document;

		Assert.Null(document.TpgVersion);
		Assert.Contains("\"tpgVersion\": null", MasterJsonWriter.ToJson(document), StringComparison.Ordinal);
	}

	[Fact]
	public void WriteSchemaCopiesTheEmbeddedSchemaIntoTheFolder()
	{
		var folder = Directory.CreateTempSubdirectory("jxschema-").FullName;

		try
		{
			MasterJsonWriter.WriteSchema(folder);

			Assert.Equal(MasterJsonWriter.ReadSchema(), File.ReadAllText(Path.Combine(folder, MasterJsonWriter.SchemaFileName)));
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}
}
