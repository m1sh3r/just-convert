using System.IO;
using JustConvert.Core.Converters;
using JustConvert.Core.Windows;

namespace JustConvert.Core.Scanning;

public enum DestinationMode
{
    InPlace,
    Subfolder,
    CustomFolder
}

public sealed record BatchCategoryPlan(
    MediaCategory Category,
    bool Enabled,
    string TargetFormat);

public sealed record BatchConversionItem(
    string SourceFilePath,
    string TargetFormat,
    string DestinationFilePath);

public static class FolderBatchPlanner
{
    public static (bool IsValid, string? ErrorMessage) ValidateDestinationDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, I18n.T("ErrorFolderNotSpecified"));
        }

        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var probeFile = Path.Combine(path, $".probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, "ok");
            File.Delete(probeFile);

            return (true, null);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, I18n.T("ErrorFolderAccessDenied", path));
        }
        catch (PathTooLongException)
        {
            return (false, I18n.T("ErrorPathTooLong", path));
        }
        catch (Exception ex)
        {
            return (false, string.Format(I18n.T("ErrorFolderInvalid"), ex.Message));
        }
    }

    public static IReadOnlyList<BatchConversionItem> Plan(
        FolderScanResult scanResult,
        IReadOnlyDictionary<MediaCategory, BatchCategoryPlan> categoryPlans,
        DestinationMode destinationMode,
        string? customOutputDirectory = null)
    {
        var items = new List<BatchConversionItem>();

        foreach (var (category, plan) in categoryPlans)
        {
            if (!plan.Enabled || string.IsNullOrWhiteSpace(plan.TargetFormat))
            {
                continue;
            }

            var categoryResult = category switch
            {
                MediaCategory.Image => scanResult.Images,
                MediaCategory.Video => scanResult.Video,
                MediaCategory.Audio => scanResult.Audio,
                _ => null
            };

            if (categoryResult == null || categoryResult.Files.Count == 0)
            {
                continue;
            }

            foreach (var file in categoryResult.Files)
            {
                if (ClassicContextMenuManager.IsSameFormat(file.Extension, plan.TargetFormat))
                {
                    continue;
                }

                var targetDir = destinationMode switch
                {
                    DestinationMode.InPlace => Path.GetDirectoryName(file.FullPath) ?? scanResult.RootDirectory,
                    DestinationMode.CustomFolder => GetCustomDestination(customOutputDirectory ?? scanResult.RootDirectory, file.RelativePath),
                    _ => GetSubfolderDestination(scanResult.RootDirectory, file.RelativePath)
                };

                var (suffix, ext, isDir) = DetermineSuffixAndExtension(category, file.Extension, plan.TargetFormat);
                var baseName = Path.GetFileNameWithoutExtension(file.FullPath);
                var destinationPath = OutputFileNameHelper.GetUniquePath(targetDir, baseName, suffix, ext, isDir);

                items.Add(new BatchConversionItem(file.FullPath, plan.TargetFormat, destinationPath));
            }
        }

        return items;
    }

    private static string GetCustomDestination(string customRoot, string relativeFilePath)
    {
        var relativeDir = Path.GetDirectoryName(relativeFilePath);
        return string.IsNullOrEmpty(relativeDir)
            ? customRoot
            : Path.Combine(customRoot, relativeDir);
    }

    private static string GetSubfolderDestination(string rootDirectory, string relativeFilePath)
    {
        var relativeDir = Path.GetDirectoryName(relativeFilePath);
        var convertedRoot = Path.Combine(rootDirectory, "Converted");

        return string.IsNullOrEmpty(relativeDir)
            ? convertedRoot
            : Path.Combine(convertedRoot, relativeDir);
    }

    private static (string? Suffix, string Extension, bool IsDirectory) DetermineSuffixAndExtension(
        MediaCategory category,
        string sourceExt,
        string targetFormat)
    {
        var fmt = targetFormat.TrimStart('.').ToLowerInvariant();

        switch (category)
        {
            case MediaCategory.Image:
                {
                    var suffix = OutputFileNameHelper.BuildImageSuffix(fmt);
                    var ext = fmt switch
                    {
                        "jpeg" => ".jpg",
                        "jpeg2000" => ".jp2",
                        "tif" => ".tiff",
                        "reencode" => $".{sourceExt}",
                        _ => $".{fmt}"
                    };
                    return (suffix, ext, false);
                }

            case MediaCategory.Audio:
                {
                    var suffix = OutputFileNameHelper.BuildAudioSuffix(fmt);
                    var ext = fmt == "reencode" ? $".{sourceExt}" : $".{fmt}";
                    return (suffix, ext, false);
                }

            case MediaCategory.Video:
                {
                    var suffix = OutputFileNameHelper.BuildVideoSuffix(fmt);
                    if (fmt is "frames" or "frames-png" or "frames-jpg")
                    {
                        return (suffix, "", true);
                    }

                    if (IsAudioTarget(fmt))
                    {
                        return (suffix, $".{fmt}", false);
                    }

                    if (fmt == "remux")
                    {
                        var remuxSetting = AppSettings.Load().GetEffectiveRemuxSetting();
                        return (suffix, $".{remuxSetting.TargetContainer.TrimStart('.')}", false);
                    }

                    if (fmt.StartsWith("remux-"))
                    {
                        return (suffix, $".{fmt[6..]}", false);
                    }

                    if (fmt is "gif")
                    {
                        return (suffix, ".gif", false);
                    }

                    if (fmt.StartsWith("mov") || fmt.StartsWith("prores"))
                    {
                        return (suffix, ".mov", false);
                    }

                    if (fmt.StartsWith("webm") || fmt is "vp9")
                    {
                        return (suffix, ".webm", false);
                    }

                    if (fmt.StartsWith("mkv"))
                    {
                        return (suffix, ".mkv", false);
                    }

                    return (suffix, ".mp4", false);
                }

            default:
                return (fmt.ToUpperInvariant(), $".{fmt}", false);
        }
    }

    private static bool IsAudioTarget(string format) => format switch
    {
        "mp3" or "wav" or "flac" or "aac" or "ogg" or "m4a" or "opus" or "aiff" => true,
        _ => false
    };
}
