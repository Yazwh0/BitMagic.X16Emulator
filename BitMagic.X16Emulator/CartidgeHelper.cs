using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitMagic.X16Emulator
{
    public static class CartidgeHelperExtension
    {
        public enum LoadCartridgeResult
        {
            Ok,
            FileNotFound,
            FileTooBig
        }

        public static LoadCartridgeResult LoadCartridge(this Emulator emulator, string filename)
        {
            if (!File.Exists(filename))
                return LoadCartridgeResult.FileNotFound;

            Span<byte> data = File.ReadAllBytes(filename);

            if (data.Length > (256 - 32) * 0x4000)
                return LoadCartridgeResult.FileTooBig;

            data.CopyTo(emulator.RomBank.Slice(32 * 0x4000));

            return LoadCartridgeResult.Ok;
        }
    }
}
