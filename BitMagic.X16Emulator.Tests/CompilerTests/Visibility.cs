using BitMagic.Common;
using BitMagic.Compiler;
using BitMagic.Compiler.Exceptions;
using BitMagic.Compiler.Files;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

// public / private on procs and variables, labels private outside their scope, private scopes, and .export.
[TestClass]
public class Visibility
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

    private static IAsmVariable Find(CompileResult result, string fullName)
    {
        foreach (var (name, value) in result.State.Globals.GetChildVariables("App"))
        {
            if (name == fullName)
                return value;
        }

        Assert.Fail($"'{fullName}' isn't defined.");
        return null!;
    }

    private static int Value(CompileResult result, string fullName) => Find(result, fullName).Value;

    // a byte of the assembled code (the output has a 2 byte header, then starts at $801)
    private static byte CodeAt(CompileResult result, int address) => result.Data["Main"].ToArray()[2 + address - 0x801];

    private static bool WarnsAboutLabel(CompileResult result) => result.Warnings.Any(w => w.Contains("is a label"));

    // ---------------------------------------------------------------------------------------
    // Procs, constants and variables

    [TestMethod]
    public async Task Public_ByDefault()
    {
        // (the proc comes first: an immediate operand can't be a forward reference)
        var result = await Compile(@"
            jmp start
        .proc feed
            .const LEVEL 5
        .endproc
        .start:
            lda #feed:LEVEL
            stp");

        Assert.AreEqual(0x05, CodeAt(result, Value(result, "App:Main:start") + 1));
    }

    [TestMethod]
    public async Task PublicKeyword_IsTheDefault()
    {
        var result = await Compile(@"
            jmp start
        .proc public feed
            .const public LEVEL 5
        .endproc
        .start:
            lda #feed:LEVEL
            stp");

        Assert.AreEqual(5, Value(result, "App:Main:feed:LEVEL"));
    }

    [TestMethod]
    public async Task PrivateConst_FromOutside_Fails()
    {
        var e = await CompileFails(@"
            stp
        .proc feed
            .const private LEVEL 5
        .endproc
        .scope sound
            lda #Main:feed:LEVEL
        .endscope");

        StringAssert.Contains(e.Message, "'Main:feed:LEVEL' is private to 'App:Main'");
    }

    [TestMethod]
    public async Task Private_VisibleInOwnProcAndBelow()
    {
        var result = await Compile(@"
            stp
        .proc feed
            .const private LEVEL 5
            .padvar private byte tmp
            lda #LEVEL
            sta tmp
            .proc inner
                lda #LEVEL
                sta tmp
            .endproc
        .endproc");

        Assert.AreEqual(5, Value(result, "App:Main:feed:LEVEL"));
    }

    [TestMethod]
    public async Task PrivateVariableKinds_FromOutside_Fail()
    {
        foreach (var declaration in new[] { ".var private byte x 1", ".padvar private byte x", ".constvar private byte x $22", ".const private x 1" })
        {
            var e = await CompileFails($@"
            stp
        .proc feed
            {declaration}
        .endproc
        .scope sound
            lda Main:feed:x
        .endscope");

            StringAssert.Contains(e.Message, "is private to 'App:Main'", declaration);
        }
    }

    [TestMethod]
    public async Task PrivateProc_VisibleInItsScope()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
            .machine CommanderX16R40
            .org $810
            jsr mix_one
            stp
        .proc private mix_one
            lda #$42
            rts
        .endproc", emulator);

        emulator.AssertState(A: 0x42);
    }

    [TestMethod]
    public async Task PrivateProc_FromAnotherScope_Fails()
    {
        var e = await CompileFails(@"
            stp
        .proc private mix_one
            rts
        .endproc
        .scope sound
        .proc play
            jsr Main:mix_one
        .endproc
        .endscope");

        StringAssert.Contains(e.Message, "is private to 'App:Main'");
    }

    [TestMethod]
    public async Task PrivateProc_HidesItsPublicNames()
    {
        var e = await CompileFails(@"
            stp
        .proc private mixer
            .const LEVEL 5
        .endproc
        .scope sound
        .proc play
            lda #Main:mixer:LEVEL
        .endproc
        .endscope");

        StringAssert.Contains(e.Message, "is private to 'App:Main'");
    }

    [TestMethod]
    public async Task PrivateProc_NeedsAName()
    {
        var e = await CompileFails(@"
        .proc private
        .endproc");

        StringAssert.Contains(e.Message, "private procedure needs a name");
    }

    [TestMethod]
    public async Task PrivateProc_ReopenedPublicKeepsPrivate_ReopenedPrivateFromPublicFails()
    {
        var e = await CompileFails(@"
        .proc feed
        .endproc
        .proc private feed
        .endproc");

        StringAssert.Contains(e.Message, "can't be reopened as private");
    }

    [TestMethod]
    public async Task EndProc_FollowsItsProc()
    {
        var result = await Compile(@"
            lda #<feed:endproc
            stp
        .proc feed
            nop
        .endproc");

        Assert.AreEqual(Value(result, "App:Main:feed:endproc") & 0xff, CodeAt(result, 0x811));

        var e = await CompileFails(@"
            stp
        .proc private feed
            nop
        .endproc
        .scope sound
            lda #<Main:feed:endproc
        .endscope");

        StringAssert.Contains(e.Message, "is private to 'App:Main'");
    }

    [TestMethod]
    public async Task Wildcard_SkipsHiddenMatches()
    {
        var result = await Compile(@"
            stp
        .scope a
            .const private counter 1
        .endscope
        .scope b
            .const counter 2
        .endscope
        .scope c
            .const found App::counter
        .endscope");

        Assert.AreEqual(2, Value(result, "App:c:found"));
    }

    // ---------------------------------------------------------------------------------------
    // Labels and operand labels

    [TestMethod]
    public async Task Label_InsideItsProcAndBelow_NoWarning()
    {
        var result = await Compile(@"
            stp
        .proc feed
        .loop:
            lda sample: $1234
            .proc inner
                jmp loop
                lda sample
            .endproc
            bne loop
        .endproc");

        Assert.IsFalse(WarnsAboutLabel(result), string.Join("\n", result.Warnings));
    }

    [TestMethod]
    public async Task Label_FromOutside_Warns()
    {
        var result = await Compile(@"
            stp
        .proc feed
        .loop:
            stp
        .endproc
        .scope sound
            jmp Main:feed:loop
        .endscope");

        Assert.IsTrue(WarnsAboutLabel(result));
        Assert.AreEqual(1, result.Warnings.Count(w => w.Contains("is a label")), "warned once, not once per pass");
    }

    [TestMethod]
    public async Task OperandLabel_FromOutside_Warns()
    {
        var result = await Compile(@"
            stp
        .proc feed
            lda sample: $1234
        .endproc
        .scope sound
            lda Main:feed:sample
        .endscope");

        Assert.IsTrue(WarnsAboutLabel(result));
    }

    [TestMethod]
    public async Task OperandLabel_TakesNoKeyword()
    {
        var e = await CompileFails(@"
            lda public count: #0
            stp");

        StringAssert.Contains(e.Message, "Operand labels can't be public");
    }

    [TestMethod]
    public async Task OperandLabel_HighByteOfOneByteOperand_Fails()
    {
        // #209: '>' on a one byte operand used to land on the next instruction's opcode
        var e = await CompileFails(@"
            lda >oops: #$12
            stp");

        Assert.IsInstanceOfType(e, typeof(LabelOutOfBoundsException));
    }

    [TestMethod]
    public async Task ReservedNames_Fail()
    {
        foreach (var code in new[] { ".const private", ".private:\n stp", ".scope public public\n.endscope", ".segment public $2000" })
        {
            var e = await CompileFails(code);
            StringAssert.Contains(e.Message, "'", code);
        }
    }

    // ---------------------------------------------------------------------------------------
    // .export

    [TestMethod]
    public async Task Export_Label()
    {
        var result = await Compile(@"
            jmp feed_loop
        .proc feed
        .loop:
            stp
        .endproc
        .export feed_loop feed:loop");

        Assert.IsFalse(WarnsAboutLabel(result));
        Assert.AreEqual(Value(result, "App:Main:feed:loop"), Value(result, "App:Main:feed_loop"));
        Assert.AreEqual("App:Main:feed:loop", ((AsmVariable)Find(result, "App:Main:feed_loop")).AliasOf);
    }

    [TestMethod]
    public async Task Export_Label_FromAnotherScope_NoWarning()
    {
        // the export takes the label's type, but isn't a label itself (BitBench)
        var result = await Compile(@"
            stp
        .scope process
        .table:
            .export process_table table
        .endscope
        .scope switcher
            lda process:process_table, x
        .endscope");

        Assert.IsFalse(WarnsAboutLabel(result), string.Join("\n", result.Warnings));
        Assert.AreEqual(VariableDataType.LabelPointer, Find(result, "App:process:process_table").VariableDataType);
    }

    [TestMethod]
    public async Task Export_OperandLabel_KeepsItsType()
    {
        var result = await Compile(@"
            stp
        .proc feed
            lda sample: <lo: >hi: $1234
            lda count: #0
        .endproc
        .export s feed:sample
        .export l feed:lo
        .export h feed:hi
        .export c feed:count");

        Assert.AreEqual(VariableDataType.Ushort, Find(result, "App:Main:s").VariableDataType);
        Assert.AreEqual(VariableDataType.Byte, Find(result, "App:Main:l").VariableDataType);
        Assert.AreEqual(VariableDataType.Byte, Find(result, "App:Main:h").VariableDataType);
        Assert.AreEqual(VariableDataType.Byte, Find(result, "App:Main:c").VariableDataType);
        Assert.AreEqual(Value(result, "App:Main:feed:sample") + 1, Value(result, "App:Main:h"));
    }

    [TestMethod]
    public async Task Export_PrivateThings_InItsScope()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
            .machine CommanderX16R40
            .org $810
            jmp start
        .proc private mixer
            .const private LEVEL 1
            .proc step
                .const INNER 2
            .endproc
        .endproc
        .proc private mix_one
            lda #$40
            rts
        .endproc
        .export mix mix_one
        .export level mixer:LEVEL
        .export inner mixer:step:INNER
        .start:
            jsr mix
            clc
            adc #level
            adc #inner
            stp", emulator);

        emulator.AssertState(A: 0x43);
    }

    [TestMethod]
    public async Task Export_EndProc_ThroughAPrivateProc()
    {
        var result = await Compile(@"
            stp
        .proc private feed
            nop
        .endproc
        .export feed_end feed:endproc");

        Assert.AreEqual(Value(result, "App:Main:feed:endproc"), Value(result, "App:Main:feed_end"));
        Assert.AreEqual(VariableDataType.ProcEnd, Find(result, "App:Main:feed_end").VariableDataType);
    }

    [TestMethod]
    public async Task Export_ForwardReferencesAndChains()
    {
        // used before the exports, which are themselves in reverse order, before the target
        var result = await Compile(@"
            lda a
            .const NEXT a + 1
            .constvar ushort copy a
            stp
        .export a b
        .export b c
        .export c feed:sample
        .proc feed
            lda sample: $1234
        .endproc");

        var sample = Value(result, "App:Main:feed:sample");
        Assert.AreEqual(sample, Value(result, "App:Main:a"));
        Assert.AreEqual(sample + 1, Value(result, "App:Main:NEXT"));
        Assert.AreEqual(sample, Value(result, "App:Main:copy"));
        Assert.AreEqual(VariableDataType.Ushort, Find(result, "App:Main:a").VariableDataType);
        Assert.AreEqual("App:Main:feed:sample", ((AsmVariable)Find(result, "App:Main:a")).AliasOf);

        Assert.AreEqual(sample & 0xff, CodeAt(result, 0x811));
        Assert.AreEqual(sample >> 8, CodeAt(result, 0x812));
    }

    [TestMethod]
    public async Task Export_Unresolved_SizedAsAbsolute()
    {
        // a label export resolves late; until then it must size like an unknown name (two
        // bytes), or the lda below would be zero page first and absolute later (BitBench)
        var result = await Compile(@"
            lda alias + 2, x
            stp
        .export alias table
        .segment data $a400
        .table:
            .padvar byte first
        .endsegment");

        Assert.AreEqual(0xbd, CodeAt(result, 0x810)); // lda abs,x
        Assert.AreEqual(0x02, CodeAt(result, 0x811));
        Assert.AreEqual(0xa4, CodeAt(result, 0x812));
    }

    [TestMethod]
    public async Task Export_Private_IsPrivateToItsScope()
    {
        var e = await CompileFails(@"
            stp
        .proc feed
        .loop:
        .endproc
        .export private loop_top feed:loop
        .scope sound
            lda Main:loop_top
        .endscope");

        StringAssert.Contains(e.Message, "'Main:loop_top' is private to 'App:Main'");
    }

    [TestMethod]
    public async Task Export_OutsideItsScope_FollowsVisibility()
    {
        var e = await CompileFails(@"
            stp
        .proc feed
            .const private SECRET 1
        .endproc
        .scope sound
            .export secret Main:feed:SECRET
        .endscope");

        StringAssert.Contains(e.Message, "Cannot export 'Main:feed:SECRET' as 'secret'");
        StringAssert.Contains(e.Message, "is private to 'App:Main'");
    }

    [TestMethod]
    public async Task Export_Loop_Fails()
    {
        var e = await CompileFails(@"
            stp
        .export a b
        .export b a");

        StringAssert.Contains(e.Message, "The exports form a loop: ");
    }

    [TestMethod]
    public async Task Export_DebugAlias_Fails()
    {
        var e = await CompileFails(@"
            stp
        .debugalias byte current $22
        .export c current");

        StringAssert.Contains(e.Message, "debugger aliases can't be exported");
    }

    // ---------------------------------------------------------------------------------------
    // The scope is the boundary: everything in a scope sees everything else in it

    [TestMethod]
    public async Task SameScope_SeesPrivateNamesInOtherProcs()
    {
        var result = await Compile(@"
            lda #feed:LEVEL
            stp
        .proc feed
            .const private LEVEL 5
            .padvar private byte tmp
        .endproc
        .proc other
            lda #feed:LEVEL
            sta feed:tmp
        .endproc");

        Assert.AreEqual(0x05, CodeAt(result, 0x811));
    }

    [TestMethod]
    public async Task SameScope_SeesInsideAPrivateProc()
    {
        var result = await Compile(@"
            lda #mixer:LEVEL
            stp
        .proc private mixer
            .const LEVEL 7
        .endproc");

        Assert.AreEqual(0x07, CodeAt(result, 0x811));
    }

    [TestMethod]
    public async Task SameScope_Labels_NoWarning()
    {
        var result = await Compile(@"
            jmp feed:loop
            lda feed:sample
        .proc feed
        .loop:
            lda sample: $1234
            stp
        .endproc");

        Assert.IsFalse(WarnsAboutLabel(result), string.Join("\n", result.Warnings));
    }

    // ---------------------------------------------------------------------------------------
    // .scope private: names default to private, exports stay public

    private const string PrivateLibrary = @"
        .scope private lib
            .const VALUE 5                  ; private, the scope's default
            .const public SHOWN 6
            .proc helper                    ; private
                rts
            .endproc
            .proc public api
                .const INNER 7              ; private too
                rts
            .endproc
            .export exported VALUE          ; exports are public
        .endscope";

    [TestMethod]
    public async Task PrivateScope_DefaultsToPrivate()
    {
        foreach (var use in new[] { "lda lib:VALUE", "jsr lib:helper", "lda lib:api:INNER" })
        {
            var e = await CompileFails($@"
            {use}
            stp
            {PrivateLibrary}");

            StringAssert.Contains(e.Message, "is private to 'App:lib'", use);
        }
    }

    [TestMethod]
    public async Task PrivateScope_PublicAndExportsAreVisible()
    {
        var result = await Compile($@"
            jsr lib:api
            lda #lib:SHOWN
            lda #lib:exported
            stp
            {PrivateLibrary}");

        Assert.AreEqual(0x06, CodeAt(result, 0x814));
        Assert.AreEqual(0x05, CodeAt(result, 0x816));
    }

    [TestMethod]
    public async Task PrivateScope_InsideTheScopeEverythingIsVisible()
    {
        var result = await Compile(@"
            stp
        .scope private lib
            .const VALUE 5
            .proc helper
                lda #VALUE
                rts
            .endproc
            .proc public api
                jsr helper
                lda #VALUE
                rts
            .endproc
        .endscope");

        Assert.AreEqual(5, Value(result, "App:lib:VALUE"));
    }

    [TestMethod]
    public async Task PrivateScope_ReopenedWithNoKeyword_StaysPrivate()
    {
        var e = await CompileFails(@"
            lda lib:LATER
            stp
        .scope private lib
        .endscope
        .scope lib
            .const LATER 1
        .endscope");

        StringAssert.Contains(e.Message, "is private to 'App:lib'");
    }

    [TestMethod]
    [DataRow(".scope lib\n.endscope\n.scope private lib\n.endscope", "opened public, so can't be reopened as private")]
    [DataRow(".scope private lib\n.endscope\n.scope public lib\n.endscope", "opened private, so can't be reopened as public")]
    [DataRow(".scope private Main\n.endscope", "opened public, so can't be reopened as private")]
    public async Task PrivateScope_ContradictingKeyword_Fails(string code, string message)
    {
        var e = await CompileFails("stp\n" + code);

        StringAssert.Contains(e.Message, message);
    }

    [TestMethod]
    public async Task PrivateScope_NeedsAName()
    {
        var e = await CompileFails(@"
            stp
        .scope private
        .endscope");

        StringAssert.Contains(e.Message, "private scope needs a name");
    }

    [TestMethod]
    public async Task PrivateScope_SegmentUsesTheScopesDefault()
    {
        var e = await CompileFails(@"
            lda lib:IN_SEGMENT
            stp
        .scope private lib
        .endscope
        .segment data $a400 _ _ lib
            .const IN_SEGMENT 1
        .endsegment");

        StringAssert.Contains(e.Message, "is private to 'App:lib'");
    }

    // ---------------------------------------------------------------------------------------
    // .export with an expression

    [TestMethod]
    public async Task ExportExpression_IsAConstant()
    {
        var result = await Compile(@"
            lda #buffer_lo
            stp
        .export buffer_lo <buffer
        .segment data $a456
        .buffer:
            .padvar byte first
        .endsegment");

        Assert.AreEqual(0x56, CodeAt(result, 0x811));
        Assert.AreEqual(VariableDataType.Constant, Find(result, "App:Main:buffer_lo").VariableDataType);
        Assert.AreEqual("<buffer", ((AsmVariable)Find(result, "App:Main:buffer_lo")).AliasOf);
    }

    [TestMethod]
    public async Task ExportExpression_NamePlusOffset_KeepsItsType()
    {
        var result = await Compile(@"
            stp
        .proc feed
            lda sample: $1234
        .endproc
        .export sample_hi feed:sample + 1
        .export before feed:sample - 1");

        var sample = Value(result, "App:Main:feed:sample");
        Assert.AreEqual(sample + 1, Value(result, "App:Main:sample_hi"));
        Assert.AreEqual(sample - 1, Value(result, "App:Main:before"));
        Assert.AreEqual(VariableDataType.Ushort, Find(result, "App:Main:sample_hi").VariableDataType);
        Assert.AreEqual("feed:sample + 1", ((AsmVariable)Find(result, "App:Main:sample_hi")).AliasOf);
    }

    [TestMethod]
    public async Task ExportExpression_ForwardReference_UsedInAnImmediateAndAConst()
    {
        // used before the export, which is before what it's made from
        var result = await Compile(@"
            lda #lib:THIRD
            .const ALSO lib:THIRD + 1
            stp
        .scope private lib
            .export THIRD FULL / 3
            .const FULL 99
        .endscope");

        Assert.AreEqual(33, CodeAt(result, 0x811));
        Assert.AreEqual(34, Value(result, "App:Main:ALSO"));
    }

    [TestMethod]
    public async Task ExportExpression_OfALabel_IsntALabel()
    {
        var result = await Compile(@"
            stp
        .scope lib
        .proc feed
        .loop:
            nop
        .endproc
            .export after_loop feed:loop + 1
        .endscope
        .scope other
            jmp lib:after_loop
        .endscope");

        Assert.IsFalse(WarnsAboutLabel(result), string.Join("\n", result.Warnings));
        Assert.AreEqual(Value(result, "App:lib:feed:loop") + 1, Value(result, "App:lib:after_loop"));
    }

    [TestMethod]
    public async Task ExportExpression_PrivateInAnotherScope_Fails()
    {
        var e = await CompileFails(@"
            stp
        .proc feed
            .const private SECRET 1
        .endproc
        .scope sound
            .export secret Main:feed:SECRET + 1
        .endscope");

        StringAssert.Contains(e.Message, "Cannot export 'Main:feed:SECRET + 1' as 'secret'");
        StringAssert.Contains(e.Message, "is private to 'App:Main'");
    }

    [TestMethod]
    public async Task ExportExpression_UnknownName_Fails()
    {
        var e = await CompileFails(@"
            stp
        .export x NOT_DEFINED + 1");

        StringAssert.Contains(e.Message, "Cannot export 'NOT_DEFINED + 1' as 'x': 'NOT_DEFINED' can't be found");
    }

    [TestMethod]
    public async Task ExportExpression_DebugAlias_Fails()
    {
        var e = await CompileFails(@"
            stp
        .debugalias byte current $22
        .export c current + 1");

        StringAssert.Contains(e.Message, "debugger aliases can't be exported");
    }
}
