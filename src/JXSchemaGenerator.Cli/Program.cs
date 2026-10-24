using JXSchemaGenerator.Build;

const string Usage = """
	Usage:
	  generate --input <SchemaFolder> [--root <XsdFile>] [--out <OutputFolder>]

	  Writes one JSON file per top-level *Master.xsd in <SchemaFolder>, or for <XsdFile> alone, into <OutputFolder>
	  <OutputFolder> defaults to artifacts
	""";

static string? GetArg(string[] args, string name)
{
	for (int i = 1; i < args.Length - 1; i += 2)
		if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
			return args[i + 1];
	return null;
}

static bool IsWellFormed(string[] args)
{
	if (args.Length % 2 == 0 || !string.Equals(args[0], "generate", StringComparison.OrdinalIgnoreCase))
		return false;

	var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	for (int i = 1; i < args.Length; i += 2)
		if (args[i].ToLowerInvariant() is not ("--input" or "--root" or "--out") || !seen.Add(args[i]) || IsMissingValue(args[i + 1]))
			return false;

	return true;
}

// ! A value starting with -- is the next option, so the option before it has no value
static bool IsMissingValue(string value) => string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal);

var input = GetArg(args, "--input");

if (!IsWellFormed(args) || input is null)
{
	Console.Error.WriteLine(Usage);
	return 2;
}

if (!Directory.Exists(input))
{
	Console.Error.WriteLine($"Input folder not found: {Path.GetFullPath(input)}");
	Console.Error.WriteLine(Usage);
	return 2;
}

return GenerateRunner.Run(input, GetArg(args, "--root"), GetArg(args, "--out") ?? "artifacts", Console.Out);
