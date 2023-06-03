namespace BitMagic.X16Emulator;

public static class CartridgeHelperExtension
{
    public enum LoadCartridgeResultCode
    {
        Ok,
        FileNotFound,
        FileTooBig
    }

    public static LoadCartridgeResult LoadCartridge(this Emulator emulator, string filename)
    {
        if (!File.Exists(filename))
            return new LoadCartridgeResult(LoadCartridgeResultCode.FileNotFound, 0);

        using var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
        var (dataArr, _) = SdCardImageHelper.ReadFile(filename, fileStream); // will uncompress if necessary
        var memoryStream = new MemoryStream();
        dataArr.CopyTo(memoryStream);

        Span<byte> data = memoryStream.ToArray();

        if (data.Length > (256 - 32) * 0x4000)
            return new LoadCartridgeResult(LoadCartridgeResultCode.FileTooBig, 0);

        data.CopyTo(emulator.RomBank[(32 * 0x4000)..]);

        return new LoadCartridgeResult(LoadCartridgeResultCode.Ok, data.Length);
    }

    public record class LoadCartridgeResult(LoadCartridgeResultCode Result, int Size);
}
