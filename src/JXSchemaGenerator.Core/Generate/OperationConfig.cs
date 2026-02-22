namespace JXSchemaGenerator.Generate;

/// <summary>
/// Minimal config for operation-schema output files.
/// Operations are discovered automatically from *InqRq_MType / *SrchRq_MType complex types —
/// no ContainerTypeRegex or TypeToEntry needed.
/// </summary>
public sealed class OperationConfig
{
	public required string OutputFileName { get; init; }
}

/// <summary>
/// Registry mapping root XSD file names to inquiry-operation output configs.
/// InquiryGenerator discovers all *InqRq_MType types automatically.
/// </summary>
public static class InquiryConfigs
{
	public static OperationConfig? FromRootXsd(string rootXsd) =>
		Path.GetFileName(rootXsd).ToLowerInvariant() switch
		{
			"tpg_achmaster.xsd" => new() { OutputFileName = "achInquiryElements.json" },
			"tpg_billpaymaster.xsd" => new() { OutputFileName = "billPayInquiryElements.json" },
			"tpg_crcardmaster.xsd" => new() { OutputFileName = "crCardInquiryElements.json" },
			"tpg_customermaster.xsd" => new() { OutputFileName = "customerInquiryElements.json" },
			"tpg_custommaster.xsd" => new() { OutputFileName = "customInquiryElements.json" },
			"tpg_depositmaster.xsd" => new() { OutputFileName = "depositInquiryElements.json" },
			"tpg_ensmaster.xsd" => new() { OutputFileName = "ensInquiryElements.json" },
			"tpg_imagemaster.xsd" => new() { OutputFileName = "imageInquiryElements.json" },
			"tpg_imsmaster.xsd" => new() { OutputFileName = "imsInquiryElements.json" },
			"tpg_inquirymaster.xsd" => new() { OutputFileName = "inquiryElements.json" },
			"tpg_loanmaster.xsd" => new() { OutputFileName = "loanInquiryElements.json" },
			"tpg_tellermaster.xsd" => new() { OutputFileName = "tellerInquiryElements.json" },
			"tpg_wiremaster.xsd" => new() { OutputFileName = "wireInquiryElements.json" },
			_ => null,
		};
}

/// <summary>
/// Registry mapping root XSD file names to add-operation output configs.
/// AddGenerator discovers all *AddRq_MType types automatically.
/// </summary>
public static class AddOpConfigs
{
	public static OperationConfig? FromRootXsd(string rootXsd) =>
		Path.GetFileName(rootXsd).ToLowerInvariant() switch
		{
			"tpg_achmaster.xsd"         => new() { OutputFileName = "achAddElements.json" },
			"tpg_billpaymaster.xsd"     => new() { OutputFileName = "billPayAddElements.json" },
			"tpg_crcardmaster.xsd"      => new() { OutputFileName = "crCardAddElements.json" },
			"tpg_customermaster.xsd"    => new() { OutputFileName = "customerAddElements.json" },
			"tpg_custommaster.xsd"      => new() { OutputFileName = "customAddElements.json" },
			"tpg_depositmaster.xsd"     => new() { OutputFileName = "depositAddElements.json" },
			"tpg_ensmaster.xsd"         => new() { OutputFileName = "ensAddElements.json" },
			"tpg_imagemaster.xsd"       => new() { OutputFileName = "imageAddElements.json" },
			"tpg_imsmaster.xsd"         => new() { OutputFileName = "imsAddElements.json" },
			"tpg_loanmaster.xsd"        => new() { OutputFileName = "loanAddElements.json" },
			"tpg_tellermaster.xsd"      => new() { OutputFileName = "tellerAddElements.json" },
			"tpg_transactionmaster.xsd" => new() { OutputFileName = "transactionAddElements.json" },
			"tpg_wiremaster.xsd"        => new() { OutputFileName = "wireAddElements.json" },
			_ => null,
		};
}

/// <summary>
/// Registry mapping root XSD file names to search-operation output configs.
/// SearchGenerator discovers all *SrchRq_MType types automatically.
/// </summary>
public static class SrchOpConfigs
{
	public static OperationConfig? FromRootXsd(string rootXsd) =>
		Path.GetFileName(rootXsd).ToLowerInvariant() switch
		{
			"tpg_achmaster.xsd" => new() { OutputFileName = "achSearchElements.json" },
			"tpg_billpaymaster.xsd" => new() { OutputFileName = "billPaySearchElements.json" },
			"tpg_brdcstmaster.xsd" => new() { OutputFileName = "brdCstSearchElements.json" },
			"tpg_crcardmaster.xsd" => new() { OutputFileName = "crCardSearchElements.json" },
			"tpg_customermaster.xsd" => new() { OutputFileName = "customerSearchElements.json" },
			"tpg_custommaster.xsd" => new() { OutputFileName = "customSearchElements.json" },
			"tpg_depositmaster.xsd" => new() { OutputFileName = "depositSearchElements.json" },
			"tpg_ensmaster.xsd" => new() { OutputFileName = "ensSearchElements.json" },
			"tpg_imagemaster.xsd" => new() { OutputFileName = "imageSearchElements.json" },
			"tpg_imsmaster.xsd" => new() { OutputFileName = "imsSearchElements.json" },
			"tpg_inquirymaster.xsd" => new() { OutputFileName = "inquirySearchElements.json" },
			"tpg_loanmaster.xsd" => new() { OutputFileName = "loanSearchElements.json" },
			"tpg_tellermaster.xsd" => new() { OutputFileName = "tellerSearchElements.json" },
			"tpg_wiremaster.xsd" => new() { OutputFileName = "wireSearchElements.json" },
			"tpg_workflowmaster.xsd" => new() { OutputFileName = "workflowSearchElements.json" },
			_ => null,
		};
}
