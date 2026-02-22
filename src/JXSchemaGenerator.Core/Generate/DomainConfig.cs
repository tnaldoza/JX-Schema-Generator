using System.Text.RegularExpressions;

namespace JXSchemaGenerator.Generate;

/// <summary>
/// Describes how to locate and classify x_ extended element containers
/// within a specific JXChange XSD domain (Inquiry, Customer, etc.)
/// </summary>
public sealed class DomainConfig
{
	/// <summary>Output JSON file name, e.g. "inquiryElements.json"</summary>
	public required string OutputFileName { get; init; }

	/// <summary>
	/// Top-level key used in the output JSON for the entity collection.
	/// Defaults to "accountTypes" for backward compatibility.
	/// Use something more meaningful for non-account domains, e.g. "entityTypes".
	/// </summary>
	public string CollectionKeyName { get; init; } = "entityTypes";

	/// <summary>
	/// Regex applied to each ComplexType name.
	/// Must capture a named group "key" used to drive TypeToEntry lookups.
	/// e.g. ^(?&lt;key&gt;[A-Za-z]+)AcctInqRec_CType$
	/// </summary>
	public required Regex ContainerTypeRegex { get; init; }

	/// <summary>
	/// Maps the regex "key" capture -> (outputKey, displayName, aliases).
	/// Return null to skip that container type entirely.
	/// </summary>
	public required Func<string, EntityMapping?> TypeToEntry { get; init; }

	/// <summary>
	/// Desired output key order in the final JSON.
	/// Keys not in this list are appended at the end in discovery order.
	/// </summary>
	public string[] OutputOrder { get; init; } = [];
}

public sealed record EntityMapping(
	string Key,
	string DisplayName,
	string[] Aliases
);

/// <summary>
/// Pre-built configs for each supported XSD domain.
/// </summary>
public static class DomainConfigs
{
	// InquiryMaster: *AcctInqRec_CType containers (Dep, TimeDep, Ln, SafeDep, Trck)
	public static readonly DomainConfig Inquiry = new()
	{
		OutputFileName = "inquiryElements.json",
		ContainerTypeRegex = new Regex(@"^(?<key>[A-Za-z]+)AcctInqRec_CType$", RegexOptions.Compiled),
		OutputOrder = ["D", "T", "L", "B", "C"],
		TypeToEntry = key => key switch
		{
			"Dep"     => new("D", "Deposit/Checking/Savings", ["S", "X"]),
			"TimeDep" => new("T", "Time Deposit/CD",          []),
			"Ln"      => new("L", "Loan",                     ["O"]),
			"SafeDep" => new("B", "Safe Deposit Box",         []),
			"Trck"    => new("C", "Collection/Trust",         []),
			_ => null,
		},
	};

	// InquiryMaster: flat *InqRs_MType containers (TaxPln, Escrw, AcctAnlys, etc.)
	public static readonly DomainConfig InquiryAux = new()
	{
		OutputFileName = "inquiryAuxElements.json",
		ContainerTypeRegex = new Regex(
			@"^(?<key>TaxPln|Escrw|AcctAnlys|AcctExcTrn|ChkRisk|FinInstInfo|LnSvc|LnUnit|AcctBal)InqRs_MType$",
			RegexOptions.Compiled),
		OutputOrder = ["TaxPln", "Escrw", "AcctAnlys", "AcctExcTrn", "AcctBal", "ChkRisk", "FinInstInfo", "LnSvc", "LnUnit"],
		TypeToEntry = key => key switch
		{
			"TaxPln"      => new("TaxPln",      "Tax Plan",                      []),
			"Escrw"       => new("Escrw",       "Escrow",                        []),
			"AcctAnlys"   => new("AcctAnlys",   "Account Analysis",              []),
			"AcctExcTrn"  => new("AcctExcTrn",  "Account Exception Transaction", []),
			"AcctBal"     => new("AcctBal",     "Account Balance History",       []),
			"ChkRisk"     => new("ChkRisk",     "Check Risk",                    []),
			"FinInstInfo" => new("FinInstInfo", "Financial Institution Info",    []),
			"LnSvc"       => new("LnSvc",       "Loan Service",                  []),
			"LnUnit"      => new("LnUnit",      "Loan Unit",                     []),
			_ => null,
		},
	};

	public static readonly DomainConfig Customer = new()
	{
		OutputFileName = "customerElements.json",
		ContainerTypeRegex = new Regex(@"^(?<key>Cust|IntnetFinInstId|PltfmProd)InqRs_MType$", RegexOptions.Compiled),
		OutputOrder = ["Cust", "IntnetFinInstId", "PltfmProd"],
		TypeToEntry = key => key switch
		{
			"Cust"            => new("Cust",            "Customer",       []),
			"IntnetFinInstId" => new("IntnetFinInstId", "IntnetFinInstId", []),
			"PltfmProd"       => new("PltfmProd",       "PltfmProd",      []),
			_ => null,
		},
	};

	public static readonly DomainConfig Image = new()
	{
		OutputFileName = "imageElements.json",
		ContainerTypeRegex = new Regex(@"^(?<key>DocElecRcpt)InqRs_MType$", RegexOptions.Compiled),
		OutputOrder = ["DocElecRcpt"],
		TypeToEntry = key => key switch
		{
			"DocElecRcpt" => new("DocElecRcpt", "Document Electronic Receipt", []),
			_ => null,
		},
	};

	public static readonly DomainConfig BillPay = new()
	{
		OutputFileName = "billPayElements.json",
		ContainerTypeRegex = new Regex(@"^(?<key>BilPay(?:Payee|SchedPmt|PmtHist))InqRs_MType$", RegexOptions.Compiled),
		OutputOrder = ["BilPayPayee", "BilPaySchedPmt", "BilPayPmtHist"],
		TypeToEntry = key => key switch
		{
			"BilPayPayee"    => new("BilPayPayee",    "Bill Pay Payee",             []),
			"BilPaySchedPmt" => new("BilPaySchedPmt", "Bill Pay Scheduled Payment", []),
			"BilPayPmtHist"  => new("BilPayPmtHist",  "Bill Pay Payment History",   []),
			_ => null,
		},
	};

	public static readonly DomainConfig CrCard = new()
	{
		OutputFileName = "crCardElements.json",
		ContainerTypeRegex = new Regex(@"^(?<key>CrCardAcct)InqRs_MType$", RegexOptions.Compiled),
		OutputOrder = ["CrCardAcct"],
		TypeToEntry = key => key switch
		{
			"CrCardAcct" => new("CrCardAcct", "CrCardAcct", []),
			_ => null,
		},
	};

	public static readonly DomainConfig IMS = new()
	{
		OutputFileName = "imsElements.json",
		ContainerTypeRegex = new Regex(@"^(?<key>PmtHubUsrDir)InqRs_MType$", RegexOptions.Compiled),
		OutputOrder = ["PmtHubUsrDir"],
		TypeToEntry = key => key switch
		{
			"PmtHubUsrDir" => new("PmtHubUsrDir", "PmtHubUsrDir", []),
			_ => null,
		},
	};

	/// <summary>
	/// Returns all domain configs that apply to the given root XSD file name.
	/// Most XSDs produce one config; InquiryMaster produces two output files.
	/// Returns null when the XSD is not registered.
	/// </summary>
	public static DomainConfig[]? FromRootXsd(string rootXsd) =>
		Path.GetFileName(rootXsd).ToLowerInvariant() switch
		{
			"tpg_inquirymaster.xsd"  => [Inquiry, InquiryAux],
			"tpg_customermaster.xsd" => [Customer],
			"tpg_imagemaster.xsd"    => [Image],
			"tpg_billpaymaster.xsd"  => [BillPay],
			"tpg_crcardmaster.xsd"   => [CrCard],
			"tpg_imsmaster.xsd"      => [IMS],
			_ => null,
		};
}
