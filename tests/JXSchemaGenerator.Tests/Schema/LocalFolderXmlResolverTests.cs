using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Tests.Schema;

public sealed class LocalFolderXmlResolverTests : IDisposable
{
	private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("jx-resolver-");
	private readonly string _schemas;
	private readonly LocalFolderXmlResolver _resolver;

	public LocalFolderXmlResolverTests()
	{
		_schemas = Directory.CreateDirectory(Path.Combine(_root.FullName, "schemas")).FullName;
		File.WriteAllText(Path.Combine(_schemas, "w3-xml.xsd"), "<schema />");
		File.WriteAllText(Path.Combine(_root.FullName, "outside.xsd"), "<schema />");
		_resolver = new LocalFolderXmlResolver(_schemas);
	}

	public void Dispose() => _root.Delete(recursive: true);

	[Fact]
	public void ResolveUriMapsAnHttpSchemaLocationOntoTheLocalFile()
	{
		var uri = _resolver.ResolveUri(null, "http://www.w3.org/2001/w3-xml.xsd");

		Assert.True(uri.IsFile);
		Assert.Equal(Path.Combine(_schemas, "w3-xml.xsd"), uri.LocalPath);
	}

	[Fact]
	public void GetEntityOpensAFileInsideTheFolder()
	{
		var entity = _resolver.GetEntity(new Uri(Path.Combine(_schemas, "w3-xml.xsd")), null, typeof(Stream));

		using var stream = Assert.IsAssignableFrom<Stream>(entity);
		Assert.True(stream.CanRead);
	}

	[Fact]
	public void GetEntityRefusesANetworkUriWithNoLocalFile()
	{
		Assert.Throws<FileNotFoundException>(() => _resolver.GetEntity(new Uri("https://example.com/missing.xsd"), null, typeof(Stream)));
	}

	[Fact]
	public void GetEntityRefusesAFileOutsideTheFolder()
	{
		var outside = new Uri(Path.Combine(_root.FullName, "outside.xsd"));

		Assert.Throws<FileNotFoundException>(() => _resolver.GetEntity(outside, null, typeof(Stream)));
	}

	[Fact]
	public void GetEntityRefusesAParentRelativeSchemaLocation()
	{
		var baseUri = new Uri(Path.Combine(_schemas, "w3-xml.xsd"));
		var resolved = _resolver.ResolveUri(baseUri, "../outside.xsd");

		Assert.Throws<FileNotFoundException>(() => _resolver.GetEntity(resolved, null, typeof(Stream)));
	}
}
