using BitMagic.X16Emulator.Snapshot;
using TestAssert = Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

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

    public static void AssertNoOtherChanges(this SnapshotResultTest snapshot)
    {
        snapshot.Changes.RemoveAll(i => i.DisplayName == "Clock");

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
}
