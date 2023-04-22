using BitMagic.X16Emulator.Snapshot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Text;

namespace BitMagic.X16Emulator.TestHelper;

public static class SnapshotExtensions
{
    public static SnapshotResult CanChange(this SnapshotResult snapshot, Registers register)
    {
        snapshot.Changes.RemoveAll(i => i switch
        {
            RegisterChange change => change.Register == register,
            _ => false
        });

        return snapshot;
    }
    public static SnapshotResult CanChange(this SnapshotResult snapshot, CpuFlags flag)
    {
        snapshot.Changes.RemoveAll(i => i switch
        {
            FlagChange change => change.Flag == flag,
            _ => false
        });

        return snapshot;
    }

    public static SnapshotResult CanChange(this SnapshotResult snapshot, MemoryAreas memoryArea, int address)
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

    public static SnapshotResult CanChange(this SnapshotResult snapshot, MemoryAreas memoryArea, int startAddress, int endAddress)
    {
        // remove all specific changes
        snapshot.Changes.RemoveAll(i => i switch
        {
            MemoryChange change => change.Address >= startAddress && change.Address <= endAddress && change.MemoryArea == memoryArea,
            _ => false
        });

        var ranges = snapshot.Changes.Where(i => i switch
        {
            MemoryRangeChange change => (startAddress >= change.Start || endAddress <= change.End) && change.MemoryArea == memoryArea,
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

    public static SnapshotResult AssertNoChanges(this SnapshotResult snapshot)
    {
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

            Assert.Fail(sb.ToString());
        }

        return snapshot;
    }
}
