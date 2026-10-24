namespace JXSchemaGenerator.Emit;

/// <summary>
/// Names the JSON file written for each Master
/// </summary>
public static class OutputFileNames
{
	private const string Prefix = "TPG_";
	private const string Suffix = "Master.xsd";

	/// <summary>
	/// Turns a Master file name such as <c>TPG_BillPayMaster.xsd</c> into its output file name, <c>billPay.json</c>
	/// </summary>
	/// <remarks>
	/// The optional <c>TPG_</c> prefix and the <c>Master.xsd</c> suffix match without regard to case
	/// An all-uppercase stem such as <c>ACH</c> is lowercased whole, any other stem only loses the case of its first character
	/// </remarks>
	/// <param name="masterFileName">The Master's file name, without a folder</param>
	/// <returns>The output file name</returns>
	/// <exception cref="ArgumentException">The name does not end in <c>Master.xsd</c>, or nothing is left once the prefix and suffix are removed</exception>
	public static string FromMaster(string masterFileName)
	{
		if (!masterFileName.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
			throw new ArgumentException($"Master file name {masterFileName} does not end in {Suffix}", nameof(masterFileName));

		var stem = masterFileName[..^Suffix.Length];

		if (stem.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
			stem = stem[Prefix.Length..];

		if (stem.Length == 0)
			throw new ArgumentException($"Master file name {masterFileName} has no name between {Prefix} and {Suffix}", nameof(masterFileName));

		var name = stem.Any(char.IsLower) ? char.ToLowerInvariant(stem[0]) + stem[1..] : stem.ToLowerInvariant();

		return name + ".json";
	}
}
