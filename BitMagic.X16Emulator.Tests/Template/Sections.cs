using BitMagic.TemplateEngine.Compiler;
using BitMagic.X16Emulator.TestHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Templating;

// 'file library', sections and 'place'
[TestClass]
public class Sections
{
    private const string Header = ".machine CommanderX16R40\n.org $810\n";

    private static Dictionary<string, string> Files(params (string Name, string Content)[] files) =>
        files.ToDictionary(i => i.Name, i => i.Content);

    private static async Task<TemplateCompilationException> BuildFails(Dictionary<string, string> files)
    {
        try
        {
            await X16TestHelper.BuildTemplateFiles(files);
        }
        catch (TemplateCompilationException e)
        {
            return e;
        }

        Assert.Fail("Expected the build to fail.");
        return null!;
    }

    private static void AssertError(TemplateCompilationException e, string file, int line, string message)
    {
        Assert.AreEqual(1, e.Errors.Count, e.Message);
        StringAssert.EndsWith(e.Filename.Replace('\\', '/'), "/" + file, e.Message);
        Assert.AreEqual(line, e.Errors[0].LineNumber, e.Message);
        StringAssert.Contains(e.Errors[0].ErrorText, message);
    }

    // where text first appears in the generated code
    private static int At(MacroAssembler.ProcessResult result, string text)
    {
        var idx = result.Source.Code.IndexOf(text, StringComparison.Ordinal);
        Assert.AreNotEqual(-1, idx, $"'{text}' isn't in the generated code:\n{result.Source.Code}");
        return idx;
    }

    private static int Count(MacroAssembler.ProcessResult result, string text) =>
        result.Source.Code.Split(text).Length - 1;

    private const string Lib = @"file library

section setup
.scope lib
    .const LIB_VALUE $42
.endscope

section code
.scope lib
    .proc get
        lda #LIB_VALUE
        rts
    .endproc
.endscope
";

    [TestMethod]
    public async Task DefaultOrder()
    {
        var (emulator, _, result) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    jsr lib:get\n    stp\n"),
            ("lib.bmasm", Lib)));

        Assert.AreEqual(0x42, emulator.A);

        // setup, then main, then code
        Assert.IsTrue(At(result, "LIB_VALUE $42") < At(result, ".org $810"));
        Assert.IsTrue(At(result, "stp") < At(result, ".proc get"));
    }

    [TestMethod]
    [DataRow("import Lib=\"lib.bmasm\"")]
    [DataRow("import Lib=\"lib.bmasm\";")]
    [DataRow("import Lib = \"lib.bmasm\"   ; the text library")]
    public async Task ImportSemicolonIsOptional(string import)
    {
        var (emulator, _, _) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", import + "\n" + Header + "    jsr lib:get\n    stp\n"),
            ("lib.bmasm", Lib)));

        Assert.AreEqual(0x42, emulator.A);
    }

    [TestMethod]
    [DataRow("include \"helper.cs\"")]
    [DataRow("include \"helper.cs\";")]
    [DataRow("include \"helper.cs\"   ; C# helpers")]
    public async Task IncludeSemicolonIsOptional(string include)
    {
        var (emulator, _, _) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", include + "\n" + Header + "    lda #@(Helper.Value)\n    stp\n"),
            ("helper.cs", "public static class Helper { public static int Value => 0x5a; }")));

        Assert.AreEqual(0x5a, emulator.A);
    }

    private sealed class CapturingLogger : BitMagic.Common.IEmulatorLogger
    {
        public List<string> Lines { get; } = new();
        public void Log(string message) => Lines.Add(message);
        public void LogLine(string message) => Lines.Add(message);
        public void LogError(string message) => Lines.Add(message);
        public void LogError(string message, BitMagic.Common.ISourceFile source, int lineNumber) => Lines.Add(message);
    }

    [TestMethod]
    public async Task ShippedInitialisationWarns()
    {
        var logger = new CapturingLogger();

        await X16TestHelper.BuildTemplateFiles(Files(
            ("main.bmasm", "import Init=\"Initialisation.bmasm\";\nimport Other=\"other.bmasm\";\n" + Header + "    stp\n"),
            ("other.bmasm", "library Test.Other;\nimport Init=\"Initialisation.bmasm\";\n")), logger: logger);

        var warnings = logger.Lines.Where(i => i.Contains("WARNING:")).ToList();

        Assert.AreEqual(1, warnings.Count, string.Join("\n", logger.Lines));    // once per build
        StringAssert.Contains(warnings[0], "'main.bmasm' imports 'Initialisation.bmasm', which library sections replace.");
    }

    [TestMethod]
    public async Task OwnInitialisationDoesNotWarn()
    {
        // BitBench has its own copy, next to its files
        var logger = new CapturingLogger();

        await X16TestHelper.BuildTemplateFiles(Files(
            ("main.bmasm", "import Init=\"initialisation.bmasm\";\n" + Header + "    stp\n"),
            ("initialisation.bmasm", "library Test.Init;\n")), logger: logger);

        Assert.IsFalse(logger.Lines.Any(i => i.Contains("WARNING:")), string.Join("\n", logger.Lines));
    }

    [TestMethod]
    public async Task SectionsSeeWhatTheAppSets()
    {
        // the library runs once the app has, so even a setting made at the end of the app is seen
        var (emulator, _, _) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    jsr lib:get\n    stp\nLib.Value = 0x42;\n"),
            ("lib.bmasm", "file library code\n.scope lib\n    .proc get\n        lda #@(Value)\n        rts\n    .endproc\n.endscope\n\nmembers\npublic static int Value = 1;\n")));

        Assert.AreEqual(0x42, emulator.A);
    }

    [TestMethod]
    public async Task MembersWithABuilderAndAMacro()
    {
        var library = """
            file library code
            .scope lib
                .proc get
                    if (Settings.Double)
                    {
                        lda #@(Settings.Value * 2)
                    }
                    else
                    {
                        lda #@(Settings.Value)
                    }
                    rts
                .endproc
            .endscope

            members
            private static Options Settings = new();

            public static Options Create() => Settings = new Options();

            // a macro: writes into the app, where the app calls it
            public static void LoadValue()
            {
                jsr lib:get
            }

            public class Options
            {
                internal int Value = 1;
                internal bool Double;

                public Options WithValue(int value) { Value = value; return this; }
                public Options Doubled() { Double = true; return this; }
                public void Build() { }
            }
            """;

        var (emulator, _, _) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\nLib.Create().WithValue(0x21).Doubled().Build();\n" + Header + "    Lib.LoadValue();\n    stp\n"),
            ("lib.bmasm", library)));

        Assert.AreEqual(0x42, emulator.A);
    }

    [TestMethod]
    public async Task SectionsAfterMembers()
    {
        var (emulator, _, _) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    jsr lib:get\n    stp\n"),
            ("lib.bmasm", "file library code\n.scope lib\n    .proc get\n        lda #LIB_VALUE + @(Offset)\n        rts\n    .endproc\n.endscope\n\nmembers\npublic static int Offset = 0;\n\nsection setup\n.scope lib\n    .const LIB_VALUE $33\n.endscope\n")));

        Assert.AreEqual(0x33, emulator.A);
    }

    [TestMethod]
    public async Task LibraryLineNamesTheClass()
    {
        var (emulator, _, _) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    lda #@(Test.Named.Value)\n    stp\n"),
            ("lib.bmasm", "file library code\nlibrary Test.Named;\n    rts\n\nmembers\npublic static int Value = 0x55;\n")));

        Assert.AreEqual(0x55, emulator.A);
    }

    [TestMethod]
    [DataRow("library Test.NoSemi", DisplayName = "no ;")]
    [DataRow("library Test.NoSemi ; a comment", DisplayName = "no ;, with a comment")]
    [DataRow("library Test.NoSemi;", DisplayName = "with ;")]
    public async Task LibraryLineSemicolonOptional(string libraryLine)
    {
        var (emulator, _, _) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    lda #@(Test.NoSemi.Value)\n    stp\n"),
            ("lib.bmasm", "file library code\n" + libraryLine + "\n    rts\n\nmembers\npublic static int Value = 0x56;\n")));

        Assert.AreEqual(0x56, emulator.A);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "file library imported first")]
    [DataRow(false, DisplayName = "C# library imported first")]
    public async Task LibrariesKeepImportOrder(bool fileFirst)
    {
        // a file library's code is captured later than a C# library's, but still goes in import order
        var file = "import FileLib=\"filelib.bmasm\";\n";
        var csharp = "import CsLib=\"cslib.bmasm\";\n";

        var result = await X16TestHelper.BuildTemplateFiles(Files(
            ("main.bmasm", (fileFirst ? file + csharp : csharp + file) + Header + "    stp\n"),
            ("filelib.bmasm", "file library code\n.file_part:\n    .byte $aa\n"),
            ("cslib.bmasm", "library Test.CsLib;\n\nprivate static void Code()\n{\n.cs_part:\n    .byte $bb\n}\n\npublic override void Initialise()\n{\n    Section.Get(\"code\").Add(Owner, Code);\n}\n")));

        Assert.AreEqual(fileFirst, At(result, ".file_part:") < At(result, ".cs_part:"), result.Source.Code);
    }

    [TestMethod]
    public async Task PlaceExplicitly()
    {
        var (emulator, _, result) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    place setup\n    jsr lib:get\n    stp\n    place code\n.after_code:\n"),
            ("lib.bmasm", Lib)));

        Assert.AreEqual(0x42, emulator.A);

        Assert.IsTrue(At(result, ".org $810") < At(result, "LIB_VALUE $42"));
        Assert.IsTrue(At(result, ".proc get") < At(result, ".after_code:"));
        Assert.AreEqual(1, Count(result, ".proc get"));
    }

    [TestMethod]
    public async Task PlaceInALibrary()
    {
        var (emulator, _, _) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Handler=\"handler.bmasm\";\nimport Music=\"music.bmasm\";\n" + Header + "    jsr handler:run\n    stp\n"),
            ("music.bmasm", "file library irq\n    lda #$11\n"),
            ("handler.bmasm", "file library code\n.scope handler\n    .proc run\n        place irq\n        rts\n    .endproc\n.endscope\n")));

        Assert.AreEqual(0x11, emulator.A);
    }

    [TestMethod]
    public async Task AsmBeforeFirstSection()
    {
        var e = await BuildFails(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    stp\n"),
            ("lib.bmasm", "file library\n\n    lda #1\nsection code\n    rts\n")));

        AssertError(e, "lib.bmasm", 3, "'lda #1' is before the first 'section'");
    }

    [TestMethod]
    public async Task StartingSection()
    {
        var (emulator, _, result) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    jsr lib:get\n    stp\n"),
            ("lib.bmasm", "file library code\n.scope lib\n    .proc get\n        lda #LIB_VALUE\n        rts\n    .endproc\n.endscope\nsection setup\n.scope lib\n    .const LIB_VALUE $24\n.endscope\n")));

        Assert.AreEqual(0x24, emulator.A);
        Assert.IsTrue(At(result, "LIB_VALUE $24") < At(result, ".org $810"));
    }

    [TestMethod]
    public async Task BadSectionName()
    {
        var e = await BuildFails(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    stp\n"),
            ("lib.bmasm", "file library code\n    rts\nsection a.b\n    rts\n")));

        AssertError(e, "lib.bmasm", 3, "'section a.b' needs a section name");
    }

    private const string DataView = @"library Test.DataView;

public static IEnumerable<string> Keywords => new[] { ""value"" };

public static void Process(ViewFile file)
{
    var value = file.Arguments;
    var items = new List<CapturedItem>();

    foreach (var item in file.Capture())
    {
        if (item is KeywordLine { Keyword: ""value"" } keyword)
        {
            value = keyword.Arguments;
            continue;
        }

        items.Add(item);
    }

    Section.Get(""data"").Add(file.Owner, () =>
    {
        Template.Write(items);
        .proc data_proc
            lda #@(value)
            rts
        .endproc
    });
}
";

    [TestMethod]
    public async Task CustomView()
    {
        var (emulator, _, result) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Data=\"data.bmasm\";\n" + Header + "    jsr data_proc\n    stp\n    place data\n"),
            ("dataview.bmasm", DataView),
            ("data.bmasm", "file dataview $21\n.data_label:\n")));

        // the argument reaches the view
        Assert.AreEqual(0x21, emulator.A);
        At(result, ".data_label:");
    }

    [TestMethod]
    public async Task CustomViewKeyword()
    {
        var (emulator, _, _) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Data=\"data.bmasm\";\n" + Header + "    jsr data_proc\n    stp\n    place data\n"),
            ("dataview.bmasm", DataView),
            ("data.bmasm", "file dataview $21\nvalue $33\n")));

        Assert.AreEqual(0x33, emulator.A);
    }

    [TestMethod]
    public async Task ViewWithoutProcess()
    {
        var e = await BuildFails(Files(
            ("main.bmasm", "import Data=\"data.bmasm\";\n" + Header + "    stp\n"),
            ("bad.bmasm", "library Test.Bad;\n"),
            ("data.bmasm", "; data\nfile bad\n")));

        AssertError(e, "data.bmasm", 2, "'bad.bmasm' can't be used with 'file bad': it has no 'public static void Process(ViewFile file)'");
    }

    [TestMethod]
    public async Task UnknownKind()
    {
        await Assert.ThrowsExceptionAsync<ImportNotFoundException>(() => X16TestHelper.BuildTemplateFiles(Files(
            ("main.bmasm", "import Data=\"data.bmasm\";\n" + Header + "    stp\n"),
            ("data.bmasm", "file nosuchview\n"))));
    }

    [TestMethod]
    public async Task FileAsMain()
    {
        var e = await BuildFails(Files(("main.bmasm", "file library code\n    stp\n")));

        AssertError(e, "main.bmasm", 1, "A 'file library' file can't be built on its own");
    }

    [TestMethod]
    public async Task CSharpFileModifierIsNotAHeader()
    {
        // 'file class' is C#, so this is a main file, not a view file. It fails as C#.
        var e = await BuildFails(Files(("main.bmasm", "file class Helper { }\n" + Header + "    stp\n")));

        Assert.IsFalse(e.Message.Contains("can't be built on its own"), e.Message);
    }

    [TestMethod]
    public async Task NeverPlaced()
    {
        var e = await BuildFails(Files(
            ("main.bmasm", "import Music=\"music.bmasm\";\n" + Header + "    stp\n"),
            ("music.bmasm", "file library irq\n    lda #1\n")));

        AssertError(e, "main.bmasm", -1, "Section 'irq' has code from 'music.bmasm' that is never placed");
    }

    [TestMethod]
    public async Task PlaceTwice()
    {
        var e = await BuildFails(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    stp\n    place code\n    place code\n"),
            ("lib.bmasm", Lib)));

        AssertError(e, "main.bmasm", 6, "'place code' is already used at main.bmasm(5)");
    }

    private static Dictionary<string, string> DosAndLz(string placing) => Files(
        ("main.bmasm", "import Dos=\"dos.bmasm\";\nimport Lz=\"lz.bmasm\";\n" + Header + "    jsr dos:get\n    sta $02\n    jsr lz:get\n    stp\n" + placing),
        ("dos.bmasm", "file library code\n.scope dos\n    .proc get\n        lda #$d0\n        rts\n    .endproc\n.endscope\n"),
        ("lz.bmasm", "file library code\n.scope lz\n    .proc get\n        lda #$1a\n        rts\n    .endproc\n.endscope\n"));

    [TestMethod]
    [DataRow(false, DisplayName = "place Dos.code first")]
    [DataRow(true, DisplayName = "place code first")]
    public async Task PlaceOneLibrary(bool restFirst)
    {
        var dos = ".dos_start:\n    place Dos.code\n.dos_end:\n";
        var rest = ".rest_start:\n    place code\n.rest_end:\n";

        var (emulator, _, result) = await X16TestHelper.EmulateTemplateFiles(DosAndLz(restFirst ? rest + dos : dos + rest));

        Assert.AreEqual(0xd0, emulator.Memory[0x02]);
        Assert.AreEqual(0x1a, emulator.A);

        var dosCode = At(result, "lda #$d0");
        var lzCode = At(result, "lda #$1a");

        Assert.IsTrue(At(result, ".dos_start:") < dosCode && dosCode < At(result, ".dos_end:"));
        Assert.IsTrue(At(result, ".rest_start:") < lzCode && lzCode < At(result, ".rest_end:"));
        Assert.AreEqual(1, Count(result, "lda #$d0"));
    }

    [TestMethod]
    public async Task PlaceOneLibraryRestByDefault()
    {
        var (emulator, _, result) = await X16TestHelper.EmulateTemplateFiles(DosAndLz(".dos_start:\n    place Dos.code\n.dos_end:\n"));

        Assert.AreEqual(0x1a, emulator.A);

        var dosCode = At(result, "lda #$d0");
        Assert.IsTrue(At(result, ".dos_start:") < dosCode && dosCode < At(result, ".dos_end:"));
        Assert.IsTrue(At(result, ".dos_end:") < At(result, "lda #$1a"));
    }

    [TestMethod]
    public async Task PlaceOneLibraryTwice()
    {
        var e = await BuildFails(DosAndLz("    place Dos.code\n    place Dos.code\n"));

        AssertError(e, "main.bmasm", 10, "'dos.bmasm' code is already placed at main.bmasm(9)");
    }

    [TestMethod]
    public async Task PlaceUnknownAlias()
    {
        var e = await BuildFails(DosAndLz("    place Dso.code\n"));

        AssertError(e, "main.bmasm", 9, "'Dso' isn't an import in this file.");
    }

    [TestMethod]
    public async Task PlaceMissingPart()
    {
        var e = await BuildFails(DosAndLz("    place Dos.cod\n"));

        AssertError(e, "main.bmasm", 9, "'dos.bmasm' has no 'cod' section.");
    }

    [TestMethod]
    public async Task ImportedOnlyByALibrary()
    {
        // main imports dos only to place it; lz is the library that uses it
        var (emulator, _, result) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Lz=\"lz.bmasm\";\nimport Dos=\"dos.bmasm\";\n" + Header + "    jsr lz:get\n    stp\n.dos_start:\n    place Dos.code\n"),
            ("dos.bmasm", "file library code\n.scope dos\n    .proc get\n        lda #$d0\n        rts\n    .endproc\n.endscope\n"),
            ("lz.bmasm", "file library code\nimport Dos=\"dos.bmasm\";\n.scope lz\n    .proc get\n        jmp dos:get\n    .endproc\n.endscope\n")));

        Assert.AreEqual(0xd0, emulator.A);
        Assert.IsTrue(At(result, ".dos_start:") < At(result, "lda #$d0"));
        Assert.AreEqual(1, Count(result, "lda #$d0"));
    }

    [TestMethod]
    public async Task NestedImportIsWritten()
    {
        // main never imports dos, lz does
        var (emulator, _, result) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Lz=\"lz.bmasm\";\n" + Header + "    jsr lz:get\n    stp\n"),
            ("dos.bmasm", "file library code\n.scope dos\n    .proc get\n        lda #$d0\n        rts\n    .endproc\n.endscope\n"),
            ("lz.bmasm", "file library code\nimport Dos=\"dos.bmasm\";\n.scope lz\n    .proc get\n        jmp dos:get\n    .endproc\n.endscope\n")));

        Assert.AreEqual(0xd0, emulator.A);
        Assert.AreEqual(1, Count(result, "lda #$d0"));
    }

    [TestMethod]
    public async Task ImportedTwiceIsWrittenOnce()
    {
        var (emulator, _, result) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import A=\"a.bmasm\";\nimport B=\"b.bmasm\";\nimport C=\"c.bmasm\";\n" + Header + "    jsr c:get\n    stp\n"),
            ("a.bmasm", "file library code\nimport C=\"c.bmasm\";\n    nop\n"),
            ("b.bmasm", "file library code\nimport C=\"c.bmasm\";\n    nop\n"),
            ("c.bmasm", "file library code\n.scope c\n    .proc get\n        lda #$cc\n        rts\n    .endproc\n.endscope\n")));

        Assert.AreEqual(0xcc, emulator.A);
        Assert.AreEqual(1, Count(result, "lda #$cc"));
    }

    [TestMethod]
    public async Task CustomWriter()
    {
        // the writer is called once per placement, with the parts placed there
        var (_, _, result) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import W=\"writer.bmasm\";\nimport A=\"a.bmasm\";\nimport B=\"b.bmasm\";\nimport C=\"c.bmasm\";\n" + Header + "    stp\n.one:\n    place A.irq\n.rest:\n    place irq\n"),
            ("writer.bmasm", "library Test.Writer;\n\npublic override void Initialise()\n{\n    Section.Get(\"irq\").Writer = parts =>\n    {\n        .byte @(parts.Count)\n        foreach (var part in parts)\n            part.Write();\n    };\n}\n"),
            ("a.bmasm", "file library irq\n    .byte $aa\n"),
            ("b.bmasm", "file library irq\n    .byte $bb\n"),
            ("c.bmasm", "file library irq\n    .byte $cc\n")));

        var code = result.Source.Code;
        var one = code[At(result, ".one:")..At(result, ".rest:")];
        var rest = code[At(result, ".rest:")..];

        StringAssert.Contains(one, ".byte 1");
        StringAssert.Contains(one, ".byte $aa");
        StringAssert.Contains(rest, ".byte 2");
        StringAssert.Contains(rest, ".byte $bb");
        StringAssert.Contains(rest, ".byte $cc");
    }

    [TestMethod]
    public async Task LateAddition()
    {
        // added after 'place code', while main runs, and still written there
        var (emulator, _, result) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import Late=\"late.bmasm\";\n" + Header + "    jsr late_proc\n    stp\n.code_here:\n    place code\n.code_end:\nLate.AddLater();\n"),
            ("late.bmasm", "library Test.Late;\n\npublic static void AddLater()\n{\n    Section.Get(\"code\").Add(\"late\", () =>\n    {\n        .proc late_proc\n            lda #$66\n            rts\n        .endproc\n    });\n}\n")));

        Assert.AreEqual(0x66, emulator.A);

        var late = At(result, "lda #$66");
        Assert.IsTrue(At(result, ".code_here:") < late && late < At(result, ".code_end:"));
    }

    [TestMethod]
    public async Task SourceMapPointsAtTheLibrary()
    {
        var result = await X16TestHelper.BuildTemplateFiles(Files(
            ("main.bmasm", "import Lib=\"lib.bmasm\";\n" + Header + "    jsr lib:get\n    stp\n"),
            ("lib.bmasm", Lib)));

        var lines = result.Source.Code.Split('\n');
        var index = Array.FindIndex(lines, i => i.Contains("lda #LIB_VALUE"));
        var map = result.Source.Map[index];

        StringAssert.EndsWith(map.SourceFilename.Replace('\\', '/'), "/lib.bmasm");
        Assert.AreEqual(10, map.Line);   // zero based: line 11 of lib.bmasm
    }

    [TestMethod]
    public async Task ImportingItselfIsCircular()
    {
        var e = await BuildFails(Files(
            ("main.bmasm", "import Me=\"main.bmasm\";\n" + Header + "    stp\n")));

        AssertError(e, "main.bmasm", 1, "Circular import: main.bmasm -> main.bmasm.");
    }

    [TestMethod]
    public async Task LibraryImportingMainIsCircular()
    {
        var e = await BuildFails(Files(
            ("main.bmasm", "import A=\"a.bmasm\";\n" + Header + "    stp\n"),
            ("a.bmasm", "library Test.A;\nimport Main=\"main.bmasm\";\n")));

        AssertError(e, "a.bmasm", 2, "Circular import: main.bmasm -> a.bmasm -> main.bmasm.");
    }

    [TestMethod]
    public async Task LibrariesImportingEachOtherIsCircular()
    {
        var e = await BuildFails(Files(
            ("main.bmasm", "import A=\"a.bmasm\";\n" + Header + "    stp\n"),
            ("a.bmasm", "library Test.A;\nimport B=\"b.bmasm\";\n"),
            ("b.bmasm", "library Test.B;\n\nimport A=\"a.bmasm\";\n")));

        AssertError(e, "b.bmasm", 3, "Circular import: main.bmasm -> a.bmasm -> b.bmasm -> a.bmasm.");
    }

    [TestMethod]
    public async Task ViewImportingItsFileIsCircular()
    {
        var e = await BuildFails(Files(
            ("main.bmasm", "import Data=\"data.bmasm\";\n" + Header + "    stp\n"),
            ("dataview.bmasm", "import Data=\"data.bmasm\";\n" + DataView),
            ("data.bmasm", "file dataview $21\n.data_label:\n")));

        AssertError(e, "dataview.bmasm", 1, "Circular import: main.bmasm -> data.bmasm -> dataview.bmasm -> data.bmasm.");
    }

    [TestMethod]
    public async Task SharedImportIsNotCircular()
    {
        // a diamond: a and b both import c
        await X16TestHelper.BuildTemplateFiles(Files(
            ("main.bmasm", "import A=\"a.bmasm\";\nimport B=\"b.bmasm\";\n" + Header + "    stp\n"),
            ("a.bmasm", "library Test.A;\nimport C=\"c.bmasm\";\n"),
            ("b.bmasm", "library Test.B;\nimport C=\"c.bmasm\";\n"),
            ("c.bmasm", "library Test.C;\n")));
    }

    [TestMethod]
    public async Task LibrariesCanShareANamespace()
    {
        // each library is its own assembly, which mustn't be named by the namespace alone
        var (emulator, _, _) = await X16TestHelper.EmulateTemplateFiles(Files(
            ("main.bmasm", "import A=\"a.bmasm\";\nimport B=\"b.bmasm\";\n" + Header + "    lda #@(Test.A.Value + Test.B.Value)\n    stp\n"),
            ("a.bmasm", "library Test.A;\npublic static int Value = 0x10;\n"),
            ("b.bmasm", "library Test.B;\npublic static int Value = 0x20;\n")));

        Assert.AreEqual(0x30, emulator.A);
    }
}
