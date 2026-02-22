namespace JXSchemaGenerator.Expand;

public sealed class ExpansionOptions
{
	public bool SkipVersionContainers { get; init; } = true;    // skip Ver_1, Ver_2 containers
	public bool SkipAny { get; init; } = true;                  // skip xsd:any
	public bool LeafTagsOnly { get; init; } = true;             // include only leaf element names
	public int MaxDepth { get; init; } = 64;                    // cycle safety / sanity
}
