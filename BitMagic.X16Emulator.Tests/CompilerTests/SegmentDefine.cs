using BitMagic.Compiler;
using BitMagic.Compiler.Files;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

// .segmentdefine defines a segment like .segment, but doesn't switch to it
[TestClass]
public class SegmentDefine
{
    private static async Task<CompileState> Compile(string code)
    {
        var project = new Project { Code = new StaticTextFile(code, "unittest.bmasm") };
        var result = await new BitMagic.Compiler.Compiler(project, new(), new NullLogger()).Compile();
        return result.State;
    }

    [TestMethod]
    public async Task DefinesWithoutSwitching()
    {
        var state = await Compile(@"
.machine CommanderX16R40
.org $810
.segmentdefine data $a000 $100
    lda #1
    stp");

        var data = state.Segments["data"];
        Assert.AreEqual(0xa000, data.StartAddress);
        Assert.AreEqual(0xa000, data.Address);      // nothing went in it
        Assert.AreEqual(0x100, data.MaxSize);
        Assert.AreEqual(0x813, state.Segments["Main"].Address);
    }

    [TestMethod]
    public async Task StaysInTheCurrentSegment()
    {
        var state = await Compile(@"
.machine CommanderX16R40
.org $810
.segment bss $400
.segmentdefine data $a000
    .padvar byte x");

        Assert.AreEqual(0x401, state.Segments["bss"].Address);
        Assert.AreEqual(0xa000, state.Segments["data"].Address);
    }

    [TestMethod]
    public async Task SegmentSwitchesToADefinedSegment()
    {
        var state = await Compile(@"
.machine CommanderX16R40
.org $810
.segmentdefine data $a000
.segment data
    .padvar byte x");

        Assert.AreEqual(0xa001, state.Segments["data"].Address);
    }

    // the scope of the line marked '<-'
    private static async Task<string> ScopeOfMarkedLine(string code)
    {
        var lines = code.Split('\n').Select(i => i.TrimEnd('\r')).ToArray();
        var marked = Array.FindIndex(lines, i => i.Contains("<-")) + 1;
        var state = await Compile(string.Join('\n', lines.Select(i => i.Contains("<-") ? i[..i.IndexOf("<-")] : i)));

        return state.ProcedureAt(marked).Variables.FullName;
    }

    [TestMethod]
    public async Task SegmentDefineRejectsAScope()
    {
        try
        {
            await Compile(@"
.machine CommanderX16R40
.org $810
.segmentdefine data $a000 _ _ sound");
        }
        catch (Exception e)
        {
            StringAssert.Contains(e.Message, "doesn't take a scope");
            return;
        }

        Assert.Fail("Expected the code not to compile.");
    }

    [TestMethod]
    public async Task ScopeOnlyAppliesToItsSwitch()
    {
        var code = @"
.machine CommanderX16R40
.org $810
.segmentdefine data $a000
.segment data scope=sound
    .padvar byte x  {0}
.endsegment
.segment data
    .padvar byte y  {1}";

        Assert.AreEqual("App:sound", await ScopeOfMarkedLine(string.Format(code, "<-", "")));
        Assert.AreEqual("App:Main", await ScopeOfMarkedLine(string.Format(code, "", "<-")));
    }

    [TestMethod]
    public async Task NoScopeIsMain()
    {
        Assert.AreEqual("App:Main", await ScopeOfMarkedLine(@"
.machine CommanderX16R40
.org $810
.segmentdefine data $a000
.segment data
    .padvar byte x  <-"));
    }
}
