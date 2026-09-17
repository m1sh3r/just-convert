using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using JustConvert.Core;
using JustConvert.Core.Converters.Tools;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public partial class ConversionOptionsDialog : FluentWindow
{
    private readonly string _targetFormat;
    private readonly string _category;
    private MediaStreamInfo? _mediaInfo;
    private int _batchCount;
    private long? _totalBatchSizeBytes;
    private bool _isUpdating = true;

    public string SelectedTargetFormat => _targetFormat;
    public VideoQualitySetting SelectedVideoQuality { get; } = new();
    public int SelectedAudioBitrate { get; private set; } = 192;
    public int SelectedImageQuality { get; private set; } = 90;
    public RemuxSetting SelectedRemuxSetting { get; private set; } = new();
    public FramesSetting SelectedFramesSetting { get; private set; } = new();
    public string SelectedFramesImageFormat => SelectedFramesSetting.ImageFormat;
    public string SelectedFramesTargetFormat => $"frames-{SelectedFramesSetting.ImageFormat}";
    public bool RememberChoice => ChkRemember?.IsChecked == true;
    public bool AppendQualitySuffix => ChkAppendSuffix?.IsChecked == true;
    public int BatchCount => _batchCount;
    public long? TotalBatchSizeBytes => _totalBatchSizeBytes;

    private record CodecItem(string Id, string DisplayName);
    private record EncoderItem(string Id, string DisplayName);
    private record BitrateItem(int Value, string DisplayName);

    public ConversionOptionsDialog() : this("mp4", "video", new VideoQualitySetting(), null, true, 1, null)
    {
    }

    public ConversionOptionsDialog(
        string targetFormat,
        string category,
        object? initialSetting,
        MediaStreamInfo? mediaInfo,
        bool appendQualitySuffix) : this(targetFormat, category, initialSetting, mediaInfo, appendQualitySuffix, 1, null)
    {
    }

    public ConversionOptionsDialog(
        string targetFormat,
        string category,
        object? initialSetting,
        MediaStreamInfo? mediaInfo,
        bool appendQualitySuffix,
        int batchCount,
        long? totalBatchSizeBytes)
    {
        _isUpdating = true;
        _targetFormat = targetFormat.TrimStart('.').ToLowerInvariant();
        _category = category.ToLowerInvariant();
        _mediaInfo = mediaInfo;
        _batchCount = Math.Max(1, batchCount);
        _totalBatchSizeBytes = totalBatchSizeBytes;

        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            Title = I18n.T("ConversionOptionsTitle");
            AppTitleBar.Title = Title;
            TxtFormatPrompt.Text = I18n.T("ConversionOptionsTitle");
            TxtSourceInfo.Text = "1920x1080, 30 fps, 45 MB";
            TxtEstimatedSize.Text = string.Format(I18n.T("EstimatedFileSizeLabel"), "14.2 MB");
            BtnCancel.Content = I18n.T("BtnCancel");
            BtnConvert.Content = I18n.T("BtnConvert");
            ChkMatchOriginalBitrate.Content = I18n.T("OptionMatchOriginalBitrateBatch");
            return;
        }

        ApplicationThemeManager.ApplySystemTheme();
        ApplicationAccentColorManager.ApplySystemAccent();
        ApplicationThemeManager.Apply(this);
        SystemThemeWatcher.Watch(this);

        Title = I18n.T("ConversionOptionsTitle");
        AppTitleBar.Title = Title;
        ChkAppendSuffix.IsChecked = appendQualitySuffix;

        InitCategoryUI(initialSetting);
    }

    private void InitCategoryUI(object? initialSetting)
    {
        _isUpdating = true;

        var displayTarget = _targetFormat.ToUpperInvariant();
        TxtFormatPrompt.Text = $"{I18n.T("LabelTargetFormat")} {displayTarget}";

        if (_category == "video")
        {
            IconCategory.Symbol = Wpf.Ui.Controls.SymbolRegular.Video24;
            PanelVideoOptions.Visibility = Visibility.Visible;
            PanelAudioOptions.Visibility = Visibility.Collapsed;
            PanelImageOptions.Visibility = Visibility.Collapsed;
            PanelRemuxOptions.Visibility = Visibility.Collapsed;
            PanelFramesOptions.Visibility = Visibility.Collapsed;

            var videoSetting = (initialSetting as VideoQualitySetting) ?? new VideoQualitySetting();
            SelectedVideoQuality.VideoCodec = videoSetting.VideoCodec;
            SelectedVideoQuality.Encoder = videoSetting.Encoder;
            SelectedVideoQuality.VideoQualityCq = videoSetting.VideoQualityCq;
            SelectedVideoQuality.AudioCodec = videoSetting.AudioCodec;
            SelectedVideoQuality.AudioBitrateKbps = videoSetting.AudioBitrateKbps;

            if (_mediaInfo?.Audio != null)
            {
                SelectedVideoQuality.AudioBitrateKbps = MediaProbe.ResolveAudioBitrate(_mediaInfo.Audio, videoSetting.AudioBitrateKbps, 320);
            }

            PopulateVideoCodecs();
            PopulateEncoders();
            PopulateAudioBitrates();

            SliderVideoCq.Value = Math.Clamp(SelectedVideoQuality.VideoQualityCq, 15, 35);

            if (_batchCount > 1)
            {
                TxtFormatPrompt.Text = string.Format(I18n.T("BatchVideoConversionPrompt"), _batchCount);
                var sizeStr = QualityEstimator.FormatFileSize(_totalBatchSizeBytes ?? (_mediaInfo?.FileSizeBytes * _batchCount));
                TxtSourceInfo.Text = string.Format(I18n.T("LabelBatchSourceMedia"), _batchCount, sizeStr);
                TxtSourceInfo.Visibility = Visibility.Visible;
            }
            else if (_mediaInfo?.Video != null)
            {
                TxtFormatPrompt.Text = $"{I18n.T("LabelTargetFormat")} {displayTarget}";
                var v = _mediaInfo.Video;
                var fps = v.FrameRateFps > 0 ? v.FrameRateFps : 30;
                var sizeStr = QualityEstimator.FormatFileSize(_mediaInfo.FileSizeBytes);
                var codecStr = QualityEstimator.FormatCodecName(v.Codec);
                if (string.IsNullOrEmpty(codecStr)) codecStr = "Video";
                TxtSourceInfo.Text = string.Format(I18n.T("LabelSourceMedia"), Path.GetFileName(_mediaInfo.FilePath ?? ""), codecStr, v.Width, v.Height, fps, sizeStr);
                TxtSourceInfo.Visibility = Visibility.Visible;
            }
            else
            {
                TxtFormatPrompt.Text = $"{I18n.T("LabelTargetFormat")} {displayTarget}";
                TxtSourceInfo.Visibility = Visibility.Collapsed;
            }
        }
        else if (_category == "remux")
        {
            IconCategory.Symbol = Wpf.Ui.Controls.SymbolRegular.ArrowRepeatAll24;
            TxtFormatPrompt.Text = I18n.T("RemuxPrompt");
            PanelVideoOptions.Visibility = Visibility.Collapsed;
            PanelAudioOptions.Visibility = Visibility.Collapsed;
            PanelImageOptions.Visibility = Visibility.Collapsed;
            PanelRemuxOptions.Visibility = Visibility.Visible;
            PanelFramesOptions.Visibility = Visibility.Collapsed;

            PopulateRemuxContainers();

            var remuxSetting = (initialSetting as RemuxSetting) ?? new RemuxSetting();
            SelectedRemuxSetting = new RemuxSetting
            {
                TargetContainer = remuxSetting.TargetContainer,
                CopyVideo = remuxSetting.CopyVideo,
                CopyAudio = remuxSetting.CopyAudio,
                CopySubtitles = remuxSetting.CopySubtitles,
                FastStart = remuxSetting.FastStart,
                IsRemembered = remuxSetting.IsRemembered
            };

            for (int i = 0; i < CmbRemuxContainer.Items.Count; i++)
            {
                if (CmbRemuxContainer.Items[i] is CodecItem item && item.Id.Equals(SelectedRemuxSetting.TargetContainer, StringComparison.OrdinalIgnoreCase))
                {
                    CmbRemuxContainer.SelectedIndex = i;
                    break;
                }
            }
            if (CmbRemuxContainer.SelectedIndex < 0 && CmbRemuxContainer.Items.Count > 0)
            {
                CmbRemuxContainer.SelectedIndex = 0;
            }

            ChkRemuxVideo.IsChecked = SelectedRemuxSetting.CopyVideo;
            ChkRemuxAudio.IsChecked = SelectedRemuxSetting.CopyAudio;
            ChkRemuxSubtitles.IsChecked = SelectedRemuxSetting.CopySubtitles;
            ChkRemuxFastStart.IsChecked = SelectedRemuxSetting.FastStart;

            if (_mediaInfo?.Video != null)
            {
                var v = _mediaInfo.Video;
                var fps = v.FrameRateFps > 0 ? v.FrameRateFps : 30;
                var sizeStr = QualityEstimator.FormatFileSize(_mediaInfo.FileSizeBytes);
                var vCodec = QualityEstimator.FormatCodecName(v.Codec);
                var aCodec = QualityEstimator.FormatCodecName(_mediaInfo.Audio?.Codec);
                var codecSummary = !string.IsNullOrEmpty(aCodec) ? $"{vCodec} / {aCodec}" : vCodec;
                TxtSourceInfo.Text = string.Format(I18n.T("LabelSourceMedia"), Path.GetFileName(_mediaInfo.FilePath ?? ""), codecSummary, v.Width, v.Height, fps, sizeStr);
            }
            else if (_mediaInfo?.Audio != null)
            {
                var a = _mediaInfo.Audio;
                var aCodec = QualityEstimator.FormatCodecName(a.Codec);
                var sizeStr = QualityEstimator.FormatFileSize(_mediaInfo.FileSizeBytes);
                TxtSourceInfo.Text = string.Format(I18n.T("LabelSourceRemux"), Path.GetFileName(_mediaInfo.FilePath ?? ""), aCodec, sizeStr);
            }
            else
            {
                TxtSourceInfo.Visibility = Visibility.Collapsed;
            }
        }
        else if (_category == "audio")
        {
            IconCategory.Symbol = Wpf.Ui.Controls.SymbolRegular.MusicNote224;
            PanelVideoOptions.Visibility = Visibility.Collapsed;
            PanelAudioOptions.Visibility = Visibility.Visible;
            PanelImageOptions.Visibility = Visibility.Collapsed;
            PanelRemuxOptions.Visibility = Visibility.Collapsed;
            PanelFramesOptions.Visibility = Visibility.Collapsed;

            var isBatch = _batchCount > 1;
            ChkMatchOriginalBitrate.Content = isBatch
                ? I18n.T("OptionMatchOriginalBitrateBatch")
                : I18n.T("OptionMatchOriginalBitrateSingle");

            var initialBitrate = initialSetting is int b ? b : (initialSetting is AudioQualitySetting aq ? aq.AudioBitrateKbps : (isBatch ? 0 : 192));
            if (initialBitrate <= 0)
            {
                ChkMatchOriginalBitrate.IsChecked = true;
                SelectedAudioBitrate = 0;
                SliderAudioBitrate.Value = 192;
            }
            else
            {
                ChkMatchOriginalBitrate.IsChecked = false;
                if (!isBatch && _mediaInfo?.Audio != null)
                {
                    initialBitrate = MediaProbe.ResolveAudioBitrate(_mediaInfo.Audio, initialBitrate, 320);
                }
                SelectedAudioBitrate = Math.Clamp(initialBitrate, 64, 320);
                SliderAudioBitrate.Value = SelectedAudioBitrate;
            }
            UpdateAudioBitrateControlsState();

            if (_batchCount > 1)
            {
                TxtFormatPrompt.Text = string.Format(I18n.T("BatchVideoConversionPrompt"), _batchCount);
                var sizeStr = QualityEstimator.FormatFileSize(_totalBatchSizeBytes ?? (_mediaInfo?.FileSizeBytes * _batchCount));
                TxtSourceInfo.Text = string.Format(I18n.T("LabelBatchSourceMedia"), _batchCount, sizeStr);
                TxtSourceInfo.Visibility = Visibility.Visible;
            }
            else if (_mediaInfo?.Audio != null)
            {
                var a = _mediaInfo.Audio;
                var sampleRate = a.SampleRate > 0 ? a.SampleRate : 44100;
                var codecStr = QualityEstimator.FormatCodecName(a.Codec);
                if (string.IsNullOrEmpty(codecStr)) codecStr = "Audio";
                TxtSourceInfo.Text = string.Format(I18n.T("LabelSourceAudio"), Path.GetFileName(_mediaInfo.FilePath ?? ""), codecStr, a.BitrateKbps, sampleRate);
                TxtSourceInfo.Visibility = Visibility.Visible;
            }
            else
            {
                TxtSourceInfo.Visibility = Visibility.Collapsed;
            }
        }
        else if (_category == "frames")
        {
            IconCategory.Symbol = Wpf.Ui.Controls.SymbolRegular.ImageMultiple24;
            TxtFormatPrompt.Text = I18n.T("MenuFrames");
            PanelVideoOptions.Visibility = Visibility.Collapsed;
            PanelAudioOptions.Visibility = Visibility.Collapsed;
            PanelImageOptions.Visibility = Visibility.Collapsed;
            PanelRemuxOptions.Visibility = Visibility.Collapsed;
            PanelFramesOptions.Visibility = Visibility.Visible;

            PopulateFramesFormats();

            var framesSetting = (initialSetting as FramesSetting) ?? new FramesSetting();
            SelectedFramesSetting = new FramesSetting
            {
                ImageFormat = framesSetting.ImageFormat,
                IsRemembered = framesSetting.IsRemembered
            };

            for (int i = 0; i < CmbFramesFormat.Items.Count; i++)
            {
                if (CmbFramesFormat.Items[i] is CodecItem item && item.Id.Equals(SelectedFramesSetting.ImageFormat, StringComparison.OrdinalIgnoreCase))
                {
                    CmbFramesFormat.SelectedIndex = i;
                    break;
                }
            }
            if (CmbFramesFormat.SelectedIndex < 0 && CmbFramesFormat.Items.Count > 0)
            {
                CmbFramesFormat.SelectedIndex = 0;
            }

            if (_batchCount > 1)
            {
                var sizeStr = QualityEstimator.FormatFileSize(_totalBatchSizeBytes ?? (_mediaInfo?.FileSizeBytes * _batchCount));
                TxtSourceInfo.Text = string.Format(I18n.T("LabelBatchSourceMedia"), _batchCount, sizeStr);
                TxtSourceInfo.Visibility = Visibility.Visible;
            }
            else if (_mediaInfo?.Video != null)
            {
                var v = _mediaInfo.Video;
                var fps = v.FrameRateFps > 0 ? v.FrameRateFps : 30;
                var sizeStr = QualityEstimator.FormatFileSize(_mediaInfo.FileSizeBytes);
                var codecStr = QualityEstimator.FormatCodecName(v.Codec);
                if (string.IsNullOrEmpty(codecStr)) codecStr = "Video";
                TxtSourceInfo.Text = string.Format(I18n.T("LabelSourceMedia"), Path.GetFileName(_mediaInfo.FilePath ?? ""), codecStr, v.Width, v.Height, fps, sizeStr);
                TxtSourceInfo.Visibility = Visibility.Visible;
            }
            else
            {
                TxtSourceInfo.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            IconCategory.Symbol = Wpf.Ui.Controls.SymbolRegular.Image24;
            PanelVideoOptions.Visibility = Visibility.Collapsed;
            PanelAudioOptions.Visibility = Visibility.Collapsed;
            PanelImageOptions.Visibility = Visibility.Visible;
            PanelRemuxOptions.Visibility = Visibility.Collapsed;
            PanelFramesOptions.Visibility = Visibility.Collapsed;

            var initialQuality = initialSetting is int q ? q : (initialSetting is ImageQualitySetting iq ? iq.Quality : 90);
            SelectedImageQuality = Math.Clamp(initialQuality, 1, 100);
            SliderImageQuality.Value = SelectedImageQuality;

            if (_mediaInfo?.Video != null)
            {
                var v = _mediaInfo.Video;
                var sizeStr = QualityEstimator.FormatFileSize(_mediaInfo.FileSizeBytes);
                TxtSourceInfo.Text = $"{v.Width}x{v.Height} ({sizeStr})";
            }
            else
            {
                TxtSourceInfo.Visibility = Visibility.Collapsed;
            }
        }

        _isUpdating = false;
        UpdatePreview();
    }

    private void PopulateVideoCodecs()
    {
        var codecs = new List<CodecItem>();
        var fmt = _targetFormat;

        if (fmt == "webm")
        {
            codecs.Add(new CodecItem("vp9", I18n.T("CodecVp9")));
            codecs.Add(new CodecItem("av1", I18n.T("CodecAv1")));
        }
        else if (fmt == "mov")
        {
            codecs.Add(new CodecItem("h264", I18n.T("CodecH264")));
            codecs.Add(new CodecItem("h265", I18n.T("CodecH265")));
            codecs.Add(new CodecItem("prores422", I18n.T("CodecProRes")));
            codecs.Add(new CodecItem("copy", I18n.T("CodecCopy")));
        }
        else
        {
            codecs.Add(new CodecItem("h264", I18n.T("CodecH264")));
            codecs.Add(new CodecItem("h265", I18n.T("CodecH265")));
            codecs.Add(new CodecItem("av1", I18n.T("CodecAv1")));
            codecs.Add(new CodecItem("copy", I18n.T("CodecCopy")));
        }

        CmbVideoCodec.ItemsSource = codecs;
        CmbVideoCodec.DisplayMemberPath = nameof(CodecItem.DisplayName);
        CmbVideoCodec.SelectedValuePath = nameof(CodecItem.Id);

        var match = codecs.FirstOrDefault(c => string.Equals(c.Id, SelectedVideoQuality.VideoCodec, StringComparison.OrdinalIgnoreCase)) ?? codecs[0];
        CmbVideoCodec.SelectedItem = match;
    }

    private void PopulateEncoders()
    {
        var currentCodec = (CmbVideoCodec.SelectedItem as CodecItem)?.Id ?? SelectedVideoQuality.VideoCodec;
        var encoders = new List<EncoderItem>
        {
            new("auto", I18n.T("EncoderAuto")),
            new("cpu", I18n.T("EncoderCpu"))
        };

        if (currentCodec == "h264")
        {
            if (HardwareAccelerationDetector.HasNvencH264) encoders.Add(new("nvenc", I18n.T("EncoderNvenc")));
            if (HardwareAccelerationDetector.HasQsvH264) encoders.Add(new("qsv", I18n.T("EncoderQsv")));
            if (HardwareAccelerationDetector.HasAmfH264) encoders.Add(new("amf", I18n.T("EncoderAmf")));
        }
        else if (currentCodec is "h265" or "hevc")
        {
            if (HardwareAccelerationDetector.HasNvencHevc) encoders.Add(new("nvenc", I18n.T("EncoderNvenc")));
            if (HardwareAccelerationDetector.HasQsvHevc) encoders.Add(new("qsv", I18n.T("EncoderQsv")));
            if (HardwareAccelerationDetector.HasAmfHevc) encoders.Add(new("amf", I18n.T("EncoderAmf")));
        }
        else if (currentCodec == "av1")
        {
            if (HardwareAccelerationDetector.HasNvencAv1) encoders.Add(new("nvenc", I18n.T("EncoderNvenc")));
            if (HardwareAccelerationDetector.HasQsvAv1) encoders.Add(new("qsv", I18n.T("EncoderQsv")));
            if (HardwareAccelerationDetector.HasAmfAv1) encoders.Add(new("amf", I18n.T("EncoderAmf")));
        }
        else if (currentCodec == "vp9")
        {
            if (HardwareAccelerationDetector.HasQsvVp9) encoders.Add(new("qsv", I18n.T("EncoderQsv")));
        }

        CmbEncoder.ItemsSource = encoders;
        CmbEncoder.DisplayMemberPath = nameof(EncoderItem.DisplayName);
        CmbEncoder.SelectedValuePath = nameof(EncoderItem.Id);

        var match = encoders.FirstOrDefault(e => string.Equals(e.Id, SelectedVideoQuality.Encoder, StringComparison.OrdinalIgnoreCase)) ?? encoders[0];
        CmbEncoder.SelectedItem = match;
    }

    private void PopulateAudioBitrates()
    {
        var originalLabel = _batchCount > 1
            ? I18n.T("OptionMatchOriginalBitrateBatch")
            : I18n.T("OptionMatchOriginalBitrateSingle");

        var bitrates = new List<BitrateItem>
        {
            new(0, originalLabel),
            new(128, "128 " + I18n.T("UnitKB") + "/s"),
            new(192, "192 " + I18n.T("UnitKB") + "/s"),
            new(256, "256 " + I18n.T("UnitKB") + "/s"),
            new(320, "320 " + I18n.T("UnitKB") + "/s")
        };

        CmbAudioBitrate.ItemsSource = bitrates;
        CmbAudioBitrate.DisplayMemberPath = nameof(BitrateItem.DisplayName);
        CmbAudioBitrate.SelectedValuePath = nameof(BitrateItem.Value);

        var match = bitrates.FirstOrDefault(b => b.Value == SelectedVideoQuality.AudioBitrateKbps)
            ?? (_batchCount > 1 ? bitrates[0] : bitrates[2]);
        CmbAudioBitrate.SelectedItem = match;
    }

    private void OnVideoOptionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (sender == CmbVideoCodec)
        {
            var codec = (CmbVideoCodec.SelectedItem as CodecItem)?.Id ?? "h264";
            SelectedVideoQuality.VideoCodec = codec;
            _isUpdating = true;
            PopulateEncoders();
            _isUpdating = false;
        }

        if (sender == CmbEncoder)
        {
            var encoder = (CmbEncoder.SelectedItem as EncoderItem)?.Id ?? "auto";
            SelectedVideoQuality.Encoder = encoder;
        }

        if (sender == CmbAudioBitrate)
        {
            if (CmbAudioBitrate.SelectedItem is BitrateItem item)
            {
                SelectedVideoQuality.AudioBitrateKbps = item.Value;
            }
        }

        UpdatePreview();
    }

    private void OnVideoCqValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        SelectedVideoQuality.VideoQualityCq = (int)e.NewValue;
        UpdatePreview();
    }

    private void OnAudioBitrateValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        if (ChkMatchOriginalBitrate?.IsChecked != true)
        {
            SelectedAudioBitrate = (int)e.NewValue;
        }
        UpdatePreview();
    }

    private void OnMatchOriginalBitrateChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        UpdateAudioBitrateControlsState();
        UpdatePreview();
    }

    private void UpdateAudioBitrateControlsState()
    {
        var isMatchOriginal = ChkMatchOriginalBitrate?.IsChecked == true;
        if (SliderAudioBitrate != null)
        {
            SliderAudioBitrate.IsEnabled = !isMatchOriginal;
            SliderAudioBitrate.Opacity = isMatchOriginal ? 0.45 : 1.0;
        }

        if (isMatchOriginal)
        {
            SelectedAudioBitrate = 0;
        }
        else if (SliderAudioBitrate != null)
        {
            SelectedAudioBitrate = (int)SliderAudioBitrate.Value;
        }
    }

    private void PopulateFramesFormats()
    {
        var formats = new List<CodecItem>
        {
            new("png", "PNG"),
            new("jpg", "JPEG"),
            new("webp", "WEBP"),
            new("bmp", "BMP"),
            new("tiff", "TIFF")
        };

        CmbFramesFormat.ItemsSource = formats;
        CmbFramesFormat.DisplayMemberPath = nameof(CodecItem.DisplayName);
        CmbFramesFormat.SelectedValuePath = nameof(CodecItem.Id);
    }

    private void OnFramesOptionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (CmbFramesFormat.SelectedItem is CodecItem item)
        {
            SelectedFramesSetting.ImageFormat = item.Id;
        }
        UpdatePreview();
    }

    private void OnImageQualityValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        SelectedImageQuality = (int)e.NewValue;
        UpdatePreview();
    }

    private void PopulateRemuxContainers()
    {
        var containers = new List<CodecItem>
        {
            new("mp4", "MP4 (.mp4)"),
            new("mkv", "MKV (.mkv)"),
            new("mov", "MOV (.mov)"),
            new("webm", "WEBM (.webm)")
        };

        CmbRemuxContainer.ItemsSource = containers;
        CmbRemuxContainer.DisplayMemberPath = nameof(CodecItem.DisplayName);
        CmbRemuxContainer.SelectedValuePath = nameof(CodecItem.Id);

        var match = containers.FirstOrDefault(c => string.Equals(c.Id, SelectedRemuxSetting.TargetContainer, StringComparison.OrdinalIgnoreCase)) ?? containers[0];
        CmbRemuxContainer.SelectedItem = match;
    }

    private void OnRemuxOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || CmbRemuxContainer == null || ChkRemuxVideo == null || ChkRemuxAudio == null || ChkRemuxSubtitles == null || ChkRemuxFastStart == null) return;
        if (CmbRemuxContainer.SelectedItem is CodecItem item)
        {
            SelectedRemuxSetting.TargetContainer = item.Id;
        }
        SelectedRemuxSetting.CopyVideo = ChkRemuxVideo.IsChecked == true;
        SelectedRemuxSetting.CopyAudio = ChkRemuxAudio.IsChecked == true;
        SelectedRemuxSetting.CopySubtitles = ChkRemuxSubtitles.IsChecked == true;
        SelectedRemuxSetting.FastStart = ChkRemuxFastStart.IsChecked == true;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_isUpdating || TxtFormatPrompt == null || TxtEstimatedSize == null) return;

        if (_category == "video")
        {
            if (TxtVideoCqValue == null || TxtVideoQualityDescription == null) return;

            var cq = SelectedVideoQuality.VideoQualityCq;
            TxtVideoCqValue.Text = $"CQ {cq}";

            var width = _mediaInfo?.Video?.Width ?? 1920;
            var height = _mediaInfo?.Video?.Height ?? 1080;
            var fps = _mediaInfo?.Video?.FrameRateFps ?? 30;

            var descKey = QualityEstimator.GetVideoQualityDescriptionKey(cq, width, height, fps);
            TxtVideoQualityDescription.Text = I18n.T(descKey);

            var effectiveAudioBitrate = SelectedVideoQuality.AudioBitrateKbps <= 0
                ? MediaProbe.ResolveAudioBitrate(_mediaInfo?.Audio, 192, 320)
                : SelectedVideoQuality.AudioBitrateKbps;

            var estimatedBytes = QualityEstimator.EstimateVideoFileSize(
                SelectedVideoQuality.VideoCodec,
                cq,
                effectiveAudioBitrate,
                _mediaInfo
            );

            UpdateEstimatedSizeText(estimatedBytes);
        }
        else if (_category == "remux")
        {
            var sourceBytes = (_batchCount > 1 && _totalBatchSizeBytes.HasValue)
                ? _totalBatchSizeBytes.Value
                : (_mediaInfo?.FileSizeBytes ?? 0L);
            if (_batchCount > 1 && !_totalBatchSizeBytes.HasValue && _mediaInfo?.FileSizeBytes.HasValue == true)
            {
                sourceBytes = _mediaInfo.FileSizeBytes.Value * _batchCount;
            }
            if (sourceBytes > 0)
            {
                var formatted = QualityEstimator.FormatFileSize(sourceBytes);
                TxtEstimatedSize.Text = string.Format(I18n.T("EstimatedSizeRemux"), formatted);
            }
            else
            {
                var key = _batchCount > 1 ? "EstimatedBatchSizeLabel" : "EstimatedFileSizeLabel";
                TxtEstimatedSize.Text = $"{I18n.T(key).Split('~')[0].TrimEnd()}: —";
            }
        }
        else if (_category == "audio")
        {
            if (TxtAudioBitrateValue == null || TxtAudioQualityDescription == null) return;

            var bitrate = SelectedAudioBitrate;
            if (bitrate <= 0)
            {
                TxtAudioBitrateValue.Text = I18n.T("BitrateOriginalValue");
                TxtAudioQualityDescription.Text = _batchCount > 1
                    ? I18n.T("DescAudioBitrateMatchOriginalBatch")
                    : I18n.T("DescAudioBitrateMatchOriginal");

                var probeBitrate = MediaProbe.ResolveAudioBitrate(_mediaInfo?.Audio, 192, 320);
                var estimatedBytes = QualityEstimator.EstimateAudioFileSize(
                    _targetFormat,
                    probeBitrate,
                    _mediaInfo
                );
                UpdateEstimatedSizeText(estimatedBytes);
            }
            else
            {
                TxtAudioBitrateValue.Text = $"{bitrate} {I18n.T("UnitKB")}/s";

                var descKey = QualityEstimator.GetAudioQualityDescriptionKey(bitrate);
                TxtAudioQualityDescription.Text = I18n.T(descKey);

                var estimatedBytes = QualityEstimator.EstimateAudioFileSize(
                    _targetFormat,
                    bitrate,
                    _mediaInfo
                );

                UpdateEstimatedSizeText(estimatedBytes);
            }
        }
        else if (_category == "frames")
        {
            var key = _batchCount > 1 ? "EstimatedBatchSizeLabel" : "EstimatedFileSizeLabel";
            if (_mediaInfo?.Video != null && _mediaInfo.Video.FrameRateFps > 0 && _mediaInfo.DurationSeconds > 0)
            {
                var fps = _mediaInfo.Video.FrameRateFps;
                var totalFrames = (long)(fps * _mediaInfo.DurationSeconds);
                var width = (long)(_mediaInfo.Video.Width ?? 1920);
                var height = (long)(_mediaInfo.Video.Height ?? 1080);
                var estimatedPerFrame = SelectedFramesSetting.ImageFormat switch
                {
                    "jpg" => 150_000L,
                    "webp" => 100_000L,
                    "bmp" => width * height * 3L,
                    "tiff" => width * height * 3L,
                    _ => 800_000L
                };
                var totalBytes = totalFrames * estimatedPerFrame;
                if (_batchCount > 1) totalBytes *= _batchCount;
                var formatted = QualityEstimator.FormatFileSize(totalBytes);
                TxtEstimatedSize.Text = string.Format(I18n.T(key), formatted);
            }
            else
            {
                var sourceBytes = (_batchCount > 1 && _totalBatchSizeBytes.HasValue)
                    ? _totalBatchSizeBytes.Value
                    : (_mediaInfo?.FileSizeBytes ?? 0L);
                if (_batchCount > 1 && !_totalBatchSizeBytes.HasValue && _mediaInfo?.FileSizeBytes.HasValue == true)
                {
                    sourceBytes = _mediaInfo.FileSizeBytes.Value * _batchCount;
                }

                if (sourceBytes > 0)
                {
                    var formatted = QualityEstimator.FormatFileSize(sourceBytes);
                    TxtEstimatedSize.Text = string.Format(I18n.T(key), $"~{formatted}");
                }
                else
                {
                    TxtEstimatedSize.Text = $"{I18n.T(key).Split('~')[0].TrimEnd()}: —";
                }
            }
        }
        else
        {
            if (TxtImageQualityValue == null || TxtImageQualityDescription == null) return;

            var quality = SelectedImageQuality;
            TxtImageQualityValue.Text = $"{quality}%";

            var descKey = QualityEstimator.GetImageQualityDescriptionKey(quality);
            TxtImageQualityDescription.Text = I18n.T(descKey);

            var estimatedBytes = QualityEstimator.EstimateImageFileSize(
                _targetFormat,
                quality,
                _mediaInfo
            );

            UpdateEstimatedSizeText(estimatedBytes);
        }
    }

    private void UpdateEstimatedSizeText(long estimatedBytes)
    {
        if (TxtEstimatedSize == null) return;

        var key = _batchCount > 1 ? "EstimatedBatchSizeLabel" : "EstimatedFileSizeLabel";

        if (estimatedBytes > 0)
        {
            var totalBytes = _batchCount > 1 ? estimatedBytes * _batchCount : estimatedBytes;
            var formatted = QualityEstimator.FormatFileSize(totalBytes);
            TxtEstimatedSize.Text = string.Format(I18n.T(key), formatted);
        }
        else
        {
            TxtEstimatedSize.Text = $"{I18n.T(key).Split('~')[0].TrimEnd()}: —";
        }
    }

    public void AddBatchFiles(IReadOnlyList<string> newFiles)
    {
        if (newFiles.Count == 0) return;
        _batchCount += newFiles.Count;
        foreach (var file in newFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    _totalBatchSizeBytes = (_totalBatchSizeBytes ?? 0) + new FileInfo(file).Length;
                }
            }
            catch { }
        }

        if (_category == "video")
        {
            TxtFormatPrompt.Text = string.Format(I18n.T("BatchVideoConversionPrompt"), _batchCount);
            var sizeStr = QualityEstimator.FormatFileSize(_totalBatchSizeBytes ?? (_mediaInfo?.FileSizeBytes * _batchCount));
            TxtSourceInfo.Text = string.Format(I18n.T("LabelBatchSourceMedia"), _batchCount, sizeStr);
            TxtSourceInfo.Visibility = Visibility.Visible;
        }
        else if (_category is "audio" or "image" or "frames")
        {
            var sizeStr = QualityEstimator.FormatFileSize(_totalBatchSizeBytes);
            TxtSourceInfo.Text = string.Format(I18n.T("LabelBatchSourceMedia"), _batchCount, sizeStr);
            TxtSourceInfo.Visibility = Visibility.Visible;
        }

        UpdatePreview();
    }

    public void UpdateMediaInfo(MediaStreamInfo mediaInfo)
    {
        _mediaInfo = mediaInfo;
        if (_category == "video")
        {
            if (_batchCount <= 1 && SelectedVideoQuality.AudioBitrateKbps > 0 && _mediaInfo.Audio != null && CmbAudioBitrate != null)
            {
                var resolved = MediaProbe.ResolveAudioBitrate(_mediaInfo.Audio, SelectedVideoQuality.AudioBitrateKbps, 320);
                SelectedVideoQuality.AudioBitrateKbps = resolved;
                CmbAudioBitrate.SelectedValue = resolved;
            }
            if (_batchCount <= 1 && _mediaInfo.Video != null)
            {
                var v = _mediaInfo.Video;
                var fps = v.FrameRateFps > 0 ? v.FrameRateFps : 30;
                var sizeStr = QualityEstimator.FormatFileSize(_mediaInfo.FileSizeBytes);
                var codecStr = QualityEstimator.FormatCodecName(v.Codec);
                if (string.IsNullOrEmpty(codecStr)) codecStr = "Video";
                TxtSourceInfo.Text = string.Format(I18n.T("LabelSourceMedia"), Path.GetFileName(_mediaInfo.FilePath ?? ""), codecStr, v.Width, v.Height, fps, sizeStr);
                TxtSourceInfo.Visibility = Visibility.Visible;
            }
        }
        else if (_category == "audio")
        {
            if (_mediaInfo.Audio != null)
            {
                if (_batchCount <= 1 && ChkMatchOriginalBitrate?.IsChecked != true)
                {
                    var resolved = MediaProbe.ResolveAudioBitrate(_mediaInfo.Audio, SelectedAudioBitrate, 320);
                    SelectedAudioBitrate = resolved;
                    SliderAudioBitrate.Value = resolved;
                }
                if (_batchCount <= 1)
                {
                    var a = _mediaInfo.Audio;
                    var sampleRate = a.SampleRate > 0 ? a.SampleRate : 44100;
                    var codecStr = QualityEstimator.FormatCodecName(a.Codec);
                    if (string.IsNullOrEmpty(codecStr)) codecStr = "Audio";
                    TxtSourceInfo.Text = string.Format(I18n.T("LabelSourceAudio"), Path.GetFileName(_mediaInfo.FilePath ?? ""), codecStr, a.BitrateKbps, sampleRate);
                    TxtSourceInfo.Visibility = Visibility.Visible;
                }
            }
        }
        else if (_category == "frames")
        {
            if (_batchCount <= 1 && _mediaInfo.Video != null)
            {
                var v = _mediaInfo.Video;
                var fps = v.FrameRateFps > 0 ? v.FrameRateFps : 30;
                var sizeStr = QualityEstimator.FormatFileSize(_mediaInfo.FileSizeBytes);
                var codecStr = QualityEstimator.FormatCodecName(v.Codec);
                if (string.IsNullOrEmpty(codecStr)) codecStr = "Video";
                TxtSourceInfo.Text = string.Format(I18n.T("LabelSourceMedia"), Path.GetFileName(_mediaInfo.FilePath ?? ""), codecStr, v.Width, v.Height, fps, sizeStr);
                TxtSourceInfo.Visibility = Visibility.Visible;
            }
        }
        UpdatePreview();
    }

    private void BtnConvert_Click(object sender, RoutedEventArgs e)
    {
        if (_category == "remux")
        {
            SelectedRemuxSetting.IsRemembered = RememberChoice;
        }
        else if (_category == "frames")
        {
            SelectedFramesSetting.IsRemembered = RememberChoice;
        }
        try { DialogResult = true; } catch (InvalidOperationException) { }
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        try { DialogResult = false; } catch (InvalidOperationException) { }
        Close();
    }
}
