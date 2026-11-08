# Failing emulator tests: plan

A full run on 2026-11-08 (2,253 tests, about 11 minutes) had 50 failures. Each was checked to see whether the test or the
emulator core is wrong, against the official emulator (X16Community/x16-emulator, master: `src/vera_psg.c`, `src/video.c`,
`src/main.c`) where the hardware behaviour mattered.

| | Tests | Status |
| --- | --- | --- |
| The test was wrong | 24 | Fixed in the tests |
| The core is wrong, the test is right | 15 | 13 fixed (sprite layers, AFLOW). 2 turned out to be something else, see below |
| Both wrong | 11 | To do: core first, then the tests |

## Running just these

Build with MSBuild, then run the project (not the dll, see CLAUDE.md), with a filter rather than the full suite.

The 13 still failing (the filter runs 14: `Name=` ignores case, so `Vstart` also picks up `Background.VStart`, which passes):

```powershell
dotnet test BitMagic.X16Emulator/BitMagic.X16Emulator.Tests/BitMagic.X16Emulator.Tests.csproj --no-build -p:Platform=x64 --filter "Name=Vstart|Name=Normal_VideoStart|Name=Noise_High_Left_Voice0|Name=Noise_Low_Left_Voice0|Name=Pulse_Both_Voice0|Name=Pulse_Both_Volume2_Voice0|Name=Pulse_Both_VolumeMax_Voice0|Name=Pulse_Left_Voice0|Name=Pulse_Left_Voice15|Name=Pulse_Left_Voice2|Name=Pulse_Right_Voice0|Name=Saw_Left_Voice0|Name=Triangle_Left_Voice0"
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

Not yet known which is right. Both tests set a display start (`DC_VSTART` and the like), and `4b8ab2f` (2025-12-30, "Fix
vstop bug and adds line irqs past line 480") changed that area, after these tests were already failing at 0,0, so nothing
showed whether it changed them. To do: check how the official `video.c` picks the sprite line when the display start isn't
0, then decide whether the core or the reference images are wrong.

Not caused by the sprite fix (which only changes pixels when sprites are off) or by the uncommitted render ahead and vsync
breakpoint changes (which don't touch rendering).

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

## To do: both wrong (11)

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

Order: fix the core, then set the tests to the official values, not to whatever the core outputs. Worked out from the
official code, with a volume of 1 and width 63, starting from phase 0:

- Pulse, frequency `$FFFF`: 3, -4, 3, -4 ... (volume 2: 7, -8; volume 64: 248, -256).
- Saw, frequency `$4000` (8 samples a cycle): -3, -2, -1, 0, 1, 2, 3, -4.
- Triangle, frequency `$4000`: -2, 0, 2, 3, 1, -1, -3, -4.
- Noise: depends on the LFSR (`noise_state` starts at 1 and steps once per voice per sample); work out from the official
  code once the core matches it.

What the core gives now, for reference: saw 2, 1, 0, -1, -2, -3, -4, 3; triangle 3, 1, -1, -3, -4, -2, 0, 2; pulse -4, 3,
-4, 3.

## Not related

The uncommitted `Core.asm` and `State.asm` changes (render ahead, the vsync breakpoint) don't touch any of the code above.
