namespace FileSorter;


public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception ex);
    void Debug(string message);
}


public class Logger(Stream consoleStream, bool writeDebug = false) : ILogger
{
    private readonly StreamWriter _writer = new(consoleStream)
    {
        AutoFlush = true
    };

    public void Info(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
         _writer.WriteLine($"({DateTime.Now.ToLocalTime()}) [LOG]: {message}");
        Console.ResetColor();
    }

    public void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        _writer.WriteLine($"({DateTime.Now.ToLocalTime()}) [WARN]: {message}");
        Console.ResetColor();
    }

    public void Error(string message, Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        _writer.WriteLine($"({DateTime.Now.ToLocalTime()}) [ERROR]: {ex.Message}");
        _writer.WriteLine("Terminating!");
        Console.ResetColor();
        throw ex;
    }

    public void Debug(string message)
    {
        if (!writeDebug) return;
        Console.ForegroundColor = ConsoleColor.Green;
        _writer.WriteLine($"({DateTime.Now.ToLocalTime()}) [DEBUG]: {message}");
        Console.ResetColor();
    }
}