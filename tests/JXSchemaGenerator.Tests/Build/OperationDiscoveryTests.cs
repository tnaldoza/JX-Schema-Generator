using JXSchemaGenerator.Build;
using JXSchemaGenerator.Model;

namespace JXSchemaGenerator.Tests.Build;

public sealed class OperationDiscoveryTests
{
	[Fact]
	public void DiscoverFindsEveryGlobalRequestElementInOrdinalOrder()
	{
		string[] expected = ["AcctAdd", "AcctAddValidate", "AcctInq", "AcctMod", "AcctSrch", "AppInfoAdd", "CustMod", "NoRsInq", "Ping"];

		Assert.Equal(expected, OperationDiscovery.Discover(SampleMaster.Index, _ => { }).Select(o => o.Name));
	}

	[Theory]
	[InlineData("AcctAdd", OperationKind.Add)]
	[InlineData("AcctAddValidate", OperationKind.Validate)]
	[InlineData("AcctInq", OperationKind.Inquiry)]
	[InlineData("AcctMod", OperationKind.Modify)]
	[InlineData("AcctSrch", OperationKind.Search)]
	[InlineData("AppInfoAdd", OperationKind.Add)]
	[InlineData("Ping", OperationKind.Other)]
	public void DiscoverTakesTheKindFromTheNameSuffix(string name, OperationKind kind) => Assert.Equal(kind, SampleMaster.Operation(name).Kind);

	[Fact]
	public void DiscoverPairsByTypeNameAndKeepsTheWireElementName()
	{
		var operation = SampleMaster.Operation("AppInfoAdd");

		Assert.Equal("AppInfoAddRq", operation.RequestElement.Name);
		Assert.Equal("AppInfoAddResponse", operation.ResponseElement?.Name);
	}

	[Fact]
	public void DiscoverWarnsWhenAnOperationHasNoResponse()
	{
		var warnings = new List<string>();

		var operation = OperationDiscovery.Discover(SampleMaster.Index, warnings.Add).Single(o => o.Name == "NoRsInq");

		Assert.Null(operation.ResponseElement);
		Assert.Null(operation.ResponseType);
		Assert.Contains(warnings, w => w.Contains("NoRsInq", StringComparison.Ordinal));
	}
}
