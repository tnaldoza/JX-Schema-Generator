using System.Xml;
using System.Xml.Schema;
using JXSchemaGenerator.Build;
using JXSchemaGenerator.Model;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Tests;

internal static class SampleMaster
{
	public const string FileName = "TPG_SampleMaster.xsd";
	public const string Namespace = "urn:jxschemagenerator:fixture";

	private static readonly Lazy<SchemaLoader.LoadResult> LoadedLazy = new(Load);
	private static readonly Lazy<SchemaIndex> IndexLazy = new(() => SchemaIndex.Build(Loaded.SchemaSet));

	public static string Folder { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures");

	public static SchemaLoader.LoadResult Loaded => LoadedLazy.Value;

	public static SchemaIndex Index => IndexLazy.Value;

	public static SchemaLoader.LoadResult Load() => SchemaLoader.LoadAndCompileRoot(Folder, FileName);

	public static OperationInfo Operation(string name) => OperationDiscovery.Discover(Index, _ => { }).Single(o => o.Name == name);

	public static IReadOnlyList<ElementNode> Request(string operation, Action<string>? warn = null) =>
		new ElementTreeBuilder(Index, warn ?? (_ => { })).BuildChildren(Operation(operation).RequestType, TreeSide.Request);

	public static IReadOnlyList<ElementNode> Response(string operation, Action<string>? warn = null) =>
		new ElementTreeBuilder(Index, warn ?? (_ => { })).BuildChildren(
			Operation(operation).ResponseType ?? throw new InvalidOperationException($"Operation {operation} has no response type"),
			TreeSide.Response
		);

	public static XmlSchemaType Type(string localName)
	{
		var qualified = new XmlQualifiedName(localName, Namespace);

		if (Index.SimpleTypes.TryGetValue(qualified, out var simple))
			return simple;

		return Index.ComplexTypes[qualified];
	}
}
