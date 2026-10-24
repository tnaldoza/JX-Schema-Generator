namespace JXSchemaGenerator.Model;

/// <summary>
/// One container of a modify request and the sections that can be changed inside it
/// </summary>
/// <param name="Name">The container's element name, or <see langword="null"/> when the sections sit directly under the request root</param>
/// <param name="Sections">The section element names in XSD order</param>
public sealed record ModifyContainer(string? Name, IReadOnlyList<string> Sections);
