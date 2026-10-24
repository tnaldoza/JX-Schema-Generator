using System.Collections.Frozen;
using JXSchemaGenerator.Model;

namespace JXSchemaGenerator.Build;

/// <summary>
/// Derives the extended elements, search records and modify containers of an operation from its element trees
/// </summary>
public static class OperationHelpers
{
	private const string ExtendedElementPrefix = "x_";
	private const string ExtensionRequestElement = "IncXtendElemArray";
	private const string ModifyContainerSuffix = "Mod";
	private const string CustomElement = "Custom";

	/// <summary>
	/// The request elements shared by many operations that are not sections of any one of them
	/// </summary>
	/// <remarks>
	/// <see cref="SuspectEnvelopeElements"/> reports candidates for this set
	/// </remarks>
	public static FrozenSet<string> EnvelopeElements { get; } =
		new[] { "ErrOvrRdInfoArray", "AccountId", "Dlt", CustomElement, "ActIntentKey", "ActIntent", "ModRmkArray" }.ToFrozenSet(StringComparer.Ordinal);

	/// <summary>
	/// Lists the <c>x_</c> elements of a response when the request asks for extended elements
	/// </summary>
	/// <param name="request">The request's root children</param>
	/// <param name="response">The response's root children, or <see langword="null"/> when the operation has none</param>
	/// <returns>The distinct names sorted with <see cref="StringComparer.Ordinal"/></returns>
	public static IReadOnlyList<string> ExtendedElements(IReadOnlyList<ElementNode> request, IReadOnlyList<ElementNode>? response)
	{
		if (response is null || !request.Any(n => n.Name == ExtensionRequestElement))
			return [];

		var found = new SortedSet<string>(StringComparer.Ordinal);
		Collect(response);
		return [.. found];

		void Collect(IEnumerable<ElementNode> nodes)
		{
			foreach (var node in nodes)
			{
				if (IsExtended(node))
					found.Add(node.Name);
				else
					Collect(node.Children);
			}
		}
	}

	/// <summary>
	/// Lists the repeating records of a response
	/// </summary>
	/// <remarks>
	/// The records are the repeating elements directly under the response root or one level below its children
	/// When those two levels hold none, the records are the shallowest repeating elements anywhere in the response
	/// A response root child named like a request root child is the request's criteria sent back, so neither it nor anything inside it is a record
	/// <c>x_</c> elements are optional extras, so neither they nor anything inside them is a record
	/// </remarks>
	/// <param name="request">The request's root children</param>
	/// <param name="response">The response's root children, or <see langword="null"/> when the operation has none</param>
	/// <returns>The paths from the response root in XSD order, joined with <c>/</c>, or none when nothing repeats</returns>
	public static IReadOnlyList<string> Records(IReadOnlyList<ElementNode> request, IReadOnlyList<ElementNode>? response)
	{
		var criteria = request.Select(n => n.Name).ToHashSet(StringComparer.Ordinal);
		var roots = (response ?? []).Where(n => !criteria.Contains(n.Name) && !IsExtended(n)).ToList();
		var records = new List<string>();

		foreach (var node in roots)
		{
			if (node.Repeats)
				records.Add(node.Name);

			records.AddRange(node.Children.Where(c => c.Repeats && !IsExtended(c)).Select(c => $"{node.Name}/{c.Name}"));
		}

		return records.Count > 0 ? records : ShallowestRepeating(roots);
	}

	/// <summary>
	/// Splits a modify request into the containers whose sections can be changed
	/// </summary>
	/// <param name="request">The request's root children</param>
	/// <returns>One container per complex <c>Mod</c> child, or a single unnamed container over the root when there is none</returns>
	public static IReadOnlyList<ModifyContainer> ModifyContainers(IReadOnlyList<ElementNode> request)
	{
		var containers = request
			.Where(n => IsComplex(n) && n.Name.EndsWith(ModifyContainerSuffix, StringComparison.Ordinal))
			.Select(n => new ModifyContainer(n.Name, SectionNames(n.Children.Where(c => c.Name != CustomElement))))
			.ToList();

		if (containers.Count > 0)
			return containers;

		return [new ModifyContainer(null, SectionNames(request.Where(n => !EnvelopeElements.Contains(n.Name))))];
	}

	/// <summary>
	/// Finds complex root children that most modify requests share but that are missing from <see cref="EnvelopeElements"/>
	/// </summary>
	/// <remarks>
	/// Simple-typed root children are record keys, which are never sections, so they are not counted
	/// </remarks>
	/// <param name="modifyRequests">The root children of every modify request in a Master</param>
	/// <returns>The names sorted with <see cref="StringComparer.Ordinal"/></returns>
	public static IReadOnlyList<string> SuspectEnvelopeElements(IEnumerable<IReadOnlyList<ElementNode>> modifyRequests)
	{
		var requests = modifyRequests.ToList();

		return
		[
			.. requests
				.SelectMany(r => r.Where(IsComplex).Select(n => n.Name).Distinct(StringComparer.Ordinal))
				.Where(name => !name.EndsWith(ModifyContainerSuffix, StringComparison.Ordinal) && !EnvelopeElements.Contains(name))
				.CountBy(name => name, StringComparer.Ordinal)
				.Where(pair => pair.Value >= 3 && pair.Value * 2 > requests.Count)
				.Select(pair => pair.Key)
				.Order(StringComparer.Ordinal),
		];
	}

	private static List<string> ShallowestRepeating(IReadOnlyList<ElementNode> response)
	{
		var level = response.Select(n => (Path: n.Name, Node: n)).ToList();

		while (level.Count > 0)
		{
			var repeating = level.Where(e => e.Node.Repeats).Select(e => e.Path).ToList();

			if (repeating.Count > 0)
				return repeating;

			level = [.. level.SelectMany(e => e.Node.Children.Where(c => !IsExtended(c)).Select(c => (Path: $"{e.Path}/{c.Name}", Node: c)))];
		}

		return [];
	}

	private static bool IsComplex(ElementNode node) => node.BuiltInType is null;

	private static bool IsExtended(ElementNode node) => node.Name.StartsWith(ExtendedElementPrefix, StringComparison.Ordinal);

	private static List<string> SectionNames(IEnumerable<ElementNode> nodes) => [.. nodes.Where(IsComplex).Select(n => n.Name)];
}
