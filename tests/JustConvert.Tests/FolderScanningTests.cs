using System.IO;
using JustConvert.Core.Scanning;

namespace JustConvert.Tests;

public class FolderScanningTests : IDisposable
{
    private readonly string _testRoot;

    public FolderScanningTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"jc_scan_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, true);
        }
    }

    [Fact]
    public void Scan_NonRecursive_DetectsOnlyRootMediaFiles()
    {
        File.WriteAllText(Path.Combine(_testRoot, "image1.png"), "");
        File.WriteAllText(Path.Combine(_testRoot, "image2.jpg"), "");
        File.WriteAllText(Path.Combine(_testRoot, "video1.mp4"), "");
        File.WriteAllText(Path.Combine(_testRoot, "audio1.mp3"), "");
        File.WriteAllText(Path.Combine(_testRoot, "document.pdf"), "");
        File.WriteAllText(Path.Combine(_testRoot, "notes.txt"), "");

        var subDir = Path.Combine(_testRoot, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.png"), "");

        var result = FolderScanner.Scan(_testRoot, recursive: false);

        Assert.Equal(4, result.TotalCount);
        Assert.True(result.HasFiles);

        Assert.NotNull(result.Images);
        Assert.Equal(2, result.Images.Count);
        Assert.Contains("png", result.Images.UniqueExtensions);
        Assert.Contains("jpg", result.Images.UniqueExtensions);

        Assert.NotNull(result.Video);
        Assert.Equal(1, result.Video.Count);
        Assert.Contains("mp4", result.Video.UniqueExtensions);

        Assert.NotNull(result.Audio);
        Assert.Equal(1, result.Audio.Count);
        Assert.Contains("mp3", result.Audio.UniqueExtensions);
    }

    [Fact]
    public void Scan_Recursive_FindsNestedMediaAndIgnoresExcludedFolders()
    {
        File.WriteAllText(Path.Combine(_testRoot, "root.png"), "");

        var sub1 = Path.Combine(_testRoot, "folder1");
        Directory.CreateDirectory(sub1);
        File.WriteAllText(Path.Combine(sub1, "photo.webp"), "");

        var sub2 = Path.Combine(sub1, "deep");
        Directory.CreateDirectory(sub2);
        File.WriteAllText(Path.Combine(sub2, "movie.mkv"), "");

        var gitDir = Path.Combine(_testRoot, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "ignored.png"), "");

        var convertedDir = Path.Combine(_testRoot, "Converted");
        Directory.CreateDirectory(convertedDir);
        File.WriteAllText(Path.Combine(convertedDir, "converted_photo.png"), "");

        var result = FolderScanner.Scan(_testRoot, recursive: true);

        Assert.Equal(3, result.TotalCount);
        Assert.DoesNotContain(result.AllFiles, f => f.FullPath.Contains(".git") || f.FullPath.Contains("Converted"));

        var deepFile = result.AllFiles.FirstOrDefault(f => f.Extension == "mkv");
        Assert.NotNull(deepFile);
        Assert.Equal(Path.Combine("folder1", "deep", "movie.mkv"), deepFile.RelativePath);
    }

    [Fact]
    public void Plan_InPlaceDestination_PlacesFilesAlongsideSource()
    {
        var subDir = Path.Combine(_testRoot, "vacation");
        Directory.CreateDirectory(subDir);
        var imgPath = Path.Combine(subDir, "pic.jpg");
        var vidPath = Path.Combine(subDir, "clip.mov");
        File.WriteAllText(imgPath, "");
        File.WriteAllText(vidPath, "");

        var scan = FolderScanner.Scan(_testRoot, recursive: true);

        var plans = new Dictionary<MediaCategory, BatchCategoryPlan>
        {
            [MediaCategory.Image] = new(MediaCategory.Image, true, "png"),
            [MediaCategory.Video] = new(MediaCategory.Video, true, "mp4-h264")
        };

        var items = FolderBatchPlanner.Plan(scan, plans, DestinationMode.InPlace);

        Assert.Equal(2, items.Count);

        var imgItem = items.First(i => i.SourceFilePath == imgPath);
        Assert.Equal("png", imgItem.TargetFormat);
        Assert.Equal(subDir, Path.GetDirectoryName(imgItem.DestinationFilePath));
        Assert.Equal("pic.png", Path.GetFileName(imgItem.DestinationFilePath));

        var vidItem = items.First(i => i.SourceFilePath == vidPath);
        Assert.Equal("mp4-h264", vidItem.TargetFormat);
        Assert.Equal(subDir, Path.GetDirectoryName(vidItem.DestinationFilePath));
        Assert.Equal("clip [H.264 - CQ 23].mp4", Path.GetFileName(vidItem.DestinationFilePath));
    }

    [Fact]
    public void Plan_SubfolderDestination_PreservesFolderHierarchyUnderConverted()
    {
        var subDir = Path.Combine(_testRoot, "holiday", "day1");
        Directory.CreateDirectory(subDir);
        var imgPath = Path.Combine(subDir, "photo.cr2");
        File.WriteAllText(imgPath, "");

        var scan = FolderScanner.Scan(_testRoot, recursive: true);

        var plans = new Dictionary<MediaCategory, BatchCategoryPlan>
        {
            [MediaCategory.Image] = new(MediaCategory.Image, true, "jpg")
        };

        var items = FolderBatchPlanner.Plan(scan, plans, DestinationMode.Subfolder);

        Assert.Single(items);
        var item = items[0];

        var expectedDir = Path.Combine(_testRoot, "Converted", "holiday", "day1");
        Assert.Equal(expectedDir, Path.GetDirectoryName(item.DestinationFilePath));
        Assert.Equal("photo [Q92].jpg", Path.GetFileName(item.DestinationFilePath));
    }

    [Fact]
    public void Plan_DisabledCategory_IsSkipped()
    {
        File.WriteAllText(Path.Combine(_testRoot, "pic.png"), "");
        File.WriteAllText(Path.Combine(_testRoot, "clip.mp4"), "");

        var scan = FolderScanner.Scan(_testRoot, recursive: false);

        var plans = new Dictionary<MediaCategory, BatchCategoryPlan>
        {
            [MediaCategory.Image] = new(MediaCategory.Image, false, "jpg"),
            [MediaCategory.Video] = new(MediaCategory.Video, true, "webm-vp9")
        };

        var items = FolderBatchPlanner.Plan(scan, plans, DestinationMode.InPlace);

        Assert.Single(items);
        Assert.Equal("webm-vp9", items[0].TargetFormat);
    }

    [Fact]
    public void Plan_WhenCategoryHasSameFormatFiles_SkipsSameFormatFiles()
    {
        var img1 = Path.Combine(_testRoot, "file1.png");
        var img2 = Path.Combine(_testRoot, "file2.jpg");
        File.WriteAllText(img1, "");
        File.WriteAllText(img2, "");

        var scan = FolderScanner.Scan(_testRoot, recursive: false);
        var plans = new Dictionary<MediaCategory, BatchCategoryPlan>
        {
            [MediaCategory.Image] = new(MediaCategory.Image, true, "png")
        };

        var items = FolderBatchPlanner.Plan(scan, plans, DestinationMode.InPlace);

        Assert.Single(items);
        Assert.Equal(img2, items[0].SourceFilePath);
        Assert.Equal("png", items[0].TargetFormat);
    }

    [Fact]
    public void Plan_WhenAllFilesAreTargetFormat_ReturnsEmptyList()
    {
        File.WriteAllText(Path.Combine(_testRoot, "file1.png"), "");
        File.WriteAllText(Path.Combine(_testRoot, "file2.png"), "");

        var scan = FolderScanner.Scan(_testRoot, recursive: false);
        var plans = new Dictionary<MediaCategory, BatchCategoryPlan>
        {
            [MediaCategory.Image] = new(MediaCategory.Image, true, "png")
        };

        var items = FolderBatchPlanner.Plan(scan, plans, DestinationMode.InPlace);

        Assert.Empty(items);
    }
}
