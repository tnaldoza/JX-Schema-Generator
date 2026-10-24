using System.Xml;
using System.Xml.Schema;
using JXSchemaGenerator.Model;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Build;

/// <summary>
/// Builds one <see cref="MasterDocument"/> from one compiled Master
/// </summary>
public static class MasterBuilder
{
	private const string FaultElement = "HdrFault";

	/// <summary>
	/// Discovers the operations, builds their trees and helpers and the fault tree, and collects the type table
	/// </summary>
	/// <param name="loaded">The compiled Master</param>
	/// <returns>The document and every distinct warning, in the order first raised</returns>
	/// <exception cref="InvalidOperationException">The Master defines no operations, or the type table cannot be built</exception>
	/// <exception cref="NotSupportedException">A content model uses a construct outside the supported XSD subset</exception>
	public static MasterBuildResult Build(SchemaLoader.LoadResult loaded)
	{
		var warnings = new List<string>();
		var seen = new HashSet<string>(StringComparer.Ordinal);

		void Warn(string message)
		{
			if (seen.Add(message))
				warnings.Add(message);
		}

		var index = SchemaIndex.Build(loaded.SchemaSet);
		var discovered = OperationDiscovery.Discover(index, Warn);

		if (discovered.Count == 0)
			throw new InvalidOperationException($"{loaded.RootFile} defines no operations; no global element has a type ending in Rq_MType");

		var trees = new ElementTreeBuilder(index, Warn);
		var operations = discovered.Select(info => BuildOperation(info, trees)).ToList();

		foreach (var name in OperationHelpers.SuspectEnvelopeElements(operations.Where(o => o.Kind == OperationKind.Modify).Select(o => o.Request)))
			Warn($"Element {name} sits at the root of most modify requests but is not in OperationHelpers.EnvelopeElements");

		foreach (var search in operations.Where(o => o.Kind == OperationKind.Search && o.Records.Count == 0))
			Warn($"Search {search.Name} has no repeating record in its response");

		var fault = index.GlobalElements.GetValueOrDefault(new XmlQualifiedName(FaultElement, loaded.RootSchema.TargetNamespace))?.ElementSchemaType
			is XmlSchemaComplexType faultType
			? trees.BuildChildren(faultType, TreeSide.Response)
			: null;

		if (fault is null)
			Warn($"{loaded.RootFile} declares no {FaultElement} element, so fault is null");

		var types = TypeTableBuilder.Build(operations.SelectMany(o => o.Request.Concat(o.Response ?? [])).Concat(fault ?? []), index);

		foreach (var spellings in ProviderSpellingConflicts(operations, fault, types.Values))
			Warn($"Provider names {string.Join(" and ", spellings)} differ only by case; add the preferred spelling to AnnotationReader");

		var version = loaded.RootSchema.Version;
		var document = new MasterDocument(loaded.RootFile, string.IsNullOrWhiteSpace(version) ? null : version, operations, fault, types);

		return new MasterBuildResult(document, warnings);
	}

	private static IEnumerable<string[]> ProviderSpellingConflicts(
		IEnumerable<OperationModel> operations,
		IReadOnlyList<ElementNode>? fault,
		IEnumerable<TypeEntry> types
	)
	{
		static IEnumerable<Annotation?> Annotations(ElementNode node) => node.Children.SelectMany(Annotations).Prepend(node.Annotation);

		var nodes = operations.SelectMany(o => o.Request.Concat(o.Response ?? [])).Concat(fault ?? []);
		var annotations = operations
			.Select(o => o.Annotation)
			.Concat(nodes.SelectMany(Annotations))
			.Concat(types.Select(t => t.Annotation))
			.OfType<Annotation>();

		return annotations
			.SelectMany(a => a.Providers.Keys)
			.Distinct(StringComparer.Ordinal)
			.GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
			.Where(group => group.Skip(1).Any())
			.Select(group => group.Order(StringComparer.Ordinal).ToArray())
			.OrderBy(spellings => spellings[0], StringComparer.Ordinal);
	}

	private static OperationModel BuildOperation(OperationInfo info, ElementTreeBuilder trees)
	{
		var request = trees.BuildChildren(info.RequestType, TreeSide.Request);
		var response = info.ResponseType is null ? null : trees.BuildChildren(info.ResponseType, TreeSide.Response);

		return new OperationModel(
			info.Name,
			info.Kind,
			info.RequestElement.QualifiedName.Name,
			info.ResponseElement?.QualifiedName.Name,
			AnnotationReader.Read(info.RequestElement) ?? AnnotationReader.Read(info.RequestType),
			request,
			response,
			OperationHelpers.ExtendedElements(request, response),
			info.Kind == OperationKind.Search ? OperationHelpers.Records(request, response) : [],
			info.Kind == OperationKind.Modify ? OperationHelpers.ModifyContainers(request) : []
		);
	}
}
