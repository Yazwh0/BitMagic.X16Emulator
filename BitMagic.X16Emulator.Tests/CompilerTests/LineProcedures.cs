using BitMagic.Compiler;
using BitMagic.Compiler.Files;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

// CompileState.ProcedureAt: the procedure every line is in, for the hover
[TestClass]
public class LineProcedures
{
    [TestMethod]
    public async Task EveryLineHasItsProcedure()
    {
        // the expected scope is after the '|' on each line
        var code = @"
.machine CommanderX16R40            | App:Main
.org $810                           | App:Main
    lda #1                          | App:Main
.proc outer                         | App:Main:outer
    .const C 1                      | App:Main:outer

    ; a comment                     | App:Main:outer
.loop:                              | App:Main:outer
    .proc inner                     | App:Main:outer:inner
        nop                         | App:Main:outer:inner
    .endproc                        | App:Main:outer
    rts                             | App:Main:outer
.endproc                            | App:Main
.scope text                         | App:text
    .const T 2                      | App:text
    .proc print                     | App:text:print
        rts                         | App:text:print
    .endproc                        | App:text
.endscope                           | App:Main
.segment data $a000                 | App:Main       ; a segment with no scope is in Main
    .padvar byte x                  | App:Main
.endsegment                         | App:Main
    stp                             | App:Main";

        var lines = code.Split('\n').Select(i => i.TrimEnd('\r')).ToArray();
        var source = string.Join('\n', lines.Select(i => i.Contains('|') ? i[..i.IndexOf('|')] : i));

        var project = new Project { Code = new StaticTextFile(source, "unittest.bmasm") };
        var result = await new BitMagic.Compiler.Compiler(project, new(), new NullLogger()).Compile();

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var procedure = result.State.ProcedureAt(lineNumber);
            Assert.IsNotNull(procedure, $"line {lineNumber}");

            var expected = lines[i].Contains('|') ? lines[i][(lines[i].IndexOf('|') + 1)..].Split(';')[0].Trim() : null;

            // a blank line is in whatever the line before it left
            if (expected == null)
                expected = result.State.ProcedureAt(lineNumber - 1)?.Variables.FullName;

            Assert.AreEqual(expected, procedure.Variables.FullName, $"line {lineNumber}: {lines[i].Trim()}");
        }

        Assert.IsNull(result.State.ProcedureAt(lines.Length + 1));
    }
}
