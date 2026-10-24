namespace JXSchemaGenerator.Model;

/// <summary>
/// One operation of a Master with its element trees and the helpers derived from them
/// </summary>
/// <param name="Name">The operation name, the request type name without its <c>Rq_MType</c> suffix</param>
/// <param name="Kind">The kind read from the operation-name suffix</param>
/// <param name="RequestElement">The global request element's name</param>
/// <param name="ResponseElement">The global response element's name, or <see langword="null"/> when the operation has none</param>
/// <param name="Annotation">The request element's annotation, or else the request type's</param>
/// <param name="Request">The request's root children</param>
/// <param name="Response">The response's root children, or <see langword="null"/> when the operation has none</param>
/// <param name="ExtendedElements">The <c>x_</c> response elements the request can ask for</param>
/// <param name="Records">The repeating response records, empty unless the operation is a search</param>
/// <param name="ModifyContainers">The containers whose sections can be changed, empty unless the operation is a modify</param>
public sealed record OperationModel(
	string Name,
	OperationKind Kind,
	string RequestElement,
	string? ResponseElement,
	Annotation? Annotation,
	IReadOnlyList<ElementNode> Request,
	IReadOnlyList<ElementNode>? Response,
	IReadOnlyList<string> ExtendedElements,
	IReadOnlyList<string> Records,
	IReadOnlyList<ModifyContainer> ModifyContainers
);

/// <summary>
/// Everything one Master file describes, ready to write as one format 2 JSON file
/// </summary>
/// <param name="Master">The Master's XSD file name</param>
/// <param name="TpgVersion">The root schema's <c>version</c> attribute, or <see langword="null"/> when it has none</param>
/// <param name="Operations">The operations sorted by name with <see cref="StringComparer.Ordinal"/></param>
/// <param name="Fault">The root children of the <c>HdrFault</c> element a SOAP fault carries, or <see langword="null"/> when the Master declares none</param>
/// <param name="Types">The named types the operations and the fault depend on, keyed by local name</param>
public sealed record MasterDocument(
	string Master,
	string? TpgVersion,
	IReadOnlyList<OperationModel> Operations,
	IReadOnlyList<ElementNode>? Fault,
	IReadOnlyDictionary<string, TypeEntry> Types
);

/// <summary>
/// A built Master and the warnings raised while building it
/// </summary>
/// <param name="Document">The built Master</param>
/// <param name="Warnings">Each distinct warning once, in the order first raised</param>
public sealed record MasterBuildResult(MasterDocument Document, IReadOnlyList<string> Warnings);
