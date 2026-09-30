using System.Windows;
using JustConvert.Cli.UI;
using JustConvert.Core;
using Xunit;

namespace JustConvert.Tests;

public class UiQueueProcessingWindowTests
{
    [Fact]
    public void Construct_InitializesPropertiesAndControls()
    {
        StaTestRunner.Run(() =>
        {
            var window = new QueueProcessingWindow();
            window.Measure(new Size(460, 190));
            window.Arrange(new Rect(0, 0, 460, 190));

            Assert.Equal(I18n.T("QueueProcessingTitle"), window.Title);
            Assert.True(window.ProgressBar.IsIndeterminate);
            Assert.False(window.IsCancelled);
            Assert.NotNull(window.BtnCancel);
        });
    }

    [Fact]
    public void UpdateProgress_UpdatesValuesCorrectly()
    {
        StaTestRunner.Run(() =>
        {
            var window = new QueueProcessingWindow();

            window.UpdateProgress(5, 10, "Custom Status");

            Assert.False(window.ProgressBar.IsIndeterminate);
            Assert.Equal(5, window.ProgressBar.Value);
            Assert.Equal(10, window.ProgressBar.Maximum);
            Assert.Equal("Custom Status", window.TxtStatus.Text);
            Assert.Equal(string.Format(I18n.T("QueueProcessingFilesProgress"), 5, 10), window.TxtCount.Text);

            window.SetIndeterminate("Indeterminate Status");
            Assert.True(window.ProgressBar.IsIndeterminate);
            Assert.Equal("Indeterminate Status", window.TxtStatus.Text);
        });
    }

    [Fact]
    public void AddBatchFiles_AndDrain_ManagesFilesProperly()
    {
        StaTestRunner.Run(() =>
        {
            var window = new QueueProcessingWindow();

            window.AddBatchFiles(["file1.mp4", "file2.mkv", "file1.mp4"]);
            var drained = window.DrainIncomingFiles();

            Assert.Equal(2, drained.Count);
            Assert.Contains("file1.mp4", drained);
            Assert.Contains("file2.mkv", drained);

            var emptyDrain = window.DrainIncomingFiles();
            Assert.Empty(emptyDrain);
        });
    }

    [Fact]
    public void Cancel_TriggersCancellationToken()
    {
        StaTestRunner.Run(() =>
        {
            var window = new QueueProcessingWindow();
            Assert.False(window.IsCancelled);

            window.Cancel();
            Assert.True(window.IsCancelled);
            Assert.True(window.CancellationToken.IsCancellationRequested);
        });
    }
}
