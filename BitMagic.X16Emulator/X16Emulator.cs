﻿using System.Runtime.InteropServices;
using BitMagic.Common;

namespace BitMagic.X16Emulator;

[StructLayout(LayoutKind.Sequential)]
public struct EmulatorHistory
{
    public ushort PC;
    public byte OpCode;
    public byte RomBank;
    public byte RamBank;
    public byte A;
    public byte X;
    public byte Y;
    public ushort Params;
    public byte Flags;
    public byte SP;
    public ushort Unused1;
    public ushort Unused2;
    public ulong Clock;
    public ulong Unused3;
}

public struct PsgVoice
{
    public ulong GenerationPtr;
    public uint Waveform;
    public uint Phase;
    public uint Frequency;
    public uint Volume;
    public uint LeftRight;
    public uint Width;
    public uint Value;
    public uint Noise;
    public ulong Padding1;
    public ulong Padding2;
    public ulong Padding3;
}

public enum Control : uint
{
    Run,
    Paused,
    Stop
}

public enum FrameControl : uint
{
    Run,
    Synced
}

[Flags]
public enum InterruptSource : uint
{
    None = 0,
    Vsync = 1,
    Line = 2,
    Spcol = 4,
    Aflow = 8,
    Via = 16,
    Ym = 32
}

public struct Sprite // 64 bytes
{
    public uint Address { get; set; }  // actual address in memory
    public uint PaletteOffset { get; set; }

    public uint CollisionMask { get; set; }
    public uint Width { get; set; }
    public uint Height { get; set; }

    public uint X { get; set; }
    public uint Y { get; set; }

    public uint Mode { get; set; } // mode, height, width, vflip, hflip - used for data fetch proc lookup. Use to set DrawProc
    public uint Depth { get; set; }

    public uint Padding1 { get; set; }

    public ulong Padding2 { get; set; }
    public ulong Padding3 { get; set; }
    public ulong Padding4 { get; set; }

    public override string ToString() => $"Address: ${Address:X4} DrawProc: ${CollisionMask:X4} X: {X} Y: {Y} Height: {Height} Width: {Width} Mode: {Mode:X4} Depth: ${Depth:X2} Palette: ${PaletteOffset:X2}";
}

public class Emulator : IDisposable
{
#if DEBUG
    //[DllImport(@"..\..\..\..\X16Emulator\EmulatorCore\x64\Debug\EmulatorCore.dll")]
    [DllImport(@"C:\Documents\Source\BitMagic\BitMagic.X16Emulator\X16Emulator\EmulatorCore\x64\Debug\EmulatorCore.dll")]
#endif
#if RELEASE
#if OS_WINDOWS
    [DllImport(@"EmulatorCore.dll")]
#endif
#if OS_LINUX
    [DllImport(@"./EmulatorCore.so")]
#endif
#endif
    private static extern int fnEmulatorCode(ref CpuState state);

    public class VeraState
    {

        [Flags]
        public enum VeraBreakpointType : byte
        {
            Read = 1,
            Write = 2,
            Load = 4
        }

        private readonly Emulator _emulator;
        public VeraState(Emulator emulator)
        {
            _emulator = emulator;
        }

        public unsafe Span<byte> Vram => new Span<byte>((void*)_emulator._state.VramPtr, VramSize);
        public int Data0_Address { get => (int)_emulator._state.Data0_Address; set => _emulator._state.Data0_Address = (ulong)value; }
        public int Data1_Address { get => (int)_emulator._state.Data1_Address; set => _emulator._state.Data1_Address = (ulong)value; }
        public int Data0_Step { get => (int)_emulator._state.Data0_Step; set => _emulator._state.Data0_Step = (ulong)value; }
        public int Data1_Step { get => (int)_emulator._state.Data1_Step; set => _emulator._state.Data1_Step = (ulong)value; }
        public bool AddrSel { get => _emulator._state.AddrSel != 0; set => _emulator._state.AddrSel = (value ? (byte)1 : (byte)0); }
        public byte DcSel { get => _emulator._state.DcSel; set => _emulator._state.DcSel = value; }
        public UInt32 VideoOutput { get => _emulator._state.VideoOutput; set => _emulator._state.VideoOutput = value; }
        public UInt32 Dc_HScale { get => _emulator._state.Dc_HScale; set => _emulator._state.Dc_HScale = value; }
        public UInt32 Dc_VScale { get => _emulator._state.Dc_VScale; set => _emulator._state.Dc_VScale = value; }
        public byte Dc_Border { get => _emulator._state.Dc_Border; set => _emulator._state.Dc_Border = value; }
        public ushort Dc_HStart { get => _emulator._state.Dc_HStart; set => _emulator._state.Dc_HStart = value; }
        public ushort Dc_HStop { get => _emulator._state.Dc_HStop; set => _emulator._state.Dc_HStop = value; }
        public ushort Dc_VStart { get => _emulator._state.Dc_VStart; set => _emulator._state.Dc_VStart = value; }
        public ushort Dc_VStop { get => _emulator._state.Dc_VStop; set => _emulator._state.Dc_VStop = value; }
        public bool SpriteEnable { get => _emulator._state.Sprite_Enable != 0; set => _emulator._state.Sprite_Enable = (value ? (byte)1 : (byte)0); }
        public bool Layer0Enable { get => _emulator._state.Layer0_Enable != 0; set => _emulator._state.Layer0_Enable = (value ? (byte)1 : (byte)0); }
        public bool Layer1Enable { get => _emulator._state.Layer1_Enable != 0; set => _emulator._state.Layer1_Enable = (value ? (byte)1 : (byte)0); }

        public byte Layer0_MapHeight { get => _emulator._state.Layer0_MapHeight; set => _emulator._state.Layer0_MapHeight = value; }
        public byte Layer0_MapWidth { get => _emulator._state.Layer0_MapWidth; set => _emulator._state.Layer0_MapWidth = value; }
        public bool Layer0_BitMapMode { get => _emulator._state.Layer0_BitMapMode != 0; set => _emulator._state.Layer0_BitMapMode = (value ? (byte)1 : (byte)0); }
        public byte Layer0_ColourDepth { get => _emulator._state.Layer0_ColourDepth; set => _emulator._state.Layer0_ColourDepth = value; }
        public bool Layer0_T256C { get => _emulator._state.Layer0_T256C == 1; set => _emulator._state.Layer0_T256C = (value ? (uint)1 : (uint)0); }
        public UInt32 Layer0_MapAddress { get => _emulator._state.Layer0_MapAddress; set => _emulator._state.Layer0_MapAddress = value; }
        public UInt32 Layer0_TileAddress { get => _emulator._state.Layer0_TileAddress; set => _emulator._state.Layer0_TileAddress = value; }
        public byte Layer0_TileHeight { get => _emulator._state.Layer0_TileHeight; set => _emulator._state.Layer0_TileHeight = value; }
        public byte Layer0_TileWidth { get => _emulator._state.Layer0_TileWidth; set => _emulator._state.Layer0_TileWidth = value; }
        public ushort Layer0_HScroll { get => _emulator._state.Layer0_HScroll; set => _emulator._state.Layer0_HScroll = value; }
        public ushort Layer0_VScroll { get => _emulator._state.Layer0_VScroll; set => _emulator._state.Layer0_VScroll = value; }

        public byte Layer1_MapHeight { get => _emulator._state.Layer1_MapHeight; set => _emulator._state.Layer1_MapHeight = value; }
        public byte Layer1_MapWidth { get => _emulator._state.Layer1_MapWidth; set => _emulator._state.Layer1_MapWidth = value; }
        public bool Layer1_BitMapMode { get => _emulator._state.Layer1_BitMapMode != 0; set => _emulator._state.Layer1_BitMapMode = (value ? (byte)1 : (byte)0); }
        public byte Layer1_ColourDepth { get => _emulator._state.Layer1_ColourDepth; set => _emulator._state.Layer1_ColourDepth = value; }
        public bool Layer1_T256C { get => _emulator._state.Layer1_T256C == 1; set => _emulator._state.Layer1_T256C = (value ? (uint)1 : (uint)0); }
        public UInt32 Layer1_MapAddress { get => _emulator._state.Layer1_MapAddress; set => _emulator._state.Layer1_MapAddress = value; }
        public UInt32 Layer1_TileAddress { get => _emulator._state.Layer1_TileAddress; set => _emulator._state.Layer1_TileAddress = value; }
        public byte Layer1_TileHeight { get => _emulator._state.Layer1_TileHeight; set => _emulator._state.Layer1_TileHeight = value; }
        public byte Layer1_TileWidth { get => _emulator._state.Layer1_TileWidth; set => _emulator._state.Layer1_TileWidth = value; }
        public ushort Layer1_HScroll { get => _emulator._state.Layer1_HScroll; set => _emulator._state.Layer1_HScroll = value; }
        public ushort Layer1_VScroll { get => _emulator._state.Layer1_VScroll; set => _emulator._state.Layer1_VScroll = value; }

        public ushort Interrupt_LineNum { get => _emulator._state.Interrupt_LineNum; set => _emulator._state.Interrupt_LineNum = value; }
        public bool Interrupt_AFlow { get => (_emulator._state.Interrupt_Mask & (uint)InterruptSource.Aflow) != 0; set => SetBit(ref _emulator._state.Interrupt_Mask, (uint)InterruptSource.Aflow, value); }
        public bool Interrupt_SpCol { get => (_emulator._state.Interrupt_Mask & (uint)InterruptSource.Spcol) != 0; set => SetBit(ref _emulator._state.Interrupt_Mask, (uint)InterruptSource.Spcol, value); }
        public bool Interrupt_Line { get => (_emulator._state.Interrupt_Mask & (uint)InterruptSource.Line) != 0; set => SetBit(ref _emulator._state.Interrupt_Mask, (uint)InterruptSource.Line, value); }
        public bool Interrupt_VSync { get => (_emulator._state.Interrupt_Mask & (uint)InterruptSource.Vsync) != 0; set => SetBit(ref _emulator._state.Interrupt_Mask, (uint)InterruptSource.Vsync, value); }

        public ushort Beam_X { get => (ushort)(_emulator._state.Beam_Position % 800); }
        public ushort Beam_Y { get => (ushort)Math.Floor(_emulator._state.Beam_Position / 800.0); }
        public UInt32 Beam_Position { get => _emulator._state.Beam_Position; set => _emulator._state.Beam_Position = value; }

        public bool Interrupt_Line_Hit { get => (_emulator._state.Interrupt_Hit & (uint)InterruptSource.Line) != 0; set => SetBit(ref _emulator._state.Interrupt_Hit, (uint)InterruptSource.Line, value); }
        public bool Interrupt_Vsync_Hit { get => (_emulator._state.Interrupt_Hit & (uint)InterruptSource.Vsync) != 0; set => SetBit(ref _emulator._state.Interrupt_Hit, (uint)InterruptSource.Vsync, value); }
        public bool Interrupt_SpCol_Hit { get => (_emulator._state.Interrupt_Hit & (uint)InterruptSource.Spcol) != 0; set => SetBit(ref _emulator._state.Interrupt_Hit, (uint)InterruptSource.Spcol, value); }
        public UInt32 Frame_Count { get => _emulator._state.Frame_Count; }
        public UInt32 Frame_Count_Breakpoint { get => _emulator._state.Frame_Count_Breakpoint; set => _emulator._state.Frame_Count_Breakpoint = value; }

        //public ushort Layer0_Config { get => _emulator._state.Layer0_Config; }
        public ushort Layer0_Tile_HShift { get => _emulator._state.Layer0_Tile_HShift; }
        public ushort Layer0_Tile_VShift { get => _emulator._state.Layer0_Tile_VShift; }
        public ushort Layer0_Map_HShift { get => _emulator._state.Layer0_Map_HShift; }
        public ushort Layer0_Map_VShift { get => _emulator._state.Layer0_Map_VShift; }

        //public ushort Layer1_Config { get => _emulator._state.Layer1_Config; }
        public ushort Layer1_Tile_HShift { get => _emulator._state.Layer1_Tile_HShift; }
        public ushort Layer1_Tile_VShift { get => _emulator._state.Layer1_Tile_VShift; }
        public ushort Layer1_Map_HShift { get => _emulator._state.Layer1_Map_HShift; }
        public ushort Layer1_Map_VShift { get => _emulator._state.Layer1_Map_VShift; }

        public uint Sprite_Wait { get => _emulator._state.Sprite_Wait; }
        public uint Sprite_Position { get => _emulator._state.Sprite_Position; }
        public uint Sprite_Width { get => _emulator._state.Sprite_Width; }
        public uint Sprite_Render_Mode { get => _emulator._state.Sprite_Render_Mode; }
        public uint Sprite_X { get => _emulator._state.Sprite_X; }
        public uint Sprite_Y { get => _emulator._state.Sprite_Y; }
        public uint Sprite_Depth { get => _emulator._state.Sprite_Depth; }
        public uint Sprite_CollisionMask { get => _emulator._state.Sprite_CollisionMask; }

        private static void SetBit(ref uint dest, uint data, bool set)
        {
            if (set)
            {
                dest |= data;
                return;
            }

            dest &= ~data;
        }
    }

    public class VeraAudioState
    {
        private readonly Emulator _emulator;
        public VeraAudioState(Emulator emulator)
        {
            _emulator = emulator;
        }

        public uint PcmBufferRead { get => _emulator._state.PcmBufferRead; set => _emulator._state.PcmBufferRead = value; }
        public uint PcmBufferWrite { get => _emulator._state.PcmBufferWrite; set => _emulator._state.PcmBufferWrite = value; }
        public uint PcmBufferCount { get => _emulator._state.PcmBufferCount; set => _emulator._state.PcmBufferCount = value; }
        public uint PcmVolume { get => _emulator._state.PcmVolume; set => _emulator._state.PcmVolume = value; }
        public uint PcmMode { get => _emulator._state.PcmMode; set => _emulator._state.PcmMode = value; }
        public uint PcmSampleRate { get => _emulator._state.PcmSampleRate; set => _emulator._state.PcmSampleRate = value; }
        public unsafe Span<byte> PcmBuffer => new Span<byte>((void*)_emulator._state.PcmPtr, PcmSize);
        public ulong PcmPtr => _emulator._pcm_Ptr;
        public unsafe Span<PsgVoice> PsgVoices => new Span<PsgVoice>((void*)_emulator._state.PsgPtr, 16);
    }

    public class VeraFxState
    {
        private readonly Emulator _emulator;

        public VeraFxState(Emulator emulator)
        {
            _emulator = emulator;
        }

        public uint AddrMode { get => _emulator._state.FxAddrMode; set => _emulator._state.FxAddrMode = value; }
        public bool Fx4BitMode { get => _emulator._state.Fx4BitMode != 0; set => _emulator._state.Fx4BitMode = value ? 1u : 0u; }
        public bool CacheWrite { get => _emulator._state.FxCacheWrite != 0; set => _emulator._state.FxCacheWrite = value ? 1u : 0u; }
        public bool Cachefill { get => _emulator._state.FxCacheFill != 0; set => _emulator._state.FxCacheFill = value ? 1u : 0u; }
        public bool TransparantWrites { get => _emulator._state.FxTransparancy != 0; set => _emulator._state.FxTransparancy = value ? 1u : 0u; }
        public bool OneByteCycling { get => _emulator._state.FxOneByteCycling != 0; set => _emulator._state.FxOneByteCycling = value ? 1u : 0u; }
        public bool TwoByteCacheIncr { get => _emulator._state.Fx2ByteCacheIncr != 0; set => _emulator._state.Fx2ByteCacheIncr = value ? 1u : 0u; }
        public uint Cache { get => _emulator._state.FxCache; set => _emulator._state.FxCache = value; }

        public byte CacheIndex
        {
            get => (byte)_emulator._state.FxCacheIndex;
            set
            {
                var v = (byte)(value & 0b111);
                _emulator._state.FxCacheIndex = v;
                _emulator._state.FxCacheFillShift = (uint)(v >> 1) << 3;
            }
        }
        public int CacheShift { get => (int)_emulator._state.FxCacheFillShift; set => _emulator._state.FxCacheFillShift = (uint)value; }

        public bool MultiplierEnable { get => _emulator._state.FxMultiplierEnable != 0; set => _emulator._state.FxMultiplierEnable = value ? 1u : 0u; }
        //public bool Accumulate { get => _emulator._state.FxAccumulate != 0; set => _emulator._state.FxAccumulate = value ? 1u : 0u; }
        public uint Accumulator { get => _emulator._state.FxAccumulator; set => _emulator._state.FxAccumulator = value; }
        public uint AccumulateDirection { get => _emulator._state.FxAccumulateDirection; set => _emulator._state.FxAccumulateDirection = value; }

        public uint IncrementX { get => _emulator._state.FxXIncrement; set => _emulator._state.FxXIncrement = value; }
        public uint PositionX { get => _emulator._state.FxXPosition; set => _emulator._state.FxXPosition = value; }
        public bool Mult32X { get => _emulator._state.FxXMult32 != 0; set => _emulator._state.FxXPosition = value ? 1u : 0u; }
    }

    public class ViaState
    {
        private readonly Emulator _emulator;
        public ViaState(Emulator emulator)
        {
            _emulator = emulator;
        }

        public ushort Timer1_Latch { get => _emulator._state.Via_Timer1_Latch; set => _emulator._state.Via_Timer1_Latch = value; }
        public ushort Timer1_Counter { get => _emulator._state.Via_Timer1_Counter; set => _emulator._state.Via_Timer1_Counter = value; }
        public ushort Timer2_Latch { get => _emulator._state.Via_Timer2_Latch; set => _emulator._state.Via_Timer2_Latch = value; }
        public ushort Timer2_Counter { get => _emulator._state.Via_Timer2_Counter; set => _emulator._state.Via_Timer2_Counter = value; }

        public bool Interrupt_Timer1 { get => (_emulator.Memory[0x9f0e] & 0b01000000) != 0; set => _emulator.Memory[0x9f0e] |= (value ? (byte)0b01000000 : (byte)0); }
        public bool Interrupt_Timer2 { get => (_emulator.Memory[0x9f0e] & 0b00100000) != 0; set => _emulator.Memory[0x9f0e] |= (value ? (byte)0b00100000 : (byte)0); }
        public bool Interrupt_Cb1 { get => (_emulator.Memory[0x9f0e] & 0b00010000) != 0; set => _emulator.Memory[0x9f0e] |= (value ? (byte)0b00010000 : (byte)0); }
        public bool Interrupt_Cb2 { get => (_emulator.Memory[0x9f0e] & 0b00001000) != 0; set => _emulator.Memory[0x9f0e] |= (value ? (byte)0b00001000 : (byte)0); }
        public bool Interrupt_ShiftRegister { get => (_emulator.Memory[0x9f0e] & 0b0000100) != 0; set => _emulator.Memory[0x9f0e] |= (value ? (byte)0b0000100 : (byte)0); }
        public bool Interrupt_Ca1 { get => (_emulator.Memory[0x9f0e] & 0b0000010) != 0; set => _emulator.Memory[0x9f0e] |= (value ? (byte)0b0000010 : (byte)0); }
        public bool Interrupt_Ca2 { get => (_emulator.Memory[0x9f0e] & 0b0000001) != 0; set => _emulator.Memory[0x9f0e] |= (value ? (byte)0b0000001 : (byte)0); }

        public bool Timer1_Continous { get => _emulator._state.Via_Timer1_Continuous != 0; set => _emulator._state.Via_Timer1_Continuous = (value ? (byte)1 : (byte)0); }
        public bool Timer1_Pb7 { get => _emulator._state.Via_Timer1_Pb7 != 0; set => _emulator._state.Via_Timer1_Pb7 = (value ? (byte)1 : (byte)0); }
        public bool Timer1_Running { get => _emulator._state.Via_Timer1_Running != 0; set => _emulator._state.Via_Timer1_Running = (value ? (byte)1 : (byte)0); }

        public bool Timer2_PulseCount { get => _emulator._state.Via_Timer2_PulseCount != 0; set => _emulator._state.Via_Timer2_PulseCount = (value ? (byte)1 : (byte)0); }
        public bool Timer2_Running { get => _emulator._state.Via_Timer2_Running != 0; set => _emulator._state.Via_Timer2_Running = (value ? (byte)1 : (byte)0); }
        public byte Register_A_OutValue { get => _emulator._state.Via_Register_A_OutValue; set => _emulator._state.Via_Register_A_OutValue = value; }
        public byte Register_A_InValue { get => _emulator._state.Via_Register_A_InValue; set => _emulator._state.Via_Register_A_InValue = value; }
        public byte Register_A_Direction { get => _emulator._state.Via_Register_A_Direction; set => _emulator._state.Via_Register_A_Direction = value; }
        public bool Interrupt { get => (_emulator._state.Interrupt_Hit & (uint)InterruptSource.Via) != 0; }
    }

    public class I2cState
    {
        private readonly Emulator _emulator;
        public I2cState(Emulator emulator)
        {
            _emulator = emulator;
        }

        public uint Position { get => _emulator._state.I2cPosition; set => _emulator._state.I2cPosition = value; }
        public uint Previous { get => _emulator._state.I2cPrevious; set => _emulator._state.I2cPrevious = value; }
        public uint ReadWrite { get => _emulator._state.I2cReadWrite; set => _emulator._state.I2cReadWrite = value; }
        public uint Transmit { get => _emulator._state.I2cTransmit; set => _emulator._state.I2cTransmit = value; }
        public uint Mode { get => _emulator._state.I2cMode; set => _emulator._state.I2cMode = value; }
        public uint Address { get => _emulator._state.I2cAddress; set => _emulator._state.I2cAddress = value; }
        public uint DataToTransmit { get => _emulator._state.I2cDataToTransmit; set => _emulator._state.I2cDataToTransmit = value; }
        public unsafe Span<byte> Buffer => new Span<byte>((void*)_emulator._i2cBuffer_ptr, I2cBufferSize);
    }

    public class SmcState
    {
        private readonly Emulator _emulator;

        public SmcState(Emulator emulator)
        {
            _emulator = emulator;
        }

        public uint Offset { get => _emulator._state.SmcOffset; set => _emulator._state.SmcOffset = value; }
        public uint Data { get => _emulator._state.SmcData; set => _emulator._state.SmcData = value; }
        public uint DataCount { get => _emulator._state.SmcDataCount; set => _emulator._state.SmcDataCount = value; }
        public uint SmcKeyboard_ReadPosition { get => _emulator._state.Keyboard_ReadPosition; set => _emulator._state.Keyboard_ReadPosition = value; }
        public uint SmcKeyboard_WritePosition { get => _emulator._state.Keyboard_WritePosition; set => _emulator._state.Keyboard_WritePosition = value; }
        public uint SmcKeyboard_ReadNoData { get => _emulator._state.Keyboard_ReadNoData; set => _emulator._state.Keyboard_ReadNoData = value; }
        public uint Led { get => _emulator._state.SmcLed; set => _emulator._state.SmcLed = value; }
    }

    public class SpiState
    {
        private readonly Emulator _emulator;

        public SpiState(Emulator emulator)
        {
            _emulator = emulator;
        }

        public uint LastRead => _emulator.State.SpiSectorRead;
        public uint Position { get => _emulator._state.SpiPosition ; set => _emulator._state.SpiPosition = value; }
        public bool ChipSelect { get => _emulator._state.SpiChipSelect != 0; set => _emulator._state.SpiChipSelect = value ? 0u : 1u; }
        public bool AutoTx { get => _emulator._state.SpiAutoTx != 0; set => _emulator._state.SpiAutoTx = value ? 0u : 1u; }
        public uint ReceiveCount { get => _emulator._state.SpiReceiveCount; set => _emulator._state.SpiReceiveCount = value; }
        public uint SendCount { get => _emulator._state.SpiSendCount; set => _emulator._state.SpiSendCount = value; }
        public bool Idle { get => _emulator._state.SpiIdle != 0; set => _emulator._state.SpiIdle = value ? 0u : 1u; }
        // public uint CommandNext { get => _emulator._state.SpiCommandNext; set => _emulator._state.SpiCommandNext = value; }
        public uint PreviousValue { get => _emulator._state.SpiPreviousValue; set => _emulator._state.SpiPreviousValue = value; }
        public uint PreviousCommand { get => _emulator._state.SpiPreviousCommand; set => _emulator._state.SpiPreviousCommand = value; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CpuState
    {
        // C wrapper
        public ulong GetTicks = 0;              // function
        public ulong Sleep = 0;                 // function
        public ulong Step_Ym = 0;               // function
        public ulong WriteRegister_Ym = 0;      // function
        public uint Ym_Timer0 = 0;
        public uint Ym_Timer1 = 0;
        public uint Ym_BusyTimer = 0;
        public uint Ym_Interrupt = 0;
        public uint Ym_Address = 0;
        public uint Ym_Data = 0;
        public int Ym_Left = 0;
        public int Ym_Right = 0;

        public uint WrapperInitialised = 0;
        public uint WrapperSpacer = 0x01020304;
        // end of C wrapper

        public ulong WrapperFlags = 0;  // used by the linux wrapper to see if these calls have had their handlers injected.

        // arrays
        public ulong MemoryPtr = 0;
        public ulong RomPtr = 0;
        public ulong RamBankPtr = 0;
        public ulong DisplayPtr = 0;
        public ulong DisplayBufferPtr = 0;
        public ulong HistoryPtr = 0;
        public ulong I2cBufferPtr = 0;
        public ulong SmcKeyboardPtr = 0;
        public ulong SmcMousePtr = 0;
        public ulong SpiHistoryPtr = 0;
        public ulong SpiInboundBufferPtr = 0;
        public ulong SpiOutboundBufferPtr = 0;
        public ulong SdCardPtr = 0;
        public ulong BreadkpointPtr = 0;
        public ulong StackInfoPtr = 0;
        public ulong StackBreakpointPtr = 0;
        public ulong RtcNvram_ptr = 0;
        public ulong PcmPtr = 0;
        public ulong AudioOutputPtr = 0;
        public ulong DebugSpriteColoursPtr = 0;

        public ulong CurrentBankAddress = 0;

        public ulong VramPtr = 0;
        public ulong VramBreakpointPtr = 0;
        public ulong PalettePtr = 0;
        public ulong SpritePtr = 0;
        public ulong PsgPtr = 0;

        public ulong Data0_Address = 0;
        public ulong Data1_Address = 0;
        public ulong Data0_Step = 0;
        public ulong Data1_Step = 0;
        public ulong Data_Mask = 0;

        public ulong Clock_Previous = 0x00;
        public ulong Clock = 0x00;
        public ulong Clock_Pause = 0x00;
        public ulong Clock_AudioNext = 0;
        public ulong Clock_YmNext = 0;
        public ulong Last_CpuLineClock = 0x00;
        public ulong VeraClock = 0x00;
        public ulong Cpu_YPos = 0x00;

        public ulong History_Pos = 0x00;
        public ulong Debug_Pos = 0x00;

        public ulong Layer0_FetchMap = 0;
        public ulong Layer0_FetchTile = 0;
        public ulong Layer0_Renderer = 0;
        public ulong Layer1_FetchMap = 0;
        public ulong Layer1_FetchTile = 0;
        public ulong Layer1_Renderer = 0;

        public ulong Sprite_Jmp = 0;

        public ulong Layer0_Cur_TileAddress = 0xffffffffffffffff;
        public ulong Layer0_Cur_TileData = 0;
        public ulong Layer1_Cur_TileAddress = 0xffffffffffffffff;
        public ulong Layer1_Cur_TileData = 0;

        public ulong SpiCommand = 0;
        public ulong SpiCsdRegister_0 = 0;
        public ulong SpiCsdRegister_1 = 0;

        public ulong JoypadLive = 0;
        public ulong Joypad = 0;
        public ulong JoypadNewMask = 0;

        public ulong BaseTicks = 0;

        public uint IgnoreBreakpoint = 0;

        public uint ExitCode = 0;

        public uint BreakpointOffset = 0;

        public uint Dc_HScale = 0x00010000;
        public uint Dc_VScale = 0x00010000;

        public uint Brk_Causes_stop = 0;
        public uint Control = 0;
        public uint Frame_Control = 0; // just run
        public uint Stepping = 0; // 1 to only process one step each time
        public uint Frame_Sprite_Collision = 0;

        public uint I2cPosition = 0;
        //public uint I2cScancodePosition = 0;
        public uint I2cPrevious = 0;
        public uint I2cReadWrite = 0;
        public uint I2cTransmit = 0;
        public uint I2cMode = 0;
        public uint I2cAddress = 0;
        public uint I2cDataToTransmit = 0;

        public uint SmcOffset = 0;
        public uint Keyboard_ReadPosition = 0;
        public uint Keyboard_WritePosition = 0;
        public uint Keyboard_ReadNoData = 1;
        public uint SmcData = 0;
        public uint SmcDataCount = 0;
        public uint SmcLed = 0;
        public uint Mouse_ReadPosition = 0;
        public uint Mouse_WritePosition = 0;

        public uint RtcOffset = 0;
        public uint RtcData = 0;
        public uint RtcDataCount = 0;

        public uint SpiPosition = 0;
        public uint SpiChipSelect = 0;
        public uint SpiAutoTx = 0;
        public uint SpiReceiveCount = 0;
        public uint SpiSendCount = 0;
        public uint SpiSendLength = 0;
        public uint SpiIdle = 0;
        public uint SpiCommandNext = 0;
        public uint SpiInitialised = 0;
        public uint SpiPreviousValue = 0;
        public uint SpiPreviousCommand = 0;
        public uint SpiWriteBlock = 0;
        public uint SpiSdCardSize = 0;
        public uint SpiSectorRead = 0;
        //public uint SpiDeplyReady = 0;

        public uint JoypadCount = 0;
        public uint JoypadPrevious = 0;

        public uint StackBreakpointHit = 0;
        public uint BreakpointSource = 0;
        public uint VideoOutput = 1; // VGA

        public uint InitialStartup = 1;

        public uint AudioWrite = 0;
        public uint AudioCpuPartial = 0;

        public uint PcmBufferRead = 0;
        public uint PcmBufferWrite = 0;
        public uint PcmBufferCount = 0;
        public uint PcmSampleRate = 0;
        public uint PcmMode = 0;
        public uint PcmCount = 0;
        public uint PcmVolume = 0;

        public uint PcmValue_L = 0;
        public uint PcmValue_R = 0;

        public uint PsgNoiseSignal = 1;
        public uint PsgNoise = 0;

        public uint YmCpuPartial = 0;

        //public uint Via_Interrupt = 0;

        public uint RamBank = 0;
        public uint RomBank = 0;
        public uint MemoryRead = 0xffffffff;
        public uint MemoryReadPtr = 0xffffffff;
        public uint MemoryWrite = 0xffffffff;

        // FX
        public uint FxAddrMode = 0;

        public uint FxCache = 0;
        public uint FxCacheFill = 0;
        public uint FxCacheWrite = 0;
        public uint FxCacheIndex = 0;
        public uint Fx4BitMode = 0;
        public uint FxTransparancy = 0;
        public uint FxOneByteCycling = 0;
        public uint Fx2ByteCacheIncr = 0;
        public uint FxCacheFillShift = 0;
        public uint FxMultiplierEnable = 0;
        public uint FxAccumulator = 0;
        public uint FxAccumulateDirection = 0;
        public uint FxXIncrement = 0;
        public uint FxXPosition = 0;
        public uint FxYIncrement = 0;
        public uint FxYPosition = 0;
        public uint FxXMult32 = 0;
        public uint FxYMult32 = 0;
        //public uint FxSpacer = 0;

        // End FX

        public uint VramData = 0;

        public uint HistoryLogMask = (0x400 * 32) - 1; // 1024 entries

        public uint Interrupt_Mask = 0;
        public uint Interrupt_Hit = 0;

        public uint DebugSprites = 0;

        public ushort Pc = 0;
        public ushort StackPointer = 0x1fd; // apparently

        public byte A = 0;
        public byte X = 0;
        public byte Y = 0;

        public byte Decimal = 0;
        public byte BreakFlag = 0;
        public byte Overflow = 0;
        public byte Negative = 0;
        public byte Carry = 0;
        public byte Zero = 0;
        public byte InterruptDisable = 0;

        public byte SpacerD = 0;
        //public byte Interrupt = 0; // used for flags
        public byte Nmi = 0;
        public byte Nmi_Previous = 0;

        public byte AddrSel = 0;
        public byte DcSel = 0;
        public byte Dc_Border = 0;

        public ushort Dc_HStart = 0;
        public ushort Dc_HStop = 640;
        public ushort Dc_VStart = 0;
        public ushort Dc_VStop = 480;

        public byte Sprite_Enable = 0;
        public byte Layer0_Enable = 0;
        public byte Layer1_Enable = 0;

        public byte Display_Carry = 0;

        // layer 0
        public uint Layer0_MapAddress = 0;
        public uint Layer0_TileAddress = 0;
        public uint Layer0_T256C = 0;
        public ushort Layer0_HScroll = 0;
        public ushort Layer0_VScroll = 0;
        public byte Layer0_MapHeight = 0;
        public byte Layer0_MapWidth = 0;
        public byte Layer0_BitMapMode = 0;
        public byte Layer0_ColourDepth = 0;
        public byte Layer0_TileHeight = 0;
        public byte Layer0_TileWidth = 0;

        public byte Cpu_Waiting = 0;
        public byte Headless = 0;

        // layer 1
        public uint Layer1_MapAddress = 0;
        public uint Layer1_TileAddress = 0;
        public uint Layer1_T256C = 0;
        public ushort Layer1_HScroll = 0;
        public ushort Layer1_VScroll = 0;
        public byte Layer1_MapHeight = 0;
        public byte Layer1_MapWidth = 0;
        public byte Layer1_BitMapMode = 0;
        public byte Layer1_ColourDepth = 0;
        public byte Layer1_TileHeight = 0;
        public byte Layer1_TileWidth = 0;

        public ushort Interrupt_LineNum = 0;
        //public byte Interrupt_AFlow = 0;
        //public byte Interrupt_SpCol = 0;
        //public byte Interrupt_Line = 0;
        //public byte Interrupt_VSync = 0;

        //public byte Interrupt_Line_Hit = 0;
        //public byte Interrupt_Vsync_Hit = 0;
        //public byte Interrupt_SpCol_Hit = 0;

        public byte SpacerA = 0;
        public byte SpacerB = 0;
        public byte SpacerC = 0;

        // Rendering data
        public byte Drawing = 0;

        public uint Beam_Position = 0; // needs to be inline with the cpu clock
        public uint Frame_Count = 0;
        public uint Frame_Count_Breakpoint = 0; // zero will never hit as we check the change.
        public uint Buffer_Render_Position = 0;
        public uint Buffer_Output_Position = 0; // so there is 1 line between the render and output
        public uint Scale_x = 0;
        public uint Scale_y = 0;
        public uint Layer0_x = 0;
        public uint Layer1_x = 0;
        public uint Layer0_State = 1; // todo: change back to zero to match hardware
        public uint Layer1_State = 1; // todo: change back to zero to match hardware
        public uint Layer0_TileData = 0;
        public uint Layer1_TileData = 0;
        public uint Layer0_MapData = 0;
        public uint Layer1_MapData = 0;
        public uint Layer0_TilePos = 0;
        public uint Layer1_TilePos = 0;
        public uint Layer0_Width = 0;
        public uint Layer1_Width = 0;
        public uint Layer0_Mask = 0;
        public uint Layer1_Mask = 0;
        public uint Layer0_TileCount = 0;
        public uint Layer1_TileCount = 0;
        public uint Layer0_Wait = 0;
        public uint Layer1_Wait = 0;
        public uint Layer0_TileDone = 0;
        public uint Layer1_TileDone = 0;
        public ushort Beam_x = 0;
        public ushort Beam_y = 0;
        public byte DisplayDirty = 2;           // always draw the first render
        public byte RenderReady = 0;            // used to signal to GL to redaw

        public ushort _Padding = 0;

        // Sprites
        public uint Sprite_Wait = 0;            // delay until sprite rendering continues
        public uint Sprite_Position = 0xffffffff;       // which sprite we're considering
        public uint Vram_Wait = 0;              // vram delay to stall sprite data read
        public uint Sprite_Width = 0;           // count down until fully rendered
        public uint Sprite_Render_Mode = 0;
        public uint Sprite_X = 0;               // need to snap x
        public uint Sprite_Y = 0;               // need to snap y
        public uint Sprite_Depth = 0;
        public uint Sprite_CollisionMask = 0;

        public ushort Unused_Layer0_next_render = 0;
        public ushort Layer0_Tile_HShift = 0;
        public ushort Layer0_Tile_VShift = 0;
        public ushort Layer0_Map_HShift = 0;
        public ushort Layer0_Map_VShift = 0;

        public ushort Unused_Layer1_next_render = 0;
        public ushort Layer1_Tile_HShift = 0;
        public ushort Layer1_Tile_VShift = 0;
        public ushort Layer1_Map_HShift = 0;
        public ushort Layer1_Map_VShift = 0;

        public ushort Via_Timer1_Latch = 0;
        public ushort Via_Timer1_Counter = 0;
        public ushort Via_Timer2_Latch = 0;
        public ushort Via_Timer2_Counter = 0;

        public byte Via_Register_A_OutValue = 0;
        public byte Via_Register_A_InValue = 0xff; // default is all high
        public byte Via_Register_A_Direction = 0xff;
        public byte Via_Timer1_Continuous = 0;
        public byte Via_Timer1_Pb7 = 0;
        public byte Via_Timer1_Running = 0;

        public byte Via_Timer2_PulseCount = 0;
        public byte Via_Timer2_Running = 0;
        public byte _Padding2 = 0;

        public CpuState()
        {
        }

        public unsafe void SetPointers(ulong memory, ulong rom, ulong ramBank, ulong vram,
            ulong display, ulong palette, ulong sprite, ulong displayBuffer, ulong history, ulong i2cBuffer,
            ulong smcKeyboardPtr, ulong smcMousePtr, ulong spiHistoryPtr, ulong spiInboundBufferPtr, ulong spiOutbandBufferPtr,
            ulong breakpointPtr, ulong stackInfoPtr, ulong stackBreakpointPtr, ulong rtcNvramPtr, ulong pcmPtr,
            ulong audioOutputPtr, ulong psgPtr, ulong vramBreakpoint_ptr, ulong debugSpriteColoursPtr)
        {
            MemoryPtr = memory;
            RomPtr = rom;
            RamBankPtr = ramBank;
            VramPtr = vram;
            DisplayPtr = display;
            PalettePtr = palette;
            SpritePtr = sprite;
            DisplayBufferPtr = displayBuffer;
            HistoryPtr = history;
            I2cBufferPtr = i2cBuffer;
            SmcKeyboardPtr = smcKeyboardPtr;
            SmcMousePtr = smcMousePtr;
            SpiHistoryPtr = spiHistoryPtr;
            SpiInboundBufferPtr = spiInboundBufferPtr;
            SpiOutboundBufferPtr = spiOutbandBufferPtr;
            BreadkpointPtr = breakpointPtr;
            StackInfoPtr = stackInfoPtr;
            StackBreakpointPtr = stackBreakpointPtr;
            RtcNvram_ptr = rtcNvramPtr;
            PcmPtr = pcmPtr;
            AudioOutputPtr = audioOutputPtr;
            PsgPtr = psgPtr;
            VramBreakpointPtr = vramBreakpoint_ptr;
            DebugSpriteColoursPtr = debugSpriteColoursPtr;
        }
    }

    public enum EmulatorResult
    {
        ExitCondition,
        UnknownOpCode,
        DebugOpCode,
        BrkHit,
        SmcPowerOff,
        Stepping,
        Breakpoint,
        SmcReset,
        Unsupported = -1
    }

    [Flags]
    public enum BreakpointSourceType
    {
        Breakpoint = 0x01,
        Vram = 0x02,
        Stack = 0x04,
        Vsync = 0x08
    }

    private CpuState _state;
    public CpuState State => _state;

    public bool DebugMode { get; set; } // lets users of the enumlator know the application is in debug mode.

    public byte A { get => _state.A; set => _state.A = value; }
    public byte X { get => _state.X; set => _state.X = value; }
    public byte Y { get => _state.Y; set => _state.Y = value; }
    public ushort Pc { get => _state.Pc; set => _state.Pc = value; }
    public ushort StackPointer { get => _state.StackPointer; set => _state.StackPointer = value; }
    public ulong Clock { get => _state.Clock; set => _state.Clock = value; }
    public bool Carry { get => _state.Carry != 0; set => _state.Carry = (byte)(value ? 0x01 : 0x00); }
    public bool Zero { get => _state.Zero != 0; set => _state.Zero = (byte)(value ? 0x01 : 0x00); }
    public bool InterruptDisable { get => _state.InterruptDisable != 0; set => _state.InterruptDisable = (byte)(value ? 0x01 : 0x00); }
    public bool Decimal { get => _state.Decimal != 0; set => _state.Decimal = (byte)(value ? 0x01 : 0x00); }
    public bool BreakFlag { get => _state.BreakFlag != 0; set => _state.BreakFlag = (byte)(value ? 0x01 : 0x00); }
    public bool Overflow { get => _state.Overflow != 0; set => _state.Overflow = (byte)(value ? 0x01 : 0x00); }
    public bool Negative { get => _state.Negative != 0; set => _state.Negative = (byte)(value ? 0x01 : 0x00); }
    public bool Interrupt { get => (_state.Interrupt_Hit & _state.Interrupt_Mask) != 0; } // set => _state.Interrupt_Hit = (byte)(value ? 0x01 : 0x00); }

    public InterruptSource InterruptHit { get => (InterruptSource)_state.Interrupt_Hit; set => _state.Interrupt_Hit = (uint)value; }
    public InterruptSource InterruptMask { get => (InterruptSource)_state.Interrupt_Mask; set => _state.Interrupt_Mask = (uint)value; }

    public bool Nmi { get => _state.Nmi != 0; set => _state.Nmi = (byte)(value ? 0x01 : 0x00); }
    public ulong HistoryPosition => _state.History_Pos / 32;
    public uint RomBankAct { get => _state.RomBank; set => _state.RomBank = value; }
    public uint RamBankAct => Memory[0];

    public bool Headless { get => _state.Headless != 0; set => _state.Headless = (byte)(value ? 0x01 : 0x00); }
    public bool RenderReady { get => _state.RenderReady != 0; set => _state.RenderReady = (byte)(value ? 0x01 : 0x00); }

    public bool Brk_Causes_Stop { get => _state.Brk_Causes_stop != 0; set => _state.Brk_Causes_stop = (uint)(value ? 0x01 : 0x00); }

    public Control Control { get => (Control)_state.Control; set => _state.Control = (uint)value; }
    public FrameControl FrameControl { get => (FrameControl)_state.Frame_Control; set => _state.Frame_Control = (uint)value; }
    public bool Stepping { get => _state.Stepping != 0; set => _state.Stepping = (uint)(value ? 1 : 0); }

    public ulong Spi_CsdRegister_0 { get => _state.SpiCsdRegister_0; set => _state.SpiCsdRegister_0 = value; }
    public ulong Spi_CsdRegister_1 { get => _state.SpiCsdRegister_1; set => _state.SpiCsdRegister_1 = value; }


    public BreakpointSourceType BreakpointSource { get => (BreakpointSourceType)_state.BreakpointSource; set => _state.BreakpointSource = (uint)value; }

    public ulong Clock_AudioNext { get => _state.Clock_AudioNext; set => _state.Clock_AudioNext = value; }

    public uint AudioWrite => _state.AudioWrite;

    private readonly VeraState _vera;
    public VeraState Vera => _vera;

    private readonly ViaState _via;
    public ViaState Via => _via;

    private readonly I2cState _i2c;
    public I2cState I2c => _i2c;

    private readonly SmcState _smc;
    public SmcState Smc => _smc;

    private readonly VeraAudioState _veraAudio;
    public VeraAudioState VeraAudio => _veraAudio;

    private readonly VeraFxState _veraFx;
    public VeraFxState VeraFx => _veraFx;

    private readonly SpiState _spiState;
    public SpiState Spi => _spiState;

    public uint Keyboard_ReadPosition => _state.Keyboard_ReadPosition;
    public uint Keyboard_WritePosition { get => _state.Keyboard_WritePosition; set => _state.Keyboard_WritePosition = value; }
    public uint Mouse_ReadPosition => _state.Mouse_ReadPosition;
    public uint Mouse_WritePosition { get => _state.Mouse_WritePosition; set => _state.Mouse_WritePosition = value; }

    public ulong JoystickData { get => _state.JoypadLive; set => _state.JoypadLive = value; }
    public ulong JoystickNewMask { get => _state.JoypadNewMask; set => _state.JoypadNewMask = value; }

    private const int _rounding = 32; // 32 byte (256bit) allignment required for AVX 256 instructions
    private const ulong _roundingMask = ~(ulong)_rounding + 1;

    private readonly ulong _memory_ptr;
    private readonly ulong _memory_ptr_rounded;
    private readonly ulong _rom_ptr;
    private readonly ulong _rom_ptr_rounded;
    private readonly ulong _ram_ptr;
    private readonly ulong _ram_ptr_rounded;
    private readonly ulong _vram_ptr;
    private readonly ulong _vramBreakpoint_ptr;
    private readonly ulong _display_ptr;
    private readonly ulong _display_buffer_ptr;
    private readonly ulong _display_buffer_ptr_rounded;
    private readonly ulong _palette_ptr;
    private ulong _history_ptr;
    private readonly ulong _sprite_ptr;
    private readonly ulong _i2cBuffer_ptr;
    private readonly ulong _smcKeyboard_ptr;
    private readonly ulong _smcMouse_ptr;
    private readonly ulong _spiHistory_ptr;
    private readonly ulong _spiInboundBufferPtr;
    private readonly ulong _spiOutboundBufferPtr;
    private readonly ulong _breakpoint_Ptr;
    private readonly ulong _breakpoint_ptr_rounded;
    private readonly ulong _stackInfo_Ptr;
    private readonly ulong _stackBreakpoint_Ptr;
    private readonly ulong _rtcNvram_Ptr;
    private readonly ulong _pcm_Ptr;
    private readonly ulong _audioOutput_ptr;
    private readonly ulong _psg_ptr;
    private readonly ulong _debug_sprite_colours_ptr;

    public const int RamSize = 0x10000;
    public const int RomSize = 0x4000 * 256; // was 32, now 256 for cartridge
    public const int BankedRamSize = 0x2000 * 256;
    public const int VramSize = 0x20000;
    public const int DisplaySize = 800 * 525 * 4 * 7; // *6 for each layer + debug layer
    public const int PaletteSize = 256 * 4;
    public const int DisplayBufferSize = 2048 * 2 * 6; // Pallette index for two lines * 4 for each layers 0, 1, sprite value, sprite depth, sprite collision, sprite debug - one line being rendered, one being output, 2048 to provide enough space so scaling of $ff works
    //public const int HistorySize = 16 * 1024;
    public const int SpriteSize = 64 * 128;
    public const int I2cBufferSize = 1024;
    public const int SmcKeyboardBufferSize = 16;
    public const int SmcMouseBufferSize = 8;
    public const int SpiHistoryPtrSize = 1024 * 2;
    public const int SpiInboundBufferPtrSize = 1024; // 512 + 4;
    public const int SpiOutboundBufferPtrSize = 1024; // 512 + 4;
    public const int BreakpointSize = 0x10000 + 0x2000 * 256 + 0x4000 * 256; // base ram, rambanks, rombanks. 256 rom banks for carts.
    public const int StackInfoSize = 256 * 4;
    public const int StackBreakpointSize = 256;
    public const int DebugSpriteColourCount = 129;
    public const int DebugSpriteColoursSize = 4 * DebugSpriteColourCount;
    public const int RtcNvramSize = 64;
    private const int PcmSize = 1024 * 4;
    public const int AudioOutputSize = 1024 * 1024 * 2; // 2meg for both Left and Right
    public const int PsgSize = 16 * 16 * 4;
    private static ulong RoundMemoryPtr(ulong inp) => (inp & _roundingMask) + (ulong)_rounding;

    public SdCard? SdCard { get; private set; }

    public double WindowScale { get; private set; }

    public void LoadSdCard(SdCard sdCard)
    {
        SdCard = sdCard;
        sdCard.SetCsdRegister(this);
        _state.SdCardPtr = SdCard.MemoryPtr;// + 0xc00; // vdi's have a header
        _state.SpiSdCardSize = (uint)(sdCard.Size / 512L);
    }

    public EmulatorOptions Options { get; private set; }

    public unsafe Emulator(EmulatorOptions? options = null)
    {
        Options = options ?? new EmulatorOptions();

        if (Options.HistorySize == 0 || (Options.HistorySize & (Options.HistorySize - 1)) != 0)
            throw new Exception("History size must be a multiple of 2 and not zero");

        WindowScale = Options.WindowScale;

        _state = new CpuState();

        _state.HistoryLogMask = (uint)(Options.HistorySize * 32 - 1);

        _memory_ptr = (ulong)NativeMemory.Alloc(RamSize + _rounding);
        _memory_ptr_rounded = RoundMemoryPtr(_memory_ptr);

        _rom_ptr = (ulong)NativeMemory.Alloc(RomSize + _rounding);
        _rom_ptr_rounded = RoundMemoryPtr(_rom_ptr);

        _ram_ptr = (ulong)NativeMemory.Alloc(BankedRamSize + _rounding);
        _ram_ptr_rounded = RoundMemoryPtr(_ram_ptr);

        _display_buffer_ptr = (ulong)NativeMemory.Alloc(DisplayBufferSize + _rounding);
        _display_buffer_ptr_rounded = RoundMemoryPtr(_display_buffer_ptr);

        _display_ptr = (ulong)NativeMemory.Alloc(DisplaySize);
        _vram_ptr = (ulong)NativeMemory.Alloc(VramSize);
        _vramBreakpoint_ptr = (ulong)NativeMemory.Alloc(VramSize);
        _palette_ptr = (ulong)NativeMemory.Alloc(PaletteSize);
        _sprite_ptr = (ulong)NativeMemory.Alloc(SpriteSize);
        _history_ptr = (ulong)NativeMemory.Alloc(State.HistoryLogMask + 1);
        _i2cBuffer_ptr = (ulong)NativeMemory.Alloc(I2cBufferSize);
        _smcKeyboard_ptr = (ulong)NativeMemory.Alloc(SmcKeyboardBufferSize);
        _smcMouse_ptr = (ulong)NativeMemory.Alloc(SmcMouseBufferSize);

        _breakpoint_Ptr = (ulong)NativeMemory.Alloc(BreakpointSize * 4 + _rounding);
        _breakpoint_ptr_rounded = RoundMemoryPtr(_breakpoint_Ptr);

        _spiHistory_ptr = (ulong)NativeMemory.Alloc(SpiHistoryPtrSize);
        _spiInboundBufferPtr = (ulong)NativeMemory.Alloc(SpiInboundBufferPtrSize);
        _spiOutboundBufferPtr = (ulong)NativeMemory.Alloc(SpiOutboundBufferPtrSize);
        _stackInfo_Ptr = (ulong)NativeMemory.Alloc(StackInfoSize);
        _stackBreakpoint_Ptr = (ulong)NativeMemory.Alloc(StackBreakpointSize);
        _rtcNvram_Ptr = (ulong)NativeMemory.Alloc(RtcNvramSize);

        _pcm_Ptr = (ulong)NativeMemory.Alloc(PcmSize);
        _audioOutput_ptr = (ulong)NativeMemory.Alloc(AudioOutputSize);
        _psg_ptr = (ulong)NativeMemory.Alloc(PsgSize);

        _debug_sprite_colours_ptr = (ulong)NativeMemory.Alloc(DebugSpriteColoursSize);

        _vera = new VeraState(this);
        _via = new ViaState(this);
        _i2c = new I2cState(this);
        _smc = new SmcState(this);
        _veraAudio = new VeraAudioState(this);
        _veraFx = new VeraFxState(this);
        _spiState = new SpiState(this);

        SetPointers();

        var memory_span = new Span<byte>((void*)_memory_ptr_rounded, RamSize);
        for (var i = 0; i < RamSize; i++)
            memory_span[i] = 0;

        var ram_span = new Span<byte>((void*)_ram_ptr_rounded, BankedRamSize);
        for (var i = 0; i < BankedRamSize; i++)
            ram_span[i] = 0;

        var vram_span = new Span<byte>((void*)_vram_ptr, VramSize);
        var vrambreakpoint_span = new Span<byte>((void*)_vramBreakpoint_ptr, VramSize);
        for (var i = 0; i < VramSize; i++)
        {
            vram_span[i] = 0;
            vrambreakpoint_span[i] = 0;
        }

        var rom_span = new Span<byte>((void*)_rom_ptr_rounded, RomSize);
        for (var i = 0; i < RomSize; i++)
            rom_span[i] = 0;

        var buffer_span = new Span<byte>((void*)_display_buffer_ptr_rounded, DisplayBufferSize);
        for (var i = 0; i < DisplayBufferSize; i++)
            buffer_span[i] = 0;

        var history_span = new Span<byte>((void*)_history_ptr, (int)State.HistoryLogMask + 1);
        for (var i = 0; i < State.HistoryLogMask + 1; i++)
            history_span[i] = 0;

        var sprite_span = new Span<byte>((void*)_sprite_ptr, SpriteSize);
        for (var i = 0; i < SpriteSize; i++)
            sprite_span[i] = 0;

        var i2cBuffer_span = new Span<byte>((void*)_i2cBuffer_ptr, I2cBufferSize);
        for (var i = 0; i < I2cBufferSize; i++)
            i2cBuffer_span[i] = 0;

        var smcKeyboard_span = new Span<byte>((void*)_smcKeyboard_ptr, SmcKeyboardBufferSize);
        for (var i = 0; i < SmcKeyboardBufferSize; i++)
            smcKeyboard_span[i] = 0;

        var smsMouse_span = new Span<byte>((void*)_smcMouse_ptr, SmcMouseBufferSize);
        for (var i = 0; i < SmcMouseBufferSize; i++)
            smsMouse_span[i] = 0;

        var spiHistory_span = new Span<byte>((void*)_spiHistory_ptr, SpiHistoryPtrSize);
        for (var i = 0; i < SpiHistoryPtrSize; i++)
            spiHistory_span[i] = 0;

        var spiInboundBuffer_span = new Span<byte>((void*)_spiInboundBufferPtr, SpiInboundBufferPtrSize);
        for (var i = 0; i < SpiInboundBufferPtrSize; i++)
            spiInboundBuffer_span[i] = 0;

        var spiOutboundBuffer_span = new Span<byte>((void*)_spiOutboundBufferPtr, SpiOutboundBufferPtrSize);
        for (var i = 0; i < SpiOutboundBufferPtrSize; i++)
            spiOutboundBuffer_span[i] = 0;

        var breakpoint_span = new Span<uint>((void*)_breakpoint_Ptr, BreakpointSize);
        for (var i = 0; i < BreakpointSize; i++)
            breakpoint_span[i] = 0;

        var breakpointStack_span = new Span<byte>((void*)_stackBreakpoint_Ptr, StackBreakpointSize);
        for (var i = 0; i < StackBreakpointSize; i++)
            breakpointStack_span[i] = 0;

        var stackinfo_span = new Span<int>((void*)_stackInfo_Ptr, StackInfoSize / 4);
        for (var i = 0; i < StackInfoSize / 4; i++)
            stackinfo_span[i] = 0;

        var rtcNvram_span = new Span<byte>((void*)_rtcNvram_Ptr, RtcNvramSize);
        for (var i = 0; i < RtcNvramSize; i++)
            rtcNvram_span[i] = 0;

        var pcm_span = new Span<byte>((void*)_pcm_Ptr, PcmSize);
        for (var i = 0; i < PcmSize; i++)
            pcm_span[i] = 0;

        var audioOutput_span = new Span<short>((void*)_audioOutput_ptr, AudioOutputSize / 2);
        for (var i = 0; i < AudioOutputSize / 2; i++)
            audioOutput_span[i] = 0;

        var debugSpriteColours_span = new Span<uint>((void*)_debug_sprite_colours_ptr, DebugSpriteColourCount);
        for (var i = 0; i < DebugSpriteColourCount; i++)
            debugSpriteColours_span[i] = 0;

        // set defaults
        var sprite_act_span = new Span<Sprite>((void*)_sprite_ptr, 128);
        for (var i = 0; i < 128; i++)
        {
            sprite_act_span[i].Height = 8;
            sprite_act_span[i].Width = 8;
        }

        var psg_span = new Span<PsgVoice>((void*)_psg_ptr, 16);
        for (var i = 0; i < 16; i++)
        {
            psg_span[i].GenerationPtr = 0;
            psg_span[i].Phase = 0;
            psg_span[i].Frequency = 0;
            psg_span[i].Volume = 0;
            psg_span[i].LeftRight = 0;
            psg_span[i].Width = 0;
            psg_span[i].Waveform = 0;
            psg_span[i].Value = 0;
            psg_span[i].Noise = 0;
        }

        SmcBuffer = new SmcBuffer(this);
    }

    public unsafe void FillMemory(byte memoryFillValue)
    {
        var memory_span = new Span<byte>((void*)_memory_ptr_rounded, RamSize);
        for (var i = 2; i < RamSize; i++) // 2 as we dont change the RAM\ROM bank, as these are used on startup to set the rom bank, a slight difference to hardware.
        {
            if (i < 0x9f00 || (i >= 0xa000 && i < 0xc000)) // dont change IO area or ROM data
                memory_span[i] = memoryFillValue;
        }

        var ram_span = new Span<byte>((void*)_ram_ptr_rounded, BankedRamSize);
        for (var i = 0; i < BankedRamSize; i++)
        {
            ram_span[i] = memoryFillValue;
        }

        var vram_span = new Span<byte>((void*)_vram_ptr, VramSize);
        for (var i = 0; i < VramSize; i++)
        {
            vram_span[i] = memoryFillValue;
        }
    }

    private void SetPointers() => _state.SetPointers(_memory_ptr_rounded, _rom_ptr_rounded, _ram_ptr_rounded, _vram_ptr, _display_ptr, _palette_ptr,
            _sprite_ptr, _display_buffer_ptr_rounded, _history_ptr, _i2cBuffer_ptr, _smcKeyboard_ptr, _smcMouse_ptr, _spiHistory_ptr,
            _spiInboundBufferPtr, _spiOutboundBufferPtr, _breakpoint_ptr_rounded, _stackInfo_Ptr, _stackBreakpoint_Ptr, _rtcNvram_Ptr,
            _pcm_Ptr, _audioOutput_ptr, _psg_ptr, _vramBreakpoint_ptr, _debug_sprite_colours_ptr);

    public ulong DisplayPtr => _display_ptr;

    public unsafe Span<byte> Memory => new Span<byte>((void*)_memory_ptr_rounded, RamSize);
    public unsafe Span<byte> RamBank => new Span<byte>((void*)_ram_ptr_rounded, BankedRamSize);
    public unsafe Span<byte> RomBank => new Span<byte>((void*)_rom_ptr_rounded, RomSize);
    public unsafe Span<PixelRgba> Display => new Span<PixelRgba>((void*)_display_ptr, DisplaySize / 4);
    public unsafe Span<byte> DisplayRaw => new Span<byte>((void*)_display_ptr, DisplaySize);
    public unsafe Span<PixelRgba> Palette => new Span<PixelRgba>((void*)_palette_ptr, PaletteSize / 4);
    public unsafe Span<Sprite> Sprites => new Span<Sprite>((void*)_sprite_ptr, 128);
    public unsafe Span<EmulatorHistory> History => new Span<EmulatorHistory>((void*)_history_ptr, ((int)State.HistoryLogMask + 1) / 32);
    public unsafe Span<byte> KeyboardBuffer => new Span<byte>((void*)_smcKeyboard_ptr, SmcKeyboardBufferSize);
    public unsafe Span<byte> MouseBuffer => new Span<byte>((void*)_smcMouse_ptr, SmcMouseBufferSize);
    public unsafe Span<uint> Breakpoints => new Span<uint>((void*)_breakpoint_ptr_rounded, BreakpointSize);
    public unsafe Span<byte> VramBreakpoints => new Span<byte>((void*)_vramBreakpoint_ptr, VramSize);
    public unsafe Span<uint> StackInfo => new Span<uint>((void*)_stackInfo_Ptr, StackInfoSize / 4);
    public unsafe Span<byte> StackBreakpoints => new Span<byte>((void*)_stackBreakpoint_Ptr, StackBreakpointSize);
    public unsafe Span<byte> RtcNvram => new Span<byte>((void*)_rtcNvram_Ptr, RtcNvramSize);
    public unsafe Span<byte> DisplayBuffer => new Span<byte>((void*)_display_buffer_ptr_rounded, DisplayBufferSize);
    public unsafe Span<byte> SpiInboundBuffer => new Span<byte>((void*)_spiInboundBufferPtr, SpiInboundBufferPtrSize);
    public unsafe Span<byte> SpiOutboundBuffer => new Span<byte>((void*)_spiOutboundBufferPtr, SpiOutboundBufferPtrSize);
    public unsafe Span<short> AudioOutputBuffer => new Span<short>((void*)_audioOutput_ptr, AudioOutputSize / 2);
    public unsafe Span<uint> DebugSpriteColours => new Span<uint>((void*)_debug_sprite_colours_ptr, DebugSpriteColourCount);
    public ulong AudioOutputPtr => _audioOutput_ptr;
    public SmcBuffer SmcBuffer { get; }
    public EmulatorResult Result { get; internal set; }

    public EmulatorResult Emulate()
    {
        //var i2c = SmcBuffer;
        //var i2c_thread = new Thread(_ => i2c.RunI2cCapture(ref _state));
        //i2c_thread.UnsafeStart();


        //var spi = new SpiBuffer();
        //var spi_thread = new Thread(_ => spi.RunSpiDisplay(ref _state));
        //spi_thread.UnsafeStart();


        System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;
        var r = fnEmulatorCode(ref _state);
        Result = (EmulatorResult)r;
        System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Normal;

        //spi.Stop();
        //i2c.Stop();

        return Result;
    }

    public void SetState(CpuState state)
    {
        _state = state;
        SetPointers();
    }

    public unsafe void SetOptions(EmulatorOptions options)
    {
        var oldOptions = Options;

        Options = options;

        WindowScale = options.WindowScale;

        if (oldOptions.HistorySize != options.HistorySize)
        {
            if (Options.HistorySize == 0 || (Options.HistorySize & (Options.HistorySize - 1)) != 0)
                throw new Exception("History size must be a multiple of 2 and not zero");

            NativeMemory.Free((void*)_history_ptr);

            _state.HistoryLogMask = (uint)(Options.HistorySize * 32 - 1);

            _history_ptr = (ulong)NativeMemory.Alloc(State.HistoryLogMask + 1);
            _state.HistoryPtr = _history_ptr;

            var history_span = new Span<byte>((void*)_history_ptr, (int)_state.HistoryLogMask + 1);
            for (var i = 0; i < State.HistoryLogMask + 1; i++)
                history_span[i] = 0;

            _state.History_Pos = 0;
        }
    }

    public unsafe void ResetHistory()
    {
        var history_span = new Span<byte>((void*)_history_ptr, (int)_state.HistoryLogMask + 1);
        for (var i = 0; i < State.HistoryLogMask + 1; i++)
            history_span[i] = 0;

        _state.History_Pos = 0;
    }

    public unsafe void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual unsafe void Dispose(bool disposing)
    {
        NativeMemory.Free((void*)_memory_ptr);
        NativeMemory.Free((void*)_rom_ptr);
        NativeMemory.Free((void*)_ram_ptr);
        NativeMemory.Free((void*)_display_ptr);
        NativeMemory.Free((void*)_vram_ptr);
        NativeMemory.Free((void*)_vramBreakpoint_ptr);
        NativeMemory.Free((void*)_palette_ptr);
        NativeMemory.Free((void*)_sprite_ptr);
        NativeMemory.Free((void*)_display_buffer_ptr);
        NativeMemory.Free((void*)_history_ptr);
        NativeMemory.Free((void*)_i2cBuffer_ptr);
        NativeMemory.Free((void*)_smcMouse_ptr);
        NativeMemory.Free((void*)_smcKeyboard_ptr);
        NativeMemory.Free((void*)_spiHistory_ptr);
        NativeMemory.Free((void*)_spiInboundBufferPtr);
        NativeMemory.Free((void*)_spiOutboundBufferPtr);
        NativeMemory.Free((void*)_breakpoint_Ptr);
        NativeMemory.Free((void*)_stackInfo_Ptr);
        NativeMemory.Free((void*)_stackBreakpoint_Ptr);
        NativeMemory.Free((void*)_rtcNvram_Ptr);
        NativeMemory.Free((void*)_pcm_Ptr);
        NativeMemory.Free((void*)_audioOutput_ptr);
        NativeMemory.Free((void*)_psg_ptr);
        NativeMemory.Free((void*)_debug_sprite_colours_ptr);
    }
}

public class EmulatorOptions
{
    public int HistorySize { get; set; } = 0x400;
    public double WindowScale { get; set; } = 1;
}