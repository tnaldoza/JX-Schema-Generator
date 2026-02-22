using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;

namespace JXSchemaGenerator.Schema;

public sealed class SchemaLoader
{
	public sealed record LoadResult(XmlSchemaSet SchemaSet, string RootFile);

	public LoadResult LoadAndCompileRoot(string folderPath, string rootXsdFileName, bool strict)
	{
		var folder = Path.GetFullPath(folderPath);
		var rootPath = Path.Combine(folder, rootXsdFileName);

		if (!File.Exists(rootPath))
			throw new FileNotFoundException($"Root XSD not found: {rootPath}");

		var errors = new List<string>();
		var warnings = new List<string>(); // optional: keep for debug if you want

		ValidationEventHandler handler = (_, e) =>
		{
			var msg = $"{e.Severity}: {e.Message}";

			// Many Jack Henry schemas emit resolver-related warnings that cascade if treated as fatal.
			if (e.Severity == XmlSeverityType.Error)
			{
				if (errors.Count < 200) errors.Add(msg);
				else if (errors.Count == 200) errors.Add("... (truncated after 200 errors)");
			}
			else
			{
				// Keep warnings for visibility; do NOT fail because of them.
				if (warnings.Count < 50) warnings.Add(msg);
				else if (warnings.Count == 50) warnings.Add("... (more warnings truncated)");
			}
		};

		// reuse a single resolver instance
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
}
