using JXSchemaGenerator.Build;
using JXSchemaGenerator.Model;

namespace JXSchemaGenerator.Tests.Build;

public sealed class TypeTableBuilderTests
{
	private static readonly Lazy<IReadOnlyDictionary<string, TypeEntry>> TableValue = new(BuildTable);

	private static IReadOnlyDictionary<string, TypeEntry> Table => TableValue.Value;

	private static IReadOnlyDictionary<string, TypeEntry> BuildTable()
	{
		var builder = new ElementTreeBuilder(SampleMaster.Index, _ => { });
		var roots = OperationDiscovery
			.Discover(SampleMaster.Index, _ => { })
			.SelectMany(o =>
				builder
					.BuildChildren(o.RequestType, TreeSide.Request)
					.Concat(o.ResponseType is null ? [] : builder.BuildChildren(o.ResponseType, TreeSide.Response))
			);
		return TypeTableBuilder.Build(roots, SampleMaster.Index);
	}

	[Fact]
	public void TableHoldsReferencedTypesAttributeTypesAndBaseChains()
	{
		Assert.Contains("AcctType_Type", Table.Keys);
		Assert.Contains("AcctTypeBase_Type", Table.Keys);
		Assert.Contains("ClosedEnum_Type", Table.Keys);
		Assert.Contains("JHANull_Type", Table.Keys);
		Assert.Contains("BaseRec_CType", Table.Keys);
		Assert.Contains("AcctAddRq_MType", Table.Keys);
		Assert.DoesNotContain("AcctInqRq_MType", Table.Keys);
		Assert.DoesNotContain("MsgRqHdr_CType", Table.Keys);
		Assert.DoesNotContain("Ver_1_CType", Table.Keys);
	}

	[Fact]
	public void TableKeysAreOrdinalSorted() => Assert.Equal(Table.Keys.Order(StringComparer.Ordinal), Table.Keys);

	[Fact]
	public void ClosedEnumInheritsValuesFromItsBase()
	{
		var entry = Table["AcctType_Type"];

		Assert.Equal("string", entry.BuiltInBase);
		Assert.Equal("AcctTypeBase_Type", entry.BaseTypeName);
		Assert.Equal(EnumKind.Closed, entry.Enumeration);
		string[] values = ["D", "L", "S"];
		Assert.Equal(values, entry.Values);
		Assert.Equal("Rstr_Type", entry.Attributes["Rstr"].TypeName);
	}

	[Fact]
	public void OpenEnumKeepsItsListAndNotes()
	{
		string[] values = ["Dr", "Cr"];

		Assert.Equal(EnumKind.Open, Table["DrCr_Type"].Enumeration);
		Assert.Equal(values, Table["DrCr_Type"].Values);
		Assert.Equal("Open list with known values", Table["DrCrBase_Type"].Annotation?.Notes["ElemCanocType"]);
	}

	[Fact]
	public void PlainStringWithAListIsOpen()
	{
		string[] values = ["AAA", "BBB"];

		Assert.Equal(EnumKind.Open, Table["RtCode_Type"].Enumeration);
		Assert.Equal(values, Table["RtCode_Type"].Values);
	}

	[Fact]
	public void ClosedEnumWithoutAListHasNoValues()
	{
		Assert.Equal(EnumKind.Closed, Table["Stat_Type"].Enumeration);
		Assert.Empty(Table["Stat_Type"].Values);
	}

	[Fact]
	public void ClosedEnumTakesValuesFromATopLevelList()
	{
		var entry = Table["SvcTier_Type"];

		Assert.Equal(EnumKind.Closed, entry.Enumeration);
		string[] values = ["Gold", "Silver", "Bronze"];
		Assert.Equal(values, entry.Values);
		Assert.Equal("Service tier of the account", entry.Annotation?.Description);
		Assert.Equal("Silver", entry.Annotation?.Default);
	}

	[Fact]
	public void ClosedEnumTakesValuesFromALowercaseJxWrapper()
	{
		var entry = Table["HostFlag_Type"];

		Assert.Equal(EnumKind.Closed, entry.Enumeration);
		string[] values = ["Y", "N"];
		Assert.Equal(values, entry.Values);
		Assert.NotNull(entry.Annotation);
		Assert.Equal("Flag the host sets", entry.Annotation.Description);
		Assert.Empty(entry.Annotation.Providers);
	}

	[Fact]
	public void TextOnlyJxBecomesTheTypeDescription() => Assert.Equal("Remark a branch adds for audit", Table["BrRmk_Type"].Annotation?.Description);

	[Fact]
	public void TypesWithoutAListHaveNoEnumeration()
	{
		Assert.Equal("decimal", Table["Amt_Type"].BuiltInBase);
		Assert.Null(Table["Amt_Type"].Enumeration);
		Assert.Null(Table["Addr_CType"].BuiltInBase);
		Assert.Null(Table["Addr_CType"].Enumeration);
	}

	[Fact]
	public void ExtensionsRecordTheirBaseType()
	{
		Assert.Equal("BaseRec_CType", Table["ExtRec_CType"].BaseTypeName);
		Assert.Null(Table["ExtRec_CType"].BuiltInBase);
	}

	[Fact]
	public void AttributesCarryTheirOwnTypes()
	{
		var attributes = Table["Name_Type"].Attributes;

		string[] names = ["JHANull", "MaskVal", "SrchType"];
		Assert.Equal(names, attributes.Keys);
		Assert.Equal(new AttributeInfo("string", null, false), attributes["MaskVal"]);
		Assert.Equal("SrchType_Type", attributes["SrchType"].TypeName);
		string[] searchValues = ["Exact", "StartsWith"];
		Assert.Equal(searchValues, Table["SrchType_Type"].Values);
	}

	[Fact]
	public void TypeAnnotationsStayOnTheirEntry() => Assert.Equal("2099-01-01", Table["OpenDt_Type"].Annotation?.Deprecated?.Date);
}
