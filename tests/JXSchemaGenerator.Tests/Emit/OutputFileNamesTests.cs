using JXSchemaGenerator.Emit;

namespace JXSchemaGenerator.Tests.Emit;

public sealed class OutputFileNamesTests
{
	[Theory]
	[InlineData("TPG_ACHMaster.xsd", "ach.json")]
	[InlineData("TPG_ENSMaster.xsd", "ens.json")]
	[InlineData("TPG_IMSMaster.xsd", "ims.json")]
	[InlineData("TPG_BillPayMaster.xsd", "billPay.json")]
	[InlineData("TPG_CrCardMaster.xsd", "crCard.json")]
	[InlineData("TPG_BrdCstMaster.xsd", "brdCst.json")]
	[InlineData("TPG_DepositMaster.xsd", "deposit.json")]
	[InlineData("FooMaster.xsd", "foo.json")]
	public void FromMasterNamesTheOutputFile(string master, string expected) => Assert.Equal(expected, OutputFileNames.FromMaster(master));

	[Theory]
	[InlineData("TPG_Master.xsd")]
	[InlineData("Deposit.xsd")]
	public void FromMasterRejectsNamesWithoutAStem(string master) => Assert.Throws<ArgumentException>(() => OutputFileNames.FromMaster(master));
}
