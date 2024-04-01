
using BitMagic.Compiler;
using BitMagic.Decompiler;
using BitMagic.X16Emulator;
using BitMagic.X16Emulator.Display;
using BitMagic.X16Emulator.Serializer;
using CommandLine;
using System.Text;
using Thread = System.Threading.Thread;

namespace X16E;

static class Program
{
    private static Thread? EmulatorThread;

    private const string RomEnvironmentVariable = "BITMAGIC_ROM";
    private static bool RestartOnStop = false;
    private static readonly AutoResetEvent ContinueEvent = new AutoResetEvent(false);
    private static Emulator Emulator;
    private static CommandLineOptions Options;

    public class CommandLineOptions
    {
        [Option('p', "prg", Required = false, HelpText = ".prg file to load.")]
        public string PrgFilename { get; set; } = "";

        [Option('r', "rom", Required = false, HelpText = "rom file to load, will look for rom.bin using locally or falling back to the BITMAGIC_ROM environment variable.")]
        public string RomFilename { get; set; } = "rom.bin";

        [Option('a', "address", Required = false, HelpText = "Start address.")]
        public ushort StartAddress { get; set; } = 0x810;

        [Option('c', "code", Required = false, HelpText = "Code file to compile. Result will be loaded at 0x801.")]
        public string CodeFilename { get; set; } = "";

        [Option('w', "write", Required = false, HelpText = "Write the result of the compilation.")]
        public bool WritePrg { get; set; } = false;

        [Option("warp", Required = false, HelpText = "Run as fast as possible.")]
        public bool Warp { get; set; } = false;

        [Option('s', "sdcard", Required = false, HelpText = "SD Card to attach. Can be a .zip or .gz file, in the form 'name.xxx.zip', where xxx is either BIN or VHD.")]
        public string? SdCardFileName { get; set; }

        [Option("sdcard-size", Required = false, HelpText = "SD Card size in mb if the card is being created by the emulator.")]
        public ulong SdCardSize { get; set; } = 16;

        [Option('d', "sdcard-folder", Required = false, HelpText = "Set the home folder for the SD Card.")]
        public string? SdCardFolder { get; set; }

        [Option("sdcard-synctox16", Required = false, HelpText = "Sync any changes to the home directory to SD Card. Root directory only.")]
        public bool SdCardSyncTo { get; set; } = false;

        [Option("sdcard-syncfromx16", Required = false, HelpText = "Sync any changes to SD Card to the home directory. Root directory only.")]
        public bool SdCardSyncFrom { get; set; } = false;

        [Option('y', "sdcard-sync", Required = false, HelpText = "Sync any changes to SD Card to the home directory, or vice versa. Root directory only. Same as setting --sdcard-synctox16 --sdcard-syncfromx16.")]
        public bool SdCardFullSync { get; set; } = false;

        [Option('f', "sdcard-file", Required = false, HelpText = "File to add to the SD Card root directory. Can add multiple files and use wildcards.")]
        public IEnumerable<string>? SdCardFiles { get; set; }

        [Option("sdcard-write", Required = false, HelpText = "SD Card file to write at the end of emulation. Can be a .zip or .gz file, in the form 'name.xxx.zip', where xxx is either BIN or VHD.")]
        public string? SdCardWrite { get; set; }

        [Option("sdcard-overwrite", Required = false, HelpText = "When writing the SD Card file, it can overwrite.")]
        public bool SdCardOverrwrite { get; set; } = false;


        [Option('u', "sdcard-update", Required = false, HelpText = "Sets 'sdcard-write' to the 'sdcard' parameter and enables overwrite.")]
        public bool SdCardUpdate { get; set; } = false;

        [Option("cart", Required = false, HelpText = "Cartridge file to load as a standard binary file. Can be a .zip or .gz file, in the form 'name.cart.zip'.")]
        public string Cartridge { get; set; } = "";

        [Option("dump", Required = false, HelpText = "Start emulator with the state from a dump file.")]
        public string DumpFile { get; set; } = "";

        [Option("dump-folder", Required = false, HelpText = "Folder to write dump files (Menu + Left Ctrl + S) to.")]
        public string DumpFolder { get; set; } = "";

        //[Option('m', "autorun", Required = false, HelpText = "Automatically run at startup. Ignored if address is specified. NOT YET IMPLEMENTED")]
        public bool AutoRun { get; set; } = false;
    }

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("BitMagic - X16E");

        var emulator = new Emulator();
        Emulator = emulator;

        ParserResult<CommandLineOptions>? argumentsResult;
        try
        {
            argumentsResult = Parser.Default.ParseArguments<CommandLineOptions>(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error processing arguments:");
            Console.WriteLine(ex.Message);
            return 1;
        }

        var options = argumentsResult.Value;
        Options = argumentsResult.Value;

        if (options == null)
        {
            return 1;
        }

        if (options.WritePrg && string.IsNullOrWhiteSpace(options.CodeFilename))
        {
            Console.WriteLine("Cannot have write the result of compilation if no codefile is set.");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(options.PrgFilename) && !string.IsNullOrWhiteSpace(options.CodeFilename) && !options.WritePrg)
        {
            Console.WriteLine("Cannot have both a prg file and code file set when not outputing the result of compilation");
            return 1;
        }

        var prgLoaded = false;
        if (!string.IsNullOrWhiteSpace(options.PrgFilename) && string.IsNullOrWhiteSpace(options.CodeFilename) && !options.WritePrg)
        {
            if (File.Exists(options.PrgFilename))
            {
                Console.WriteLine($"Loading '{options.PrgFilename}'.");
                var prgData = await File.ReadAllBytesAsync(options.PrgFilename);
                int destAddress = (prgData[1] << 8) + prgData[0];
                for (var i = 2; i < prgData.Length; i++)
                {
                    emulator.Memory[destAddress++] = prgData[i];
                }
                prgLoaded = true;
            }
            else
            {
                Console.WriteLine($"PRG '{options.PrgFilename}' not found.");
                return 2;
            }
        }

        if (!string.IsNullOrWhiteSpace(options.CodeFilename))
        {
            if (File.Exists(options.CodeFilename))
            {
                Console.WriteLine($"Compiling '{options.CodeFilename}'.");
                var code = await File.ReadAllTextAsync(options.CodeFilename);
                var compiler = new Compiler(code, new ConsoleLogger());
                try
                {
                    var compileResult = await compiler.Compile();

                    if (compileResult.Warnings.Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Warnings:");
                        foreach (var warning in compileResult.Warnings)
                        {
                            Console.WriteLine(warning);
                        }
                        Console.ResetColor();
                    }

                    var prg = compileResult.Data["Main"].ToArray();
                    var destAddress = 0x801;
                    for (var i = 2; i < prg.Length; i++)
                    {
                        emulator.Memory[destAddress++] = prg[i];
                    }

                    if (options.WritePrg)
                    {
                        Console.WriteLine($"Writing to '{options.PrgFilename}'.");
                        await File.WriteAllBytesAsync(options.PrgFilename, prg);
                    }
                    prgLoaded = true;
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Compile Error:");
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                    return 2;
                }
            }
            else
            {
                Console.WriteLine($"Code file '{options.PrgFilename}' not found.");
                return 2;
            }
        }

        var rom = options.RomFilename;

        if (rom == null || !File.Exists(rom))
        {
            rom = "rom.bin";
        }

        if (!File.Exists(rom))
        {
            var env = Environment.GetEnvironmentVariable(RomEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(env))
            {
                rom = env;

                if (!File.Exists(rom))
                {
                    rom = @$"{env}\rom.bin";
                }
            }
        }

        if (File.Exists(rom))
        {
            Console.WriteLine($"Loading '{rom}'.");
            var romData = await File.ReadAllBytesAsync(rom);
            for (var i = 0; i < romData.Length; i++)
            {
                emulator.RomBank[i] = romData[i];
            }
        }
        else
        {
            Console.WriteLine($"ROM '{rom}' not found.");
            return 2;
        }

        if (!string.IsNullOrWhiteSpace(options.Cartridge))
        {
            Console.Write($"Loading Cartridge '{options.Cartridge}'... ");
            var result = emulator.LoadCartridge(options.Cartridge);
            if (result.Result == CartridgeHelperExtension.LoadCartridgeResultCode.Ok)
                Console.WriteLine("Done.");
            else
            {
                Console.WriteLine("Error.");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(result.Result switch
                {
                    CartridgeHelperExtension.LoadCartridgeResultCode.FileNotFound => "*** File not found.",
                    CartridgeHelperExtension.LoadCartridgeResultCode.FileTooBig => "*** File too big.",
                    _ => "*** Unknown error."
                });
                Console.ResetColor();
            }
        }

        if (options.StartAddress != 0 && prgLoaded)
            emulator.Pc = options.StartAddress;
        else
        {
            emulator.Pc = (ushort)((emulator.RomBank[0x3ffd] << 8) + emulator.RomBank[0x3ffc]);
            if (options.AutoRun)
            {
                emulator.SmcBuffer.KeyDown(Silk.NET.Input.Key.R);
                emulator.SmcBuffer.KeyUp(Silk.NET.Input.Key.R);
                emulator.SmcBuffer.KeyDown(Silk.NET.Input.Key.U);
                emulator.SmcBuffer.KeyUp(Silk.NET.Input.Key.U);
                emulator.SmcBuffer.KeyDown(Silk.NET.Input.Key.N);
                emulator.SmcBuffer.KeyUp(Silk.NET.Input.Key.N);
                emulator.SmcBuffer.KeyDown(Silk.NET.Input.Key.Enter);
                emulator.SmcBuffer.KeyUp(Silk.NET.Input.Key.Enter);
            }
        }

        if (!string.IsNullOrWhiteSpace(options.DumpFile))
        {
            Console.Write($"Loading Dumpfile '{options.DumpFile}'... ");
            if (!File.Exists(options.DumpFile))
            {
                Console.WriteLine("Error.");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Dump file '{options.DumpFile}' not found.");
                Console.ResetColor();
            }
            else
            {
                using var fileStream = new FileStream(options.DumpFile, FileMode.Open, FileAccess.Read);
                var (data, _) = SdCardImageHelper.ReadFile(options.DumpFile, fileStream); // decompresses if necessary
                emulator.Deserialize(data);
                emulator.Control = Control.Run;
                Console.WriteLine("Done.");
            }
        }

        emulator.Control = Control.Paused; // window load start the emulator

        if (options.Warp)
            emulator.FrameControl = FrameControl.Run;
        else
            emulator.FrameControl = FrameControl.Synced;

        emulator.Brk_Causes_Stop = false;

        SdCard sdCard = string.IsNullOrEmpty(options.SdCardFileName) ? new SdCard(options.SdCardSize, new ConsoleLogger()) : new SdCard(options.SdCardFileName , new ConsoleLogger());

        emulator.LoadSdCard(sdCard);

        // set syncing options
        if (options.SdCardFullSync)
        {
            options.SdCardSyncFrom = true;
            options.SdCardSyncTo = true;
        }

        // create the sdcard
        if (!string.IsNullOrWhiteSpace(options.SdCardFolder))
        {
            emulator.SdCard!.SetHomeDirectory(options.SdCardFolder, options.SdCardSyncTo);
        }

        // add files after directories.
        if (options.SdCardFiles != null)
        {
            foreach (var file in options.SdCardFiles)
            {
                sdCard.AddFiles(file, "\\");
            }
        }

        if (options.SdCardUpdate)
        {
            if (!string.IsNullOrEmpty(options.SdCardFileName))
            {
                options.SdCardWrite = options.SdCardFileName;
                options.SdCardOverrwrite = true;
            }
            else
            {
                Console.WriteLine("Cannot set `update source SD Card` when the source SD Card is not set. Use with --sdcard.");
            }
        }

        Thread? syncThread = null;
        if (options.SdCardSyncFrom)
        {
            syncThread = emulator.SdCard!.StartX16Watcher();
            syncThread.Start();
        }

        EmulatorWork.Emulator = emulator;
        EmulatorThread = new Thread(EmulatorWork.DoWork);

        EmulatorWindow.ControlKeyPressed += EmulatorWindow_ControlKeyPressed;

        EmulatorThread.Priority = System.Threading.ThreadPriority.Highest;
        EmulatorThread.Start();

        EmulatorWindow.Run(emulator);

        EmulatorThread.Join();

        if (syncThread != null)
        {
            emulator.SdCard!.StopX16Watcher();
            syncThread.Join();
        }

        Console.WriteLine($"Emulator finished with return '{EmulatorWork.Return}'.");

        // once emulation is over write sdcard if requested
        if (!string.IsNullOrWhiteSpace(options.SdCardWrite))
            emulator.SdCard!.Save(options.SdCardWrite, options.SdCardOverrwrite);

        return 0;
    }

    private static void EmulatorWindow_ControlKeyPressed(object? sender, ControlKeyPressedEventArgs e)
    {
        if (e.Key != Silk.NET.Input.Key.S)
            return;

        RestartOnStop = true;

        Emulator.Stepping = true;
        Emulator.Control = Control.Run;

        ContinueEvent.WaitOne(); // wait for main thread to stop

        Emulator.Stepping = false;

        var toSave = Emulator.Serialize();

        var filename = Path.Combine(
                string.IsNullOrWhiteSpace(Options.DumpFolder) ?
                    System.IO.Directory.GetCurrentDirectory() :
                    Options.DumpFolder,
                $"BitMagic.Dump.{DateTime.Now:yyyymmdd-HHmmss}.json.zip");

        SdCardImageHelper.WriteFile(filename, new MemoryStream(Encoding.UTF8.GetBytes(toSave)));

        Console.WriteLine($"Dump saved to {filename}");

        ContinueEvent.Set(); // tell main thread to continue
    }

    public static class EmulatorWork
    {
        public static Emulator.EmulatorResult Return { get; set; }
        public static Emulator? Emulator { get; set; }

        public static void DoWork()
        {
            if (Emulator == null)
                throw new ArgumentNullException(nameof(Emulator), "Emulator is null");

            var done = false;

            while (!done)
            {
                do
                {
                    Return = Emulator.Emulate();

                    if (RestartOnStop)
                    {
                        ContinueEvent.Set();        // lets the event know we've stopped
                        ContinueEvent.WaitOne();
                    }

                } while (RestartOnStop);


                if (Return != Emulator.EmulatorResult.ExitCondition)
                {
                    Console.WriteLine($"Result: {Return}");
                    var history = Emulator.History;
                    var idx = (int)Emulator.HistoryPosition - 1;
                    if (idx == -1)
                        idx = 1023;

                    var steps = Emulator.Stepping ? 1 : 1000;

                    if (!Emulator.Stepping)
                        Console.WriteLine($"Last {steps} steps:");

                    var toOutput = new List<string>();
                    for (var i = 0; i < steps; i++)
                    {
                        var opCodeDef = OpCodes.GetOpcode(history[idx].OpCode);
                        var opCode = $"{opCodeDef.OpCode.ToLower()} {Addressing.GetModeText(opCodeDef.AddressMode, history[idx].Params, history[idx].PC)}".PadRight(15);

                        toOutput.Add($"Ram:${history[idx].RamBank:X2} Rom:${history[idx].RomBank:X2} ${history[idx].PC:X4} A:${history[idx].A:X2} X:${history[idx].X:X2} Y:${history[idx].Y:X2} SP:${history[idx].SP:X2} {Flags(history[idx].Flags)} - ${history[idx].OpCode:X2}: {opCode}");
                        if (idx <= 0)
                            idx = 1024;
                        idx--;
                    }
                    toOutput.Reverse();
                    foreach (var l in toOutput)
                    {
                        Console.WriteLine(l);
                    }
                }

                //Console.WriteLine($"Ram: ${Emulator.Memory[0x00]:X2} Rom: ${Emulator.Memory[0x01]:X2}");
                //for (var i = 0; i < 512; i += 16)
                //{
                //    Console.ForegroundColor = ConsoleColor.White;
                //    Console.Write($"{i:X4}: ");
                //    for (var j = 0; j < 16; j++)
                //    {
                //        if (Emulator.Memory[i + j] != 0)
                //            Console.ForegroundColor = ConsoleColor.White;
                //        else
                //            Console.ForegroundColor = ConsoleColor.DarkGray;
                //        Console.Write($"{Emulator.Memory[i + j]:X2} ");
                //        if (j == 7)
                //            Console.Write(" ");
                //    }
                //    Console.WriteLine();
                //}

                //DisplayMemory(0x800-1, 0xd00 - 0x800);
                //DisplayMemory(0x9f30, 16);

                //var fsImage = new FsImage(Emulator.RamBank.Slice(0xb40c - 0xa000, 100).ToArray());

                if (Return == Emulator.EmulatorResult.DebugOpCode || Return == Emulator.EmulatorResult.Stepping)
                {
                    Console.WriteLine("(C)ontinue, (S)tep?");
                    var inp = Console.ReadKey(true);
                    switch (inp.Key)
                    {
                        case ConsoleKey.C:
                            done = false;
                            Emulator.Stepping = false;
                            break;
                        case ConsoleKey.S:
                            done = false;
                            Emulator.Stepping = true;
                            break;
                        default:
                            done = true;
                            break;
                    }
                }
                else
                    done = true;

            }


            if (Emulator.Control != Control.Stop && Return != Emulator.EmulatorResult.Stepping)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("*** Close emulator window to exit ***");
                EmulatorWindow.PauseAudio();
            }
            Console.ResetColor();
        }

        public static void DisplayMemory(int start, int length)
        {
            Console.WriteLine();
            for (var i = start; i < start + length; i += 16)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{i:X4}: ");
                for (var j = 0; j < 16; j++)
                {
                    var val = i + j >= 0xa000 ? Emulator.RamBank[i + j - 0xa000] : Emulator.Memory[i + j];
                    if (val != 0)
                        Console.ForegroundColor = ConsoleColor.White;
                    else
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"{val:X2} ");
                    if (j == 7)
                        Console.Write(" ");
                }
                Console.WriteLine();
            }
        }
    }

    public static string Flags(byte flags)
    {
        var sb = new StringBuilder();

        sb.Append("[");

        if ((flags & (byte)CpuFlags.Negative) > 0)
            sb.Append("N");
        else
            sb.Append(" ");

        if ((flags & (byte)CpuFlags.Overflow) > 0)
            sb.Append("V");
        else
            sb.Append(" ");

        sb.Append(" "); // unused
        if ((flags & (byte)CpuFlags.Break) > 0)
            sb.Append("B");
        else
            sb.Append(" ");

        if ((flags & (byte)CpuFlags.Decimal) > 0)
            sb.Append("D");
        else
            sb.Append(" ");

        if ((flags & (byte)CpuFlags.InterruptDisable) > 0)
            sb.Append("I");
        else
            sb.Append(" ");

        if ((flags & (byte)CpuFlags.Zero) > 0)
            sb.Append("Z");
        else
            sb.Append(" ");

        if ((flags & (byte)CpuFlags.Carry) > 0)
            sb.Append("C");
        else
            sb.Append(" ");

        sb.Append("]");

        return sb.ToString();
    }

    [Flags]
    public enum CpuFlags : byte
    {
        None = 0,
        Carry = 1,
        Zero = 2,
        InterruptDisable = 4,
        Decimal = 8,
        Break = 16,
        Unused = 32,
        Overflow = 64,
        Negative = 128
    }
}
