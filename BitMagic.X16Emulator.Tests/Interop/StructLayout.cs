using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Interop;

// Regression guard for the layout risk called out in EmulatorCore.h / State.asm's header
// comments: these structs are passed by-value/by-ref straight into hand-written x64 MASM,
// so the CLR's chosen field layout has to exactly match field declaration order with zero
// padding. [StructLayout(Sequential, Pack = 1)] on a fully blittable struct is a hard CLR
// guarantee -- LayoutKind.Auto is the only kind the runtime is free to reorder -- but this
// test exists so a future mistake (a missing attribute, a field that quietly makes the
// struct non-blittable, a copy-pasted struct missing the attribute) fails loudly here
// instead of surfacing as silent memory corruption, e.g. after a .NET SDK upgrade.
[TestClass]
public class StructLayout
{
    private static int UnmanagedSizeOf(Type t) => t.IsPointer ? IntPtr.Size : Marshal.SizeOf(t);

    private static void AssertSequentialPacked<T>() where T : struct
    {
        var type = typeof(T);

        var layout = type.StructLayoutAttribute;
        Assert.IsNotNull(layout, $"{type.Name} is missing [StructLayout] -- it will use LayoutKind.Auto, which the CLR is free to reorder.");
        Assert.AreEqual(LayoutKind.Sequential, layout!.Value, $"{type.Name} must be LayoutKind.Sequential.");
        Assert.AreEqual(1, layout.Pack, $"{type.Name} must be Pack = 1 (no alignment padding).");

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsTrue(fields.Length > 0, $"{type.Name} has no fields to check.");

        long expectedOffset = 0;
        foreach (var field in fields)
        {
            var actualOffset = Marshal.OffsetOf<T>(field.Name).ToInt64();
            Assert.AreEqual(expectedOffset, actualOffset,
                $"{type.Name}.{field.Name}: expected offset {expectedOffset} (immediately after the previous field -- Pack = 1 allows no padding), but the CLR placed it at {actualOffset}. " +
                "Field order has been reordered relative to the source -- this is the exact failure mode that silently corrupts memory against the hand-written asm/native side.");

            expectedOffset += UnmanagedSizeOf(field.FieldType);
        }

        Assert.AreEqual(expectedOffset, Marshal.SizeOf<T>(),
            $"{type.Name}: total size doesn't match the sum of its fields -- there may be trailing padding the runtime inserted despite Pack = 1.");
    }

    [TestMethod]
    public void EmulatorHistory_LayoutIsSequentialAndPacked() => AssertSequentialPacked<EmulatorHistory>();

    [TestMethod]
    public void PsgVoice_LayoutIsSequentialAndPacked() => AssertSequentialPacked<PsgVoice>();

    [TestMethod]
    public void Sprite_LayoutIsSequentialAndPacked() => AssertSequentialPacked<Sprite>();

    [TestMethod]
    public void ZiModemRegisters_LayoutIsSequentialAndPacked() => AssertSequentialPacked<Emulator.ZiModemRegisters>();

    [TestMethod]
    public void UartRegisters_LayoutIsSequentialAndPacked() => AssertSequentialPacked<Emulator.UartRegisters>();

    [TestMethod]
    public void CpuState_LayoutIsSequentialAndPacked() => AssertSequentialPacked<Emulator.CpuState>();
}
