using BitMagic.DiscUtils.Fat;
//using BitMagic.DiscUtils.Raw;
using BitMagic.DiscUtils.Vhd;
using BitMagic.DiscUtils;
using BitMagic.DiscUtils.Partitions;
using BitMagic.Common;

namespace BitMagic.X16Emulator;

public unsafe class SdCard : IDisposable
{
    private ulong _memoryPtr;
    private MemoryStream _data;
    private ulong _size;
    private ulong _offset;
    private const int Padding = 32;
    private FileSystemWatcher? _watcher;
    public FatFileSystem FileSystem { get; }

    private string? _homeFolder;
    internal bool _watching = false;

    internal object Lock = new object();
    internal List<string> FileUpdates = new List<string>();

    private readonly IEmulatorLogger _logger;

    public SdCard(ulong size, IEmulatorLogger logger)
    {
        _logger = logger;
        _logger.LogLine($"Creating new {size}MB SD Card.");
        InitNewCard(size * 1024 * 1024 + 512); // add VHD header

        if (_data is null)
            throw new Exception("Data is null.");

        var disk = Disk.InitializeFixed(_data, DiscUtils.Streams.Ownership.None, (long)_size - 512);
        BiosPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsFat);

        FileSystem = FatFileSystem.FormatPartition(disk, 0, "BITMAGIC!", true);
    }

    public SdCard(string sdcardFilename, IEmulatorLogger logger)
    {
        _logger = logger;
        _logger.Log($"Loading SD Card from '{sdcardFilename}'");
        using var fileStream = new FileStream(sdcardFilename, FileMode.Open, FileAccess.Read);
        var (data, requiresVhd) = SdCardImageHelper.ReadFile(sdcardFilename, fileStream);

        if (requiresVhd)
        {
            _logger.LogLine(" adding VHD header.");
            InitNewCard((ulong)data.Length + 512); // add VHD header

            if (_data is null)
                throw new Exception("Data is null.");

            var disk = Disk.InitializeFixed(_data, DiscUtils.Streams.Ownership.None, (long)_size - 512);
            BiosPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsFat);

            _data.Position = 0;
            data.CopyTo(_data);

            disk = new Disk(_data, DiscUtils.Streams.Ownership.None);
            FileSystem = new FatFileSystem(disk.Partitions[0].Open(), true);
        }
        else
        {
            _logger.LogLine(".");
            InitNewCard(data);

            if (_data is null)
                throw new Exception("Data is null.");

            var disk = new Disk(_data, DiscUtils.Streams.Ownership.None);
            FileSystem = new FatFileSystem(disk.Partitions[0].Open(), true);
        }
    }


    internal void SetCsdRegister(Emulator emulator)
    {
        ulong reg_0 = 0;
        ulong reg_1 = 0;

        reg_0 |= 1 << 0; // bit 0 is always set
        reg_0 |= 0x09 << 22; // WRITE_BL_LEN - 512 bytes
        reg_0 |= 0x7f << 39; // 64k SECTOR_SIZE
        reg_0 |= 1 << 46; // bit 46 should always be set for ERASE_BLK_EN

        reg_1 |= (((ulong)_data.Length >> 9) - 2) << (69 - 64); // totalsize is (C_SIZE + 1) * 512bytes. So take (size / 512) -2, 1 for VHD header, 1 for conversion
        reg_1 |= 0x09L << (83 - 64); // READ_BL_LEN - 512 bytes
        reg_1 |= 0x2bL << (94 - 64); // TRAN_SPEED

        emulator.Spi_CsdRegister_0 = reg_0;
        emulator.Spi_CsdRegister_1 = reg_1;
    }

    public Thread StartX16Watcher()
    {
        if (string.IsNullOrWhiteSpace(_homeFolder))
            throw new X16FileWatcherButNoHomeFolderException("Cannot watch the X16 SD Card to host without setting a home directory.");

        _watching = true;
        return new Thread(_ => WatchSdCard(this));
    }

    public void StopX16Watcher()
    {
        _watching = false;
    }

    private void WatchSdCard(SdCard card)
    {
        Dictionary<string, long> previousEntries = card.FileSystem.Root.GetFiles().ToDictionary(i => i.Name, i => i.Length);
        List<DiscFileInfo> files = new List<DiscFileInfo>();

        while (card._watching)
        {
            Thread.Sleep(1000);

            card.FileSystem.UpdateCaches();
            var root = card.FileSystem.Root;

            // compare previous with current to see if there are any changes
            lock (card.Lock)
            {
                card.FileUpdates.Clear();
                files.Clear();

                foreach (var file in root.GetFiles())
                {
                    var writeFile = false;
                    if (previousEntries.ContainsKey(file.Name))
                    {
                        var previous = previousEntries[file.Name];

                        if (previous != file.Length) // todo change to a proper check
                        {
                            // amend!
                            _logger.LogLine($"[X16] >> [PC] Amend: '{file.Name}'");
                            writeFile = true;
                        }
                    }
                    else
                    {
                        // new file!
                        _logger.Log($"[X16] >> [PC] New: '{file.Name}'...");
                        writeFile = true;
                    }


                    if (writeFile)
                    {
                        card.FileUpdates.Add(file.Name);
                        var localname = Path.Join(card._homeFolder, file.Name);

                        using var datastream = card.FileSystem.OpenFile(file.Name, FileMode.Open, FileAccess.Read);
                        using var fs = new FileStream(localname, FileMode.OpenOrCreate, FileAccess.Write);

                        datastream.CopyTo(fs);
                        datastream.Close();
                        fs.Close();

                        _logger.LogLine(" Done.");
                    }
                    files.Add(file);
                }
            }

            previousEntries.Clear();
            foreach (var file in files)
                previousEntries.Add(file.Name, file.Length);
        }
    }

    private void InitNewCard(ulong size)
    {
        _size = size;
        var rawMemory = new byte[_size + Padding]; // 32 so we can allign the data correctly
        fixed (byte* ptr = rawMemory)
        {
            _memoryPtr = (ulong)ptr;
            _memoryPtr = (_memoryPtr & ~((ulong)Padding - 1)) + Padding;
            _offset = (_memoryPtr - (ulong)ptr);
        }
        _data = new MemoryStream(rawMemory, (int)_offset, (int)_size, true);
    }

    private void InitNewCard(Stream stream)
    {
        _size = (ulong)stream.Length;
        var rawMemory = new byte[_size + Padding]; // 32 so we can allign the data correctly
        fixed (byte* ptr = rawMemory)
        {
            _memoryPtr = (ulong)ptr;
            _memoryPtr = (_memoryPtr & ~((ulong)Padding - 1)) + Padding;
            _offset = (_memoryPtr - (ulong)ptr);
        }

        stream.Read(rawMemory, (int)_offset, (int)_size);

        _data = new MemoryStream(rawMemory, (int)_offset, (int)_size, true);
    }

    public ulong Size => _size - 512; // take off VHD header

    public ulong MemoryPtr => _memoryPtr;

    public unsafe Span<byte> Image => new Span<byte>((void*)_memoryPtr, (int)Size);

    public void Dispose()
    {
        _watcher?.Dispose();
        _data?.Dispose();
        FileSystem?.Dispose();
    }

    // Copies a directory and starts a watcher
    public void SetHomeDirectory(string directory, bool hostFsToSdCardSync, bool allowOverwrite)
    {
        _logger.LogLine($"Setting home directory to '{directory}'.");
        _homeFolder = directory;

        AddDirectoryFiles(directory, "\\", allowOverwrite);

        if (hostFsToSdCardSync)
        {
            _watcher = new FileSystemWatcher(directory, "*.*");
            _watcher.Changed += _watcher_Changed;
            _watcher.Deleted += _watcher_Deleted;
            _watcher.Created += _watcher_Created;
            _watcher.Renamed += _watcher_Renamed;
            _watcher.Error += _watcher_Error;
            _watcher.EnableRaisingEvents = true;
        }
    }

    private void _watcher_Error(object sender, ErrorEventArgs e)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        _logger.LogLine(e.ToString());
        Console.ResetColor();
    }

    private void _watcher_Renamed(object _, RenamedEventArgs e)
    {
        DeleteFile(e.OldFullPath);
        AddFile(e.FullPath, "\\", true);
    }

    private void _watcher_Created(object _, FileSystemEventArgs e) => AddFile(e.FullPath, "\\", true);

    private void _watcher_Deleted(object _, FileSystemEventArgs e) => DeleteFile(e.FullPath);

    private void _watcher_Changed(object _, FileSystemEventArgs e) => AddFile(e.FullPath, "\\", true);

    public void AddDirectory(string directory, string destFolder, bool allowOverwrite)
    {
        _logger.LogLine($"Adding files from '{directory}' to '{destFolder}':");
        AddDirectoryFiles(directory, destFolder, allowOverwrite);
    }

    private void AddDirectoryFiles(string directory, string destFolder, bool allowOverwrite)
    {
        string wildcard = "*.*";
        if (!System.IO.Directory.Exists(directory)) { 
            wildcard = Path.GetFileName(directory);
            directory = Path.GetDirectoryName(directory) ?? "";
        }

        foreach (var filename in System.IO.Directory.GetFiles(directory, wildcard))
        {
            AddFile(filename, destFolder, allowOverwrite);
        }
    }

    public void AddFiles(string filenames, string destFolder, bool allowOverwrite)
    {
        var searchName = Path.GetFileName(filenames);
        var path = Path.GetDirectoryName(filenames) ?? throw new Exception("No path!");
        var entries = System.IO.Directory.GetFiles(path, searchName);

        foreach (var filename in entries)
        {
            AddFile(filename, destFolder, allowOverwrite);
        }
    }

    public void AddCompiledFile(string filename, byte[] data)
    {
        lock (Lock)
        {
            var actName = GetFilename(filename);

            var idx = filename.IndexOf('/');
            if (idx == -1)
                idx = filename.IndexOf('\\');

            var destFolder = "";

            if (idx != -1)
            {
                destFolder = filename[..idx];
                filename = filename[(idx+1)..];

                EnsureDirectoryExists(destFolder);

                actName = $"{destFolder}\\{actName}";
            }

            _logger.LogLine($"[PC] >> [16] Creating : {actName}");

            using var file = FileSystem.OpenFile(actName, FileMode.CreateNew, FileAccess.Write);
            file.Write(data);

            file.Close();
        }
    }

    private void AddFile(string filename, string destFolder, bool allowOverwrite)
    {
        lock (Lock)
        {
            var actName = GetFilename(filename);

            if (!destFolder.EndsWith('\\'))
                actName = $"{destFolder}\\{actName}";
            else
                actName = $"{destFolder}{actName}";

            if (FileUpdates.Contains(actName))
            {
                _logger.LogLine($"[PC] >> [16] Skipping : {actName}");
                return;
            }

            _logger.Log($"[PC] >> [16] Adding: '{filename}'");
            byte[] source;
            try
            {
                source = File.ReadAllBytes(filename);
            }
            catch (IOException e)
            {
                _logger.LogLine(" Error.");
                _logger.LogLine($"Error opening file, ({e.Message}) trying again in 1s.");
                Thread.Sleep(1000);

                _logger.Log($"[PC] >> [16] Adding: '{filename}'");
                source = File.ReadAllBytes(filename);
            }

            _logger.Log($" -> '{actName}'...");

            EnsureDirectoryExists(destFolder);

            if (FileSystem.FileExists(actName))
            {
                if (allowOverwrite)
                    FileSystem.DeleteFile(actName);
                else
                {
                    _logger.LogLine("Alread Exists.");
                    return;
                }
            }
            using var file = FileSystem.OpenFile(actName, FileMode.CreateNew, FileAccess.Write);
            file.Write(source);

            file.Close();

            FileSystem.UpdateFsInfoFreeSpace();

            _logger.LogLine(" Done.");
        }
    }

    private void EnsureDirectoryExists(string folder)
    {
        var path = folder.Split('\\');

        var toCreate = "";

        for (var i = 0; i < path.Length; i++)
        {
            toCreate += '\\' + path[0];

            if (!FileSystem.DirectoryExists(toCreate))
                FileSystem.CreateDirectory(toCreate);
        }
    }

    private void DeleteFile(string filename)
    {
        lock (Lock)
        {
            var actName = GetFilename(filename);
            if (FileUpdates.Contains(actName))
            {
                _logger.LogLine($"[PC] >> [16] Skipping : {actName}");
                return;
            }

            _logger.Log($"[PC] >> [16] Deleteing: '{filename}' -> '{actName}'... ");

            if (FileSystem.FileExists(actName))
            {
                FileSystem.DeleteFile(actName);
                FileSystem.UpdateFsInfoFreeSpace();

                _logger.LogLine(" Done.");
            }
            else
            {
                _logger.LogLine(" Does not exist.");
            }
        }
    }

    public void Save(string filename, bool canOverwrite)
    {
        if (!File.Exists(filename) || canOverwrite)
        {
            _logger.Log($"Writing '{filename}'...");
            SdCardImageHelper.WriteFile(filename, _data);
            _logger.LogLine(" Done.");
            return;
        }
        Console.ForegroundColor = ConsoleColor.Red;
        _logger.LogLine($"SD Card image already exists '{filename}'. Nothing saved.");
        Console.ResetColor();
    }

    private static string GetFilename(string filename)
    {
        return Path.GetFileName(filename.ToUpper());
        //filename = filename.ToUpper().Replace(" ", "");
        //return Path.GetFileName(filename);
        //var ext = Path.GetExtension(filename);
        //ext = ext[..Math.Min(4, ext.Length)];
        //var rawname = Path.GetFileNameWithoutExtension(filename);
        //return rawname + ext;

        //return rawname[..Math.Min(8, rawname.Length)] + ext;
    }
}

public class UnhandledFileSysetmChangeException : Exception
{
    public UnhandledFileSysetmChangeException(string message) : base(message) { }
}

public class CantSyncLoadedImageException : Exception
{
    public CantSyncLoadedImageException(string message) : base(message) { }
}

public class X16FileWatcherButNoHomeFolderException : Exception
{
    public X16FileWatcherButNoHomeFolderException(string message) : base(message) { }
}