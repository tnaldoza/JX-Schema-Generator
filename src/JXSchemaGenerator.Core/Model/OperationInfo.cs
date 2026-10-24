using System.Xml.Schema;

namespace JXSchemaGenerator.Model;

public enum OperationKind
{
	Inquiry,
	Search,
	Add,
	Modify,
	Validate,
	Other,
}

public sealed record OperationInfo(
	string Name,
	OperationKind Kind,
	XmlSchemaElement RequestElement,
	XmlSchemaComplexType RequestType,
	XmlSchemaElement? ResponseElement,
	XmlSchemaComplexType? ResponseType
);
