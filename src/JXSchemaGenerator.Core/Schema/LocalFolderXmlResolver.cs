using System;
using System.IO;
using System.Net;
using System.Xml;

namespace JXSchemaGenerator.Schema;

public sealed class LocalFolderXmlResolver : XmlResolver
{
	private readonly string _baseFolder;
	private readonly XmlUrlResolver _httpFallback = new();

	public LocalFolderXmlResolver(string baseFolder)
	{
		_baseFolder = Path.GetFullPath(baseFolder);
	}

	public override ICredentials? Credentials
	{
		set
		{
			_httpFallback.Credentials = value;
		}
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
		// 1. Prefer exact local file match
		if (absoluteUri.IsFile && File.Exists(absoluteUri.LocalPath))
			return File.OpenRead(absoluteUri.LocalPath);

		// 2. Try the filename under the base folder (handles relative imports)
		var fileName = Path.GetFileName(absoluteUri.OriginalString);
		var candidate = Path.Combine(_baseFolder, fileName);
		if (File.Exists(candidate))
			return File.OpenRead(candidate);

		// 3. For external HTTP(S) URIs (e.g. OASIS WS-Security schemas that JXChange
		//    XSDs import via schemaLocation), fall back to a standard URL resolver
		//    rather than throwing. These are infrastructure schemas for SOAP headers
		//    and are not available locally, but .NET's XmlUrlResolver can fetch them.
		if (absoluteUri.Scheme is "http" or "https")
			return _httpFallback.GetEntity(absoluteUri, role, ofObjectToReturn);

		throw new FileNotFoundException($"Could not resolve schema resource: {absoluteUri}");
	}
}
