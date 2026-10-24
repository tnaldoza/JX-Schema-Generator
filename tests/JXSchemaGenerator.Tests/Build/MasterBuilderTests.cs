using JXSchemaGenerator.Build;
using JXSchemaGenerator.Model;

namespace JXSchemaGenerator.Tests.Build;

public sealed class MasterBuilderTests
{
	private static readonly Lazy<MasterBuildResult> ResultValue = new(() => MasterBuilder.Build(SampleMaster.Loaded));

	private static MasterBuildResult Result => ResultValue.Value;

	private static OperationModel Operation(string name) => Result.Document.Operations.Single(o => o.Name == name);

	private static IEnumerable<ElementNode> Flatten(ElementNode node) => node.Children.SelectMany(Flatten).Prepend(node);

	[Fact]
	public void BuildDescribesTheMasterAndEveryOperation()
	{
		Assert.Equal("TPG_SampleMaster.xsd", Result.Document.Master);
		Assert.Equal("2099.0.01", Result.Document.TpgVersion);
		Assert.Equal(9, Result.Document.Operations.Count);
		Assert.Equal("AppInfoAddRq", Operation("AppInfoAdd").RequestElement);
		Assert.Null(Operation("NoRsInq").Response);
		Assert.Null(Operation("NoRsInq").ResponseElement);
	}

	[Fact]
	public void BuildAttachesHelpersByKind()
	{
		Assert.Equal(3, Operation("AcctInq").ExtendedElements.Count);
		Assert.Empty(Operation("AcctInq").Records);
		Assert.Equal(2, Operation("AcctSrch").Records.Count);
		Assert.Equal(3, Operation("AcctMod").ModifyContainers.Count);
		Assert.Empty(Operation("AcctAdd").ModifyContainers);
	}

	[Fact]
	public void BuildTakesTheOperationAnnotationFromTheElementOrElseItsType()
	{
		Assert.Equal("Opens an account", Operation("AcctAdd").Annotation?.Description);
		Assert.Equal("2099-06-30", Operation("Ping").Annotation?.Deprecated?.Date);
	}

	[Fact]
	public void EveryTypeNameResolvesInTheTypeTable()
	{
		var types = Result.Document.Types;
		var referenced = Result
			.Document.Operations.SelectMany(o => o.Request.Concat(o.Response ?? []))
			.SelectMany(Flatten)
			.Select(n => n.TypeName?.Name)
			.Concat(types.Values.Select(t => t.BaseTypeName))
			.Concat(types.Values.SelectMany(t => t.Attributes.Values.Select(a => a.TypeName)))
			.OfType<string>();

		Assert.All(referenced, name => Assert.Contains(name, types.Keys));
	}

	[Fact]
	public void BuildReportsEachWarningOnce()
	{
		Assert.Equal(Result.Warnings.Distinct(StringComparer.Ordinal), Result.Warnings);
		Assert.Single(Result.Warnings, w => w.Contains("QuesArray_AType", StringComparison.Ordinal));
		Assert.Contains(Result.Warnings, w => w.Contains("CollatAddr", StringComparison.Ordinal));
		Assert.Contains(Result.Warnings, w => w.Contains("NoRsInq", StringComparison.Ordinal));
	}

	[Fact]
	public void BuildTakesTheOperationAnnotationFromTheTypeWhenTheElementDocumentationIsEmpty()
	{
		var loaded = InlineSchema.Compile(
			"""
			<xsd:element name="SoloInq" type="SoloInqRq_MType"><xsd:annotation><xsd:documentation><Episys><ElemRules> </ElemRules></Episys></xsd:documentation></xsd:annotation></xsd:element>
			<xsd:complexType name="SoloInqRq_MType">
				<xsd:annotation><xsd:documentation><Jx><ElemDesc>Reads one record</ElemDesc></Jx></xsd:documentation></xsd:annotation>
				<xsd:sequence><xsd:element name="Note" type="xsd:string" minOccurs="0" /></xsd:sequence>
			</xsd:complexType>
			<xsd:element name="SoloInqResponse" type="SoloInqRs_MType" />
			<xsd:complexType name="SoloInqRs_MType"><xsd:sequence><xsd:element name="Note" type="xsd:string" minOccurs="0" /></xsd:sequence></xsd:complexType>
			"""
		);

		Assert.Equal("Reads one record", Assert.Single(MasterBuilder.Build(loaded).Document.Operations).Annotation?.Description);
	}

	[Fact]
	public void BuildWarnsAboutASearchWithoutARepeatingRecord()
	{
		var loaded = InlineSchema.Compile(
			"""
			<xsd:element name="SoloSrch" type="SoloSrchRq_MType" />
			<xsd:complexType name="SoloSrchRq_MType"><xsd:sequence><xsd:element name="Note" type="xsd:string" minOccurs="0" /></xsd:sequence></xsd:complexType>
			<xsd:element name="SoloSrchResponse" type="SoloSrchRs_MType" />
			<xsd:complexType name="SoloSrchRs_MType"><xsd:sequence><xsd:element name="Note" type="xsd:string" minOccurs="0" /></xsd:sequence></xsd:complexType>
			"""
		);

		var result = MasterBuilder.Build(loaded);

		Assert.Empty(Assert.Single(result.Document.Operations).Records);
		Assert.Contains("Search SoloSrch has no repeating record in its response", result.Warnings);
	}

	[Fact]
	public void BuildDoesNotWarnAboutSearchesWithRecords() =>
		Assert.DoesNotContain(Result.Warnings, w => w.Contains("repeating record", StringComparison.Ordinal));

	[Fact]
	public void BuildFailsForAMasterWithoutOperations() =>
		Assert.Throws<InvalidOperationException>(() => MasterBuilder.Build(InlineSchema.Compile("""<xsd:element name="Lonely" type="xsd:string" />""")));
}
