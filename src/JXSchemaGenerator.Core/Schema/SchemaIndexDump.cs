using System.Collections.Generic;

namespace JXSchemaGenerator.Schema;

public sealed record SchemaIndexDump(
	int ComplexTypes,
	int SimpleTypes,
	int GlobalElements,
	List<string> ComplexTypeNames,
	List<string> GlobalElementNames
);
