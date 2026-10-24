using System.Xml;
using System.Xml.Schema;
using JXSchemaGenerator.Build;
using JXSchemaGenerator.Model;

namespace JXSchemaGenerator.Tests.Build;

public sealed class AnnotationReaderTests
{
	private static Annotation? ReadInline(string documentation)
	{
		var loaded = InlineSchema.Compile(
			$"""<xsd:simpleType name="Doc_Type"><xsd:annotation><xsd:documentation>{documentation}</xsd:documentation></xsd:annotation><xsd:restriction base="xsd:string" /></xsd:simpleType>"""
		);

		return AnnotationReader.Read((XmlSchemaType?)loaded.SchemaSet.GlobalTypes[new XmlQualifiedName("Doc_Type", InlineSchema.Namespace)]);
	}

	[Fact]
	public void ReadSplitsCanonicalValuesAndKeepsTheDefault()
	{
		var annotation = AnnotationReader.Read(SampleMaster.Type("Rstr_Type"));

		Assert.NotNull(annotation);
		Assert.Equal("Controls whether a caller may change the value", annotation.Description);
		string[] values = ["ReadOnly", "ReadWrite", "Hidden"];
		Assert.Equal(values, annotation.Values);
		Assert.Equal("ReadWrite", annotation.Default);
	}

	[Fact]
	public void ReadCollapsesWhitespaceInJxAndPlainTextDescriptions()
	{
		Assert.Equal("Amount in the account currency", AnnotationReader.Read(SampleMaster.Type("Amt_Type"))?.Description);
		Assert.Equal("Account identifier as text", AnnotationReader.Read(SampleMaster.Type("AcctId_Type"))?.Description);
	}

	[Fact]
	public void ReadTakesDeprecationAndProviderBlocks()
	{
		var annotation = AnnotationReader.Read(SampleMaster.Type("OpenDt_Type"));

		Assert.NotNull(annotation);
		Assert.Equal(new Deprecation("2099-01-01", "Use OrigDt instead"), annotation.Deprecated);
		Assert.Equal("NA", annotation.Providers["SilverLake"]["ElemSPMap"]);
		Assert.Equal("OpenDate", annotation.Providers["Episys"]["ElemSPMap"]);
		Assert.Equal("Read only", annotation.Providers["Episys"]["ElemRules"]);
	}

	[Fact]
	public void ReadFlattensNestedProviderChildrenToText() =>
		Assert.Equal("First rule Second rule", AnnotationReader.Read(SampleMaster.Type("Stamp_Type"))?.Providers["CoreDirector"]["ElemRules"]);

	[Fact]
	public void ReadKeepsOtherJxChildrenAsNotesUnderTheirCorrectedSpelling()
	{
		var annotation = AnnotationReader.Read(SampleMaster.Type("DrCrBase_Type"));

		Assert.NotNull(annotation);
		string[] notes = ["ElemCanonType"];
		Assert.Equal(notes, annotation.Notes.Keys);
		Assert.Equal("Open list with known values", annotation.Notes["ElemCanonType"]);
	}

	[Theory]
	[InlineData("ElemDft")]
	[InlineData("ElemDef")]
	[InlineData("Dft")]
	[InlineData("default")]
	[InlineData("Defalut")]
	[InlineData("Defalult")]
	public void ReadTakesTheDefaultFromEachOfItsSpellings(string field)
	{
		var annotation = ReadInline($"<Jx><{field}>Act</{field}></Jx>");

		Assert.NotNull(annotation);
		Assert.Equal("Act", annotation.Default);
		Assert.Empty(annotation.Notes);
	}

	[Fact]
	public void ReadTakesValuesFromCanonVal()
	{
		var annotation = ReadInline("<Jx><CanonVal>Act, InAct</CanonVal></Jx>");

		Assert.NotNull(annotation);
		string[] values = ["Act", "InAct"];
		Assert.Equal(values, annotation.Values);
		Assert.Empty(annotation.Notes);
	}

	[Fact]
	public void ReadTakesADeprecationInsideJx()
	{
		var annotation = ReadInline("<Jx><ElemDesc>Old flag</ElemDesc><Deprecate><Dt>2099-04-01</Dt><Cmnt>Use NewFlag</Cmnt></Deprecate></Jx>");

		Assert.NotNull(annotation);
		Assert.Equal(new Deprecation("2099-04-01", "Use NewFlag"), annotation.Deprecated);
		Assert.Empty(annotation.Notes);
	}

	[Fact]
	public void ReadSpellsKnownProviderNamesOneWay()
	{
		var annotation = ReadInline("<SILVERLAKE><ElemSPMap>NA</ElemSPMap></SILVERLAKE><episys>Mapped by the host</episys>");

		Assert.NotNull(annotation);
		string[] providers = ["SilverLake"];
		Assert.Equal(providers, annotation.Providers.Keys);
		string[] notes = ["Episys"];
		Assert.Equal(notes, annotation.Notes.Keys);
	}

	[Fact]
	public void ReadKeepsEscapedAndNonAsciiCharacters() =>
		Assert.Equal("Fees & \"charges\" for caf\u00e9 accounts", AnnotationReader.Read(SampleMaster.Type("Misc_CType"))?.Description);

	[Fact]
	public void ReadReturnsNullWithoutDocumentation() => Assert.Null(AnnotationReader.Read(SampleMaster.Type("Cnt_Type")));

	[Fact]
	public void ReadTakesTheOwnTextOfAJxElementAsDescription() =>
		Assert.Equal("Remark a branch adds for audit", AnnotationReader.Read(SampleMaster.Type("BrRmk_Type"))?.Description);

	[Fact]
	public void ReadKeepsJxTextAndDescriptionFieldsInDocumentOrder() =>
		Assert.Equal(
			"First second third fourth fifth",
			ReadInline("First <Jx>second <ElemDesc>third</ElemDesc> fourth</Jx><ElemDesc>fifth</ElemDesc>")?.Description
		);

	[Fact]
	public void ReadMatchesTheJxWrapperWithoutRegardToCase()
	{
		var annotation = AnnotationReader.Read(SampleMaster.Type("HostFlag_Type"));

		Assert.NotNull(annotation);
		Assert.Equal("Flag the host sets", annotation.Description);
		string[] values = ["Y", "N"];
		Assert.Equal(values, annotation.Values);
		Assert.Empty(annotation.Providers);
	}

	[Fact]
	public void ReadMatchesFieldNamesWithoutRegardToCaseAndKeepsTheirSpelling()
	{
		var annotation = ReadInline(
			"<JX><elemdesc>Mixed case</elemdesc><canonicalVAL>A,B</canonicalVAL><ELEMDEFAULT>A</ELEMDEFAULT><SrcNote>Kept as written</SrcNote></JX><deprecate><DT>2099-02-01</DT><cmnt>Gone soon</cmnt></deprecate>"
		);

		Assert.NotNull(annotation);
		Assert.Equal("Mixed case", annotation.Description);
		string[] values = ["A", "B"];
		Assert.Equal(values, annotation.Values);
		Assert.Equal("A", annotation.Default);
		Assert.Equal(new Deprecation("2099-02-01", "Gone soon"), annotation.Deprecated);
		string[] notes = ["SrcNote"];
		Assert.Equal(notes, annotation.Notes.Keys);
		Assert.Empty(annotation.Providers);
	}

	[Fact]
	public void ReadAppliesTheFieldRulesToTopLevelFieldsOutsideJx()
	{
		var annotation = AnnotationReader.Read(SampleMaster.Type("SvcTier_Type"));

		Assert.NotNull(annotation);
		Assert.Equal("Service tier of the account", annotation.Description);
		string[] values = ["Gold", "Silver", "Bronze"];
		Assert.Equal(values, annotation.Values);
		Assert.Equal("Silver", annotation.Default);
	}

	[Fact]
	public void ReadKeepsChildlessTopLevelElementsAsNotes()
	{
		var annotation = AnnotationReader.Read(SampleMaster.Type("SvcTier_Type"));

		Assert.NotNull(annotation);
		string[] notes = ["Cmnt", "Synergy"];
		Assert.Equal(notes, annotation.Notes.Keys);
		Assert.Equal("Set when the account opens", annotation.Notes["Cmnt"]);
		Assert.Equal("Read from the pricing table", annotation.Notes["Synergy"]);
	}

	[Fact]
	public void ReadSkipsEmptyProviderFieldsAndProviderBlocksWithoutText()
	{
		var annotation = AnnotationReader.Read(SampleMaster.Type("SvcTier_Type"));

		Assert.NotNull(annotation);
		string[] providers = ["Episys"];
		Assert.Equal(providers, annotation.Providers.Keys);
		string[] fields = ["ElemSPMap"];
		Assert.Equal(fields, annotation.Providers["Episys"].Keys);
	}

	[Fact]
	public void ReadReturnsNullWhenEveryFieldIsEmpty() =>
		Assert.Null(ReadInline("<Jx><ElemDesc> </ElemDesc><ElemDefault /><Cmnt /></Jx><ElemDesc /><Episys><ElemRules>  </ElemRules></Episys><Note />"));
}
