namespace FileSorter;

public class ConfigLoader
{
    public string UnsortedDirectory { get; private set; }
    public string OrderedDirectory { get; private set; }
    
    public bool WriteDebug { get; private set; }

    public string? LogFileName { get; private set; }
    public TypeOfLogging Type { get; private set; }
    public enum TypeOfLogging
    {
        Stdout,
        Stderr,
        File
    }

    public ConfigLoader(string unsortedDirectory, string orderedDirectory, TypeOfLogging typeOfLogging, string? logFileName, bool writeDebug)
    {
        UnsortedDirectory = unsortedDirectory;
        OrderedDirectory = orderedDirectory;
        Type = typeOfLogging;
        if (Type == TypeOfLogging.File && logFileName == null)
        {
            LogFileName = "Sorting.log";
        }
        else
        {
            LogFileName = logFileName;
        }
        WriteDebug = writeDebug;
    }

    public ConfigLoader()
    {
        UnsortedDirectory = "FilesToSort";
        OrderedDirectory = "SortedFiles";
        Type = TypeOfLogging.Stdout;
        LogFileName = null;
        WriteDebug = true;
    }
}