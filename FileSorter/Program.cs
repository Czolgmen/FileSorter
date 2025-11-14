namespace FileSorter;

static class Program
{
    static void Main(string[] args)
    {
        var logger = new Logger(Console.OpenStandardOutput(), true);
        //var logger =
            //new Logger(new FileStream(Path.Combine(Directory.GetCurrentDirectory(), "logFile.log"), FileMode.Append),
               // true);
        string filesToSort;
        string sortedFiles;
        if (args.Length < 2)
        {
            filesToSort = Path.Combine(Directory.GetCurrentDirectory(), "FilesToSort"); 
            sortedFiles = Path.Combine(Directory.GetCurrentDirectory(), "SortedFiles");
            logger.Info("Using default values");
            logger.Info($"Unsorted Directory: {filesToSort}");
            logger.Info($"Sorted Directory: {sortedFiles}");
            //Console.WriteLine("If you wish to change these settings stop the program and provide the directories in the command line: ");
            //Console.WriteLine($"{Environment.CommandLine} [Unsorted_directory] [Sorted_directory]");
            //Console.WriteLine("There is no option to provide one without specifying the other");
        }
        else
        { 
            filesToSort = args[0];
            sortedFiles = args[1];
            logger.Info($"Unsorted Directory: {filesToSort}");
            logger.Info($"Sorted Directory: {sortedFiles}");
        }
        
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