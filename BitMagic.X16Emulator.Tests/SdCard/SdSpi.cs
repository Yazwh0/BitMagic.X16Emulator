namespace BitMagic.X16Emulator.Tests.SdCard;

// Drives the SD card over SPI from 6502 code, as a program would. A test is a list of steps, and every byte the card
// sends back is kept, in order, from Capture.
internal static class SdSpi
{
    public const int Capture = 0x4000;
    public const int CaptureLength = 0x800;

    public static byte[] Command(int command, uint block) =>
        [(byte)(0x40 | command), (byte)(block >> 24), (byte)(block >> 16), (byte)(block >> 8), (byte)block, 0x01];

    // a step: send these bytes, keeping what comes back
    public static string Send(IEnumerable<byte> bytes) =>
        string.Concat(bytes.Select(b => $"lda #${b:X2}\n jsr exchange\n"));

    // a step: clock out this many bytes with $FF, keeping what comes back
    public static string Clock(int count) => $@"
            lda #<{count}
            sta $24
            lda #>{count}
            sta $25
            jsr clock";

    // a step: turn on auto transmit, where reading SPI_DATA clocks out the next byte
    public const string AutoTx = @"
            lda #$05        ; select, auto transmit
            sta SPI_CTRL";

    // a step: read SPI_DATA this many times (with auto transmit on), keeping each byte
    public static string AutoRead(int count) => $@"
            lda #<{count}
            sta $24
            lda #>{count}
            sta $25
            jsr auto_read";

    public static async Task<byte[]> Run(X16Emulator.SdCard card, params string[] steps)
    {
        using var emulator = new Emulator();
        emulator.LoadSdCard(card);

        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                    sei
                    lda #<${Capture:X4}
                    sta $22
                    lda #>${Capture:X4}
                    sta $23
                    lda #1
                    sta SPI_CTRL    ; select the card
                    {string.Join("\n", steps)}
                    stp

                ; sends A, keeps what came back
                .proc exchange
                    sta SPI_DATA
                    lda SPI_DATA
                    jmp store
                .endproc

                ; stores A at ($22) and moves it on
                .proc store
                    ldy #0
                    sta ($22), y
                    inc $22
                    bne done
                    inc $23
                .done:
                    rts
                .endproc

                ; exchanges $FF ($24/$25) times
                .proc clock
                .loop:
                    lda #$ff
                    jsr exchange
                    jsr count_down
                    bne loop
                    rts
                .endproc

                ; reads SPI_DATA ($24/$25) times
                .proc auto_read
                .loop:
                    lda SPI_DATA
                    jsr store
                    jsr count_down
                    bne loop
                    rts
                .endproc

                ; counts $24/$25 down, Z set at 0
                .proc count_down
                    lda $24
                    bne low
                    dec $25
                .low:
                    dec $24
                    lda $24
                    ora $25
                    rts
                .endproc",
                emulator);

        return emulator.Memory.Slice(Capture, CaptureLength).ToArray();
    }

    // a 16MB card with a pattern in each of the first blocks
    public static X16Emulator.SdCard MakeCard(out byte[][] blocks, int count = 4)
    {
        var card = new X16Emulator.SdCard(16, new NullLogger());
        blocks = Enumerable.Range(0, count).Select(Pattern).ToArray();

        for (var i = 0; i < count; i++)
            blocks[i].CopyTo(card.Image[(i * 512)..]);

        return card;
    }

    public static byte[] Pattern(int block) => Enumerable.Range(0, 512).Select(i => (byte)(i * (block * 2 + 3) + block + 1)).ToArray();

    public static string Hex(IEnumerable<byte> bytes) => string.Join(" ", bytes.Select(i => i.ToString("X2")));
}
