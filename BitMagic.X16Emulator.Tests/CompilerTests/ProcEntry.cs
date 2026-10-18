using BitMagic.Common;
using BitMagic.Compiler;
using BitMagic.Compiler.Files;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

// .entry moves a proc's entry point, eg past .padvars declared at the top of it. Code above it can only be fallen into.
[TestClass]
public class ProcEntry
{
    private const string Header = @"
            .machine CommanderX16R40
            .org $810
";

    private static async Task<CompileResult> Compile(string code)
    {
        var project = new Project { Code = new StaticTextFile(Header + code, "unittest.bmasm") };
        var compiler = new BitMagic.Compiler.Compiler(project, new(), new NullLogger());
        return await compiler.Compile();
    }

    private static async Task<Exception> CompileFails(string code)
    {
        try
        {
            await Compile(code);
        }
        catch (Exception e)
        {
            return e;
        }

        Assert.Fail("Expected the code not to compile.");
        return null!;
    }

    private static int Value(CompileResult result, string fullName)
    {
        foreach (var (name, value) in result.State.Globals.GetChildVariables("App"))
        {
            if (name == fullName)
                return value.Value;
        }

        Assert.Fail($"'{fullName}' isn't defined.");
        return 0;
    }

    // a byte of the assembled code (the output has a 2 byte header, then starts at $801)
    private static byte CodeAt(CompileResult result, int address) => result.Data["Main"].ToArray()[2 + address - 0x801];

    [TestMethod]
    public async Task ForwardCall_LandsOnEntry()
    {
        var result = await Compile(@"
            jsr decompress_loop
            stp
        .proc decompress_loop
            .padvar byte low_count
            .padvar byte high_count
        .entry
            sty high_count
            stx low_count
            rts
        .endproc");

        Assert.AreEqual(0x816, Value(result, "App:Main:decompress_loop"));
        Assert.AreEqual(0x16, CodeAt(result, 0x811));
        Assert.AreEqual(0x08, CodeAt(result, 0x812));
    }

    [TestMethod]
    public async Task BackwardCall_LandsOnEntry()
    {
        var result = await Compile(@"
            jmp start
        .proc decompress_loop
            .padvar byte low_count
            .padvar byte high_count
        .entry
            sty high_count
            rts
        .endproc
        .start:
            jsr decompress_loop
            stp");

        var start = Value(result, "App:Main:start");
        Assert.AreEqual(0x815, Value(result, "App:Main:decompress_loop"));
        Assert.AreEqual(0x15, CodeAt(result, start + 1));
        Assert.AreEqual(0x08, CodeAt(result, start + 2));
    }

    [TestMethod]
    public async Task PadVars_StayInTheProc()
    {
        var result = await Compile(@"
            stp
        .proc decompress_loop
            .padvar byte low_count
            .padvar byte high_count
        .entry
            sty high_count
            stx low_count
            rts
        .endproc");

        Assert.AreEqual(0x811, Value(result, "App:Main:decompress_loop:low_count"));
        Assert.AreEqual(0x812, Value(result, "App:Main:decompress_loop:high_count"));

        // sty high_count, stx low_count
        Assert.AreEqual(0x8c, CodeAt(result, 0x813));
        Assert.AreEqual(0x12, CodeAt(result, 0x814));
        Assert.AreEqual(0x8e, CodeAt(result, 0x816));
        Assert.AreEqual(0x11, CodeAt(result, 0x817));
    }

    [TestMethod]
    public async Task CodeBeforeEntry_IsKeptForFallingInto()
    {
        var result = await Compile(@"
            jmp start
        .proc setup
            lda #1
        .entry
            rts
        .endproc
        .start:
            jsr setup
            stp");

        var start = Value(result, "App:Main:start");
        Assert.AreEqual(0xa9, CodeAt(result, 0x813));
        Assert.AreEqual(0x815, Value(result, "App:Main:setup"));
        Assert.AreEqual(0x15, CodeAt(result, start + 1));
    }

    [TestMethod]
    public async Task OutsideAProc_Fails()
    {
        var e = await CompileFails(@"
            .entry
            stp");

        StringAssert.Contains(e.Message, ".entry can only be used inside a .proc");
    }

    [TestMethod]
    public async Task InAScopeOutsideAProc_Fails()
    {
        var e = await CompileFails(@"
            stp
        .scope lib
            .entry
            rts
        .endscope");

        StringAssert.Contains(e.Message, ".entry can only be used inside a .proc");
    }

    [TestMethod]
    public async Task Twice_Fails()
    {
        var e = await CompileFails(@"
            stp
        .proc p
        .entry
            nop
        .entry
            rts
        .endproc");

        StringAssert.Contains(e.Message, "already has an .entry");
    }

    [TestMethod]
    public async Task TwiceAcrossAReopenedProc_Fails()
    {
        var e = await CompileFails(@"
            stp
        .proc p
        .entry
            rts
        .endproc
        .proc p
        .entry
            rts
        .endproc");

        StringAssert.Contains(e.Message, "already has an .entry");
    }

    [TestMethod]
    public async Task NestedProc_OnlyMovesItsOwnEntry()
    {
        var result = await Compile(@"
            stp
        .proc outer
            .padvar byte tmp
            .proc inner
                .padvar byte tmp2
            .entry
                rts
            .endproc
            rts
        .endproc");

        Assert.AreEqual(0x811, Value(result, "App:Main:outer"));
        Assert.AreEqual(0x813, Value(result, "App:Main:outer:inner"));
    }

    [TestMethod]
    public async Task PrivateProc_StaysPrivate()
    {
        var e = await CompileFails(@"
            stp
        .proc private feed
            .padvar byte tmp
        .entry
            rts
        .endproc
        .scope sound
            jsr Main:feed
        .endscope");

        StringAssert.Contains(e.Message, "is private to");
    }
}
