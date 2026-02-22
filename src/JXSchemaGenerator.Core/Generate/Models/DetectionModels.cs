using System.Xml.Schema;

namespace JXSchemaGenerator.Generate.Models;

public enum ContainerPattern
{
	MultiAcctInqRec,    // e.g. DepAcctInqRec_CType
	SingleInqRs,        // e.g. CustInqRs_MType
	MultiAcctModRq,     // e.g. DepAcctModRq_MType
	SingleModRq,        // e.g. CustModRq_MType
}

public enum TagResolutionStrategy
{
	None = 0,           // all strategies failed - tags will be empty
	DirectType = 1,     // ElementSchemaType.QualifiedName (compiler resolved)
	SchemaTypeName = 2, // SchemaTypeName attribute (explicit type ref)
	Convention = 3,     // JXChange naming convention: Foo_CType / Foo_AType
}

public sealed class ContainerResult
{
	public string ContainerName { get; set; } = "";
	public string Namespace { get; set; } = "";
	public List<XmlSchemaElement> XChildren { get; set; } = new();
	public string? ModStructureName { get; set; }
	public List<XmlSchemaElement> ModSectionElements { get; set; } = new();
}

public sealed class XElementValidation
{
	public string ElementName { get; set; } = "";
	public TagResolutionStrategy Strategy { get; set; }
	public string? ResolvedTypeName { get; set; }
	public int TagCount { get; set; }
	public bool IsEmpty => TagCount == 0;
}

public sealed class ModSectionValidation
{
	public string SectionName { get; set; } = "";
	public TagResolutionStrategy Strategy { get; set; }
	public string? ResolvedTypeName { get; set; }
	public int TagCount { get; set; }
	public bool IsEmpty => TagCount == 0;
}

public sealed class InquiryDetectionEntry
{
	public string ContainerName { get; set; } = "";
	public List<string> ContainerNames { get; set; } = new();
	public string DetectedPrefix { get; set; } = "";
	public ContainerPattern Pattern { get; set; }
	public List<XmlSchemaElement> XElements { get; set; } = new();
	public List<XElementValidation> Validations { get; set; } = new();
}

public sealed class ModificationDetectionEntry
{
	public string ContainerName { get; set; } = "";
	public List<string> ContainerNames { get; set; } = new();
	public string DetectedPrefix { get; set; } = "";
	public ContainerPattern Pattern { get; set; }
	public string? ModStructureName { get; set; }
	public List<ModSectionValidation> SectionValidations { get; set; } = new();
}

public sealed class DetectionReport
{
	public string XsdFileName { get; }
	public bool AlreadyRegisteredInquiry { get; set; }
	public bool AlreadyRegisteredMod { get; set; }
	public Dictionary<string, InquiryDetectionEntry> Inquiry { get; } = new();
	public Dictionary<string, ModificationDetectionEntry> Modification { get; } = new();

	public DetectionReport(string xsdFileName) => XsdFileName = xsdFileName;

	public InquiryDetectionEntry GetOrAddInquiry(string key)
	{
		if (!Inquiry.TryGetValue(key, out var e))
			Inquiry[key] = e = new InquiryDetectionEntry { ContainerName = key };
		return e;
	}

	public ModificationDetectionEntry GetOrAddModification(string key)
	{
		if (!Modification.TryGetValue(key, out var e))
			Modification[key] = e = new ModificationDetectionEntry { ContainerName = key };
		return e;
	}
}
