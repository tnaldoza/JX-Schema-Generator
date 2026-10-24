using System.Globalization;
using System.Text;
using JXSchemaGenerator.Emit;
using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Build;

/// <summary>
/// Writes one format 2 file per Master in a folder, logs what it wrote and reports failures through the exit code
/// </summary>
public static class GenerateRunner
{
	private const string MasterPattern = "*Master.xsd";
	private const string JsonPattern = "*.json";

	// ! Windows and macOS file systems treat names that differ only in case as one file
	private static readonly StringComparer FileNameComparer = StringComparer.OrdinalIgnoreCase;

	/// <summary>
	/// Builds and writes every target Master, then writes the format's JSON Schema and flags JSON files this run did not write
	/// </summary>
	/// <remarks>
	/// A Master that fails is logged and counted, and the remaining Masters are still written
	/// A Master whose output name another target already claimed fails
	/// Stale JSON files in the output folder are only reported, never deleted
	/// A <paramref name="rootXsd"/> with a folder part is rejected before anything is read or written
	/// </remarks>
	/// <param name="inputFolder">An existing folder holding the Masters and every XSD they include</param>
	/// <param name="rootXsd">The one Master to build, as a file name inside <paramref name="inputFolder"/>, or <see langword="null"/> for every top-level <c>*Master.xsd</c> there</param>
	/// <param name="outputFolder">The folder to write into, created when missing</param>
	/// <param name="log">Receives one line per Master, each of its warnings, the stale-file warnings and a summary</param>
	/// <returns>0 when every target was written, 1 when the root has a folder part, there is no target or any target failed</returns>
	public static int Run(string inputFolder, string? rootXsd, string outputFolder, TextWriter log)
	{
		// ! The loader opens the root without the resolver's folder guard, and the output name derived from it is joined onto the output folder
		if (rootXsd is not null && Path.GetFileName(rootXsd) != rootXsd)
		{
			log.WriteLine($"Root XSD {rootXsd} must be a file name inside {inputFolder}, without a folder");
			return 1;
		}

		string[] targets = rootXsd is not null ? [rootXsd] : [.. TopLevelFileNames(inputFolder, MasterPattern)];

		if (targets.Length == 0)
		{
			log.WriteLine($"No {MasterPattern} files found in {inputFolder}");
			return 1;
		}

		Directory.CreateDirectory(outputFolder);

		var claimedBy = new Dictionary<string, string>(FileNameComparer);
		var written = new HashSet<string>(FileNameComparer);
		var operations = 0;
		var failed = 0;

		foreach (var target in targets)
		{
			try
			{
				var fileName = OutputFileNames.FromMaster(target);

				if (!claimedBy.TryAdd(fileName, target))
					throw new InvalidOperationException($"{fileName} is already the output of {claimedBy[fileName]}");

				var result = MasterBuilder.Build(SchemaLoader.LoadAndCompileRoot(inputFolder, target));
				var document = result.Document;
				var json = MasterJsonWriter.ToJson(document);

				File.WriteAllText(Path.Combine(outputFolder, fileName), json);
				written.Add(fileName);
				operations += document.Operations.Count;

				log.WriteLine(
					string.Create(
						CultureInfo.InvariantCulture,
						$"{target}: {document.Operations.Count} operations, {document.Types.Count} types, {Encoding.UTF8.GetByteCount(json):N0} bytes -> {fileName}"
					)
				);

				foreach (var warning in result.Warnings)
					log.WriteLine($"  warning: {warning}");
			}
			catch (Exception ex)
			{
				log.WriteLine($"{target}: FAILED {ex.Message}");
				failed++;
			}
		}

		MasterJsonWriter.WriteSchema(outputFolder);

		var stale = TopLevelFileNames(outputFolder, JsonPattern)
			.Where(name => !written.Contains(name) && !FileNameComparer.Equals(name, MasterJsonWriter.SchemaFileName));

		foreach (var name in stale)
			log.WriteLine($"warning: stale file {name} was not produced by this run");

		log.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{written.Count} Masters, {operations} operations, {failed} failed"));

		// ! A failed Master fails the run, so a release never ships an incomplete bundle
		return failed > 0 ? 1 : 0;
	}

	private static IEnumerable<string> TopLevelFileNames(string folder, string pattern) =>
		Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal);
}
