using System.Runtime.InteropServices;

namespace BitMagic.X16Emulator.TestHelper;

/// <summary>
/// A pure-managed stand-in for zimodem_host.dll's C ABI (see
/// External/BitMagic.ZiModem/native/wrapper/include/zimodem_host.h), for tests that need
/// deterministic control over the emulated UART's "modem side" without a real ZiModem
/// firmware instance, background thread, or AT-command round-trip.
///
/// Only rx_available/rx_read (modem -> host) and write_serial (host -> modem) have real
/// behaviour -- those are the only three functions Uart.asm/zimodem.asm actually call.
/// create/set_pin/destroy/get_line_config are no-op stubs; nothing in the UART FIFO/
/// interrupt logic depends on them. set_callbacks does capture the on_serial_out callback
/// (and its user_context), because it's the only way to set zimodem.data_available -- the
/// flag uart_tick's own gate checks before it will even attempt to drain the inbound
/// queue. EnqueueInbound fires that callback synchronously, mirroring what the real HAL's
/// write() does on the modem's background thread.
/// </summary>
public sealed class MockZiModemHost
{
    private const nint Handle = 1;

    private readonly Queue<byte> _inbound = new();
    private readonly List<byte> _sentBytes = new();

    private nint _onSerialOut;
    private nint _serialOutUserContext;

    // Keep delegate instances alive for the mock's lifetime -- Marshal.GetFunctionPointerForDelegate
    // does not root them, and a collected delegate leaves a dangling native thunk.
    private readonly CreateDelegate _create;
    private readonly SetCallbacksDelegate _setCallbacks;
    private readonly StartDelegate _start;
    private readonly WriteSerialDelegate _writeSerial;
    private readonly RxAvailableDelegate _rxAvailable;
    private readonly RxReadDelegate _rxRead;
    private readonly SetPinDelegate _setPin;
    private readonly GetLineConfigDelegate _getLineConfig;
    private readonly DestroyDelegate _destroy;

    public MockZiModemHost()
    {
        _create = _ => Handle;

        _setCallbacks = (h, onSerialOut, onSignal, onLog, onLineConfig, userContext) =>
        {
            _onSerialOut = onSerialOut;
            _serialOutUserContext = userContext;

            // Callbacks are only wired up once the emulated CPU actually reaches
            // zimodem_init, which happens well after a test's EnqueueInbound() call --
            // if bytes are already queued at that point, the "go drain it" wake-up they
            // would have fired never had anywhere to go. Fire it retroactively now.
            if (_inbound.Count > 0)
                FireSerialOut();
        };

        _start = _ => 0;

        _writeSerial = (h, data, len) =>
        {
            for (var i = 0; i < (int)len; i++)
                _sentBytes.Add(Marshal.ReadByte(data, i));
            return 0;
        };

        _rxAvailable = _ => _inbound.Count > 0 ? 1 : 0;
        _rxRead = _ => _inbound.Count > 0 ? _inbound.Dequeue() : -1;
        _setPin = (h, pin, value) => { };

        _getLineConfig = (h, outBaud, outDataBits, outParity, outStopBitsX10) =>
        {
            if (outBaud != 0) Marshal.WriteInt32(outBaud, 0);
            if (outDataBits != 0) Marshal.WriteInt32(outDataBits, 0);
            if (outParity != 0) Marshal.WriteInt32(outParity, 0);
            if (outStopBitsX10 != 0) Marshal.WriteInt32(outStopBitsX10, 0);
        };

        _destroy = _ => { };

        Exports = new ZiModemHostFunctions
        {
            Create = Marshal.GetFunctionPointerForDelegate(_create),
            SetCallbacks = Marshal.GetFunctionPointerForDelegate(_setCallbacks),
            Start = Marshal.GetFunctionPointerForDelegate(_start),
            WriteSerial = Marshal.GetFunctionPointerForDelegate(_writeSerial),
            RxAvailable = Marshal.GetFunctionPointerForDelegate(_rxAvailable),
            RxRead = Marshal.GetFunctionPointerForDelegate(_rxRead),
            SetPin = Marshal.GetFunctionPointerForDelegate(_setPin),
            Destroy = Marshal.GetFunctionPointerForDelegate(_destroy),
            GetLineConfig = Marshal.GetFunctionPointerForDelegate(_getLineConfig),
            KeepAlive = this,
        };
    }

    /// <summary>Pass as <see cref="EmulatorOptions.ZiModemHostOverride"/>.</summary>
    public ZiModemHostFunctions Exports { get; }

    /// <summary>
    /// Queues a byte as if it just arrived from the modem, and -- matching the real HAL's
    /// write() -- fires the on_serial_out callback so uart_tick's data_available gate lets
    /// the next tick actually drain it.
    /// </summary>
    public void EnqueueInbound(byte value)
    {
        _inbound.Enqueue(value);
        FireSerialOut();
    }

    public void EnqueueInbound(IEnumerable<byte> values)
    {
        foreach (var value in values)
            _inbound.Enqueue(value);
        FireSerialOut();
    }

    /// <summary>Bytes the emulator has sent to the modem (the outbound FIFO's drained contents), in order.</summary>
    public IReadOnlyList<byte> SentBytes => _sentBytes;

    private void FireSerialOut()
    {
        if (_onSerialOut == 0)
            return;

        var callback = Marshal.GetDelegateForFunctionPointer<OnSerialOutDelegate>(_onSerialOut);
        callback(_serialOutUserContext);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint CreateDelegate(nint cfg);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetCallbacksDelegate(nint h, nint onSerialOut, nint onSignal, nint onLog, nint onLineConfig, nint userContext);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int StartDelegate(nint h);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int WriteSerialDelegate(nint h, nint data, nuint len);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int RxAvailableDelegate(nint h);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int RxReadDelegate(nint h);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetPinDelegate(nint h, int pin, int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void GetLineConfigDelegate(nint h, nint outBaud, nint outDataBits, nint outParity, nint outStopBitsX10);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DestroyDelegate(nint h);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnSerialOutDelegate(nint userContext);
}
