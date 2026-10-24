using System.Xml.Schema;
using JXSchemaGenerator.Model;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Build;

/// <summary>
/// Pairs each global request element with its response element by complex type name
/// </summary>
public static class OperationDiscovery
{
	private const string RequestSuffix = "Rq_MType";
	private const string ResponseSuffix = "Rs_MType";

	/// <summary>
	/// Finds every operation in the index, sorted by name with <see cref="StringComparer.Ordinal"/>
	/// </summary>
	/// <param name="index">The compiled schema index</param>
	/// <param name="warn">Receives one message per missing response or duplicated element</param>
	/// <returns>The operations in ordinal name order</returns>
	public static IReadOnlyList<OperationInfo> Discover(SchemaIndex index, Action<string> warn)
	{
		var requests = ByTypeName(index, RequestSuffix, warn);
		var responses = ByTypeName(index, ResponseSuffix, warn);
		var operations = new List<OperationInfo>();

		foreach (var (name, request) in requests.OrderBy(pair => pair.Key, StringComparer.Ordinal))
		{
			responses.TryGetValue(name, out var response);

			if (response is null)
				warn($"Operation {name} has no response element of type {name}{ResponseSuffix}");

			operations.Add(
				new OperationInfo(
					name,
					KindOf(name),
					request,
					(XmlSchemaComplexType)request.ElementSchemaType!,
					response,
					response?.ElementSchemaType as XmlSchemaComplexType
				)
			);
		}

		return operations;
	}

	private static Dictionary<string, XmlSchemaElement> ByTypeName(SchemaIndex index, string suffix, Action<string> warn)
	{
		var found = new Dictionary<string, XmlSchemaElement>(StringComparer.Ordinal);

		foreach (var element in index.GlobalElements.Values.OrderBy(e => e.Name, StringComparer.Ordinal))
		{
			if (element.ElementSchemaType is not XmlSchemaComplexType { Name: { } typeName } || !typeName.EndsWith(suffix, StringComparison.Ordinal))
				continue;

			var name = typeName[..^suffix.Length];

			if (found.TryGetValue(name, out var kept))
				warn($"Elements {kept.Name} and {element.Name} share type {typeName}; keeping {kept.Name}");
			else
				found[name] = element;
		}

		return found;
	}

	private static OperationKind KindOf(string name) =>
		name switch
		{
			_ when name.EndsWith("Validate", StringComparison.Ordinal) => OperationKind.Validate,
			_ when name.EndsWith("Inq", StringComparison.Ordinal) => OperationKind.Inquiry,
			_ when name.EndsWith("Srch", StringComparison.Ordinal) => OperationKind.Search,
			_ when name.EndsWith("Add", StringComparison.Ordinal) => OperationKind.Add,
			_ when name.EndsWith("Mod", StringComparison.Ordinal) => OperationKind.Modify,
			_ => OperationKind.Other,
		};
}
