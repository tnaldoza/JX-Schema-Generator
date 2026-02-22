using JXSchemaGenerator.Schema;
using JXSchemaGenerator.Emit;
using JXSchemaGenerator.Generate;
using JXSchemaGenerator.Expand;

static string? GetArg(string[] args, string name)
{
	for (int i = 0; i < args.Length - 1; i++)
		if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
			return args[i + 1];
	return null;
}

// --- Command dispatch ---
var command = args.FirstOrDefault()?.ToLowerInvariant();

if (command is not "generate" and not "detect")
{
	Console.WriteLine("Usage:");
	Console.WriteLine("  generate --input <SchemaFolder> [--root <XsdFile>] [--out <OutputFolder>] [--strict true|false]");
	Console.WriteLine("  detect   --input <SchemaFolder> [--root <XsdFile>] [--strict true|false]");
	Console.WriteLine();
	Console.WriteLine("  generate  Process master XSD(s) and write JSON element mapping files.");
	Console.WriteLine("  detect    Analyse master XSD(s) and print config recommendations with");
	Console.WriteLine("            exact tag counts from the real expansion pipeline.");
	Console.WriteLine();
	Console.WriteLine("  --root is optional for both commands. Omit to process all *Master.xsd files.");
	return 2;
}

var inputFolder = GetArg(args, "--input");
var rootXsd = GetArg(args, "--root");
var output = GetArg(args, "--out") ?? ".\\artifacts";
var strict = bool.TryParse(GetArg(args, "--strict"), out var s) ? s : true;

if (string.IsNullOrWhiteSpace(inputFolder))
{
	Console.WriteLine("Error: --input is required.");
	return 2;
}

if (!Directory.Exists(inputFolder))
{
	Console.WriteLine($"Error: Input folder not found: {Path.GetFullPath(inputFolder)}");
	return 2;
}

var targets = rootXsd is not null
	? [rootXsd]
	: Directory.GetFiles(inputFolder, "*Master.xsd", SearchOption.TopDirectoryOnly)
			   .Select(Path.GetFileName)
			   .Where(f => f is not null)
			   .Cast<string>()
			   .OrderBy(f => f)
			   .ToArray();

if (targets.Length == 0)
{
	Console.WriteLine("No *Master.xsd files found.");
	return 1;
}

Console.WriteLine($"Command      : {command}");
Console.WriteLine($"Input folder : {Path.GetFullPath(inputFolder)}");
Console.WriteLine($"Strict       : {strict}");
Console.WriteLine($"Targets      : {targets.Length} file(s)");

var loader = new SchemaLoader();

var options = new ExpansionOptions
{
	LeafTagsOnly = false,
	SkipAny = true,
	SkipVersionContainers = true,
};

// DETECT
if (command == "detect")
{
	foreach (var xsdFile in targets)
	{
		Console.WriteLine($"\nLoading: {xsdFile}");
		SchemaIndex index;
		try
		{
			var result = loader.LoadAndCompileRoot(inputFolder, xsdFile, strict);
			index = SchemaIndex.Build(result.SchemaSet);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"  SKIPPED: {ex.Message}");
			continue;
		}

		var report = new DomainDetector(index, xsdFile).Detect(options);
		DetectionReportPrinter.Print(report);
	}

	return 0;
}

// GENERATE
Directory.CreateDirectory(output);
Console.WriteLine($"Output       : {Path.GetFullPath(output)}");

int written = 0;
int skipped = 0;
var warnings = new List<string>();

foreach (var xsdFile in targets)
{
	Console.WriteLine($"\n--- {xsdFile} ---");

	SchemaIndex index;
	try
	{
		var result = loader.LoadAndCompileRoot(inputFolder, xsdFile, strict);
		index = SchemaIndex.Build(result.SchemaSet);
		Console.WriteLine(index.ToString());
	}
	catch (Exception ex)
	{
		Console.WriteLine($"SKIPPED: {ex.Message}");
		skipped++;
		continue;
	}

	bool wroteAny = false;

	var modConfig = ModificationDomainConfigs.FromRootXsd(xsdFile);
	if (modConfig is not null)
	{
		var modModel = new ModificationGenerator(modConfig).Generate(index, options);

		var modPayload = new Dictionary<string, object?>
		{
			["version"] = modModel.Version,
			["lastUpdated"] = modModel.LastUpdated,
			["entityTypes"] = modModel.entityTypes,
		};

		var modPath = Path.Combine(output, modConfig.OutputFileName);
		JsonEmitter.WriteToFile(modPath, modPayload);
		Console.WriteLine($"Wrote: {modPath}");

		CollectEmptyTagWarnings(xsdFile, modModel.entityTypes
			.SelectMany(et => et.Value.modificationSections
				.Where(s => s.schema is null)
				.Select(s => $"[{et.Key}] {s.name}")),
			warnings);

		written++;
		wroteAny = true;
	}

	var searchConfig = SrchOpConfigs.FromRootXsd(xsdFile);
	if (searchConfig is not null)
	{
		var searchModel = new SearchGenerator().Generate(index, options);

		var searchPayload = new Dictionary<string, object?>
		{
			["version"] = searchModel.Version,
			["lastUpdated"] = searchModel.LastUpdated,
			["searchOperations"] = searchModel.searchOperations,
		};

		var searchPath = Path.Combine(output, searchConfig.OutputFileName);
		JsonEmitter.WriteToFile(searchPath, searchPayload);
		Console.WriteLine($"Wrote: {searchPath}");

		written++;
		wroteAny = true;
	}

	var InquiryConfig = InquiryConfigs.FromRootXsd(xsdFile);
	if (InquiryConfig is not null)
	{
		var InquiryModel = new InquiryGenerator().Generate(index, options);

		var InquiryPayload = new Dictionary<string, object?>
		{
			["version"] = InquiryModel.Version,
			["lastUpdated"] = InquiryModel.LastUpdated,
			["inquiryOperations"] = InquiryModel.inquiryOperations,
		};

		var InquiryPath = Path.Combine(output, InquiryConfig.OutputFileName);
		JsonEmitter.WriteToFile(InquiryPath, InquiryPayload);
		Console.WriteLine($"Wrote: {InquiryPath}");

		written++;
		wroteAny = true;
	}

	if (!wroteAny)
	{
		Console.WriteLine("No domain config registered -- skipped.");
		skipped++;
	}
}

Console.WriteLine("\n=== Summary ===");
Console.WriteLine($"Files written : {written}");
Console.WriteLine($"Files skipped : {skipped}");

if (warnings.Count > 0)
{
	Console.WriteLine($"\nWARNING: {warnings.Count} section(s) resolved zero tags:");
	foreach (var w in warnings) Console.WriteLine($"  {w}");
}
else
{
	Console.WriteLine("All sections resolved tags successfully.");
}

return 0;

static void CollectEmptyTagWarnings(string xsdFile, IEnumerable<string> items, List<string> sink)
{
	foreach (var item in items)
		sink.Add($"{xsdFile}: {item}");
}
