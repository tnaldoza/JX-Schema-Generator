namespace JXSchemaGenerator.Model;

public sealed record Deprecation(string? Date, string? Comment);

public sealed record Annotation(
	string? Description,
	IReadOnlyList<string> Values,
	string? Default,
	Deprecation? Deprecated,
	IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Providers,
	IReadOnlyDictionary<string, string> Notes
);
