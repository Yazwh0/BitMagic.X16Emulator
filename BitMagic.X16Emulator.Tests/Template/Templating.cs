using BitMagic.X16Emulator.Snapshot;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Templating;

[TestClass]
public class Templating
{
    [TestMethod]
    public async Task Build()
    {
        var (_, snapshot) = await X16TestHelper.EmulateTemplateChanges(@"
                .machine CommanderX16R40
                .org $810
            
                proc();

                static void proc()
                {
                    lda #$01
                    stp
                }
                ");
        
        snapshot.Compare()
            .Is(Registers.A, 0x01)
            .IgnoreVera()
            .IgnoreVia()
            .AssertNoOtherChanges();
    }
}