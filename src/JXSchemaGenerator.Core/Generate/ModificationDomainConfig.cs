using System.Text.RegularExpressions;

namespace JXSchemaGenerator.Generate;

public sealed record EntityMapping(
	string Key,
	string DisplayName,
	string[] Aliases
);

/// <summary>
/// Describes how to locate modification request containers within a JXChange XSD.
/// Pattern: *AcctModRq_MType -> *Mod_CType -> sections -> tags
/// </summary>
public sealed class ModificationDomainConfig
{
	public required string OutputFileName { get; init; }

	/// <summary>
	/// Regex applied to each ComplexType name. Must capture named group "key".
	/// e.g. ^(?&lt;key&gt;[A-Za-z]+)AcctModRq_MType$
	/// </summary>
	public required Regex ContainerTypeRegex { get; init; }

	/// <summary>
	/// Maps the regex "key" capture to an EntityMapping. Return null to skip.
	/// </summary>
	public required Func<string, EntityMapping?> TypeToEntry { get; init; }

	public string[] OutputOrder { get; init; } = [];

	// Elements inside *ModRq_MType to skip - infrastructure, not mod sections
	public static readonly HashSet<string> SkippedRqElements =
		new(StringComparer.OrdinalIgnoreCase)
		{
			"MsgRqHdr", "ErrOvrRdInfoArray", "AccountId", "Dlt",
			"Custom", "ActIntentKey", "ActIntent",
		};
}

public static class ModificationDomainConfigs
{
	// ACH: ACHCompModRq_MType, ACHFltrModRq_MType
	public static readonly ModificationDomainConfig ACH = new()
	{
		OutputFileName = "achModificationElements.json",
		ContainerTypeRegex = new Regex(@"^(?<key>ACHComp|ACHFltr)ModRq_MType$", RegexOptions.Compiled),
		OutputOrder = ["ACHComp", "ACHFltr"],
		TypeToEntry = key => key switch
		{
			"ACHComp" => new("ACHComp", "ACH Company", []),
			"ACHFltr" => new("ACHFltr", "ACH Filter", []),
			_ => null,
		},
	};

	// BillPay: BilPayPayeeModRq_MType, BilPaySchedPmtModRq_MType,
	//          BilPaySubModRq_MType, BilPayElecBilSchedModRq_MType
	public static readonly ModificationDomainConfig BillPay = new()
	{
		OutputFileName = "billPayModificationElements.json",
		ContainerTypeRegex = new Regex(
			@"^(?<key>BilPayPayee|BilPaySchedPmt|BilPaySub|BilPayElecBilSched)ModRq_MType$",
			RegexOptions.Compiled),
		OutputOrder = ["BilPayPayee", "BilPaySchedPmt", "BilPaySub", "BilPayElecBilSched"],
		TypeToEntry = key => key switch
		{
			"BilPayPayee" => new("BilPayPayee", "Bill Pay Payee", []),
			"BilPaySchedPmt" => new("BilPaySchedPmt", "Bill Pay Scheduled Payment", []),
			"BilPaySub" => new("BilPaySub", "Bill Pay Subscription", []),
			"BilPayElecBilSched" => new("BilPayElecBilSched", "Bill Pay Elec Bil Schedule", []),
			_ => null,
		},
	};

	// CrCard: CrCardStmtModRq_MType, CrCardAutoPayModRq_MType
	public static readonly ModificationDomainConfig CrCard = new()
	{
		OutputFileName = "crCardModificationElements.json",
		ContainerTypeRegex = new Regex(@"^(?<key>CrCardStmt|CrCardAutoPay)ModRq_MType$", RegexOptions.Compiled),
		OutputOrder = ["CrCardStmt", "CrCardAutoPay"],
		TypeToEntry = key => key switch
		{
			"CrCardStmt" => new("CrCardStmt", "Credit Card Statement", []),
			"CrCardAutoPay" => new("CrCardAutoPay", "Credit Card AutoPay", []),
			_ => null,
		},
	};

	// Custom: FarmCrBankRateIdxModRq_MType, WireTrnModRq_MType
	public static readonly ModificationDomainConfig Custom = new()
	{
		OutputFileName = "customModificationElements.json",
		ContainerTypeRegex = new Regex(@"^(?<key>FarmCrBankRateIdx|WireTrn)ModRq_MType$", RegexOptions.Compiled),
		OutputOrder = ["FarmCrBankRateIdx", "WireTrn"],
		TypeToEntry = key => key switch
		{
			"FarmCrBankRateIdx" => new("FarmCrBankRateIdx", "Farm Credit Bank Rate Index", []),
			"WireTrn" => new("WireTrn", "Wire Transaction", []),
			_ => null,
		},
	};

	// Deposit: DepAcctModRq_MType, TimeDepAcctModRq_MType, SafeDepAcctModRq_MType, TrckAcctModRq_MType
	//          AcctSweepModRq_MType, AcctCombStmtModRq_MType, AcctProdOvrrdModRq_MType
	//          TaxPlnModRq_MType, TaxPlnBenfModRq_MType, AcctBenfModRq_MType, AcctAnlysModRq_MType
	public static readonly ModificationDomainConfig Deposit = new()
	{
		OutputFileName = "depositModificationElements.json",
		ContainerTypeRegex = new Regex(
			@"^(?<key>[A-Za-z]+)AcctModRq_MType$|^(?<key>AcctSweep|AcctCombStmt|AcctProdOvrrd|TaxPln|TaxPlnBenf|AcctBenf|AcctAnlys)ModRq_MType$",
			RegexOptions.Compiled),
		OutputOrder = ["D", "T", "B", "C", "AcctSweep", "AcctCombStmt", "AcctProdOvrrd", "TaxPln", "TaxPlnBenf", "AcctBenf", "AcctAnlys"],
		TypeToEntry = key => key switch
		{
			"Dep" => new("D", "Deposit/Checking/Savings", ["S", "X"]),
			"TimeDep" => new("T", "Time Deposit/CD", []),
			"SafeDep" => new("B", "Safe Deposit Box", []),
			"Trck" => new("C", "Collection/Trust", []),
			"AcctSweep" => new("AcctSweep", "Account Sweep", []),
			"AcctCombStmt" => new("AcctCombStmt", "Combined Statement", []),
			"AcctProdOvrrd" => new("AcctProdOvrrd", "Product Override", []),
			"TaxPln" => new("TaxPln", "Tax Plan", []),
			"TaxPlnBenf" => new("TaxPlnBenf", "Tax Plan Beneficiary", []),
			"AcctBenf" => new("AcctBenf", "Account Beneficiary", []),
			"AcctAnlys" => new("AcctAnlys", "Account Analysis", []),
			_ => null,
		},
	};

	// ENS: NotRecipModRq_MType, NotRecipSubModRq_MType,
	//      NotSMSKeyWordModRq_MType, NotDistGroupModRq_MType
	public static readonly ModificationDomainConfig ENS = new()
	{
		OutputFileName = "ensModificationElements.json",
		ContainerTypeRegex = new Regex(
			@"^(?<key>NotRecipSub|NotRecip|NotSMSKeyWord|NotDistGroup)ModRq_MType$",
			RegexOptions.Compiled),
		OutputOrder = ["NotRecip", "NotRecipSub", "NotSMSKeyWord", "NotDistGroup"],
		TypeToEntry = key => key switch
		{
			"NotRecip" => new("NotRecip", "Notification Recipient", []),
			"NotRecipSub" => new("NotRecipSub", "Notification Recipient Sub", []),
			"NotSMSKeyWord" => new("NotSMSKeyWord", "Notification SMS Keyword", []),
			"NotDistGroup" => new("NotDistGroup", "Notification Dist Group", []),
			_ => null,
		},
	};

	// Image: DocImgModRq_MType, DocElecSigAssigneeModRq_MType
	public static readonly ModificationDomainConfig Image = new()
	{
		OutputFileName = "imageModificationElements.json",
		ContainerTypeRegex = new Regex(@"^(?<key>DocImg|DocElecSigAssignee)ModRq_MType$", RegexOptions.Compiled),
		OutputOrder = ["DocImg", "DocElecSigAssignee"],
		TypeToEntry = key => key switch
		{
			"DocImg" => new("DocImg", "Document Image", []),
			"DocElecSigAssignee" => new("DocElecSigAssignee", "Doc Elec Sig Assignee", []),
			_ => null,
		},
	};

	// IMS: UsrConsmCredModRq_MType, MFAUsrQnAModRq_MType, PmtHubUsrDirModRq_MType,
	//      MFATokenUsrModRq_MType, MFATokenModRq_MType
	public static readonly ModificationDomainConfig IMS = new()
	{
		OutputFileName = "imsModificationElements.json",
		ContainerTypeRegex = new Regex(
			@"^(?<key>UsrConsmCred|MFAUsrQnA|PmtHubUsrDir|MFATokenUsr|MFAToken)ModRq_MType$",
			RegexOptions.Compiled),
		OutputOrder = ["UsrConsmCred", "MFAUsrQnA", "PmtHubUsrDir", "MFATokenUsr", "MFAToken"],
		TypeToEntry = key => key switch
		{
			"UsrConsmCred" => new("UsrConsmCred", "User Consumer Credentials", []),
			"MFAUsrQnA" => new("MFAUsrQnA", "MFA User Q&A", []),
			"PmtHubUsrDir" => new("PmtHubUsrDir", "Payment Hub User Directory", []),
			"MFATokenUsr" => new("MFATokenUsr", "MFA Token User", []),
			"MFAToken" => new("MFAToken", "MFA Token", []),
			_ => null,
		},
	};

	// Loan: LnAcctModRq_MType, plus many flat *ModRq_MType containers
	public static readonly ModificationDomainConfig Loan = new()
	{
		OutputFileName = "loanModificationElements.json",
		ContainerTypeRegex = new Regex(
			@"^(?<key>Ln)AcctModRq_MType$|^(?<key>LnRateSched|LnFee|LnBil|LnRateSwap|LnShdw|MLLMasterRel|MLLTrancheRel|CollatTrack|RealEstateProp|LOCConstInProc|LOC|Escrw|EscrwAgentDistr|FASB91|LnAppRgtr|CrBurInfo|LnPmtSched|LnUnit|LnFeePrtcp)ModRq_MType$",
			RegexOptions.Compiled),
		OutputOrder = ["L", "LnRateSched", "LnFee", "LnBil", "LnRateSwap", "LnShdw", "MLLMasterRel", "MLLTrancheRel",
					   "CollatTrack", "RealEstateProp", "LOC", "LOCConstInProc", "Escrw", "EscrwAgentDistr",
					   "FASB91", "LnAppRgtr", "CrBurInfo", "LnPmtSched", "LnUnit", "LnFeePrtcp"],
		TypeToEntry = key => key switch
		{
			"Ln" => new("L", "Loan", ["O"]),
			"LnRateSched" => new("LnRateSched", "Rate Schedule", []),
			"LnFee" => new("LnFee", "Fee", []),
			"LnBil" => new("LnBil", "Billing", []),
			"LnRateSwap" => new("LnRateSwap", "Rate Swap", []),
			"LnShdw" => new("LnShdw", "Shadow Loan", []),
			"MLLMasterRel" => new("MLLMasterRel", "MLL Master Relationship", []),
			"MLLTrancheRel" => new("MLLTrancheRel", "MLL Tranche Relationship", []),
			"CollatTrack" => new("CollatTrack", "Collateral Track", []),
			"RealEstateProp" => new("RealEstateProp", "Real Estate Property", []),
			"LOC" => new("LOC", "Line of Credit", []),
			"LOCConstInProc" => new("LOCConstInProc", "LOC Construction in Process", []),
			"Escrw" => new("Escrw", "Escrow", []),
			"EscrwAgentDistr" => new("EscrwAgentDistr", "Escrow Agent Distribution", []),
			"FASB91" => new("FASB91", "FASB 91", []),
			"LnAppRgtr" => new("LnAppRgtr", "Loan App Register", []),
			"CrBurInfo" => new("CrBurInfo", "Credit Bureau Info", []),
			"LnPmtSched" => new("LnPmtSched", "Loan Payment Schedule", []),
			"LnUnit" => new("LnUnit", "Loan Unit", []),
			"LnFeePrtcp" => new("LnFeePrtcp", "Loan Fee Participant", []),
			_ => null,
		},
	};

	// Customer: CustModRq_MType and many other flat *ModRq_MType containers
	public static readonly ModificationDomainConfig Customer = new()
	{
		OutputFileName = "customerModificationElements.json",
		ContainerTypeRegex = new Regex(
			@"^(?<key>Addr|CustRel|Cust|IdVerify|CustMsg|EFTCard|IntnetFinInstIdUsr|IntnetFinInstId|PersonName|CRMEvent|CRMRefer|CRMAct|IntRate|CRMProsp|CardAlrtSub|LobbyQue|EFTCardMsg|TeaserRate|InstDefFld|CRMClientRelData|EFTCardAwards|ColQue|CRMRmnd|CustIdRel|Col)ModRq_MType$",
			RegexOptions.Compiled),
		OutputOrder = ["Cust", "Addr", "CustRel", "IdVerify", "CustMsg",
					   "EFTCard", "EFTCardMsg", "EFTCardAwards",
					   "IntnetFinInstId", "IntnetFinInstIdUsr",
					   "PersonName", "IntRate",
					   "CRMClientRelData", "CRMEvent", "CRMRefer", "CRMAct", "CRMProsp", "CRMRmnd",
					   "CardAlrtSub", "LobbyQue", "TeaserRate", "InstDefFld",
					   "ColQue", "CustIdRel", "Col"],
		TypeToEntry = key => key switch
		{
			"Cust" => new("Cust", "Customer", []),
			"Addr" => new("Addr", "Address", []),
			"CustRel" => new("CustRel", "Customer Relationship", []),
			"IdVerify" => new("IdVerify", "ID Verify", []),
			"CustMsg" => new("CustMsg", "Customer Message", []),
			"EFTCard" => new("EFTCard", "EFT Card", []),
			"EFTCardMsg" => new("EFTCardMsg", "EFT Card Message", []),
			"EFTCardAwards" => new("EFTCardAwards", "EFT Card Awards", []),
			"IntnetFinInstId" => new("IntnetFinInstId", "Internet Fin Inst ID", []),
			"IntnetFinInstIdUsr" => new("IntnetFinInstIdUsr", "Internet Fin Inst ID User", []),
			"PersonName" => new("PersonName", "Person Name", []),
			"IntRate" => new("IntRate", "Interest Rate", []),
			"CRMClientRelData" => new("CRMClientRelData", "CRM Client Rel Data", []),
			"CRMEvent" => new("CRMEvent", "CRM Event", []),
			"CRMRefer" => new("CRMRefer", "CRM Referral", []),
			"CRMAct" => new("CRMAct", "CRM Activity", []),
			"CRMProsp" => new("CRMProsp", "CRM Prospect", []),
			"CRMRmnd" => new("CRMRmnd", "CRM Reminder", []),
			"CardAlrtSub" => new("CardAlrtSub", "Card Alert Subscription", []),
			"LobbyQue" => new("LobbyQue", "Lobby Queue", []),
			"TeaserRate" => new("TeaserRate", "Teaser Rate", []),
			"InstDefFld" => new("InstDefFld", "Inst Defined Field", []),
			"ColQue" => new("ColQue", "Collections Queue", []),
			"CustIdRel" => new("CustIdRel", "Customer ID Relationship", []),
			"Col" => new("Col", "Collections", []),
			_ => null,
		},
	};

	// Teller: TellerTrnModRq_MType, TellerCurrTrnModRq_MType,
	//         TellerDrwModRq_MType, TellerNonCustModRq_MType
	public static readonly ModificationDomainConfig Teller = new()
	{
		OutputFileName = "tellerModificationElements.json",
		ContainerTypeRegex = new Regex(
			@"^(?<key>TellerTrn|TellerCurrTrn|TellerDrw|TellerNonCust)ModRq_MType$",
			RegexOptions.Compiled),
		OutputOrder = ["TellerTrn", "TellerCurrTrn", "TellerDrw", "TellerNonCust"],
		TypeToEntry = key => key switch
		{
			"TellerTrn" => new("TellerTrn", "Teller Transaction", []),
			"TellerCurrTrn" => new("TellerCurrTrn", "Teller Currency Transaction", []),
			"TellerDrw" => new("TellerDrw", "Teller Drawer", []),
			"TellerNonCust" => new("TellerNonCust", "Teller Non-Customer", []),
			_ => null,
		},
	};

	// Transaction: flat *ModRq_MType containers
	public static readonly ModificationDomainConfig Transaction = new()
	{
		OutputFileName = "transactionModificationElements.json",
		ContainerTypeRegex = new Regex(
			@"^(?<key>Xfer|AcctNSFTrn|StopChk|PromoXfer|Trn|AcctAnlysTrn|SafeDepPmt|PosPayItem|SvcFeeTrn|AcctReconItem|PmtHubCrXfer|PmtHubPmtRq|AcctExcTrn)ModRq_MType$",
			RegexOptions.Compiled),
		OutputOrder = ["Xfer", "Trn", "AcctAnlysTrn", "AcctNSFTrn", "SafeDepPmt",
					   "PosPayItem", "SvcFeeTrn", "AcctReconItem",
					   "PmtHubCrXfer", "PmtHubPmtRq", "AcctExcTrn",
					   "StopChk", "PromoXfer"],
		TypeToEntry = key => key switch
		{
			"Xfer" => new("Xfer", "Transfer", []),
			"Trn" => new("Trn", "Transaction", []),
			"AcctAnlysTrn" => new("AcctAnlysTrn", "Account Analysis Transaction", []),
			"AcctNSFTrn" => new("AcctNSFTrn", "NSF Transaction", []),
			"SafeDepPmt" => new("SafeDepPmt", "Safe Deposit Payment", []),
			"PosPayItem" => new("PosPayItem", "Positive Pay Item", []),
			"SvcFeeTrn" => new("SvcFeeTrn", "Service Fee Transaction", []),
			"AcctReconItem" => new("AcctReconItem", "Account Reconciliation Item", []),
			"PmtHubCrXfer" => new("PmtHubCrXfer", "Payment Hub Credit Xfer", []),
			"PmtHubPmtRq" => new("PmtHubPmtRq", "Payment Hub Payment Request", []),
			"AcctExcTrn" => new("AcctExcTrn", "Account Exception Transaction", []),
			"StopChk" => new("StopChk", "Stop Check", []),
			"PromoXfer" => new("PromoXfer", "Promo Transfer", []),
			_ => null,
		},
	};

	// Wire: multiple flat *ModRq_MType containers
	public static readonly ModificationDomainConfig Wire = new()
	{
		OutputFileName = "wireModificationElements.json",
		ContainerTypeRegex = new Regex(
			@"^(?<key>WireTmplt|WireTrnISO|WireExcTrn|WireDrwdwnTrnReply|WireDrwdwnTrn|WireTrnISOTmplt|WireTrnFinInstISOTmplt|WireDrwdwnTrnTmplt|WireInvstg|WirePmtRet|WireTrnRetISO|WireTrnStatusISO|WireTrnFinInstISO)ModRq_MType$",
			RegexOptions.Compiled),
		OutputOrder = ["WireTmplt", "WireTrnISO", "WireTrnFinInstISO", "WireExcTrn",
					   "WireDrwdwnTrn", "WireDrwdwnTrnReply",
					   "WireTrnISOTmplt", "WireTrnFinInstISOTmplt", "WireDrwdwnTrnTmplt",
					   "WireInvstg", "WirePmtRet", "WireTrnRetISO", "WireTrnStatusISO"],
		TypeToEntry = key => key switch
		{
			"WireTmplt" => new("WireTmplt", "Wire Template", []),
			"WireTrnISO" => new("WireTrnISO", "Wire Transaction ISO", []),
			"WireTrnFinInstISO" => new("WireTrnFinInstISO", "Wire Trn Financial Institution ISO", []),
			"WireExcTrn" => new("WireExcTrn", "Wire Exception Transaction", []),
			"WireDrwdwnTrn" => new("WireDrwdwnTrn", "Wire Drawdown Transaction", []),
			"WireDrwdwnTrnReply" => new("WireDrwdwnTrnReply", "Wire Drawdown Trn Reply", []),
			"WireTrnISOTmplt" => new("WireTrnISOTmplt", "Wire Trn ISO Template", []),
			"WireTrnFinInstISOTmplt" => new("WireTrnFinInstISOTmplt", "Wire Trn Fin Inst ISO Template", []),
			"WireDrwdwnTrnTmplt" => new("WireDrwdwnTrnTmplt", "Wire Drawdown Trn Template", []),
			"WireInvstg" => new("WireInvstg", "Wire Investigation", []),
			"WirePmtRet" => new("WirePmtRet", "Wire Payment Return", []),
			"WireTrnRetISO" => new("WireTrnRetISO", "Wire Transaction Return ISO", []),
			"WireTrnStatusISO" => new("WireTrnStatusISO", "Wire Transaction Status ISO", []),
			_ => null,
		},
	};

	public static ModificationDomainConfig? FromRootXsd(string rootXsd) =>
		Path.GetFileName(rootXsd).ToLowerInvariant() switch
		{
			"tpg_achmaster.xsd" => ACH,
			"tpg_billpaymaster.xsd" => BillPay,
			"tpg_crcardmaster.xsd" => CrCard,
			"tpg_custommaster.xsd" => Custom,
			"tpg_depositmaster.xsd" => Deposit,
			"tpg_ensmaster.xsd" => ENS,
			"tpg_imagemaster.xsd" => Image,
			"tpg_imsmaster.xsd" => IMS,
			"tpg_loanmaster.xsd" => Loan,
			"tpg_customermaster.xsd" => Customer,
			"tpg_tellermaster.xsd" => Teller,
			"tpg_transactionmaster.xsd" => Transaction,
			"tpg_wiremaster.xsd" => Wire,
			_ => null,
		};
}
