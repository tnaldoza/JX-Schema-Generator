using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;

namespace JXSchemaGenerator.Schema;

public sealed class SchemaLoader
{
	public sealed record LoadResult(XmlSchemaSet SchemaSet, string RootFile);

	/// <summary>
	/// Namespace prefixes whose unresolved declarations should be treated as
	/// non-fatal warnings rather than hard errors.  These are SOAP infrastructure
	/// namespaces (WS-Security, WS-Utility) that JXChange XSDs import but whose
	/// declarations have no bearing on the JXChange domain content we generate.
	/// </summary>
	private static readonly string[] KnownInfraNamespacePrefixes =
	[
		"http://docs.oasis-open.org/wss/",
		"http://schemas.xmlsoap.org/",
		"http://www.w3.org/2005/08/addressing",
	];

	public LoadResult LoadAndCompileRoot(string folderPath, string rootXsdFileName, bool strict)
	{
		var folder = Path.GetFullPath(folderPath);
		var rootPath = Path.Combine(folder, rootXsdFileName);

		if (!File.Exists(rootPath))
			throw new FileNotFoundException($"Root XSD not found: {rootPath}");

		var errors = new List<string>();
		var warnings = new List<string>();

		ValidationEventHandler handler = (_, e) =>
		{
			var msg = $"{e.Severity}: {e.Message}";

			if (e.Severity == XmlSeverityType.Error)
			{
				// Demote errors that are purely about unresolvable SOAP/WS-Security
				// infrastructure schemas.  These never affect JXChange domain content.
				if (IsInfrastructureError(e.Message))
				{
					if (warnings.Count < 50) warnings.Add($"[infra-demoted] {msg}");
				}
				else
				{
					if (errors.Count < 200) errors.Add(msg);
					else if (errors.Count == 200) errors.Add("... (truncated after 200 errors)");
				}
			}
			else
			{
				if (warnings.Count < 50) warnings.Add(msg);
				else if (warnings.Count == 50) warnings.Add("... (more warnings truncated)");
			}
		};

		var resolver = new LocalFolderXmlResolver(folder);

		var set = new XmlSchemaSet
		{
			XmlResolver = resolver
		};
		set.ValidationEventHandler += handler;

		using var fs = File.OpenRead(rootPath);
		using var reader = XmlReader.Create(fs, new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Ignore,
			XmlResolver = resolver,
			CloseInput = true
		});

		var schema = XmlSchema.Read(reader, handler)
			?? throw new InvalidOperationException($"XmlSchema.Read returned null for {rootPath}");

		schema.SourceUri = new Uri(Path.GetFullPath(rootPath)).AbsoluteUri;

		set.Add(schema);

		try
		{
			set.Compile();
		}
		catch (Exception ex)
		{
			errors.Add($"Error compiling schemas: {ex.Message}");
		}

		if (errors.Count > 0)
		{
			var warnText = warnings.Count > 0
				? "\nWarnings (first):\n" + string.Join("\n", warnings)
				: "";

			throw new InvalidOperationException(
				$"Schema compile failed for root '{rootXsdFileName}'. First errors:\n{string.Join("\n", errors)}{warnText}");
		}

		return new LoadResult(set, rootXsdFileName);
	}

	/// <summary>
	/// Returns true when a validation error message is about an infrastructure
	/// namespace (WS-Security, SOAP addressing) that cannot be resolved locally
	/// and is irrelevant to JXChange domain-content generation.
	/// </summary>
	private static bool IsInfrastructureError(string message)
	{
		foreach (var prefix in KnownInfraNamespacePrefixes)
			if (message.Contains(prefix, StringComparison.OrdinalIgnoreCase))
				return true;
		return false;
	}
}
