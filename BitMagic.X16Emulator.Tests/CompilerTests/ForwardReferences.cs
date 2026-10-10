using BitMagic.Compiler;
using BitMagic.Compiler.Files;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

// A name used before it's defined. On the first pass it's only a two byte placeholder, so an operand that only
// takes a byte (#x, (x),y) needs to compile with a placeholder and be filled in on the final pass.
[TestClass]
public class ForwardReferences
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

    // the assembled code from $810 (the output has a 2 byte header, then starts at $801)
    private static byte[] CodeFrom810(CompileResult result, int count) => result.Data["Main"].ToArray()[(2 + 0x810 - 0x801)..][..count];

    [TestMethod]
    public async Task Immediate_ConstantDefinedLater()
    {
        var result = await Compile(@"
            lda #LATER
            stp
            .const LATER $42");

        CollectionAssert.AreEqual(new byte[] { 0xa9, 0x42, 0xdb }, CodeFrom810(result, 3));
    }

    [TestMethod]
    public async Task IndirectY_ConstantDefinedLater()
    {
        var result = await Compile(@"
            lda (PTR), y
            stp
            .const PTR $22");

        CollectionAssert.AreEqual(new byte[] { 0xb1, 0x22, 0xdb }, CodeFrom810(result, 3));
    }

    // the Visibility example's case: the library's scope comes after the code that uses it
    [TestMethod]
    public async Task Immediate_PublicConstantInAScopeDefinedLater()
    {
        var result = await Compile(@"
            lda #lib:VALUE
            stp
        .scope lib
            .const VALUE 5
        .endscope");

        CollectionAssert.AreEqual(new byte[] { 0xa9, 0x05, 0xdb }, CodeFrom810(result, 3));
    }

    [TestMethod]
    public async Task Immediate_PrivateConstantInAScopeDefinedLater_SaysItsPrivate()
    {
        var e = await CompileFails(@"
            lda #lib:VALUE
            stp
        .scope lib
            .const private VALUE 5
        .endscope");

        StringAssert.Contains(e.Message, "is private to");
    }

    [TestMethod]
    public async Task Immediate_UnknownName_Fails()
    {
        await CompileFails(@"
            lda #NOT_DEFINED
            stp");
    }

    [TestMethod]
    public async Task Immediate_ConstantDefinedLater_TooBigForAByte_Fails()
    {
        await CompileFails(@"
            lda #BIG
            stp
            .const BIG $1234");
    }

    // unchanged: a name not known yet can't be assumed to be zero page, so the operand is two bytes
    [TestMethod]
    public async Task Absolute_ConstantDefinedLater_StaysAbsolute()
    {
        var result = await Compile(@"
            lda LATER
            stp
            .const LATER $1234");

        CollectionAssert.AreEqual(new byte[] { 0xad, 0x34, 0x12, 0xdb }, CodeFrom810(result, 4));
    }
}
