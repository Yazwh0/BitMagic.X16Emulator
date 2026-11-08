using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.InteropServices;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class RenderAhead
{
    // a 320x240 8bpp bitmap doubled up to fill the screen, then a loop that doesn't touch vera
    private const string Program = @"
                .machine CommanderX16R40
                .org $810
                sei
                lda #$11        ; vga, layer 0
                sta DC_VIDEO
                lda #$07        ; bitmap, 8bpp
                sta L0_CONFIG
                stz L0_TILEBASE ; 320 wide from 0
                lda #64
                sta DC_HSCALE
                sta DC_VSCALE
        .loop:
                nop
                inx
                jmp loop";

    private const int LayerSize = Emulator.BeamWidth * Emulator.BeamHeight * 4;
    private const int LineSize = Emulator.BeamWidth * 4;

    [TestMethod]
    public async Task FinishFrame_MatchesRunningToVsync()
    {
        using var emulator = await Paused();
        var cycles = emulator.RenderAheadCycles(Emulator.DisplayRenderMode.FinishFrame);

        AssertAbout(240 * Emulator.BeamCyclesPerLine, cycles); // line 240 to vsync

        var preview = Render(emulator, cycles);

        RunToVsync(emulator, 1);

        CollectionAssert.AreEqual(Visible(emulator.DisplayRaw), preview);
    }

    [TestMethod]
    public async Task FromState_MatchesAWholeFrame()
    {
        using var emulator = await Paused();
        var cycles = emulator.RenderAheadCycles(Emulator.DisplayRenderMode.FromState);

        AssertAbout(240 * Emulator.BeamCyclesPerLine + Emulator.BeamCyclesPerFrame, cycles);

        var preview = Render(emulator, cycles);
        var finish = Render(emulator, emulator.RenderAheadCycles(Emulator.DisplayRenderMode.FinishFrame));

        CollectionAssert.AreNotEqual(finish, preview, "The top half should be from the edited vram.");

        RunToVsync(emulator, 2); // the end of this frame, then a whole frame from the edited vram

        CollectionAssert.AreEqual(Visible(emulator.DisplayRaw), preview);
    }

    [TestMethod]
    public async Task FromState_InBlanking_IsOneFrame()
    {
        using var emulator = await Paused();

        // into the blanking lines after vsync, the edited bitmap hasn't been drawn yet
        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 500)]);
        Assert.AreEqual(Emulator.EmulatorResult.Breakpoint, emulator.EmulateWithTimeout());
        Assert.AreEqual(500, emulator.Vera.Beam_Y);

        var cycles = emulator.RenderAheadCycles(Emulator.DisplayRenderMode.FromState);
        AssertAbout(Emulator.BeamCyclesPerFrame - 20 * Emulator.BeamCyclesPerLine, cycles); // to the next vsync only

        var preview = Render(emulator, cycles);

        RunToVsync(emulator, 1);

        CollectionAssert.AreEqual(Visible(emulator.DisplayRaw), preview);
    }

    [TestMethod]
    public async Task ToBeam_MatchesRunningThere()
    {
        using var emulator = await Paused();

        // behind the beam, so it wraps into the next frame
        var cycles = emulator.CyclesToBeam(0, 100);
        AssertAbout(Emulator.BeamCyclesPerFrame - 140 * Emulator.BeamCyclesPerLine, cycles);

        byte[] preview = [];
        emulator.RenderAhead(cycles, () => preview = Visible(emulator.DisplayRaw, 100));

        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 100)]);
        Assert.AreEqual(Emulator.EmulatorResult.Breakpoint, emulator.EmulateWithTimeout());
        Assert.AreEqual(100, emulator.Vera.Beam_Y);

        // the cpu stops a few dots into line 100, so only the lines above it
        CollectionAssert.AreEqual(Visible(emulator.DisplayRaw, 100), preview);
    }

    [TestMethod]
    public async Task LeavesTheMachineAlone()
    {
        using var emulator = await Paused();

        var state = StateBytes(emulator);
        var display = emulator.DisplayRaw.ToArray();
        var displayBuffer = emulator.DisplayBuffer.ToArray();
        var changed = false;

        emulator.RenderAhead(emulator.RenderAheadCycles(Emulator.DisplayRenderMode.FromState), () => changed = !emulator.DisplayRaw.SequenceEqual(display));

        Assert.IsTrue(changed, "The display should have been rendered.");
        Assert.AreEqual(0ul, emulator.State.Render_Ahead);
        CollectionAssert.AreEqual(state, StateBytes(emulator));
        CollectionAssert.AreEqual(display, emulator.DisplayRaw.ToArray());
        CollectionAssert.AreEqual(displayBuffer, emulator.DisplayBuffer.ToArray());
    }

    [TestMethod]
    public async Task CarriesOnTheSame()
    {
        using var previewed = await Paused();
        using var plain = await Paused();

        previewed.RenderAhead(previewed.RenderAheadCycles(Emulator.DisplayRenderMode.FromState), () => { });

        RunToVsync(previewed, 2);
        RunToVsync(plain, 2);

        Assert.AreEqual(plain.Clock, previewed.Clock);
        Assert.AreEqual(plain.Pc, previewed.Pc);
        Assert.AreEqual(plain.X, previewed.X);
        Assert.AreEqual(plain.Vera.Beam_Position, previewed.Vera.Beam_Position);
        CollectionAssert.AreEqual(plain.DisplayRaw.ToArray(), previewed.DisplayRaw.ToArray());
    }

    // the cpu stops up to an instruction past the beam position, so it's a little closer
    private static void AssertAbout(ulong expected, ulong cycles) =>
        Assert.IsTrue(cycles <= expected && expected - cycles < 16, $"Expected about {expected}, was {cycles}.");

    // stopped on line 240 of the second frame, with the bitmap changed since it was drawn
    private static async Task<Emulator> Paused()
    {
        var emulator = new Emulator(); // caller disposes
        emulator.InterruptMask = InterruptSource.None;

        Fill(emulator, 0);
        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 240)]);

        await X16TestHelper.Emulate(Program, emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);
        Assert.AreEqual(Emulator.EmulatorResult.Breakpoint, emulator.EmulateWithTimeout());
        Assert.AreEqual(240, emulator.Vera.Beam_Y);

        Fill(emulator, 1); // like editing vram while paused

        return emulator;
    }

    private static void Fill(Emulator emulator, int pattern)
    {
        var vram = emulator.Vera.Vram;
        for (var i = 0; i < 320 * 240; i++)
            vram[i] = (byte)(pattern == 0 ? i % 320 + i / 320 : i % 320 * 3 - i / 320);
    }

    private static void RunToVsync(Emulator emulator, int count)
    {
        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, Emulator.BeamVsyncLine)]);

        for (var i = 0; i < count; i++)
            Assert.AreEqual(Emulator.EmulatorResult.Breakpoint, emulator.EmulateWithTimeout(), $"Vsync {i}");
    }

    private static byte[] Render(Emulator emulator, ulong cycles)
    {
        byte[] toReturn = [];
        emulator.RenderAhead(cycles, () => toReturn = Visible(emulator.DisplayRaw));
        return toReturn;
    }

    // each layer's first lines, by default the visible ones as the lines after vsync aren't drawn
    private static byte[] Visible(Span<byte> display, int lines = Emulator.BeamVsyncLine)
    {
        var toReturn = new List<byte>();
        for (var layer = 0; layer * LayerSize < display.Length; layer++)
            toReturn.AddRange(display.Slice(layer * LayerSize, lines * LineSize).ToArray());

        return toReturn.ToArray();
    }

    private static byte[] StateBytes(Emulator emulator)
    {
        var state = emulator.State;
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref state, 1)).ToArray();
    }
}
