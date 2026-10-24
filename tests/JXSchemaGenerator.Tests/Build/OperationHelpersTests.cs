using JXSchemaGenerator.Build;
using JXSchemaGenerator.Model;

namespace JXSchemaGenerator.Tests.Build;

public sealed class OperationHelpersTests
{
	[Fact]
	public void ExtendedElementsListsXElementsWithoutDescendingIntoThem()
	{
		string[] expected = ["x_DepInfoRec", "x_MailAddr", "x_Misc"];

		Assert.Equal(expected, OperationHelpers.ExtendedElements(SampleMaster.Request("AcctInq"), SampleMaster.Response("AcctInq")));
	}

	[Fact]
	public void ExtendedElementsFindsXElementsInsideSearchRecords()
	{
		string[] expected = ["x_Bal"];

		Assert.Equal(expected, OperationHelpers.ExtendedElements(SampleMaster.Request("AcctSrch"), SampleMaster.Response("AcctSrch")));
	}

	[Fact]
	public void ExtendedElementsNeedsIncXtendElemArrayInTheRequest() =>
		Assert.Empty(OperationHelpers.ExtendedElements(SampleMaster.Request("AcctAdd"), SampleMaster.Response("AcctInq")));

	[Fact]
	public void ExtendedElementsIsEmptyWithoutAResponse() => Assert.Empty(OperationHelpers.ExtendedElements(SampleMaster.Request("AcctInq"), null));

	[Fact]
	public void RecordsNamesEveryRepeatingRecordUnderTheResponseRoot()
	{
		IReadOnlyList<ElementNode> response =
		[
			new("Key") { BuiltInType = "string" },
			new("Item") { Repeats = true },
			new("RecArray") { Children = [new("Rec") { Repeats = true }] },
			new("DocArray") { Children = [new("Doc") { Repeats = true }] },
		];

		string[] expected = ["Item", "RecArray/Rec", "DocArray/Doc"];
		Assert.Equal(expected, OperationHelpers.Records([], response));
	}

	[Fact]
	public void RecordsLeaveOutListsTheRequestSendsBack()
	{
		string[] expected = ["AcctSrchRecArray/AcctSrchRec"];

		Assert.Equal(expected, OperationHelpers.Records(SampleMaster.Request("AcctSrch"), SampleMaster.Response("AcctSrch")));
	}

	[Fact]
	public void RecordsLeaveOutXElements()
	{
		IReadOnlyList<ElementNode> response =
		[
			new("RecArray") { Children = [new("Rec") { Repeats = true }] },
			new("x_ErrArray") { Children = [new("ErrInfo") { Repeats = true }] },
			new("Info") { Children = [new("x_Line") { Repeats = true }] },
		];

		string[] expected = ["RecArray/Rec"];
		Assert.Equal(expected, OperationHelpers.Records([], response));
	}

	[Fact]
	public void RecordsFallBackPastListsTheRequestSendsBack()
	{
		IReadOnlyList<ElementNode> request = [new("Filter")];
		IReadOnlyList<ElementNode> response =
		[
			new("Filter") { Children = [new("Mid") { Children = [new("Item") { Repeats = true }] }] },
			new("Info") { Children = [new("Mid") { Children = [new("Rec") { Repeats = true }] }] },
		];

		string[] expected = ["Info/Mid/Rec"];
		Assert.Equal(expected, OperationHelpers.Records(request, response));
	}

	[Fact]
	public void RecordsFallBackPastXElements()
	{
		IReadOnlyList<ElementNode> response =
		[
			new("x_Extra") { Children = [new("Mid") { Children = [new("Item") { Repeats = true }] }] },
			new("Info")
			{
				Children = [new("Mid") { Children = [new("x_Mid") { Children = [new("Line") { Repeats = true }] }, new("Rec") { Repeats = true }] }],
			},
		];

		string[] expected = ["Info/Mid/Rec"];
		Assert.Equal(expected, OperationHelpers.Records([], response));
	}

	[Fact]
	public void RecordsIsEmptyWithoutAResponse() => Assert.Empty(OperationHelpers.Records([], null));

	[Fact]
	public void RecordsFallBackToTheShallowestRepeatingElementsBelowTheSecondLevel()
	{
		IReadOnlyList<ElementNode> response =
		[
			new("SrchInfo")
			{
				Children =
				[
					new("Key") { BuiltInType = "string" },
					new("RecArray") { Children = [new("Rec") { Repeats = true, Children = [new("Line") { Repeats = true }] }] },
					new("NoteArray") { Children = [new("Note") { Repeats = true }] },
				],
			},
			new("Tail") { Children = [new("Mid") { Children = [new("Deep") { Children = [new("Deepest") { Repeats = true }] }] }] },
		];

		string[] expected = ["SrchInfo/RecArray/Rec", "SrchInfo/NoteArray/Note"];
		Assert.Equal(expected, OperationHelpers.Records([], response));
	}

	[Fact]
	public void RecordsIgnoreDeeperRepeatingElementsWhenTheFirstTwoLevelsHaveOne()
	{
		IReadOnlyList<ElementNode> response =
		[
			new("RecArray") { Children = [new("Rec") { Repeats = true }] },
			new("Info") { Children = [new("Mid") { Children = [new("Deep") { Repeats = true }] }] },
		];

		string[] expected = ["RecArray/Rec"];
		Assert.Equal(expected, OperationHelpers.Records([], response));
	}

	[Fact]
	public void RecordsIsEmptyWhenNothingRepeats() =>
		Assert.Empty(OperationHelpers.Records([], [new("Info") { Children = [new("Mid") { Children = [new("Deep") { BuiltInType = "string" }] }] }]));

	[Fact]
	public void ModifyContainersSplitMultiContainerRequests()
	{
		var containers = OperationHelpers.ModifyContainers(SampleMaster.Request("AcctMod"));

		string[] names = ["DepMod", "TimeDepMod", "LnMod"];
		Assert.Equal(names, containers.Select(c => c.Name!));
		string[] dep = ["DepInfoRec", "EmailArray"];
		Assert.Equal(dep, containers[0].Sections);
		string[] timeDep = ["TimeDepInfoRec"];
		Assert.Equal(timeDep, containers[1].Sections);
		string[] loan = ["LnInfoRec"];
		Assert.Equal(loan, containers[2].Sections);
	}

	[Fact]
	public void ModifyContainersFallBackToOneUnnamedContainer()
	{
		var container = Assert.Single(OperationHelpers.ModifyContainers(SampleMaster.Request("CustMod")));

		Assert.Null(container.Name);
		string[] sections = ["CustDetail", "RegDetail"];
		Assert.Equal(sections, container.Sections);
	}

	[Fact]
	public void ModifyContainersLeaveModificationRemarksOutOfTheSections()
	{
		IReadOnlyList<ElementNode> request =
		[
			new("RecId") { BuiltInType = "string" },
			new("RecInfo"),
			new("Dlt") { BuiltInType = "string" },
			new("ModRmkArray"),
			new("Custom"),
		];

		var container = Assert.Single(OperationHelpers.ModifyContainers(request));

		string[] sections = ["RecInfo"];
		Assert.Equal(sections, container.Sections);
	}

	[Fact]
	public void SuspectEnvelopeElementsFlagsCommonRootChildrenMissingFromTheList()
	{
		IReadOnlyList<ElementNode>[] requests =
		[
			[new("RqTag"), new("Dlt"), new("AcctMod")],
			[new("RqTag"), new("Dlt")],
			[new("RqTag"), new("Rare")],
			[new("Other")],
		];

		string[] expected = ["RqTag"];
		Assert.Equal(expected, OperationHelpers.SuspectEnvelopeElements(requests));
	}

	[Fact]
	public void SuspectEnvelopeElementsIgnoresSimpleTypedRootChildren()
	{
		IReadOnlyList<ElementNode>[] requests =
		[
			[new("RecKey") { BuiltInType = "string" }, new("RecInfo")],
			[new("RecKey") { BuiltInType = "string" }, new("RecInfo")],
			[new("RecKey") { BuiltInType = "string" }, new("RecInfo")],
		];

		string[] expected = ["RecInfo"];
		Assert.Equal(expected, OperationHelpers.SuspectEnvelopeElements(requests));
	}

	[Fact]
	public void SuspectEnvelopeElementsCountsARepeatedNameOncePerRequest()
	{
		IReadOnlyList<ElementNode>[] requests =
		[
			[new("Twice"), new("Twice")],
			[new("Twice"), new("Twice")],
			[new("Other")],
		];

		Assert.Empty(OperationHelpers.SuspectEnvelopeElements(requests));
	}
}
