using System.Xml;

namespace JXSchemaGenerator.Schema;

/// <summary>
/// Resolves schema includes, imports and DTD references by file name inside one flat folder
/// </summary>
/// <remarks>
/// ! Never opens a network URI or a file outside the folder, whatever a schemaLocation says
/// The TPG zip ships every dependency schema next to the Master XSDs, so a missing file is a fetch problem, not a reason to go online
/// </remarks>
public sealed class LocalFolderXmlResolver : XmlResolver
{
	private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

	private readonly string _baseFolder;

	public LocalFolderXmlResolver(string baseFolder)
	{
		_baseFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseFolder));
	}

	public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
	{
		var resolved = base.ResolveUri(baseUri, relativeUri);
		if (resolved.IsFile && IsInBaseFolder(resolved.LocalPath))
			return resolved;

		var candidate = MapIntoBaseFolder(relativeUri ?? resolved.OriginalString);
		return candidate is null ? resolved : new Uri(candidate);
	}

	public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
	{
		if (ofObjectToReturn is not null && ofObjectToReturn != typeof(Stream) && ofObjectToReturn != typeof(object))
			throw new XmlException($"Unsupported entity type {ofObjectToReturn} for {absoluteUri}");

		var path = absoluteUri.IsFile && IsInBaseFolder(absoluteUri.LocalPath) ? absoluteUri.LocalPath : MapIntoBaseFolder(absoluteUri.OriginalString);
		if (path is null || !File.Exists(path))
			throw new FileNotFoundException($"Schema resource is not in {_baseFolder}: {absoluteUri}");

		return File.OpenRead(path);
	}

	private string? MapIntoBaseFolder(string uriOrPath)
	{
		var fileName = Path.GetFileName(uriOrPath.Replace('\\', '/'));
		if (string.IsNullOrEmpty(fileName))
			return null;

		var candidate = Path.Combine(_baseFolder, fileName);
		return IsInBaseFolder(candidate) && File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
	}

	private bool IsInBaseFolder(string path) => string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), _baseFolder, PathComparison);
}
