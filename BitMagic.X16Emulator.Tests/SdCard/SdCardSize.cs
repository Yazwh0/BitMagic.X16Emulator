using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.SdCard;

// Cards were held in a managed byte[], so they stopped at about 2GB.
[TestClass]
public class SdCardSize
{
    [TestMethod]
    [DataRow(2047ul)]
    [DataRow(2048ul)]
    public void Create(ulong megabytes)
    {
        using var card = new X16Emulator.SdCard(megabytes, new NullLogger());

        Assert.AreEqual(megabytes * 1024 * 1024, card.Size);
    }
}
