using System;
using System.IO;
using System.Net;
using System.Xml;

namespace JXSchemaGenerator.Schema;

public sealed class LocalFolderXmlResolver : XmlResolver
{
	private readonly string _baseFolder;

	public LocalFolderXmlResolver(string baseFolder)
	{
		_baseFolder = Path.GetFullPath(baseFolder);
	}

	public override ICredentials? Credentials
	{
		set { /* not used */ }
	}

	public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
	{
		// Keep default behavior first
		var resolved = base.ResolveUri(baseUri, relativeUri);

		// If it already resolves to an existing local file, great
		if (resolved.IsFile && File.Exists(resolved.LocalPath))
			return resolved;

		// Otherwise, map by filename into the base folder
		if (!string.IsNullOrWhiteSpace(relativeUri))
		{
			var fileName = Path.GetFileName(relativeUri);
			var candidate = Path.Combine(_baseFolder, fileName);
			if (File.Exists(candidate))
				return new Uri(Path.GetFullPath(candidate));
		}

		return resolved;
	}

	public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
	{
		// Prefer local file if possible
		if (absoluteUri.IsFile && File.Exists(absoluteUri.LocalPath))
			return File.OpenRead(absoluteUri.LocalPath);

		// Fall back: try the filename under base folder
		var fileName = Path.GetFileName(absoluteUri.OriginalString);
		var candidate = Path.Combine(_baseFolder, fileName);
		if (File.Exists(candidate))
			return File.OpenRead(candidate);

		throw new FileNotFoundException($"Could not resolve schema resource: {absoluteUri}");
	}
}
