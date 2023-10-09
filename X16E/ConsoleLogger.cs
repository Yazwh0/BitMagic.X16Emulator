using BitMagic.Common;

namespace X16E;

internal class ConsoleLogger : IEmulatorLogger
{
    public void Log(string message)
    {
        Console.Write(message);
    }

    public void LogError(string message)
    {
        Console.WriteLine("ERROR: " + message);
    }

    public void LogError(string message, ISourceFile source, int lineNumber)
    {
        Console.WriteLine("ERROR: " + message);
    }

    public void LogLine(string message)
    {
        Console.WriteLine(message);
    }
}
