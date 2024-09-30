using BitMagic.X16Emulator.Snapshot;
using TestAssert = Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using BitMagic.Common.Address;
using System.Net;
using static BitMagic.X16Emulator.Emulator;

namespace BitMagic.X16Emulator.TestHelper;

public static class SnapshotExtensions
{
    public static SnapshotResultTest CanChange(this SnapshotResultTest snapshot, Registers register)
    {
        snapshot.Changes.RemoveAll(i => i switch
        {
            RegisterChange change => change.Register == register,
            _ => false
        });

        return snapshot;
    }

    public static SnapshotResultTest CanChange(this SnapshotResultTest snapshot, CpuFlags flag)
    {
        snapshot.Changes.RemoveAll(i => i switch
        {
            FlagChange change => change.Flag == flag,
            _ => false
        });

        return snapshot;
    }

    public static SnapshotResultTest CanChange(this SnapshotResultTest snapshot, MemoryAreas memoryArea, int address)
    {
        if (memoryArea == MemoryAreas.BankedRam)
        {
            address = ((address & 0xff0000) >> 16) * 0x2000 + (address & 0x1fff); // convert from debugger address to address in array
        }

        // remove all specific changes
        snapshot.Changes.RemoveAll(i => i switch
        {
            MemoryChange change => change.Address == address && change.MemoryArea == memoryArea,
            _ => false
        });

        var ranges = snapshot.Changes.Where(i => i switch
        {
            MemoryRangeChange change => address >= change.Start && address <= change.End && change.MemoryArea == memoryArea,
            _ => false
        }).Cast<MemoryRangeChange>().ToArray();

        if (ranges.Length == 0)
            return snapshot;

        var newChanges = new List<ISnapshotChange>();
        foreach (var range in ranges)
        {
            snapshot.Changes.Remove(range);

            var pos = address - range.Start;
            if (pos != 0)
                newChanges.AddRange(Snapshot.Snapshot.CompareMemory(
                    range.MemoryArea, range.Start, range.OriginalValues[..pos], range.NewValues[..pos]));

            if (pos != range.OriginalValues.Length)
                newChanges.AddRange(Snapshot.Snapshot.CompareMemory(
                    range.MemoryArea, range.Start + pos + 1, range.OriginalValues[(pos+1)..], range.NewValues[(pos+1)..]));
        }

        snapshot.Changes.AddRange(newChanges);

        return snapshot;
    }

    public static SnapshotResultTest CanChange(this SnapshotResultTest snapshot, MemoryAreas memoryArea, int startAddress, int endAddress)
    {
        if (memoryArea == MemoryAreas.BankedRam)
        {
            startAddress = ((startAddress & 0xff0000) >> 16) * 0x2000 + (startAddress & 0x1fff); // convert from debugger address to address in array
            endAddress = ((endAddress & 0xff0000) >> 16) * 0x2000 + (endAddress & 0x1fff); // convert from debugger address to address in array
        }

        // remove all specific changes
        snapshot.Changes.RemoveAll(i => i switch
        {
            MemoryChange change => change.Address >= startAddress && change.Address <= endAddress && change.MemoryArea == memoryArea,
            _ => false
        });

        // look for overlapping ranges
        var ranges = snapshot.Changes.Where(i => i switch
        {
            MemoryRangeChange change =>  Math.Max(endAddress, change.End) - Math.Min(startAddress, change.Start)
                < (change.End - change.Start) + (endAddress - startAddress)
                && change.MemoryArea == memoryArea,
            _ => false
        }).Cast<MemoryRangeChange>().ToArray();

        if (ranges.Length == 0)
            return snapshot;

        var newChanges = new List<ISnapshotChange>();
        foreach (var range in ranges)
        {
            snapshot.Changes.Remove(range);

            var leftPos = startAddress - range.Start;

            if (leftPos > 0)
                newChanges.AddRange(Snapshot.Snapshot.CompareMemory(
                    range.MemoryArea, range.Start, range.OriginalValues[..leftPos], range.NewValues[..leftPos]));

            var rightPos = range.End - endAddress;

            if (rightPos > 0)
                newChanges.AddRange(Snapshot.Snapshot.CompareMemory(
                    range.MemoryArea, endAddress + 1, range.OriginalValues[^rightPos..], range.NewValues[^rightPos..]));
        }

        snapshot.Changes.AddRange(newChanges);

        return snapshot;
    }

    public static SnapshotResultTest IgnoreVia(this SnapshotResultTest snapshot) => snapshot.CanChange(MemoryAreas.Ram, 0x9f00, 0x9f0f);

    public static SnapshotResultTest IgnoreVera(this SnapshotResultTest snapshot) => snapshot.CanChange(MemoryAreas.Ram, 0x9f20, 0x9f3f);

    public static SnapshotResultTest IgnoreStackHistory(this SnapshotResultTest snapshot) => snapshot.CanChange(MemoryAreas.Ram, 0x100, snapshot.Emulator.StackPointer);

    public static SnapshotResultTest IgnoreNumericCpuFlags(this SnapshotResultTest snapshot)
    {
        snapshot.CanChange(CpuFlags.Overflow);
        snapshot.CanChange(CpuFlags.Zero);
        snapshot.CanChange(CpuFlags.Negative);

        return snapshot;
    }

    public static SnapshotResultTest ResultIs(this SnapshotResultTest snapshot, EmulatorResult result)
    {
        var r = result;
        snapshot.Tests.Add(() => TestAssert.Assert.AreEqual(r, snapshot.Emulator.Result));

        return snapshot;
    }

    public static SnapshotResultTest ResultIs(this SnapshotResultTest snapshot, BreakpointSourceType breakpointSource)
    {
        var r = breakpointSource;
        snapshot.Tests.Add(() => {
            TestAssert.Assert.AreEqual(EmulatorResult.Breakpoint, snapshot.Emulator.Result);
            TestAssert.Assert.AreEqual(r, snapshot.Emulator.BreakpointSource);
            });

        return snapshot;
    }

    public static SnapshotResultTest Is(this SnapshotResultTest snapshot, Action test)
    {
        snapshot.Tests.Add(test);

        return snapshot;
    }

    public static SnapshotResultTest Is(this SnapshotResultTest snapshot, Registers register, byte value)
    {
        var v = (int)value;
        var r = register;
        snapshot.Tests.Add(() => TestAssert.Assert.AreEqual(v, GetValue(register, snapshot.Emulator), $"Register {r} is not ${v:X2}, actually ${GetValue(register, snapshot.Emulator):X2}."));

        return snapshot.CanChange(register);
    }

    public static SnapshotResultTest Is(this SnapshotResultTest snapshot, Registers register, int value)
    {
        var v = value;
        var r = register;
        snapshot.Tests.Add(() => TestAssert.Assert.AreEqual(v, GetValue(register, snapshot.Emulator), $"Register {r} is not ${v:X2}, actually ${GetValue(register, snapshot.Emulator):X2}."));

        return snapshot.CanChange(register);
    }

    public static SnapshotResultTest Is(this SnapshotResultTest snapshot, CpuFlags cpuFlag, bool value)
    {
        var v = value;
        var r = cpuFlag;
        snapshot.Tests.Add(() => TestAssert.Assert.AreEqual(v, GetValue(cpuFlag, snapshot.Emulator), $"CpuFlag {r} is not {v}"));

        return snapshot.CanChange(cpuFlag);
    }

    public static SnapshotResultTest Is(this SnapshotResultTest snapshot, MemoryAreas memoryArea, int address, int value)
    {
        var v = value;
        var a = address;
        var r = memoryArea;

        if (r == MemoryAreas.BankedRam)
            snapshot.Tests.Add(() => TestAssert.Assert.AreEqual(v, GetValue(r, a, snapshot.Emulator), $"Memory 0x{(address & 0xff0000) >> 16 :X2}:{address & 0xffff:X4} is not 0x{v:X2} its 0x{GetValue(r, a, snapshot.Emulator):X2}"));
        else
            snapshot.Tests.Add(() => TestAssert.Assert.AreEqual(v, GetValue(r, a, snapshot.Emulator), $"Memory 0x{a:X4} is not 0x{v:X2} its 0x{GetValue(r, a, snapshot.Emulator):X2}"));

        return snapshot.CanChange(memoryArea, address);
    }

    public static SnapshotResultTest Is(this SnapshotResultTest snapshot, MemoryAreas memoryArea, int address, IEnumerable<byte> values)
    {
        var toReturn = snapshot;
        var a = address;

        foreach (var v in values)
        {
            var r = memoryArea;
            var thisa = a;

            if (r == MemoryAreas.BankedRam)
                snapshot.Tests.Add(() => TestAssert.Assert.AreEqual(v, GetValue(r, thisa, snapshot.Emulator), $"Memory 0x{(thisa & 0xff0000) >> 16:X2}:{thisa & 0xffff:X4} is not {v}"));
            else
                snapshot.Tests.Add(() => TestAssert.Assert.AreEqual(v, GetValue(r, thisa, snapshot.Emulator), $"Memory 0x{thisa:X4} is not {v}"));

            toReturn = snapshot.CanChange(memoryArea, thisa);

            a++;
        }

        return toReturn;
    }

    private static int GetValue(Registers register, Emulator emulator) => register switch 
    {
        Registers.A => emulator.A,
        Registers.X => emulator.X,
        Registers.Y => emulator.Y,
        Registers.Sp => emulator.StackPointer,
        Registers.Pc => emulator.Pc,
        _ => throw new Exception("Unhandled Register")
    };

    private static bool GetValue(CpuFlags cpuFlags, Emulator emulator) => cpuFlags switch
    {
        CpuFlags.Carry => emulator.Carry,
        CpuFlags.Zero => emulator.Zero,
        CpuFlags.InterruptDisable => emulator.InterruptDisable,
        CpuFlags.Decimal => emulator.Decimal,
        CpuFlags.Break => emulator.BreakFlag,
        CpuFlags.Overflow => emulator.Overflow,
        CpuFlags.Negative => emulator.Negative,
        _ => throw new Exception("Unhandled CPU Flag")
    };

    public static int GetValue(MemoryAreas memoryArea, int address, Emulator emulator) => memoryArea switch
    {
        MemoryAreas.Ram => emulator.Memory[address],
        MemoryAreas.BankedRam => emulator.RamBank[((address & 0xff0000) >> 16) * 0x2000 + (address & 0x1fff)],
        MemoryAreas.Vram => emulator.Vera.Vram[address],
        MemoryAreas.NVram => emulator.RtcNvram[address],
        _ => throw new Exception("Unhandled Memory Area")
    };

    public static void AssertNoOtherChanges(this SnapshotResultTest snapshot)
    {
        snapshot.DisplayStatus();

        snapshot.Changes.RemoveAll(i => i.DisplayName == "Clock");
        snapshot.CanChange(MemoryAreas.Ram, 0xa000, 0xc000 - 1); // banked ram will show as well, so lets remove one.

        if (snapshot.Changes.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Unexpected Changes:");

            foreach (var change in snapshot.Changes)
            {
                sb.AppendLine("---------------------------------------");
                sb.AppendLine(change.DisplayName);
                sb.AppendLine($"Org: {change.OriginalValue}");
                sb.AppendLine($"Now: {change.NewValue}");
            }

            TestAssert.Assert.Fail(sb.ToString());
        }

        foreach(var i in snapshot.Tests)
        {
            i();
        }
    }

    public static void Assert(this SnapshotResultTest snapshot)
    {
        foreach (var i in snapshot.Tests)
        {
            i();
        }
    }

    public static void DisplayStatus(this SnapshotResultTest snapshot)
    {
        Console.WriteLine($"A:   \t${snapshot.Emulator.A:X2}");
        Console.WriteLine($"X:   \t${snapshot.Emulator.X:X2}");
        Console.WriteLine($"Y:   \t${snapshot.Emulator.Y:X2}");
        Console.WriteLine($"PC:  \t${snapshot.Emulator.Pc:X4}");
        Console.WriteLine($"SP:  \t${snapshot.Emulator.StackPointer:X4}");

        Console.WriteLine($"Ticks:\t${snapshot.Emulator.Clock:X4}");

        Console.Write("Flags:\t[");
        Console.Write(snapshot.Emulator.Negative ? "N" : " ");
        Console.Write(snapshot.Emulator.Overflow ? "V" : " ");
        Console.Write(" ");
        Console.Write(snapshot.Emulator.BreakFlag ? "B" : " ");
        Console.Write(snapshot.Emulator.Decimal ? "D" : " ");
        Console.Write(snapshot.Emulator.InterruptDisable ? "I" : " ");
        Console.Write(snapshot.Emulator.Zero ? "Z" : " ");
        Console.Write(snapshot.Emulator.Carry ? "C]" : " ]");
        Console.WriteLine();
        Console.WriteLine($"D0 Adr:\t${snapshot.Emulator.Vera.Data0_Address:X5} (step ${snapshot.Emulator.Vera.Data0_Step:X2})");
        Console.WriteLine($"D1 Adr:\t${snapshot.Emulator.Vera.Data1_Address:X5} (step ${snapshot.Emulator.Vera.Data1_Step:X2})");
        Console.WriteLine();
        Console.WriteLine($"Beam:\t{snapshot.Emulator.Vera.Beam_X}, {snapshot.Emulator.Vera.Beam_Y} ({snapshot.Emulator.Vera.Beam_Position})");
        Console.WriteLine();
    }
}
