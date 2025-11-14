using System.IO.Pipes;
using System.Text.Json;

namespace FileSorter;

static class Program
{
    static void Main(string[] args)
    {
        ConfigLoader? configLoader;
        if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "config.json")))
        {
            Console.WriteLine("Using Default Config!");
            configLoader = new ConfigLoader();
        }
        else
        {
            Console.WriteLine("Loading Config From File!");
            var text = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "config.json"));
            var options = new JsonSerializerOptions()
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            };
            configLoader = JsonSerializer.Deserialize<ConfigLoader>(text, options);
        }

        if (configLoader == null)
        {
            throw new ArgumentNullException(nameof(configLoader), "is null");
        }

        Logger logger;
        if (configLoader.Type == ConfigLoader.TypeOfLogging.Stdout)
        {
            logger = new Logger(Console.OpenStandardOutput(), configLoader.WriteDebug);
        } else if (configLoader.Type == ConfigLoader.TypeOfLogging.Stderr)
        {
            logger = new Logger(Console.OpenStandardError(), configLoader.WriteDebug);
        }
        else
        {
            logger = new Logger(
                new FileStream(Path.Combine(Directory.GetCurrentDirectory(), configLoader.LogFileName!),
                    FileMode.Append), configLoader.WriteDebug);
        }
        logger.Info("Logger has been initialized");
        if (configLoader.WriteDebug == true)
        {
            logger.Debug("Debug messages will be shown!");
        }
        string filesToSort = Path.Combine(Directory.GetCurrentDirectory(), configLoader.UnsortedDirectory);
        string sortedFiles = Path.Combine(Directory.GetCurrentDirectory(), configLoader.OrderedDirectory);
        logger.Info($"UnsortedDirectory: {filesToSort}");
        logger.Info($"OrderedDirectory: {sortedFiles}");
        
        if (!Directory.Exists(filesToSort))
        {
            logger.Error($"Directory {filesToSort} doesn't exist!", new ArgumentException("Directory doesn't exist"));
        }
        var watcher = new FileSystemWatcher(filesToSort)
        {
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size
        };
        
        if (!Directory.Exists(sortedFiles))
        {
            Directory.CreateDirectory(sortedFiles);
        }

        var sorter = new FileSort(sortedFiles, logger);

        watcher.Created += sorter.HandleFileCreated;

        Console.WriteLine("Watching for new files. Press Ctrl+C to exit.");

        var quitEvent = new ManualResetEventSlim(false);

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            logger.Warn("Ctrl+C detected, shutting down...");
            quitEvent.Set();
        };

        quitEvent.Wait();
        watcher.Dispose();
        Console.WriteLine("Exiting program, goodbye!");
    }
}