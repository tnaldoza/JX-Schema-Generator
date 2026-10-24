using JXSchemaGenerator.Model;

namespace JXSchemaGenerator.Tests;

internal static class NodePaths
{
	public static ElementNode Find(this IReadOnlyList<ElementNode> nodes, string path)
	{
		var segments = path.Split('/');
		var node = nodes.Single(n => n.Name == segments[0]);

		foreach (var segment in segments.Skip(1))
			node = node.Children.Single(n => n.Name == segment);

		return node;
	}
}
