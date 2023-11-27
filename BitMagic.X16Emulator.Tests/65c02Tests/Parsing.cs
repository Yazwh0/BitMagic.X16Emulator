using BitMagic.Compiler.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class Parsing
{
    [TestMethod]
    public async Task Stz_AbsInd_Error()
    {
        var emulator = new Emulator();

        var exception = false;
        try
        {
            await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stz ($abcd), x
                stp",
                emulator);
        } 
        catch (CannotCompileException)
        {
            exception = true;
        }

        Assert.IsTrue(exception, "No exception hit.");
    }

    [TestMethod]
    public async Task Stz_AbsZpInd_Error()
    {
        var emulator = new Emulator();

        var exception = false;
        try
        {
            await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stz ($ab), x
                stp",
                emulator);
        }
        catch (CannotCompileException)
        {
            exception = true;
        }

        Assert.IsTrue(exception, "No exception hit.");
    }

    [TestMethod]
    public async Task Stz_Ind_Error()
    {
        var emulator = new Emulator();

        var exception = false;
        try
        {
            await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stz ($abcd)
                stp",
                emulator);
        }
        catch (CannotCompileException)
        {
            exception = true;
        }

        Assert.IsTrue(exception, "No exception hit.");
    }
}