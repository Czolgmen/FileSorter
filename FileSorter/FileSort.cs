using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.JavaScript;
using System.Security.AccessControl;
using System.Text;

namespace FileSorter;

public class FileSort
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".tiff", ".bmp", ".heic", ".svg"];
    private static readonly string[] RawImageExtensions = [".cr2", ".nef", ".arw", ".rw2", ".orf", ".dng"];
    private static readonly string[] VectorGraphicsExtensions = [".svg", ".ai", ".eps", ".pdf"];

    private static readonly string[] AudioExtensions = [".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma", ".opus"];
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".flv", ".m4v"];
    private static readonly string[] SubtitleExtensions = [".srt", ".sub", ".ass", ".vtt"];

    private static readonly string[] TextDocumentExtensions = [".txt", ".rtf", ".doc", ".docx", ".odt", ".md", ".csv", ".tex"];
    private static readonly string[] SpreadsheetExtensions = [".xlsx", ".xls", ".ods", ".csv", ".tsv", ".xlsm"];
    private static readonly string[] PresentationExtensions = [".pptx", ".ppt", ".odp", ".key"];
    private static readonly string[] PdfAndEbookExtensions = [".pdf", ".epub", ".mobi", ".azw3"];
    
    private static readonly string[] ExecutableWindowsExtensions = [".exe", ".msi", ".bat", ".cmd", ".com", ".ps1"];
    private static readonly string[] ExecutableUnixScriptExtensions = [".sh", ".bash", ".zsh", ".fish", ".run", ".bin"];

    private readonly string _sortedFilesPath;
    private readonly ILogger _logger;
    
    public FileSort(string sortedFilesPath, ILogger logger)
    {
        _sortedFilesPath = sortedFilesPath;
        _logger = logger;
        
        _extensionToCategory = new(StringComparer.OrdinalIgnoreCase);

        foreach (var ext in ImageExtensions)
            _extensionToCategory[ext] = FileCategory.ImageBitmap;

        foreach (var ext in RawImageExtensions)
            _extensionToCategory[ext] = FileCategory.ImageRaw;

        foreach (var ext in VectorGraphicsExtensions)
            _extensionToCategory[ext] = FileCategory.ImageVector;

        foreach (var ext in AudioExtensions)
            _extensionToCategory[ext] = FileCategory.Audio;

        foreach (var ext in VideoExtensions)
            _extensionToCategory[ext] = FileCategory.Video;

        foreach (var ext in SubtitleExtensions)
            _extensionToCategory[ext] = FileCategory.Subtitle;

        foreach (var ext in TextDocumentExtensions)
            _extensionToCategory[ext] = FileCategory.TextDocument;

        foreach (var ext in SpreadsheetExtensions)
            _extensionToCategory[ext] = FileCategory.Spreadsheet;

        foreach (var ext in PresentationExtensions)
            _extensionToCategory[ext] = FileCategory.Presentation;

        foreach (var ext in PdfAndEbookExtensions)
            _extensionToCategory[ext] = FileCategory.PdfOrEbook;

        foreach (var ext in ExecutableWindowsExtensions)
            _extensionToCategory[ext] = FileCategory.WindowsExecutable;

        foreach (var ext in ExecutableUnixScriptExtensions)
            _extensionToCategory[ext] = FileCategory.UnixScript;
    }

    private enum FileCategory
    {
        ImageBitmap,
        ImageRaw,
        ImageVector,
        Audio,
        Video,
        Subtitle,
        TextDocument,
        Spreadsheet,
        Presentation,
        PdfOrEbook,
        WindowsExecutable,
        UnixScript,
        Unknown
    }

    private static readonly IReadOnlyDictionary<FileCategory, string> CategoryToFolder =
        new Dictionary<FileCategory, string>
        {
            [FileCategory.ImageBitmap]      = Path.Combine("Images", "Bitmap"),
            [FileCategory.ImageRaw]         = Path.Combine("Images", "RawImages"),
            [FileCategory.ImageVector]      = Path.Combine("Images", "Vector"),
            [FileCategory.Audio]            = "Audio",
            [FileCategory.Video]            = "Videos",
            [FileCategory.TextDocument]     = "TextDocuments",
            [FileCategory.Spreadsheet]      = "Spreadsheets",
            [FileCategory.Presentation]     = "Presentations",
            [FileCategory.PdfOrEbook]       = "PdfAndEbooks",
            [FileCategory.WindowsExecutable]= "WindowsExecutables",
            [FileCategory.UnixScript]       = "UnixScripts",
            // Unknown handled specially
        };


    private static Dictionary<string, FileCategory>? _extensionToCategory;
    
    private static FileCategory GetCategory(string fileNameOrExt)
    {
        var ext = fileNameOrExt.StartsWith('.')
            ? fileNameOrExt
            : Path.GetExtension(fileNameOrExt);

        if (string.IsNullOrEmpty(ext))
            return FileCategory.Unknown;

        return _extensionToCategory!.GetValueOrDefault(ext, FileCategory.Unknown);
    }
    
    private string GetBaseTargetDir(FileCategory category, string sourcePath)
    {
        // Special case: Unknown -> group by extension / no extension
        if (category == FileCategory.Unknown)
        {
            var unknownRoot = Path.Combine(_sortedFilesPath, "Unknown");
            EnsureDirectory(unknownRoot);

            var extension = Path.GetExtension(sourcePath);
            var sub = string.IsNullOrEmpty(extension) ? "NoExtension" : extension;

            var fullUnknownPath = Path.Combine(unknownRoot, sub);
            EnsureDirectory(fullUnknownPath);
            return fullUnknownPath;
        }

        if (!CategoryToFolder.TryGetValue(category, out var relativeFolder))
        {
            // fallback – should basically never happen
            var fallback = Path.Combine(_sortedFilesPath, "Unknown");
            EnsureDirectory(fallback);
            return fallback;
        }

        var fullPath = Path.Combine(_sortedFilesPath, relativeFolder);
        EnsureDirectory(fullPath);
        return fullPath;
    }

    private void EnsureDirectory(string fullPath)
    {
        if (!Directory.Exists(fullPath))
        {
            _logger.Debug($"Creating directory: {fullPath}");
            Directory.CreateDirectory(fullPath);
        }
    }
    
    public void HandleFileCreated(object sender, FileSystemEventArgs args)
    {
        var pathOfFileCreated = args.FullPath;
        var nameOfFileWithExtension = args.Name;

        if (nameOfFileWithExtension is null)
        {
            _logger.Warn("FileSystemEventArgs.Name was null, skipping event");
            return;
        }
        
        try
        {
            // 🔹 Wait until copy/save finishes
            WaitForFileReady(pathOfFileCreated);
        }
        catch (Exception ex)
        {
            _logger.Warn($"File {pathOfFileCreated} is not ready after timeout, skipping. {ex.Message}");
            return;
        }

        var fileExtension = Path.GetExtension(pathOfFileCreated);
        var dateCreated = File.GetCreationTime(pathOfFileCreated);
        var category = GetCategory(fileExtension);

        try
        {
            MoveToDatedSubfolder(
                pathOfFileCreated,
                nameOfFileWithExtension,
                category,
                dateCreated);
        }
        catch (Exception ex)
        {
            _logger.Error(ex.Message, ex);
        }
    }
    
    private static void WaitForFileReady(string path, int timeoutMs = 10000, int pollDelayMs = 200)
    {
        var sw = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                // Try to open with exclusive access.
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);

                // If we got here, file is accessible and not locked exclusively.
                if (stream.Length > 0)
                    return;

                // If length == 0, might still be in progress, keep looping.
            }
            catch (IOException)
            {
                if (sw.ElapsedMilliseconds >= timeoutMs)
                    throw; // give up after timeout

                Thread.Sleep(pollDelayMs);
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                if (sw.ElapsedMilliseconds >= timeoutMs)
                    throw;

                Thread.Sleep(pollDelayMs);
                continue;
            }

            if (sw.ElapsedMilliseconds >= timeoutMs)
                return;
        }
    }
    
    private void MoveToDatedSubfolder(
        string sourcePath,
        string fileName,
        FileCategory category,
        DateTime created)
    {
        var baseTargetDir = GetBaseTargetDir(category, sourcePath);

        var yearDir  = Path.Combine(baseTargetDir, created.Year.ToString());
        var monthDir = Path.Combine(yearDir, created.Month.ToString("D2"));

        EnsureDirectory(yearDir);
        EnsureDirectory(monthDir);

        var targetPath = Path.Combine(monthDir, fileName);
        
        var i = 1;
        var checkPath = Path.Combine(monthDir, fileName);
        while (File.Exists(checkPath))
        {
            _logger.Debug($"File {checkPath} already exists! Trying to add suffix: {i}");
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);
            var fileNameWithSuffix = String.Concat(fileNameWithoutExtension, $" ({i})", Path.GetExtension(targetPath));
            checkPath = Path.Combine(monthDir, fileNameWithSuffix);
            i++;
        }
        _logger.Info($"Moving {sourcePath} -> {checkPath}");
        File.Move(sourcePath, checkPath, overwrite: false);
    }
}