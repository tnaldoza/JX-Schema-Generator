using JXSchemaGenerator.Generate.Models;

namespace JXSchemaGenerator.Generate;

/// <summary>
/// Renders a DetectionReport to the console with copy-paste ready config snippets
/// and exact tag counts from the real expansion pipeline.
/// </summary>
public static class DetectionReportPrinter
{
	public static void Print(DetectionReport report)
	{
		var xsd = report.XsdFileName;

		Console.WriteLine();
		Console.WriteLine($"====================================================================");
		Console.WriteLine($"  DETECTION REPORT: {xsd}");
		Console.WriteLine($"====================================================================");

		Console.WriteLine();
		Console.WriteLine("  Registration status");
		Console.WriteLine($"    InquiryConfigs              : {(report.AlreadyRegisteredInquiry ? "already registered" : "NOT registered")}");
		Console.WriteLine($"    ModificationDomainConfigs : {(report.AlreadyRegisteredMod ? "already registered" : "NOT registered")}");

		if (report.Inquiry.Count == 0 && report.Modification.Count == 0)
		{
			Console.WriteLine();
			Console.WriteLine("  RESULT: No domain configs needed for this XSD.");
			Console.WriteLine("          No x_ inquiry containers or modification request containers were found.");
			Console.WriteLine();
			return;
		}

		// INQUIRY
		if (report.Inquiry.Count > 0)
		{
			Console.WriteLine();
			Console.WriteLine("  --------------------------------------------------------------------");
			Console.WriteLine("  INQUIRY  ->  auto-discovered by InquiryGenerator (*InqRs_MType with x_ elements)");
			Console.WriteLine("  --------------------------------------------------------------------");
			Console.WriteLine();
			Console.WriteLine($"  {report.Inquiry.Count} response type(s) with extended elements found.");
			Console.WriteLine();

			// Validation results per x_ element
			Console.WriteLine("  x_ element validation (real tag counts from expansion pipeline):");
			Console.WriteLine();
			Console.WriteLine($"    {"Element",-42} {"Strategy",-14} {"Type Resolved",-36} Tags");
			Console.WriteLine($"    {new string('-', 42)} {new string('-', 14)} {new string('-', 36)} ----");

			foreach (var entry in report.Inquiry.Values)
			{
				foreach (var v in entry.Validations)
				{
					var icon = v.IsEmpty ? "[WARN]" : "[OK]  ";
					var strat = v.Strategy switch
					{
						TagResolutionStrategy.DirectType => "S1:direct",
						TagResolutionStrategy.SchemaTypeName => "S2:typeName",
						TagResolutionStrategy.Convention => "S3:convention",
						_ => "NONE",
					};
					var resolved = v.ResolvedTypeName ?? "(not resolved)";
					var tags = v.IsEmpty ? "EMPTY" : v.TagCount.ToString();
					Console.WriteLine($"  {icon} {v.ElementName,-42} {strat,-14} {resolved,-36} {tags}");
				}
			}

			if (!report.AlreadyRegisteredInquiry)
			{
				Console.WriteLine();
				Console.WriteLine("  Add to InquiryConfigs.FromRootXsd in OperationConfig.cs:");
				Console.WriteLine($"    \"{xsd.ToLowerInvariant()}\" => new() {{ OutputFileName = \"<name>InquiryElements.json\" }},");
			}
		}

		// MODIFICATION
		if (report.Modification.Count > 0)
		{
			Console.WriteLine();
			Console.WriteLine("  --------------------------------------------------------------------");
			Console.WriteLine("  MODIFICATION CONFIG  ->  add to ModificationDomainConfig.cs");
			Console.WriteLine("  --------------------------------------------------------------------");

			var multiMod = report.Modification.Values
				.Where(e => e.Pattern == ContainerPattern.MultiAcctModRq)
				.ToList();

			var singleMod = report.Modification.Values
				.Where(e => e.Pattern == ContainerPattern.SingleModRq)
				.ToList();

			if (multiMod.Count > 0)
			{
				Console.WriteLine();
				Console.WriteLine("  Pattern : Multiple *AcctModRq_MType containers");
				Console.WriteLine("  Suggested config:");
				Console.WriteLine();
				Console.WriteLine("    public static readonly ModificationDomainConfig <Name> = new()");
				Console.WriteLine("    {");
				Console.WriteLine($"        OutputFileName     = \"<name>ModificationElements.json\",");
				Console.WriteLine($"        ContainerTypeRegex = new Regex(@\"^(?<key>[A-Za-z]+)AcctModRq_MType$\", RegexOptions.Compiled),");
				Console.WriteLine($"        OutputOrder        = [{string.Join(", ", multiMod.Select(e => $"\"{SuggestKey(e.DetectedPrefix, e.Pattern)}\""))}],");
				Console.WriteLine($"        TypeToEntry        = key => key switch");
				Console.WriteLine("        {");
				foreach (var e in multiMod)
				{
					var key = SuggestKey(e.DetectedPrefix, e.Pattern);
					Console.WriteLine($"            \"{e.DetectedPrefix}\" => new(\"{key}\", \"{SuggestDisplayName(e.DetectedPrefix)}\", []),");
				}
				Console.WriteLine("            _  => null,");
				Console.WriteLine("        },");
				Console.WriteLine("    };");
			}

			foreach (var e in singleMod)
			{
				var key = SuggestKey(e.DetectedPrefix, e.Pattern);
				Console.WriteLine();
				Console.WriteLine($"  Pattern : Single flat container ({e.ContainerName})");
				Console.WriteLine("  Suggested config:");
				Console.WriteLine();
				Console.WriteLine("    public static readonly ModificationDomainConfig <Name> = new()");
				Console.WriteLine("    {");
				Console.WriteLine($"        OutputFileName     = \"<name>ModificationElements.json\",");
				Console.WriteLine($"        ContainerTypeRegex = new Regex(@\"^(?<key>{e.DetectedPrefix})ModRq_MType$\", RegexOptions.Compiled),");
				Console.WriteLine($"        OutputOrder        = [\"{key}\"],");
				Console.WriteLine($"        TypeToEntry        = key => key switch");
				Console.WriteLine("        {");
				Console.WriteLine($"            \"{e.DetectedPrefix}\" => new(\"{key}\", \"{SuggestDisplayName(e.DetectedPrefix)}\", []),");
				Console.WriteLine("            _  => null,");
				Console.WriteLine("        },");
				Console.WriteLine("    };");
			}

			// Section validation table
			Console.WriteLine();
			Console.WriteLine("  Modification section validation (real tag counts from expansion pipeline):");
			Console.WriteLine();
			Console.WriteLine($"    {"Section",-36} {"Structure",-24} {"Strategy",-14} {"Type Resolved",-36} Tags");
			Console.WriteLine($"    {new string('-', 36)} {new string('-', 24)} {new string('-', 14)} {new string('-', 36)} ----");

			foreach (var entry in report.Modification.Values)
			{
				var structure = entry.ModStructureName ?? "?";
				foreach (var sv in entry.SectionValidations)
				{
					var icon = sv.IsEmpty ? "[WARN]" : "[OK]  ";
					var strat = sv.Strategy switch
					{
						TagResolutionStrategy.DirectType => "S1:direct",
						TagResolutionStrategy.SchemaTypeName => "S2:typeName",
						TagResolutionStrategy.Convention => "S3:convention",
						_ => "NONE",
					};
					var resolved = sv.ResolvedTypeName ?? "(not resolved)";
					var tags = sv.IsEmpty ? "EMPTY" : sv.TagCount.ToString();
					Console.WriteLine($"  {icon} {sv.SectionName,-36} {structure,-24} {strat,-14} {resolved,-36} {tags}");
				}
			}

			Console.WriteLine();
			Console.WriteLine("  Add to ModificationDomainConfigs.FromRootXsd:");
			Console.WriteLine($"    \"{xsd.ToLowerInvariant()}\" => <Name>,");
		}

		// EDGE CASES
		var emptyXElements = report.Inquiry.Values
			.SelectMany(e => e.Validations)
			.Where(v => v.IsEmpty)
			.ToList();

		var emptySections = report.Modification.Values
			.SelectMany(e => e.SectionValidations)
			.Where(v => v.IsEmpty)
			.ToList();

		if (emptyXElements.Count > 0 || emptySections.Count > 0)
		{
			Console.WriteLine();
			Console.WriteLine("  ====================================================================");
			Console.WriteLine("  EDGE CASES - these will produce empty tags in the output JSON");
			Console.WriteLine("  ====================================================================");

			foreach (var v in emptyXElements)
			{
				Console.WriteLine();
				Console.WriteLine($"  [INQUIRY EMPTY] {v.ElementName}");
				Console.WriteLine($"    All 3 resolution strategies returned 0 tags:");
				Console.WriteLine($"      S1: ElementSchemaType.QualifiedName - not set or not in index");
				Console.WriteLine($"      S2: SchemaTypeName attribute        - not set or not in index");
				Console.WriteLine($"      S3: Convention {v.ElementName}_CType / {v.ElementName}_AType - not found");
				Console.WriteLine($"    Fix options:");
				Console.WriteLine($"      a) Verify the XSD defines {v.ElementName}_CType in this namespace");
				Console.WriteLine($"      b) Check if the element uses a type from a different namespace (ns mismatch)");
				Console.WriteLine($"      c) The type may be empty-by-design - confirm with schema owner");
			}

			foreach (var sv in emptySections)
			{
				Console.WriteLine();
				Console.WriteLine($"  [MOD SECTION EMPTY] {sv.SectionName}");
				Console.WriteLine($"    All 3 resolution strategies returned 0 tags:");
				Console.WriteLine($"      S1: ElementSchemaType.QualifiedName - not set or not in index");
				Console.WriteLine($"      S2: SchemaTypeName attribute        - not set or not in index");
				Console.WriteLine($"      S3: Convention {sv.SectionName}_CType / {sv.SectionName}_AType - not found");
				Console.WriteLine($"    Fix options:");
				Console.WriteLine($"      a) Verify the XSD defines {sv.SectionName}_CType");
				Console.WriteLine($"      b) Check for namespace mismatch");
				Console.WriteLine($"      c) The section may be empty-by-design - confirm with schema owner");
			}
		}
		else
		{
			Console.WriteLine();
			Console.WriteLine("  No edge cases - all elements and sections resolved tags successfully.");
		}

		Console.WriteLine();
		Console.WriteLine("====================================================================");
		Console.WriteLine();
	}

	private static string SuggestKey(string prefix, ContainerPattern pattern) => prefix switch
	{
		"Dep" => "D",
		"TimeDep" => "T",
		"Ln" => "L",
		"SafeDep" => "B",
		"Trck" => "C",
		"Cust" => "Cust",
		_ => prefix,
	};

	private static string SuggestDisplayName(string prefix) => prefix switch
	{
		"Dep" => "Deposit/Checking/Savings",
		"TimeDep" => "Time Deposit/CD",
		"Ln" => "Loan",
		"SafeDep" => "Safe Deposit Box",
		"Trck" => "Collection/Trust",
		"Cust" => "Customer",
		"DocElecRcpt" => "Document Electronic Receipt",
		_ => prefix,
	};
}
