# Failing emulator tests: plan

A full run on 2026-11-08 (2,253 tests, about 11 minutes) had 50 failures. Each was checked to see whether the test or the
emulator core is wrong, against the official emulator (X16Community/x16-emulator, master: `src/vera_psg.c`, `src/video.c`,
`src/main.c`) where the hardware behaviour mattered.

| | Tests | Status |
| --- | --- | --- |
| The test was wrong | 24 | Fixed in the tests |
| The core is wrong, the test is right | 15 | 13 fixed (sprite layers, AFLOW). 2 turned out to be something else, see below |
| Both wrong | 11 | Fixed in the core and the tests |

## Running just these

Build with MSBuild, then run the project (not the dll, see CLAUDE.md), with a filter rather than the full suite.

The 2 still failing (the filter runs 3: `Name=` ignores case, so `Vstart` also picks up `Background.VStart`, which passes):

```powershell
dotnet test BitMagic.X16Emulator/BitMagic.X16Emulator.Tests/BitMagic.X16Emulator.Tests.csproj --no-build -p:Platform=x64 --filter "Name=Vstart|Name=Normal_VideoStart"
```

The classes touched by the test fixes, to check nothing else in them broke:

```powershell
dotnet test BitMagic.X16Emulator/BitMagic.X16Emulator.Tests/BitMagic.X16Emulator.Tests.csproj --no-build -p:Platform=x64 --filter "FullyQualifiedName~Via.Timer|FullyQualifiedName~Interrupt_Vsync|FullyQualifiedName~Interrupt_Line|FullyQualifiedName~PsgRegisters|FullyQualifiedName~PsgPlay|FullyQualifiedName~LineDrawHelper|FullyQualifiedName~PcmPlay|FullyQualifiedName~Core.EmulatorTests|FullyQualifiedName~Vera_Initialise|FullyQualifiedName~Vera.Background|FullyQualifiedName~Vera.Display"
```

The order of work: fix a group, run the filter above, then a full run only once everything here is done.

## Done: the test was wrong (24)

| Tests | Why | Change |
| --- | --- | --- |
| `VeraFx/LineDrawHelper.cs`: `SetXIncr_Top`, `SetXIncr_Top_Set23x`, `SetXIncr_Top_NotLineDraw`, `SetXIncr_Bottom` (4) | The X increment is 6.9 fixed point (1/512 of a pixel). The core stores it as 16.16 (`shl 7`, `Vera.asm` dc_sel2/dc_sel3), to match the position (half a pixel is `0x8000`, a step is `0x10000`). The tests expected `shl 6`, which would make every line half as steep; the core has done `shl 7` since the helper was added in 2024, so they never passed. | Expected values doubled. `AddrMode = 2` was commented "line draw" but 2 is polygon fill; changed to 1. |
| `VeraAudio/PsgRegisters.cs`: `Volume1_*`, `Volume16_*`, `VolumeMax_*` (voices 0, 1, 15) and `SideVolume_Voice0` (10) | `0b6b0e6` (2024-10-12) moved the core to the 9 bit volume table (0, 4, 8, 12 ... 511), which is exactly `volume_lut` in the official `vera_psg.c`. The old table is still in `Vera_Audio.asm` as `psg_volume_old`. The tests still expected the old one. | Expect 4, 33 and 511 for 1, 16 and 63. |
| `Vera/Interrupt_Vsync.cs`: `Hit_Disabled`, `Hit_SetIen_Return`, `Hit_SetIen_Return_ReEnable`, `Hit_OnlyOnce_Clear` (4) | They expected the LINE flag to stay clear when the line interrupt isn't enabled. In the official `video.c` the flag is set whenever the beam reaches IRQLINE (`if (y == compare) isr \|= 2;`) and IEN only gates the IRQ (`video_get_irq_out`). IRQLINE is 0 by default, so it's hit every frame. | Expect `Interrupt_Line_Hit` true and LINE in `$9F27`. |
| `Via/Timer1.cs`: `Timer1_Interrupt_OneShot`, `Timer1_Interrupt_OneShot_ClearT1Cl`, `Timer1_Interrupt_Continuous`; `Via/Timer2.cs`: `Timer2_Interrupt_OneShot` (4) | They expected VIA1 to raise an NMI. `6ee93bb` (2024-02-03, "Change VIA1 to fire IRQ not NMI") changed the core, which matches the official `main.c` (`via1_irq()` is ORed into `irq6502()`, nothing calls `nmi6502()` for the VIA). The IRQ vector was empty, so they hit a BRK. | Use the IRQ vector (`$FFFE`) and check `Via.Interrupt` rather than `Nmi`. IRQ is level triggered, so `OneShot`'s handler now stops instead of `rti` (which would take the interrupt again forever), and `ClearT1Cl` uses `sei` like its siblings. The passing `ClearIen` and `ClearT1Lh` were moved over too, so none still describe NMI. |
| `EmulatorTests/Emulator.cs`: `Interrupt_Notset` (1) | It used to set `Interrupt = false`. `0b6b0e6` replaced every `Interrupt = ...` in the file with `InterruptHit = Vsync; InterruptMask = Vsync`, including this one, so it set up a pending interrupt and expected none. | `InterruptHit = None`. |
| `VeraAudio/PcmPlay.cs`: `Play_8bitMono` (1) | It expected 171, `$AB` as unsigned. PCM samples are signed: the passing `Play_8bitMono_16Volume` (volume 64) expects `0xab00`, so the output is `(sample << 8) * volume >> 6`, and volume 1 gives -340, as the core does. | Expect `(short)0xab00 * 1 >> 6`. |

## To do: core bugs, the tests are right (15)

These need `.asm` changes, so check with the user first.

### Done: sprite layers got garbage when sprites were disabled (10 of the 12)

Fixed in `Vera_Display.asm` as described below, on 2026-11-08. Results:

- 10 of the 12 pass.
- `Sprites_8bpp_64x64.Disabeled`, which passed before, then failed: its reference image
  (`Vera/Images/sprites_8bpp_64x64_disabled.png`) had been captured with the bug, and every one of its differences was
  `0x00000200` expected where the core now writes 0. The image was replaced with the new output.
- `Vstart` and `Normal_VideoStart` still fail, for a different reason that the garbage at 0,0 was hiding. See "Sprites are
  a line out with a display start" below.
- Everything in `Vera.Background`, `Vera.Display`, `Sprites*` and `Tiles*`: 207 pass, only those 2 fail.

The original notes:

`Vera/Background.cs`: `Area`, `Colour`, `HStart`, `HStop`, `VStart`, `VStop`. `Vera/Display/Sprites_8bpp_64x64.cs` (3),
`Sprites_8bpp_8x8.cs` (1), `Tiles_1Bpp.cs` (2): `Vstart`, `EnabledMidway`, `DisabeledMidway`, `Normal_VideoStart`,
`Normal_192HScale_Layer0`, `Normal_192HScale_Layer1`.

Every difference is a pixel that should be `0,0,0,0` coming out as `0,2,0,0` (still alpha 0, so invisible), in display
layers 1, 3 and 5: the sprite layers (`SPRITE_L1`, `SPRITE_L2`, `SPRITE_L3`).

In `Vera_Display.asm`, the sprite part of the pixel loop does

```asm
mov al, byte ptr [rdx].state.sprite_enable
test al, al
jz sprites_not_enabled
...
sprites_not_enabled:     ; rax will be zero, so no write
mov dword ptr [rdi + r9 * 4 + SPRITE_L1], eax
```

Only `al` is cleared, so whatever is in the rest of `eax` (here `0x200`) is written. `sprites_not_enabled` is also the
`depth_0` label, which writes `r11d` to `SPRITE_DEBUG`. On the enabled path `r11d` has just been loaded with the debug
colour, but on the disabled path it's still the line buffer index, so the debug layer gets the pixel's x position as a
colour.

Fix: give the disabled path its own two instructions before falling into `depth_0`:

```asm
sprites_not_enabled:
	xor eax, eax			; transparent in the sprite layers
	xor r11d, r11d			; and in the debug layer
depth_0:
	mov dword ptr [rdi + r9 * 4 + SPRITE_L1], eax
	...
```

`depth_0` (sprites on, no sprite at this pixel) is reached through the jump table with `eax` already zeroed and
`r11d` holding the debug colour, so it's unchanged. The extra instructions only run when sprites are off.

The similar `mov al, byte ptr [rdx].state.sprite_enable` in `Vera.asm` (the DC_VIDEO read, `dc_sel0`) is fine: only `al`
is stored after the shift, so the upper bits can't reach it.

Check: the 12 tests, then everything in `Vera.Background` and `Vera.Display` (which includes the sprite tests that pass
now), then look at a sprite debug view in X16E with sprites turned off and on.

### Sprites are a line out with a display start (2)

`Vera/Sprites_8bpp_64x64.cs`: `Vstart` (`sprites_8bpp_64x64_vstart.png`). `Vera/Sprites_8bpp_8x8.cs`: `Normal_VideoStart`
(`sprites_8bpp_8x8_videostart.png`).

Found once the sprite layer garbage was fixed. Every difference is in sprite layer 1 (`SPRITE_L1`), and the sprites are a
line out:

- `Vstart`: 840 pixels differ. The expected sprite covers rows 30 to 93 (64 lines), the actual 30 to 92 (63 lines); inside
  it, 343 pixels go from white to clear and 336 from clear to white, as if one line of the sprite is skipped.
- `Normal_VideoStart`: 6,340 pixels differ, the same pattern; the sprites' last row moves from 478 to 479.

Not caused by the sprite fix (which only changes pixels when sprites are off) or by the render ahead and vsync breakpoint
changes (which don't touch rendering).

#### What VERA does

From the VERA Verilog (X16Community/vera-module, main, `fpga/source/graphics/composer.v`, `sprite_renderer.v`, `top.v`,
`video/video_vga.v`). The official emulator isn't reliable here.

- The line number the layers and sprites draw (`line_idx`) is a scaled counter, `scaled_y_counter_r[15:7]`. It's set to
  0 (not moved on) on the first line where the composer's line counter reaches `active_vstart`, then has `DC_VSCALE`
  added once per line while active, up to 480.
- Each line is rendered one line ahead into a double buffer (`video_vga.v`: "Start rendering one line earlier"), so the
  first active line (`scanline == vstart`) shows `line_idx` 0, the next shows 1 (at a scale of 128), and so on.
- `DC_VSTART` is in 2 line units: `dc_active_vstart_next[8:1] = write_data; [0] = 0`. `DC_HSTART` is in 4 pixel units.
- A sprite covers a line when `line_idx - y <= height` (10 bit, so lines above it wrap and fail), and the row drawn is
  `line_idx - y` (or `height - that` flipped).
- Horizontally the line buffer index is 0 at the first active pixel (`x_counter == active_hstart`), so sprite and layer
  x are relative to `DC_HSTART`.

So with `DC_VSTART = $0F` (line 30) and a 64 high sprite at y = 1: line 30 shows none of it, lines 31 to 93 show its
rows 0 to 62 (its row 63 is blank). With `DC_VSTART = 1` (line 2) and sprites at y = 0: line 2 shows their row 0.

#### Which side is wrong: both

Row by row (sprite layer 1):

| | First active line | After that |
| --- | --- | --- |
| VERA | `line_idx` 0 | 1, 2, 3 ... |
| Reference images (from the emulator in 2023) | 1 | 1, 2, 3 ... (right) |
| Core now | 1 | 2, 3, 4 ... (one line early throughout) |

`Vstart`: the reference has rows 31 to 93 right, but row 30 repeats row 31. The core has the sprite on rows 30 to 92.
`Normal_VideoStart`: the reference has rows 3 onwards right (row 8 is sprite row 6), but row 2 repeats row 3. The core is
one line early everywhere, top and bottom. Horizontally both are right (`DC_HSTART = 2` puts x 0 at column 8).

#### Why

In `Vera_Display.asm`:

- `renderstep_reset_buffer` (the line before the frame wraps) sets `scale_y` to 0.
- `renderstep_next_line` adds `dc_vscale` when `vstart <= beam_y < vstop`.
- `renderstep_next_frame` (the wrap to line 0) always adds `dc_vscale`, with no vstart check.

With `vstart` 0 the add at the wrap is the right one, so every other sprite and layer test passes. With `vstart` above 0
it moves the counter on before the active area starts, so everything is a line early.

Before `4b8ab2f`, `renderstep_next_line` used `jge` (add only when `beam_y > vstart`). That cancelled the extra add
except on the first active line, which is exactly what the reference images show. `4b8ab2f` changed it to `jg`, which is
the right rule on its own, but with the add at the wrap it moved every line.

#### Plan

1. Core (`Vera_Display.asm`, needs the go ahead): in `renderstep_next_frame`, only add `dc_vscale` when line 0 is in the
   active area, the same check `renderstep_next_line` makes (`vstart <= 0 < vstop`, so `dc_vstart == 0` and
   `dc_vstop > 0`). Keep `jg` in `renderstep_next_line`. With `vstart` 0 nothing changes. With `vstart` above 0, the
   counter stays at 0 until the line before the first active line, which matches VERA's reset to 0 at `vstart`.
2. Check the new output against VERA before touching the reference images: it should be identical to the current
   reference everywhere except the first active line, which should show `line_idx` 0 (`Vstart`: row 30 empty of sprite;
   `Normal_VideoStart`: row 2 shows sprite row 0, the same row the reference shows at 474). Diff with the fast
   (LockBits) comparison.
3. Replace `sprites_8bpp_64x64_vstart.png` and `sprites_8bpp_8x8_videostart.png` with the new output.
4. Add tests for what isn't covered:
   - a layer (bitmap) with `DC_VSTART` set, as the layers use the same counter and were a line early too;
   - `DC_VSTART` with `DC_VSCALE` other than 128 (eg 64, each line shown twice from the first active line), where the
     first fractional step matters.
   These can check where the first rows land rather than compare whole images.
5. Run everything in `Vera.*` (display, sprites, tiles, bitmaps, background, display registers), the beam breakpoint,
   render ahead and vsync breakpoint tests (they step the renderer), then the full suite, as this was the last group.

Only these two tests use a non-zero `DC_VSTART`: `Background.VStart` has no layer or sprite content, and the
`Sprites_*_16x8` tests write 0.

### Done: AFLOW (3)

Both fixes below were made on 2026-11-08 (`and eax, 0111b` to `01111b` in each place in `Vera.asm`). The 3 tests pass,
and so does everything in `Interrupt_Vsync`, `Interrupt_Line`, `Vera_Initialise`, the `Pcm*` tests, `Core.EmulatorTests`
and `Interrupts` (124 tests).

### Writing ISR wipes AFLOW (2)

`Hit_Reset` in `Vera/Interrupt_Vsync.cs` and `Vera/Interrupt_Line.cs`: `$9F27` reads 0 instead of AFLOW (8) after the
handler writes `ISR`.

AFLOW is read only status: the official `video.c` ORs it in when ISR is read (`return isr | (pcm_is_fifo_almost_empty() ? 8 : 0);`)
and an ISR write (`isr &= value ^ 0xff;`) can't affect it. In `Vera.asm`, `vera_update_isr` rebuilds the register from
`interrupt_hit` with `and eax, 0111b`, which drops bit 3, so AFLOW is gone until the next audio step recalculates it.

Fix: keep bit 3 from `interrupt_hit` (`and eax, 01111b`), while still only letting a write clear bits 0 to 2.

### VERA init drops the AFLOW enable (1)

`Vera/Initialise.cs`: `Interrupt_Aflow` sets `Vera.Interrupt_AFlow` and expects IEN (`$9F26`) to be 8; it's 0.

`vera_init` in `Vera.asm` builds IEN from `interrupt_mask` with `and eax, 0111b`. Before `0b6b0e6` it included
`interrupt_aflow << 3`.

Fix: `and eax, 01111b`.

## Done: both wrong (11)

Done on 2026-11-08, as planned below:

- `Vera_Audio.asm`: the five fixes in the `vera_psg` macro. The code that kept `state.psg_noise` up to date was removed
  (nothing read it); the field stays.
- `VeraAudio/PsgReference.cs`: the port of the official `render()`.
- `VeraAudio/PsgPlay.cs`: every test now plays a voice and compares the first 64 stereo samples with the reference, and
  fails if both are silent. The test names are kept. Changes beyond that:
  - `Pulse_Both_VolumeMax_Voice0` uses 511, the real maximum after the volume table, rather than 64.
  - New: `Pulse_Left_HalfWidth`, `Saw_Left_Width32`, `Triangle_Left_Width32` and `Noise_Left_Width0` (the width
    handling), and `Reference` (the port against the hand worked values below).
  - The commented out `Pulse_Left_MultipleVoice` was removed.
- Checked: everything in `PsgPlay`, `PsgRegisters`, `Pcm*` and `AudioRecording` passes (76 tests). Still to do: listen to
  PSG music in X16E, especially for the left and right fix.

The original notes:

`VeraAudio/PsgPlay.cs`: `Pulse_Left_Voice0`, `Pulse_Left_Voice2`, `Pulse_Left_Voice15`, `Pulse_Right_Voice0`,
`Pulse_Both_Voice0`, `Pulse_Both_Volume2_Voice0`, `Pulse_Both_VolumeMax_Voice0`, `Saw_Left_Voice0`, `Triangle_Left_Voice0`,
`Noise_Low_Left_Voice0`, `Noise_High_Left_Voice0`.

The tests: every expected value is 8 times too big. The official output is `sv * volume >> 3` per channel (`l += val >> 3;`),
and the core's `sar ecx, 3` is right. The tests also start some waveforms a sample out.

The core: the waveforms differ from the official `render()` in `vera_psg.c`, in the `vera_psg` macro in `Vera_Audio.asm`:

| Wave | Official | Core |
| --- | --- | --- |
| Pulse | `((phase >> 10) > pw) ? 0 : 0x3F` | the comparison is the other way round, so it's inverted |
| Saw | `(phase >> 11) ^ ((pw ^ 0x3f) & 0x3f)` | `xor` with `pw`, so at the default width of 63 it's upside down |
| Triangle | from the new phase, then `^ ((pw ^ 0x3f) & 0x3f)` | uses the phase from before the add (`ecx`), and `xor` with `pw` |
| Noise | reloads `noiseval = (noise_state >> 1) & 0x3F` only on a falling edge of bit 16, and doesn't use `pw` | reloads on both edges, and masks the value with `pw ^ 0x3f` |

The official phase is advanced before the wave is worked out:
`new_phase = (left \|\| right) ? ((phase + freq) & 0x1FFFF) : 0`, and `sv = (v ^ 0x20)` sign extended from bit 5 (the core's
`sub ecx, 32` is the same thing).

There's also a fifth problem, in how a voice is added to the left and right totals:

```asm
	test ebx, 1			; left?
	je right
	add r12d, ecx		; left total
	je done				; <-- tests the flags from the add, not the right bit
right:
	add r13d, ecx		; right total
```

So a left only voice is also added to the right whenever the left total so far isn't zero, and a voice on both sides
skips the right whenever the left total happens to be zero. Official: `if (ch->left) l += val >> 3; if (ch->right) r += val >> 3;`.

### Plan

Step 1, the core: all in the `vera_psg` macro in `Vera_Audio.asm`, which is expanded once per voice, so each change
covers all 16. Needs the go ahead, as it's `.asm`.

| | Change |
| --- | --- |
| Pulse | After `and ecx, 03fh`, add `xor ecx, 03fh`, so it's `0x3F` while `phase >> 10 <= pw`, and 0 after. |
| Saw | After `xor ecx, [rsi].psg_voice.widthx`, add `xor ecx, 03fh`, making it `^ (pw ^ 0x3f)`. `phase >> 11` is never over 63, so this is exact. |
| Triangle | Start with `mov ecx, eax` (the new phase; `ecx` is the old one at that point), and the same `xor ecx, 03fh` after the width. |
| Noise | Reload only on a falling edge of bit 16 (old phase has it, new phase doesn't), from `(psg_noise_signal >> 1) & 3fh`, with no width mask. Do the reload before the waveform jump, as the official code does for every waveform, so switching to noise picks up the right value. `state.psg_noise` (the core's own 6 bit history, which is one step out from the official value) is then unused, but stays in `State.asm` and the C# struct, as their layouts must match. |
| Left/right | Replace `je done` with `test ebx, 2` / `jz done`. |

The LFSR itself already matches: it starts at 1 (`PsgNoiseSignal = 1`), uses the same taps, and steps once per voice per
sample, as the macro runs for all 16 voices.

Step 2, the tests: take the expected values from the official code rather than typing them in. Add a small C# port of
the official `render()` (phase, the four waves, the LFSR, volume and `>> 3`, left and right) to the test project, and have
each `PsgPlay` test compare the core's first samples with it, for the same voice settings. The noise tests especially
need this, as their values depend on the LFSR. The hand worked values below are a check on the port.

Step 3, check: the 11 tests, `PsgRegisters`, `PcmPlay` and `AudioRecording` (the other audio tests; none of the passing
tests depends on PSG output, and `Pulse_Left_MultipleVoice` is commented out), then listen to something with PSG music
in X16E.

Worked out from the official code, with a volume of 1 and width 63, starting from phase 0:

- Pulse, frequency `$FFFF`: 3, -4, 3, -4 ... (volume 2: 7, -8; volume 64: 248, -256).
- Saw, frequency `$4000` (8 samples a cycle): -3, -2, -1, 0, 1, 2, 3, -4.
- Triangle, frequency `$4000`: -2, 0, 2, 3, 1, -1, -3, -4.
- Noise: depends on the LFSR (`noise_state` starts at 1 and steps once per voice per sample); work out from the official
  code once the core matches it.

What the core gives now, for reference: saw 2, 1, 0, -1, -2, -3, -4, 3; triangle 3, 1, -1, -3, -4, -2, 0, 2; pulse -4, 3,
-4, 3.

## Not related

The uncommitted `Core.asm` and `State.asm` changes (render ahead, the vsync breakpoint) don't touch any of the code above.
