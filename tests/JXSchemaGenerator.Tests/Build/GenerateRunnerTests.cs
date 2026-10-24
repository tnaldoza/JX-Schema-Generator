using JXSchemaGenerator.Build;
using JXSchemaGenerator.Emit;

namespace JXSchemaGenerator.Tests.Build;

public sealed class GenerateRunnerTests : IDisposable
{
	private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("jx-runner-");
	private readonly StringWriter _log = new();
	private readonly string _input;
	private readonly string _output;

	public GenerateRunnerTests()
	{
		_input = Directory.CreateDirectory(Path.Combine(_root.FullName, "in")).FullName;
		_output = Path.Combine(_root.FullName, "out");
		File.Copy(Path.Combine(SampleMaster.Folder, SampleMaster.FileName), Path.Combine(_input, SampleMaster.FileName));
	}

	public void Dispose()
	{
		_log.Dispose();
		_root.Delete(recursive: true);
	}

	[Fact]
	public void RunWritesOneFilePerMasterAndTheFormatSchema()
	{
		var exitCode = GenerateRunner.Run(_input, null, _output, _log);

		Assert.Equal(0, exitCode);
		Assert.True(File.Exists(Path.Combine(_output, "sample.json")));
		Assert.True(File.Exists(Path.Combine(_output, MasterJsonWriter.SchemaFileName)));
		Assert.Contains("TPG_SampleMaster.xsd: 9 operations", _log.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public void RunWarnsAboutStaleJsonFilesWithoutDeletingThem()
	{
		Directory.CreateDirectory(_output);
		var stale = Path.Combine(_output, "DepositInquiryElements.json");
		File.WriteAllText(stale, "{}");

		var exitCode = GenerateRunner.Run(_input, null, _output, _log);

		Assert.Equal(0, exitCode);
		Assert.Contains("stale file DepositInquiryElements.json", _log.ToString(), StringComparison.Ordinal);
		Assert.True(File.Exists(stale));
	}

	[Fact]
	public void RunReportsAFailingMasterAndStillWritesTheOthers()
	{
		File.WriteAllText(Path.Combine(_input, "TPG_BrokenMaster.xsd"), "<not-a-schema");

		var exitCode = GenerateRunner.Run(_input, null, _output, _log);

		Assert.Equal(1, exitCode);
		Assert.True(File.Exists(Path.Combine(_output, "sample.json")));
		Assert.Contains("TPG_BrokenMaster.xsd: FAILED", _log.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public void RunFailsTheSecondMasterThatMapsToTheSameOutputName()
	{
		File.Copy(Path.Combine(SampleMaster.Folder, SampleMaster.FileName), Path.Combine(_input, "SampleMaster.xsd"));

		var exitCode = GenerateRunner.Run(_input, null, _output, _log);

		Assert.Equal(1, exitCode);
		Assert.StartsWith("SampleMaster.xsd: 9 operations", _log.ToString(), StringComparison.Ordinal);
		Assert.Contains("TPG_SampleMaster.xsd: FAILED sample.json is already the output of SampleMaster.xsd", _log.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public void RunFailsWhenTheFolderHasNoMasters()
	{
		File.Delete(Path.Combine(_input, SampleMaster.FileName));

		Assert.Equal(1, GenerateRunner.Run(_input, null, _output, _log));
	}

	[Fact]
	public void RunRejectsARootThatClimbsOutOfTheInputFolder() => AssertRootIsRejected(Path.Combine("..", "elsewhere", SampleMaster.FileName));

	[Fact]
	public void RunRejectsARootedRoot() => AssertRootIsRejected(Path.Combine(_root.FullName, "elsewhere", SampleMaster.FileName));

	private void AssertRootIsRejected(string rootXsd)
	{
		var elsewhere = Directory.CreateDirectory(Path.Combine(_root.FullName, "elsewhere")).FullName;
		File.Copy(Path.Combine(SampleMaster.Folder, SampleMaster.FileName), Path.Combine(elsewhere, SampleMaster.FileName));
		var before = Entries();

		var exitCode = GenerateRunner.Run(_input, rootXsd, _output, _log);

		Assert.Equal(1, exitCode);
		Assert.Contains("must be a file name inside", _log.ToString(), StringComparison.Ordinal);
		Assert.False(Directory.Exists(_output));
		Assert.Equal(before, Entries());
	}

	private string[] Entries() => [.. Directory.GetFileSystemEntries(_root.FullName, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];
}
