using System.Windows;
using JustConvert.Cli.UI;
using JustConvert.Core;
using JustConvert.Core.Scanning;

namespace JustConvert.Tests;

public class UiConversionProgressWindowTests
{
    [Fact]
    public void Construct_SingleFile_InitializesQueueAndControls()
    {
        StaTestRunner.Run(() =>
        {
            var window = new ConversionProgressWindow("input.wav", "mp3");

            Assert.Single(window.Items);
            Assert.Equal("input.wav", window.Items[0].InputPath);
            Assert.Equal("mp3", window.Items[0].TargetFormat);
            Assert.Equal("1", window.TxtParallelValue.Text);
            Assert.False(window.BtnParallelDec.IsEnabled);
            Assert.True(window.BtnParallelInc.IsEnabled);
        });
    }

    [Fact]
    public void Construct_BatchItems_InitializesMultipleItems()
    {
        StaTestRunner.Run(() =>
        {
            var window = new ConversionProgressWindow(["a.mp4", "b.mov"], "mkv", null, true);

            Assert.Equal(2, window.Items.Count);
            Assert.Equal("0 / 2", window.TxtOverallCount.Text);
        });
    }

    [Fact]
    public void Construct_FolderBatchItems_EnqueuesBatchItems()
    {
        StaTestRunner.Run(() =>
        {
            var batch = new[]
            {
                new BatchConversionItem("image1.png", "image1.webp", "webp"),
                new BatchConversionItem("image2.jpg", "image2.webp", "webp")
            };

            var window = new ConversionProgressWindow(batch);

            Assert.Equal(2, window.Items.Count);
            Assert.Equal("image1.png", window.Items[0].InputPath);
            Assert.Equal("image2.jpg", window.Items[1].InputPath);
        });
    }

    [Fact]
    public void CreateForError_ShowsDirectErrorOverlayAndExitCode()
    {
        StaTestRunner.Run(() =>
        {
            var window = ConversionProgressWindow.CreateForError("bad.xyz", "mp4", "Unsupported format", "ffmpeg error log");

            Assert.Equal(Visibility.Visible, window.ErrorDetailOverlay.Visibility);
            Assert.Equal("Unsupported format", window.InfoBarError.Message);
            Assert.Contains("ffmpeg error log", window.TxtErrorLog.Text);
            Assert.Equal(1, window.ExitCode);
        });
    }

    [Fact]
    public void ParallelButtons_IncrementAndDecrement_RespectLimits()
    {
        StaTestRunner.Run(() =>
        {
            var window = new ConversionProgressWindow("test.mp4", "mp3");

            window.BtnParallelInc.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal("2", window.TxtParallelValue.Text);
            Assert.True(window.BtnParallelDec.IsEnabled);

            window.BtnParallelDec.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal("1", window.TxtParallelValue.Text);
            Assert.False(window.BtnParallelDec.IsEnabled);
        });
    }

    [Fact]
    public void EnqueueFiles_AddsItemsDynamically()
    {
        StaTestRunner.Run(() =>
        {
            var window = new ConversionProgressWindow("file1.mp4", "mkv");
            Assert.Single(window.Items);

            window.EnqueueFiles(["file2.mp4", "file3.mp4"], "mkv");
            Assert.Equal(3, window.Items.Count);
        });
    }

    [Fact]
    public void Construct_VideoSameFormat_DoesNotSkipAsAlreadyTarget()
    {
        StaTestRunner.Run(() =>
        {
            var window = new ConversionProgressWindow("video.mp4", "mp4");
            Assert.Single(window.Items);
            Assert.NotEqual(I18n.T("StatusSkippedAlreadyTarget"), window.Items[0].StatusText);
        });
    }

    [Fact]
    public void QueueItem_CpuFallback_UpdatesSymbolAndBrushes()
    {
        var item = new ConversionQueueItem("video.mp4", "mp4")
        {
            Status = QueueItemStatus.Done,
            CpuFallback = true
        };

        Assert.Equal(Wpf.Ui.Controls.SymbolRegular.Warning16, item.StatusSymbol);
        Assert.NotNull(item.StatusBrush);
        Assert.NotNull(item.StatusTextBrush);
    }
}
