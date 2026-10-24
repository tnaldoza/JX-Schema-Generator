using JXSchemaGenerator.Schema;

namespace JXSchemaGenerator.Tests.Schema;

public sealed class SchemaLoaderTests
{
	[Fact]
	public void LoadAndCompileRootReturnsTheRootSchemaAndItsVersion()
	{
		var loaded = SchemaLoader.LoadAndCompileRoot(SampleMaster.Folder, SampleMaster.FileName);

		Assert.Equal(SampleMaster.FileName, loaded.RootFile);
		Assert.Equal("2099.0.01", loaded.RootSchema.Version);
		Assert.True(loaded.SchemaSet.IsCompiled);
	}
}
