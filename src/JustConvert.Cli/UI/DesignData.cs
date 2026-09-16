using JustConvert.Core;

namespace JustConvert.Cli.UI;

public static class DesignData
{
    private static List<ConversionQueueItem>? _sampleItems;

    public static List<ConversionQueueItem> SampleItems
    {
        get
        {
            if (_sampleItems != null) return _sampleItems;

            var item1 = new ConversionQueueItem("sample_video.mov", "mp4-h264")
            {
                Status = QueueItemStatus.Converting,
                ProgressPercentage = 45,
                IsIndeterminate = false,
                StatusText = "45%",
                Detail = "00:01:23 / 00:03:00"
            };

            var item2 = new ConversionQueueItem("audio_track.wav", "flac")
            {
                Status = QueueItemStatus.Done,
                ProgressPercentage = 100,
                IsIndeterminate = false,
                StatusText = I18n.T("StatusDone"),
                Detail = "100%"
            };

            var item3 = new ConversionQueueItem("screencast.mkv", "webm-vp9")
            {
                Status = QueueItemStatus.Error,
                ProgressPercentage = 20,
                IsIndeterminate = false,
                StatusText = I18n.T("TitleError"),
                ErrorMessage = I18n.T("ErrorDefault"),
                ErrorLog = "ffmpeg -i screencast.mkv -c:v libvpx-vp9 output.webm\nError: unsupported video codec"
            };

            var item4 = new ConversionQueueItem("animation.mp4", "gif")
            {
                Status = QueueItemStatus.Paused,
                ProgressPercentage = 12,
                IsIndeterminate = false,
                StatusText = I18n.T("StatusPaused"),
                Detail = "12%"
            };

            _sampleItems = [item1, item2, item3, item4];
            return _sampleItems;
        }
    }
}
