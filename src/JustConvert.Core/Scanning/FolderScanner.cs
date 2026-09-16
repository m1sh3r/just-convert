using System.IO;

namespace JustConvert.Core.Scanning;

public enum MediaCategory
{
    Image,
    Video,
    Audio
}

public sealed record ScannedFile(
    string FullPath,
    string RelativePath,
    string Extension,
    MediaCategory Category);

public sealed record CategoryScanResult(
    MediaCategory Category,
    IReadOnlyList<ScannedFile> Files,
    IReadOnlyList<string> UniqueExtensions)
{
    public int Count => Files.Count;
}

public sealed record FolderScanResult(
    string RootDirectory,
    bool Recursive,
    IReadOnlyList<ScannedFile> AllFiles,
    CategoryScanResult? Images,
    CategoryScanResult? Video,
    CategoryScanResult? Audio)
{
    public int TotalCount => AllFiles.Count;
    public bool HasFiles => AllFiles.Count > 0;
}

public static class FolderScanner
{
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "converted", "bin", "obj", "node_modules", "$recycle.bin", "system volume information"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus", "aiff", "aif", "m4b", "alac", "ape", "wv"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "webp", "ico", "bmp", "gif", "jp2", "jpeg2000", "tiff", "tif", "tga", "pcx", "ppm", "avif", "heic",
        "svg", "psd", "dng", "cr2", "cr3", "nef", "arw"
    };

    public static FolderScanResult Scan(string rootDirectory, bool recursive = false)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return new FolderScanResult(rootDirectory, recursive, [], null, null, null);
        }

        var files = new List<ScannedFile>();
        ScanInternal(rootDirectory, rootDirectory, recursive, files);

        var imageFiles = files.Where(f => f.Category == MediaCategory.Image).ToList();
        var videoFiles = files.Where(f => f.Category == MediaCategory.Video).ToList();
        var audioFiles = files.Where(f => f.Category == MediaCategory.Audio).ToList();

        var images = imageFiles.Count > 0
            ? new CategoryScanResult(MediaCategory.Image, imageFiles, imageFiles.Select(f => f.Extension).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList())
            : null;

        var video = videoFiles.Count > 0
            ? new CategoryScanResult(MediaCategory.Video, videoFiles, videoFiles.Select(f => f.Extension).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList())
            : null;

        var audio = audioFiles.Count > 0
            ? new CategoryScanResult(MediaCategory.Audio, audioFiles, audioFiles.Select(f => f.Extension).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList())
            : null;

        return new FolderScanResult(rootDirectory, recursive, files, images, video, audio);
    }

    private static void ScanInternal(string rootDirectory, string currentDirectory, bool recursive, List<ScannedFile> destination)
    {
        try
        {
            var dirInfo = new DirectoryInfo(currentDirectory);
            if ((dirInfo.Attributes & FileAttributes.Hidden) != 0 || (dirInfo.Attributes & FileAttributes.System) != 0)
            {
                if (!string.Equals(rootDirectory, currentDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            foreach (var file in Directory.EnumerateFiles(currentDirectory))
            {
                var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) continue;

                MediaCategory? category = null;
                if (VideoExtensions.Contains(ext)) category = MediaCategory.Video;
                else if (AudioExtensions.Contains(ext)) category = MediaCategory.Audio;
                else if (ImageExtensions.Contains(ext)) category = MediaCategory.Image;

                if (category != null)
                {
                    var relative = Path.GetRelativePath(rootDirectory, file);
                    destination.Add(new ScannedFile(file, relative, ext, category.Value));
                }
            }

            if (recursive)
            {
                foreach (var subDir in Directory.EnumerateDirectories(currentDirectory))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (IgnoredDirectoryNames.Contains(dirName)) continue;

                    ScanInternal(rootDirectory, subDir, recursive, destination);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
    }
}
