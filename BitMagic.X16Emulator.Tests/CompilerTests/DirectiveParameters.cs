using BitMagic.Compiler;
using BitMagic.Compiler.Files;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

// how a directive's parameters are read, eg '.segment name address maxsize filename scope'. Shared by every
// directive, so .segmentdefine stands in for them all.
[TestClass]
public class DirectiveParameters
{
    private static async Task<CompileState> Compile(string line)
    {
        var project = new Project { Code = new StaticTextFile($".machine CommanderX16R40\n.org $810\n{line}\n    stp", "unittest.bmasm") };
        var result = await new BitMagic.Compiler.Compiler(project, new(), new NullLogger()).Compile();
        return result.State;
    }

    private static async Task<Segment> Define(string parameters) => (await Compile($".segmentdefine {parameters}")).Segments["data"];

    private static async Task<string> Fails(string line)
    {
        try
        {
            await Compile(line);
        }
        catch (Exception e)
        {
            return e.Message;
        }

        Assert.Fail($"Expected '{line}' not to compile.");
        return "";
    }

    private static void AssertSegment(Segment segment, int address, int maxSize, string filename)
    {
        Assert.AreEqual(address, segment.StartAddress);
        Assert.AreEqual(maxSize, segment.MaxSize);
        Assert.AreEqual(filename, segment.Filename ?? "");
    }

    // what already works

    [TestMethod]
    public async Task Positional() => AssertSegment(await Define("data $a000 $100 data.bin"), 0xa000, 0x100, "data.bin");

    [TestMethod]
    public async Task PositionalWithCommas() => AssertSegment(await Define("data, $a000, $100, data.bin"), 0xa000, 0x100, "data.bin");

    [TestMethod]
    public async Task PositionalWithCommasAndSpacesMixed() => AssertSegment(await Define("data,$a000 , $100,data.bin"), 0xa000, 0x100, "data.bin");

    [TestMethod]
    public async Task Named() => AssertSegment(await Define("data address=$a000 filename=data.bin maxsize=$100"), 0xa000, 0x100, "data.bin");

    [TestMethod]
    public async Task NamedWithAColon() => AssertSegment(await Define("data address:$a000 maxsize:$100 filename:data.bin"), 0xa000, 0x100, "data.bin");

    [TestMethod]
    public async Task UnderscoreSkips() => AssertSegment(await Define("data $a000 _ data.bin"), 0xa000, 0, "data.bin");

    [TestMethod]
    public async Task QuotedWithSpaces() => AssertSegment(await Define("data $a000 _ \"my data.bin\""), 0xa000, 0, "my data.bin");

    [TestMethod]
    public async Task QuotedWithAComma() => AssertSegment(await Define("data $a000 _ \"a,b.bin\""), 0xa000, 0, "a,b.bin");

    // a Windows path, as '.map' is given, isn't a parameter called 'C'
    [TestMethod]
    public async Task DrivePath() => AssertSegment(await Define(@"data $a000 _ C:\x\data.bin"), 0xa000, 0, @"C:\x\data.bin");

    [TestMethod]
    public async Task SemicolonComment() => AssertSegment(await Define("data $a000 ; maxsize $100"), 0xa000, 0, ":data");

    [TestMethod]
    public async Task SlashComment() => AssertSegment(await Define("data $a000 // maxsize $100"), 0xa000, 0, ":data");

    // what now works

    [TestMethod]
    public async Task NamedAndQuoted() => AssertSegment(await Define("data, address=$a000, filename=\"ROM TEST.BIN\""), 0xa000, 0, "ROM TEST.BIN");

    [TestMethod]
    public async Task NamedWithSpaces() => AssertSegment(await Define("data, $a000, $100, filename = data.bin"), 0xa000, 0x100, "data.bin");

    [TestMethod]
    public async Task CommentStraightAfterAValue() => AssertSegment(await Define("data $a000; maxsize $100"), 0xa000, 0, ":data");

    // errors

    [TestMethod]
    public async Task NamedTwice() =>
        StringAssert.Contains(await Fails(".segment data address=$a000 address=$b000"), "'.segment' has 'address' twice.");

    [TestMethod]
    public async Task PositionalAfterTheSameNamed() =>
        StringAssert.Contains(await Fails(".segment data, address=$a000, $b000"), "'.segment' has 'address' twice: '$b000' is in its place, but it's already given by name.");

    [TestMethod]
    public async Task TooMany() =>
        StringAssert.Contains(await Fails(".segment data $a000 $100 data.bin sound extra"), "'.segment' takes name, address, maxsize, filename, scope, so 'extra' is one too many.");

    [TestMethod]
    public async Task NoneTaken() =>
        StringAssert.Contains(await Fails(".segment data $a000\n.endsegment extra"), "'.endsegment' doesn't take any parameters, but has 'extra'.");

    [TestMethod]
    public async Task UnknownName() =>
        StringAssert.Contains(await Fails(".segment data $a000 fliename=data.bin"), "'.segment' has no parameter 'fliename'. It takes name, address, maxsize, filename, scope.");

    // the example from https://github.com/Yazwh0/BitMagic/issues/180
    [TestMethod]
    public async Task FilenameInTheMaxsizePlace() =>
        StringAssert.Contains(await Fails(".segment ROM $a000 ROMTEST.BIN"),
            "'.segment' maxsize must be a number, but is 'ROMTEST.BIN'. Parameters go name, address, maxsize, filename, scope: use '_' to skip one, or give it by name, eg 'filename=ROMTEST.BIN'.");

    [TestMethod]
    public async Task NotANumberWithNothingAfter() =>
        Assert.IsTrue((await Fails(".org main")).EndsWith("'.org' address must be a number, but is 'main'."));

    [TestMethod]
    public async Task HexThatIsntHex() =>
        StringAssert.Contains(await Fails(".segment data $a0g0"), "'.segment' address must be a number, but is '$a0g0'.");

    // assignments, eg '.const name value', are read separately

    // the value 'lda #FOO' loads, after the line given defines FOO
    private static async Task<byte> ConstValue(string line)
    {
        var project = new Project { Code = new StaticTextFile($".machine CommanderX16R40\n.org $810\n{line}\n    lda #FOO\n    stp", "unittest.bmasm") };
        var result = await new BitMagic.Compiler.Compiler(project, new(), new NullLogger()).Compile();

        // the output has a 2 byte header, then starts at $801. 'lda #' is $a9, then the value
        var code = result.Data["Main"].ToArray()[(2 + 0x810 - 0x801)..];
        Assert.AreEqual(0xa9, code[0]);
        return code[1];
    }

    [TestMethod]
    [DataRow(".const FOO $42")]
    [DataRow(".const FOO = $42")]
    [DataRow(".const FOO=$42")]
    [DataRow(".const FOO= $42")]
    [DataRow(".const FOO =$42")]
    [DataRow(".const FOO\t$42")]
    [DataRow(".const FOO=$40 + 2 ; comment")]
    public async Task Const(string line) => Assert.AreEqual(0x42, await ConstValue(line));

    [TestMethod]
    [DataRow(".const OTHER=1, FOO=$42")]
    [DataRow(".const OTHER $40, FOO = OTHER + 2")]
    [DataRow(".const OTHER=$41, FOO OTHER+1")]
    public async Task SeveralConstsOnALine(string line) => Assert.AreEqual(0x42, await ConstValue(line));

    // a comma in brackets is part of the value, not the start of another constant
    [TestMethod]
    public async Task CommaInBrackets() => Assert.AreEqual(0x42, await ConstValue(".const FOO=(int)Max($40, $42)"));

    [TestMethod]
    public async Task ConstWithoutAValue() =>
        StringAssert.Contains(await Fails(".const FOO"), "'.const FOO' needs a value, eg '.const FOO = 1'.");

    [TestMethod]
    public async Task SecondConstWithoutAValue() =>
        StringAssert.Contains(await Fails(".const FOO=1, BAR"), "'BAR' in '.const' needs a name and a value, eg 'BAR=1'.");
}
