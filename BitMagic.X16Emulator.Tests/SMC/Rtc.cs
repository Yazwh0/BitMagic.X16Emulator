using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Smc;

// The MCP7940N RTC on I2C at $6F, driven through the KERNAL's own I2C bit banging (ported below). The time is worked
// out from the cpu clock when it's read, so the tests move Emulator.Clock on to make time pass. Only by a few seconds:
// the audio and timers catch up on the clock too, so a bigger jump makes the next run take a long time.
[TestClass]
public class Rtc
{
    private const ulong Second = 8_000_000;

    // The KERNAL's I2C routines (kernal/drivers/x16/i2c.s), without the mutex.
    // read_byte / write_byte:    x = device, y = register (a = value to write), as i2c_read_byte / i2c_write_byte
    // read_first_byte, read_next_byte, read_stop, write_first_byte, write_next_byte, write_stop: sequential access
    private const string I2c = @"
            .const SDA = 1
            .const SCL = 2

            .proc read_byte
                jsr read_first_byte
                pha
                jsr read_stop
                pla
                rts
            .endproc

            ; sets the register, stops, then starts again to read, as the KERNAL does
            .proc read_first_byte
                jsr i2c_init
                jsr i2c_start
                txa
                asl
                pha
                phy
                jsr i2c_write
                ply
                plx
                tya
                phx
                jsr i2c_write
                jsr i2c_stop
                jsr i2c_start
                pla
                inc
                jsr i2c_write
                jmp i2c_read
            .endproc

            .proc read_next_byte
                clc
                jsr send_bit    ; ack
                jmp i2c_read
            .endproc

            .proc read_stop
                sec
                jsr send_bit    ; nack
                jmp i2c_stop
            .endproc

            .proc write_byte
                jsr write_first_byte
                jmp i2c_stop
            .endproc

            .proc write_first_byte
                pha
                jsr i2c_init
                jsr i2c_start
                txa
                asl
                phy
                jsr i2c_write
                ply
                tya
                phy
                jsr i2c_write
                ply
                pla
                jsr i2c_write
                rts
            .endproc

            .proc write_next_byte
                jmp i2c_write
            .endproc

            .proc write_stop
                jmp i2c_stop
            .endproc

            .proc i2c_write
                ldx #8
            .write_loop:
                rol
                tay
                jsr send_bit
                tya
                dex
                bne write_loop
                jsr rec_bit
                rts
            .endproc

            .proc i2c_read
                ldx #8
            .read_loop:
                tay
                jsr rec_bit
                tya
                rol
                dex
                bne read_loop
                rts
            .endproc

            .proc i2c_start
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_low
                rts
            .endproc

            .proc i2c_stop
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_high
                jsr i2c_brief_delay
                jsr sda_high
                jsr i2c_brief_delay
                rts
            .endproc

            .proc sda_low
                lda #SDA
                tsb V_DDRA
                rts
            .endproc

            .proc sda_high
                lda #SDA
                trb V_DDRA
                rts
            .endproc

            .proc scl_low
                lda #SCL
                tsb V_DDRA
                rts
            .endproc

            .proc scl_high
                lda #SCL
                trb V_DDRA
            .wait:
                lda V_PRA
                and #SCL
                beq wait
                rts
            .endproc

            .proc send_bit
                bcs high
                jsr sda_low
                bra done
            .high:
                jsr sda_high
            .done:
                jsr scl_high
                jsr scl_low
                rts
            .endproc

            .proc i2c_init
                lda #SDA | SCL
                trb V_PRA
                jsr sda_high
                jsr scl_high
                rts
            .endproc

            .proc rec_bit
                jsr sda_high
                jsr scl_high
                lda V_PRA
                lsr
                jsr scl_low
                rts
            .endproc

            .proc i2c_brief_delay
                nop
                nop
                nop
                nop
                rts
            .endproc";

    private static async Task Run(Emulator emulator, string code)
    {
        await X16TestHelper.Emulate($@"
            .machine CommanderX16R42
            .org $810
                {code}
                stp
            {I2c}",
            emulator);
    }

    // Reads $00-$06 one at a time, as the KERNAL does
    private static async Task<byte[]> ReadTime(Emulator emulator)
    {
        var code = string.Join("\n", Enumerable.Range(0, 7).Select(i => $@"
                ldx #$6f
                ldy #{i}
                jsr read_byte
                sta $1000 + {i}"));

        await Run(emulator, code);
        return emulator.Memory.Slice(0x1000, 7).ToArray();
    }

    private static Task WriteRegister(Emulator emulator, int register, int value) => Run(emulator, $@"
                lda #{value}
                ldx #$6f
                ldy #{register}
                jsr write_byte");

    private static void AssertTime(byte[] expected, byte[] actual) =>
        Assert.AreEqual(Convert.ToHexString(expected), Convert.ToHexString(actual), "seconds, minutes, hours, weekday, date, month, year");

    [TestMethod]
    public async Task Read_Bcd()
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2026, 11, 22, 14, 35, 7));   // a Sunday

        var time = await ReadTime(emulator);

        // ST, then OSCRUN and VBATEN with weekday 7. 2026 isn't a leap year.
        AssertTime([0x87, 0x35, 0x14, 0x2f, 0x22, 0x11, 0x26], time);
    }

    [TestMethod]
    public async Task Read_LeapYear()
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2028, 2, 29, 0, 0, 0));

        var time = await ReadTime(emulator);

        Assert.AreEqual(0x22, time[5], "LPYR with February");
        Assert.AreEqual(0x29, time[4]);
    }

    [TestMethod]
    [DataRow("2024-02-28 23:59:59", 2, "2024-02-29 00:00:01")]          // leap day
    [DataRow("2025-02-28 23:59:59", 1, "2025-03-01 00:00:00")]
    [DataRow("2025-12-31 23:59:59", 1, "2026-01-01 00:00:00")]
    [DataRow("2026-04-30 23:59:59", 1, "2026-05-01 00:00:00")]          // a 30 day month
    [DataRow("2026-11-22 14:59:59", 2, "2026-11-22 15:00:01")]          // into the next hour
    [DataRow("2099-12-31 23:59:59", 1, "2000-01-01 00:00:00")]          // the chip only has 2 digits
    public async Task Advances_WithTheCpuClock(string start, int seconds, string expected)
    {
        using var emulator = new Emulator();
        emulator.SetRtc(DateTime.Parse(start));
        emulator.Clock += (ulong)seconds * Second;

        var time = await ReadTime(emulator);
        var e = DateTime.Parse(expected);

        AssertTime([(byte)(0x80 | Bcd(e.Second)), Bcd(e.Minute), Bcd(e.Hour), time[3], Bcd(e.Day), (byte)(Bcd(e.Month) | (e.Year % 4 == 0 ? 0x20 : 0)), Bcd(e.Year % 100)], time);
        Assert.AreEqual(e, emulator.RtcDateTime);
    }

    [TestMethod]
    public async Task Weekday_CountsOn()
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2026, 11, 22, 23, 59, 59));  // Sunday, 7
        emulator.Clock += Second;

        var time = await ReadTime(emulator);

        Assert.AreEqual(0x29, time[3], "Monday, 1, with OSCRUN and VBATEN");
    }

    [TestMethod]
    public async Task Stopped_DoesntMove()
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2026, 11, 22, 14, 35, 7), running: false);
        emulator.Clock += 10 * Second;

        var time = await ReadTime(emulator);

        AssertTime([0x07, 0x35, 0x14, 0x0f, 0x22, 0x11, 0x26], time);
    }

    [TestMethod]
    public async Task PowerOn_Stopped()
    {
        using var emulator = new Emulator();
        emulator.Clock += 10 * Second;

        var time = await ReadTime(emulator);

        AssertTime([0x00, 0x00, 0x00, 0x01, 0x01, 0x21, 0x00], time);
    }

    // as rtc_set_date_time: stop the clock, write year to minutes, then the seconds with ST
    [TestMethod]
    public async Task Set_AsTheKernalDoes()
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2026, 11, 22, 14, 35, 7));

        await Run(emulator, @"
                ldx #$6f
                ldy #0
                tya
                jsr write_byte      ; stop
                ldx #$6f
                ldy #6
                lda #$30
                jsr write_byte      ; 2030
                ldx #$6f
                ldy #5
                lda #$06
                jsr write_byte      ; June
                ldx #$6f
                ldy #4
                lda #$15
                jsr write_byte      ; 15th
                ldx #$6f
                ldy #3
                lda #$0b
                jsr write_byte      ; weekday 3, VBATEN
                ldx #$6f
                ldy #2
                lda #$09
                jsr write_byte      ; 9, 24 hour
                ldx #$6f
                ldy #1
                lda #$45
                jsr write_byte
                ldx #$6f
                ldy #0
                lda #$b0
                jsr write_byte      ; 30 seconds, start");

        Assert.AreEqual(new DateTime(2030, 6, 15, 9, 45, 30), emulator.RtcDateTime);
        Assert.IsTrue(emulator.RtcRunning);

        var time = await ReadTime(emulator);
        AssertTime([0xb0, 0x45, 0x09, 0x2b, 0x15, 0x06, 0x30], time);

        emulator.Clock += 3 * Second;
        Assert.AreEqual(new DateTime(2030, 6, 15, 9, 45, 33), emulator.RtcDateTime);
    }

    [TestMethod]
    public async Task Stop_KeepsTheTime()
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2026, 11, 22, 14, 35, 7));
        emulator.Clock += 5 * Second;

        await WriteRegister(emulator, 0, 0x12);     // 12 seconds, ST off
        emulator.Clock += 60 * Second;

        Assert.IsFalse(emulator.RtcRunning);
        Assert.AreEqual(new DateTime(2026, 11, 22, 14, 35, 12), emulator.RtcDateTime);
    }

    [TestMethod]
    [DataRow(0x62, 14, 0x62)]   // 2 PM
    [DataRow(0x52, 0, 0x52)]    // 12 AM is midnight
    [DataRow(0x72, 12, 0x72)]   // 12 PM is midday
    public async Task TwelveHour(int write, int hour, int read)
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2026, 11, 22, 8, 0, 0), running: false);

        await WriteRegister(emulator, 2, write);
        var time = await ReadTime(emulator);

        Assert.AreEqual(hour, emulator.RtcDateTime.Hour);
        Assert.AreEqual(read, time[2]);
    }

    [TestMethod]
    public async Task WritingTheDate_KeepsTheWeekday()
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2026, 11, 22, 8, 0, 0), running: false);   // Sunday, 7

        await WriteRegister(emulator, 4, 0x25);
        var time = await ReadTime(emulator);

        Assert.AreEqual(0x25, time[4]);
        Assert.AreEqual(0x07, time[3] & 7, "the weekday is its own counter");
    }

    [TestMethod]
    public async Task ImpossibleDate_IsMadeReal()
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2026, 2, 10, 8, 0, 0), running: false);

        await WriteRegister(emulator, 4, 0x31);     // 31 February

        Assert.AreEqual(new DateTime(2026, 2, 28, 8, 0, 0), emulator.RtcDateTime);
    }

    // one transaction writes the date, month and year, then one reads all 7 from the seconds
    [TestMethod]
    public async Task Sequential()
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2026, 11, 22, 14, 35, 7), running: false);

        await Run(emulator, @"
                ldx #$6f
                ldy #4
                lda #$01
                jsr write_first_byte    ; 1st
                lda #$02
                jsr write_next_byte     ; February
                lda #$27
                jsr write_next_byte     ; 2027
                jsr write_stop

                ldx #$6f
                ldy #0
                jsr read_first_byte
                sta $1000
                jsr read_next_byte
                sta $1001
                jsr read_next_byte
                sta $1002
                jsr read_next_byte
                sta $1003
                jsr read_next_byte
                sta $1004
                jsr read_next_byte
                sta $1005
                jsr read_next_byte
                sta $1006
                jsr read_stop");

        AssertTime([0x07, 0x35, 0x14, 0x0f, 0x01, 0x02, 0x27], emulator.Memory.Slice(0x1000, 7).ToArray());
    }

    [TestMethod]
    public async Task Nvram()
    {
        using var emulator = new Emulator();

        await Run(emulator, @"
                ldx #$6f
                ldy #$40
                lda #$5a
                jsr write_byte
                ldx #$6f
                ldy #$40
                jsr read_byte
                sta $1000");

        Assert.AreEqual(0x5a, emulator.RtcNvram[0x20]);
        Assert.AreEqual(0x5a, emulator.Memory[0x1000]);
    }

    // past the end of the SRAM the pointer goes back to its start
    [TestMethod]
    public async Task Sequential_WrapsWithinTheSram()
    {
        using var emulator = new Emulator();
        emulator.RtcNvram[0x3f] = 0x11;
        emulator.RtcNvram[0x00] = 0x22;

        await Run(emulator, @"
                ldx #$6f
                ldy #$5f
                jsr read_first_byte
                sta $1000
                jsr read_next_byte
                sta $1001
                jsr read_stop");

        Assert.AreEqual(0x11, emulator.Memory[0x1000]);
        Assert.AreEqual(0x22, emulator.Memory[0x1001]);
    }

    [TestMethod]
    public async Task NotEmulated_ReadsZero()
    {
        using var emulator = new Emulator();

        await Run(emulator, @"
                ldx #$6f
                ldy #$07
                jsr read_byte
                sta $1000");

        Assert.AreEqual(0x00, emulator.Memory[0x1000]);
    }

    [TestMethod]
    public void KeptOverASnapshot()
    {
        using var emulator = new Emulator();
        emulator.SetRtc(new DateTime(2026, 11, 22, 14, 35, 7));
        emulator.Clock += 5 * Second;

        var saved = emulator.SaveState();
        emulator.Clock += 100 * Second;
        emulator.RestoreState(saved);

        Assert.AreEqual(new DateTime(2026, 11, 22, 14, 35, 12), emulator.RtcDateTime);
    }

    [TestMethod]
    public void StartRtc_Settings()
    {
        using var emulator = new Emulator();

        emulator.StartRtc("stopped");
        Assert.IsFalse(emulator.RtcRunning);

        emulator.StartRtc("2026-01-01T12:00:00");
        Assert.IsTrue(emulator.RtcRunning);
        Assert.AreEqual(new DateTime(2026, 1, 1, 12, 0, 0), emulator.RtcDateTime);

        var before = DateTime.Now;
        emulator.StartRtc("host");
        Assert.IsTrue(Math.Abs((emulator.RtcDateTime - before).TotalSeconds) < 5, "the host's local time");

        Assert.ThrowsException<ArgumentException>(() => emulator.StartRtc("tomorrow"));
    }

    private static byte Bcd(int value) => (byte)(value / 10 * 16 + value % 10);
}
