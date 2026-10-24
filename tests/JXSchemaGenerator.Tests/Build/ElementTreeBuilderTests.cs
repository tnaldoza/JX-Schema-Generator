using JXSchemaGenerator.Build;
using JXSchemaGenerator.Model;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Tests.Build;

public sealed class ElementTreeBuilderTests
{
	[Fact]
	public void BuildChildrenDropsHeadersVersionMarkersAndExtensionPoints()
	{
		string[] expected = ["InAcctId", "IncXtendElemArray", "InqDt"];

		Assert.Equal(expected, SampleMaster.Request("AcctInq").Select(n => n.Name));
	}

	[Fact]
	public void RequestTreesMarkRequiredFromMinOccursAndEnclosingSequences()
	{
		var request = SampleMaster.Request("AcctInq");

		Assert.True(request.Find("InAcctId").Required);
		Assert.True(request.Find("InAcctId/AcctId").Required);
		Assert.False(request.Find("IncXtendElemArray").Required);
		Assert.False(request.Find("InqDt").Required);
	}

	[Fact]
	public void RequiredIsRelativeToTheParentElement()
	{
		var request = SampleMaster.Request("AcctAdd");

		Assert.False(request.Find("EmailArray").Required);
		Assert.True(request.Find("EmailArray/EmailInfo").Required);
	}

	[Fact]
	public void ResponseTreesNeverMarkRequired()
	{
		var response = SampleMaster.Response("AcctInq");

		Assert.False(response.Find("AccountId").Required);
		Assert.False(response.Find("AccountId/AcctId").Required);
		Assert.False(response.Find("DepAcctInqRec").Required);
	}

	[Fact]
	public void RepeatsMarksTheRepeatingElementNotItsWrapper()
	{
		var request = SampleMaster.Request("AcctAdd");
		var response = SampleMaster.Response("AcctInq");

		Assert.False(request.Find("EmailArray").Repeats);
		Assert.True(request.Find("EmailArray/EmailInfo").Repeats);
		Assert.False(response.Find("x_MailAddr").Repeats);
		Assert.True(response.Find("x_MailAddr/MailAddr").Repeats);
	}

	[Fact]
	public void SimpleElementsCarryBuiltInTypeTypeNameAndSortedAttributes()
	{
		var node = SampleMaster.Request("AcctInq").Find("InAcctId/AcctType");

		Assert.Equal("string", node.BuiltInType);
		Assert.Equal("AcctType_Type", node.TypeName?.Name);
		string[] attributes = ["JHANull", "Rstr"];
		Assert.Equal(attributes, node.Attributes);
		Assert.Empty(node.Children);
	}

	[Theory]
	[InlineData("x_Misc/Memo", "string", null)]
	[InlineData("x_Misc/Cnt", "int", "Cnt_Type")]
	[InlineData("x_Misc/Num", "long", "Num_Type")]
	[InlineData("x_Misc/Tm", "time", "Tm_Type")]
	[InlineData("x_Misc/Stamp", "dateTime", "Stamp_Type")]
	[InlineData("x_Misc/Img", "base64Binary", "Img_Type")]
	[InlineData("DepAcctInqRec/x_DepInfoRec/Bal", "decimal", "Amt_Type")]
	[InlineData("DepAcctInqRec/x_DepInfoRec/OpenDt", "date", "OpenDt_Type")]
	public void BuiltInTypeFollowsTheBaseChain(string path, string builtIn, string? typeName)
	{
		var node = SampleMaster.Response("AcctInq").Find(path);

		Assert.Equal(builtIn, node.BuiltInType);
		Assert.Equal(typeName, node.TypeName?.Name);
	}

	[Fact]
	public void ComplexElementsHaveATypeNameButNoBuiltInType()
	{
		var node = SampleMaster.Request("AcctInq").Find("InAcctId");

		Assert.Null(node.BuiltInType);
		Assert.Equal("AccountId_CType", node.TypeName?.Name);
	}

	[Fact]
	public void GroupRefsElementRefsAndExtensionsExpandInPlace()
	{
		var request = SampleMaster.Request("AcctAdd");
		var custom = request.Find("Custom");

		string[] postal = ["StreetAddr", "City", "PostalCode"];
		Assert.Equal(postal, request.Find("PostalAddr").Children.Select(n => n.Name));
		string[] extended = ["AcctId", "Bal"];
		Assert.Equal(extended, request.Find("ExtRec").Children.Select(n => n.Name));
		Assert.Equal("Custom_CType", custom.TypeName?.Name);
		Assert.Empty(custom.Children);
		Assert.Equal("Institution-specific extension elements", custom.Annotation?.Description);
	}

	[Fact]
	public void RecursiveTypesStopAndWarn()
	{
		var warnings = new List<string>();

		var inner = SampleMaster.Request("AcctAdd", warnings.Add).Find("QuesArray/QuesRec/QuesArray");

		Assert.True(inner.Recursive);
		Assert.Empty(inner.Children);
		Assert.Contains(warnings, w => w.Contains("QuesArray_AType", StringComparison.Ordinal));
	}

	[Fact]
	public void DuplicateNamesKeepTheFirstDeclarationAndWarn()
	{
		var warnings = new List<string>();

		var collateral = SampleMaster.Response("AcctAdd", warnings.Add).Find("CollatInfo");

		string[] expected = ["CollatAddr", "CollatDesc"];
		Assert.Equal(expected, collateral.Children.Select(n => n.Name));
		Assert.Contains(warnings, w => w.Contains("CollatInfo_CType", StringComparison.Ordinal) && w.Contains("CollatAddr", StringComparison.Ordinal));
	}

	[Fact]
	public void NestedOperationRequestsLoseTheirHeaders()
	{
		var nested = SampleMaster.Request("AcctAddValidate").Find("AcctAdd");

		Assert.Equal("AcctAddRq_MType", nested.TypeName?.Name);
		Assert.True(nested.Required);
		Assert.DoesNotContain(nested.Children, n => n.Name == "MsgRqHdr");
	}

	[Fact]
	public void ElementAnnotationsStayOnTheNode() =>
		Assert.Equal("Date to report balances as of", SampleMaster.Request("AcctInq").Find("InqDt").Annotation?.Description);

	[Fact]
	public void ElementAnnotationsFallBackToTheReferencedElementWhenTheirOwnIsEmpty()
	{
		var index = SchemaIndex.Build(
			InlineSchema
				.Compile(
					"""
					<xsd:element name="Shared" type="xsd:string"><xsd:annotation><xsd:documentation><Jx><ElemDesc>Shared remark</ElemDesc></Jx></xsd:documentation></xsd:annotation></xsd:element>
					<xsd:complexType name="RefRq_MType">
						<xsd:sequence>
							<xsd:element ref="Shared" minOccurs="0"><xsd:annotation><xsd:documentation><Episys><ElemRules /></Episys></xsd:documentation></xsd:annotation></xsd:element>
						</xsd:sequence>
					</xsd:complexType>
					"""
				)
				.SchemaSet
		);
		var type = index.ComplexTypes.Values.Single(t => t.Name == "RefRq_MType");

		var request = new ElementTreeBuilder(index, _ => { }).BuildChildren(type, TreeSide.Request);

		Assert.Equal("Shared remark", request.Find("Shared").Annotation?.Description);
	}

	[Theory]
	[InlineData(
		"""<xsd:sequence><xsd:choice><xsd:element name="A" type="xsd:string" /><xsd:element name="B" type="xsd:string" /></xsd:choice></xsd:sequence>"""
	)]
	[InlineData("""<xsd:all><xsd:element name="A" type="xsd:string" /></xsd:all>""")]
	[InlineData("""<xsd:sequence maxOccurs="2"><xsd:element name="A" type="xsd:string" /></xsd:sequence>""")]
	public void BuildChildrenRejectsContentModelsOutsideTheSupportedSubset(string content)
	{
		var index = SchemaIndex.Build(InlineSchema.Compile($"""<xsd:complexType name="BadRq_MType">{content}</xsd:complexType>""").SchemaSet);
		var type = index.ComplexTypes.Values.Single(t => t.Name == "BadRq_MType");

		var error = Assert.Throws<NotSupportedException>(() => new ElementTreeBuilder(index, _ => { }).BuildChildren(type, TreeSide.Request));

		Assert.Contains("BadRq_MType", error.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildChildrenRejectsAnonymousTypesWithAttributes()
	{
		var index = SchemaIndex.Build(
			InlineSchema
				.Compile(
					"""
					<xsd:complexType name="OddRq_MType">
						<xsd:sequence>
							<xsd:element name="Flagged">
								<xsd:complexType><xsd:simpleContent><xsd:extension base="xsd:string"><xsd:attribute name="Flag" type="xsd:string" /></xsd:extension></xsd:simpleContent></xsd:complexType>
							</xsd:element>
						</xsd:sequence>
					</xsd:complexType>
					"""
				)
				.SchemaSet
		);
		var type = index.ComplexTypes.Values.Single(t => t.Name == "OddRq_MType");

		var error = Assert.Throws<NotSupportedException>(() => new ElementTreeBuilder(index, _ => { }).BuildChildren(type, TreeSide.Request));

		Assert.Contains("Flagged", error.Message, StringComparison.Ordinal);
	}
}
