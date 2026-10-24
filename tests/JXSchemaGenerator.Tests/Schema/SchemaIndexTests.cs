using System.Xml;
using System.Xml.Schema;
using JXSchemaGenerator.Build;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Tests.Schema;

public sealed class SchemaIndexTests
{
	private const string IncludeMaster = "TPG_IncludeMaster.xsd";
	private const string IncludeNamespace = "urn:jxschemagenerator:include";

	private static readonly Lazy<SchemaLoader.LoadResult> LoadedValue = new(() => SchemaLoader.LoadAndCompileRoot(SampleMaster.Folder, IncludeMaster));

	private static SchemaIndex Index => SchemaIndex.Build(LoadedValue.Value.SchemaSet);

	[Fact]
	public void BuildIndexesTheDeclarationsOfIncludedDocuments()
	{
		var index = Index;

		Assert.Contains(new XmlQualifiedName("MemoAdd", IncludeNamespace), index.GlobalElements.Keys);
		Assert.Contains(new XmlQualifiedName("MemoAddRq_MType", IncludeNamespace), index.ComplexTypes.Keys);
		Assert.Contains(new XmlQualifiedName("MemoStat_Type", IncludeNamespace), index.SimpleTypes.Keys);
		Assert.Contains(new XmlQualifiedName("Memo_Type", IncludeNamespace), index.ComplexTypes.Keys);
	}

	[Fact]
	public void BuildKeepsSimpleAndComplexTypesApartAndLeavesBuiltInTypesOut()
	{
		var index = Index;

		Assert.DoesNotContain(index.SimpleTypes.Keys.Concat(index.ComplexTypes.Keys), name => name.Namespace == XmlSchema.Namespace);
		Assert.DoesNotContain(new XmlQualifiedName("MemoStat_Type", IncludeNamespace), index.ComplexTypes.Keys);
		Assert.DoesNotContain(new XmlQualifiedName("Memo_Type", IncludeNamespace), index.SimpleTypes.Keys);
	}

	[Fact]
	public void MasterBuilderFindsTheOperationsOfIncludedDocuments()
	{
		var document = MasterBuilder.Build(LoadedValue.Value).Document;

		string[] operations = ["MemoAdd", "MemoInq"];
		Assert.Equal(operations, document.Operations.Select(o => o.Name));
		Assert.Contains("MemoStat_Type", document.Types.Keys);
	}
}
