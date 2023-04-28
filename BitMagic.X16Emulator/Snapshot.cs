﻿
using ICSharpCode.SharpZipLib.Core;
using System.Security.Principal;

namespace BitMagic.X16Emulator.Snapshot;

public static class SnapshotExtensions
{
    public static Snapshot Snapshot(this Emulator emulator)
    {
        return new Snapshot(emulator);
    }
}

public enum Registers
{
    A,
    X,
    Y,
    SP
}

public enum CpuFlags
{
    Carry,
    Zero,
    InterruptDisable,
    Decimal,
    Break,
    Overflow,
    Negative
}

public enum MemoryAreas
{
    Ram,
    Vram,
    BankedRam,
    NVram
}

public class Snapshot
{
    public static readonly int UnchangedSpanSize = 8; // how far we can look ahead to check for change blocks
    public static readonly int MinRangeSize = 4; // when we go from individual bytes to a range        

    private readonly Emulator _emulator;
    private readonly byte[] _mainRam = new byte[Emulator.RamSize];
    private readonly byte[] _vram = new byte[Emulator.VramSize];
    private readonly byte[] _bankedRam = new byte[Emulator.BankedRamSize];
    private readonly byte[] _nvram = new byte[Emulator.RtcNvramSize];
    private byte _a;
    private byte _x;
    private byte _y;
    private ushort _sp;
    private bool _zero;
    private bool _interruptDisable;
    private bool _decimal;
    private bool _breakFlag;
    private bool _overflow;
    private bool _negative;
    private bool _carry;

    internal Snapshot(Emulator emulator)
    {
        _emulator = emulator;
        Snap();
    }

    private void Snap()
    {
        _a = _emulator.A;
        _x = _emulator.X;
        _y = _emulator.Y;
        _sp = _emulator.StackPointer;
        _zero = _emulator.Zero;
        _interruptDisable = _emulator.InterruptDisable;
        _decimal = _emulator.Decimal;
        _breakFlag = _emulator.BreakFlag;
        _overflow = _emulator.Overflow;
        _negative = _emulator.Negative;
        _carry = _emulator.Carry;

        for (var i = 0; i < Emulator.RamSize; i++)
        {
            _mainRam[i] = _emulator.Memory[i];
        }
        for (var i = 0; i < Emulator.VramSize; i++)
        {
            _vram[i] = _emulator.Vera.Vram[i];
        }
        for (var i = 0; i < Emulator.BankedRamSize; i++)
        {
            _bankedRam[i] = _emulator.RamBank[i];
        }
        for (var i = 0; i < Emulator.RtcNvramSize; i++)
        {
            _nvram[i] = _emulator.RtcNvram[i];
        }
    }

    public SnapshotResult Compare()
    {
        var toReturn = new SnapshotResult();

        if (_emulator.A != _a)
            toReturn.Add(new RegisterChange() { Register = Registers.A, OriginalValue = _a, NewValue = _emulator.A });
        if (_emulator.X != _x)
            toReturn.Add(new RegisterChange() { Register = Registers.X, OriginalValue = _x, NewValue = _emulator.X });
        if (_emulator.Y != _y)
            toReturn.Add(new RegisterChange() { Register = Registers.Y, OriginalValue = _y, NewValue = _emulator.Y });
        if (_emulator.StackPointer != _sp)
            toReturn.Add(new RegisterChange() { Register = Registers.SP, OriginalValue = (byte)(_sp & 0xff), NewValue = (byte)(_emulator.StackPointer & 0xff) });

        if (_emulator.Zero != _zero)
            toReturn.Add(new FlagChange() { Flag = CpuFlags.Zero, OriginalValue = _zero, NewValue = _emulator.Zero });
        if (_emulator.InterruptDisable != _interruptDisable)
            toReturn.Add(new FlagChange() { Flag = CpuFlags.InterruptDisable, OriginalValue = _interruptDisable, NewValue = _emulator.InterruptDisable });
        if (_emulator.Decimal != _decimal)
            toReturn.Add(new FlagChange() { Flag = CpuFlags.Decimal, OriginalValue = _decimal, NewValue = _emulator.Decimal });
        if (_emulator.BreakFlag != _breakFlag)
            toReturn.Add(new FlagChange() { Flag = CpuFlags.Break, OriginalValue = _breakFlag, NewValue = _emulator.BreakFlag });
        if (_emulator.Overflow != _overflow)
            toReturn.Add(new FlagChange() { Flag = CpuFlags.Overflow, OriginalValue = _overflow, NewValue = _emulator.Overflow });
        if (_emulator.Negative != _negative)
            toReturn.Add(new FlagChange() { Flag = CpuFlags.Negative, OriginalValue = _negative, NewValue = _emulator.Negative });
        if (_emulator.Carry != _carry)
            toReturn.Add(new FlagChange() { Flag = CpuFlags.Carry, OriginalValue = _carry, NewValue = _emulator.Carry });

        toReturn.AddRange(CompareMemory(MemoryAreas.Ram, 0, _mainRam, _emulator.Memory.ToArray()));
        toReturn.AddRange(CompareMemory(MemoryAreas.Vram, 0, _vram, _emulator.Vera.Vram.ToArray()));
        toReturn.AddRange(CompareMemory(MemoryAreas.BankedRam, 0, _bankedRam, _emulator.RamBank.ToArray()));
        toReturn.AddRange(CompareMemory(MemoryAreas.NVram, 0, _nvram, _emulator.RtcNvram.ToArray()));

        Snap();

        return toReturn;
    }

    public static HashSet<int>? IgnoredAreas(MemoryAreas memoryArea) =>
        memoryArea switch
        {
            MemoryAreas.Ram => new HashSet<int>() { 0x9f04, 0x9f05, 0x9f06, 0x9f07, 0x9f08, 0x9f09, 0x9F29 },// ignore VIA timers + vera
            _ => null
        };

    public static IEnumerable<ISnapshotChange> CompareMemory(MemoryAreas memoryArea, int baseAddress, byte[] originalValues, byte[] newValues)
    {
        var ignoredChanges = IgnoredAreas(memoryArea);
        var index = 0;

        if (originalValues.Length != newValues.Length)
            throw new Exception($"Array sizes are different for {memoryArea}");

        while (index < originalValues.Length)
        {
            if (originalValues[index] == newValues[index])
            {
                index++;
                continue;
            }

            var newIndex = index + 1;
            var done = false;
            while (!done)
            {
                if (newIndex >= originalValues.Length)
                    break;

                if (originalValues[newIndex] == newValues[newIndex])
                {
                    for (var i = 0; i < UnchangedSpanSize; i++)
                    {
                        if (newIndex + 1 >= originalValues.Length)
                        {
                            newIndex += i;
                            done = true;
                            break;
                        }

                        if (originalValues[newIndex + i] != newValues[newIndex + i])
                        {
                            newIndex += i;
                            break;
                        }

                        done = true;
                    }
                }
                else
                {
                    newIndex++;
                }
            }

            if (newIndex - index > MinRangeSize)
            {
                yield return new MemoryRangeChange() { 
                    MemoryArea = memoryArea, 
                    Start = baseAddress + index, 
                    End = baseAddress + newIndex - 1, 
                    OriginalValues = originalValues[index..newIndex],
                    NewValues = newValues[index..newIndex]
                };
            }
            else
            {
                for (var i = index; i < newIndex; i++)
                {
                    if (ignoredChanges == null || !ignoredChanges.Contains(i))
                        yield return new MemoryChange() { MemoryArea = memoryArea, Address = baseAddress + i, OriginalValue = originalValues[i], NewValue = newValues[i] };
                }
            }

            index = newIndex;
        }
    }
}

public class SnapshotResult
{
    private readonly List<ISnapshotChange> _changes = new();
    internal void Add(ISnapshotChange change) => _changes.Add(change);
    internal void AddRange(IEnumerable<ISnapshotChange> changes) => _changes.AddRange(changes);

    public List<ISnapshotChange> Changes => _changes;
}

public interface ISnapshotChange
{
    public string DisplayName { get; }
    public string OriginalValue { get; }
    public string NewValue { get; }
}

public class RegisterChange : ISnapshotChange
{
    public Registers Register { get; init; }
    public byte OriginalValue { get; init; }
    public byte NewValue { get; init; }

    string ISnapshotChange.DisplayName => Register.ToString();
    string ISnapshotChange.OriginalValue => $"${OriginalValue:X2}";
    string ISnapshotChange.NewValue => $"${NewValue:X2}";
}

public class FlagChange : ISnapshotChange
{
    public CpuFlags Flag { get; init; }
    public bool OriginalValue { get; init; }
    public bool NewValue { get; init; }

    string ISnapshotChange.DisplayName => Flag.ToString();
    string ISnapshotChange.OriginalValue => OriginalValue.ToString();
    string ISnapshotChange.NewValue => NewValue.ToString();
}

public class MemoryRangeChange : ISnapshotChange
{
    public MemoryAreas MemoryArea { get; init; }
    public int Start { get; init; }
    public int End { get; init; }
    public byte[] OriginalValues { get; init; }
    public byte[] NewValues { get; init; }

    string ISnapshotChange.DisplayName => $"{MemoryArea} Range ${Start:X4} -> ${End:X4} ({End - Start + 1} bytes)";
    string ISnapshotChange.OriginalValue => ArrayToString(OriginalValues);
    string ISnapshotChange.NewValue => ArrayToString(NewValues);

    private static string ArrayToString(byte[] values) => string.Join(" ", values.Take(16).Select(i => $"${i:X2}"))
        + (values.Length > 16 ? "..." : "");
}

public class MemoryChange : ISnapshotChange
{
    public MemoryAreas MemoryArea { get; init; }
    public int Address { get; init; }
    public byte OriginalValue { get; init; }
    public byte NewValue { get; init; }

    string ISnapshotChange.DisplayName => $"{MemoryArea} ${Address:X4}";
    string ISnapshotChange.OriginalValue => $"${OriginalValue:X2}";
    string ISnapshotChange.NewValue => $"${NewValue:X2}";
}