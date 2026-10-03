using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JustConvert.Core;
using JustConvert.Core.Converters.Tools;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public partial class ConversionOptionsDialog : FluentWindow
{
    private readonly string _targetFormat;
    private readonly string _category;
    private readonly string? _sourceFormat;
    private MediaStreamInfo? _mediaInfo;
    private int _batchCount;
    private long? _totalBatchSizeBytes;
    private bool _isUpdating = true;

    public string SelectedTargetFormat => _targetFormat;
    public VideoQualitySetting SelectedVideoQuality { get; } = new();
    public int SelectedAudioBitrate { get; private set; } = 192;
    public int SelectedImageQuality { get; private set; } = 90;
    public FramesSetting SelectedFramesSetting { get; private set; } = new();
    public bool IsResetRequested { get; private set; }

    public string SelectedFramesImageFormat => SelectedFramesSetting.ImageFormat;
    public string SelectedFramesTargetFormat => $"frames-{SelectedFramesSetting.ImageFormat}";
    public string SelectedRateControl => (CmbRateControl?.SelectedItem as RateControlItem)?.Id ?? "cq";
    public int SelectedVideoBitrate => (int)(SliderVideoBitrate?.Value ?? 15000);
    public bool RememberChoice => ChkRemember?.IsChecked == true;
    public bool AppendQualitySuffix => ChkAppendSuffix?.IsChecked == true;
    public int BatchCount => _batchCount;
    public long? TotalBatchSizeBytes => _totalBatchSizeBytes;

    public int SelectedSvgWidth
    {
        get
        {
            if (CmbSvgWidth?.SelectedItem is SvgDimensionItem item) return item.Value;
            var text = CmbSvgWidth?.Text?.Replace("px", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (int.TryParse(text, out var parsed) && parsed > 0) return parsed;
            return 0;
        }
    }

    public SvgRasterSetting SelectedSvgSetting => new()
    {
        Width = SelectedSvgWidth,
        IsRemembered = RememberChoice,
        AppendQualitySuffix = AppendQualitySuffix
    };

    private record CodecItem(string Id, string DisplayName);
    private record EncoderItem(string Id, string DisplayName);
    private record RateControlItem(string Id, string DisplayName);
    private record BitrateItem(int Value, string DisplayName);
    private record SvgDimensionItem(int Value, string DisplayName);

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
        int batchCount = 1,
        long? totalBatchSizeBytes = null,
        string? sourceFormat = null,
        bool isSettingsMode = false)
    {
        _isUpdating = true;
        _targetFormat = targetFormat.TrimStart('.').ToLowerInvariant();
        _category = category.ToLowerInvariant();
        _mediaInfo = mediaInfo;
        _batchCount = Math.Max(1, batchCount);
        _totalBatchSizeBytes = totalBatchSizeBytes;
        _sourceFormat = sourceFormat?.TrimStart('.').ToLowerInvariant()
            ?? (!string.IsNullOrEmpty(mediaInfo?.FilePath) ? Path.GetExtension(mediaInfo.FilePath).TrimStart('.').ToLowerInvariant() : null);

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

        FluentThemeService.Watch(this);

        Title = I18n.T("ConversionOptionsTitle");
        AppTitleBar.Title = Title;
        ChkAppendSuffix.IsChecked = appendQualitySuffix;

        InitCategoryUI(initialSetting);

        if (isSettingsMode)
        {
            BtnConvert.Content = I18n.T("BtnSave");
            BtnReset.Visibility = Visibility.Visible;
            ChkRemember.IsChecked = true;
            ChkRemember.Visibility = Visibility.Collapsed;
            TxtSourceInfo.Visibility = Visibility.Collapsed;
            BorderEstimatedSize.Visibility = Visibility.Collapsed;
            Grid.SetColumn(PanelOptionsCheckboxes, 0);
            Grid.SetColumnSpan(PanelOptionsCheckboxes, 3);
        }
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
            PanelFramesOptions.Visibility = Visibility.Collapsed;

            var videoSetting = (initialSetting as VideoQualitySetting) ?? new VideoQualitySetting();
            SelectedVideoQuality.VideoCodec = videoSetting.VideoCodec;
            SelectedVideoQuality.Encoder = videoSetting.Encoder;
            SelectedVideoQuality.RateControl = videoSetting.RateControl;
            SelectedVideoQuality.VideoQualityCq = videoSetting.VideoQualityCq;
            SelectedVideoQuality.VideoBitrateKbps = videoSetting.VideoBitrateKbps;
            SelectedVideoQuality.AudioCodec = videoSetting.AudioCodec;
            SelectedVideoQuality.AudioBitrateKbps = videoSetting.AudioBitrateKbps;
            SelectedVideoQuality.AppendQualitySuffix = videoSetting.AppendQualitySuffix;

            if (videoSetting.AudioBitrateKbps > 0 && _mediaInfo?.Audio != null)
            {
                SelectedVideoQuality.AudioBitrateKbps = MediaProbe.ResolveAudioBitrate(_mediaInfo.Audio, videoSetting.AudioBitrateKbps, 320);
            }

            PopulateVideoCodecs();
            PopulateEncoders();
            PopulateRateControls();
            PopulateAudioBitrates();

            SliderVideoCq.Value = Math.Clamp(SelectedVideoQuality.VideoQualityCq, 15, 35);
            SliderVideoBitrate.Value = Math.Clamp(SelectedVideoQuality.VideoBitrateKbps, 500, 50000);
            UpdateBitrateDisplay((int)SliderVideoBitrate.Value);
            UpdateVideoRateControlVisibility();

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
        else if (_category == "audio")
        {
            IconCategory.Symbol = Wpf.Ui.Controls.SymbolRegular.MusicNote224;
            PanelVideoOptions.Visibility = Visibility.Collapsed;
            PanelAudioOptions.Visibility = Visibility.Visible;
            PanelImageOptions.Visibility = Visibility.Collapsed;
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
            PanelFramesOptions.Visibility = Visibility.Visible;

            PopulateFramesFormats();

            var framesSetting = (initialSetting as FramesSetting) ?? new FramesSetting();
            SelectedFramesSetting = new FramesSetting
            {
                ImageFormat = framesSetting.ImageFormat,
                IsRemembered = framesSetting.IsRemembered,
                AppendQualitySuffix = framesSetting.AppendQualitySuffix
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
            PanelFramesOptions.Visibility = Visibility.Collapsed;

            var isSvg = string.Equals(_sourceFormat, "svg", StringComparison.OrdinalIgnoreCase);
            PanelSvgOptions.Visibility = isSvg ? Visibility.Visible : Visibility.Collapsed;
            PanelImageQuality.Visibility = (!isSvg || AppSettings.SupportsQuality(_targetFormat)) ? Visibility.Visible : Visibility.Collapsed;

            if (isSvg)
            {
                PopulateSvgOptions(initialSetting as SvgRasterSetting);
            }

            var initialQuality = initialSetting is int q
                ? q
                : (initialSetting is ImageQualitySetting iq ? iq.Quality : AppSettings.GetDefaultQuality(_targetFormat));
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
        var hasHw = currentCodec switch
        {
            "h264" => HardwareAccelerationDetector.HasNvencH264 || HardwareAccelerationDetector.HasQsvH264 || HardwareAccelerationDetector.HasAmfH264,
            "h265" or "hevc" => HardwareAccelerationDetector.HasNvencHevc || HardwareAccelerationDetector.HasQsvHevc || HardwareAccelerationDetector.HasAmfHevc,
            "av1" => HardwareAccelerationDetector.HasNvencAv1 || HardwareAccelerationDetector.HasQsvAv1 || HardwareAccelerationDetector.HasAmfAv1,
            "vp9" => HardwareAccelerationDetector.HasQsvVp9,
            _ => false
        };

        var encoders = new List<EncoderItem>
        {
            new("auto", hasHw ? I18n.T("EncoderAutoHw") : I18n.T("EncoderAutoCpu")),
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
            ?? bitrates[0];
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
            UpdateVideoRateControlVisibility();
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

    private void PopulateRateControls()
    {
        var items = new List<RateControlItem>
        {
            new("cq", I18n.T("RateControlCq")),
            new("vbr", I18n.T("RateControlVbr")),
            new("cbr", I18n.T("RateControlCbr"))
        };

        CmbRateControl.ItemsSource = items;
        CmbRateControl.DisplayMemberPath = nameof(RateControlItem.DisplayName);
        CmbRateControl.SelectedValuePath = nameof(RateControlItem.Id);

        var targetRc = !string.IsNullOrEmpty(SelectedVideoQuality.RateControl)
            ? SelectedVideoQuality.RateControl.ToLowerInvariant()
            : "cq";

        var match = items.FirstOrDefault(i => i.Id.Equals(targetRc, StringComparison.OrdinalIgnoreCase)) ?? items[0];
        CmbRateControl.SelectedItem = match;
    }

    private void OnRateControlSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var rc = (CmbRateControl.SelectedItem as RateControlItem)?.Id
            ?? (CmbRateControl.SelectedValue as string)
            ?? "cq";
        SelectedVideoQuality.RateControl = rc;

        UpdateVideoRateControlVisibility();
        if (!_isUpdating)
        {
            UpdatePreview();
        }
    }

    private void UpdateVideoRateControlVisibility()
    {
        var codec = (CmbVideoCodec.SelectedItem as CodecItem)?.Id?.ToLowerInvariant() ?? "";
        if (codec is "copy" or "prores422" or "prores4444")
        {
            GridRateControl.Visibility = Visibility.Collapsed;
            PanelVideoCq.Visibility = Visibility.Collapsed;
            PanelVideoBitrate.Visibility = Visibility.Collapsed;
            return;
        }

        GridRateControl.Visibility = Visibility.Visible;
        var rc = SelectedRateControl;
        if (rc is "vbr" or "cbr")
        {
            PanelVideoCq.Visibility = Visibility.Collapsed;
            PanelVideoBitrate.Visibility = Visibility.Visible;
            if (SliderVideoBitrate != null)
            {
                UpdateBitrateDisplay((int)SliderVideoBitrate.Value);
            }
        }
        else
        {
            PanelVideoCq.Visibility = Visibility.Visible;
            PanelVideoBitrate.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateBitrateDisplay(int kbps)
    {
        if (TxtVideoBitrateValue != null)
        {
            TxtVideoBitrateValue.Text = $"{kbps} {I18n.T("BitrateUnitKbps")}";
        }
    }

    private void OnVideoBitrateValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var kbps = (int)e.NewValue;
        UpdateBitrateDisplay(kbps);

        if (!_isUpdating)
        {
            SelectedVideoQuality.VideoBitrateKbps = kbps;
            UpdatePreview();
        }
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

    private void PopulateSvgOptions(SvgRasterSetting? initial)
    {
        var settings = AppSettings.Load();
        var effective = initial ?? settings.GetEffectiveSvgSetting();

        var widthItems = new List<SvgDimensionItem>
        {
            new(0, I18n.T("SvgWidthOriginal")),
            new(512, string.Format(I18n.T("SvgWidthItem"), 512)),
            new(1024, string.Format(I18n.T("SvgWidthItem"), 1024)),
            new(2048, string.Format(I18n.T("SvgWidthItem"), 2048)),
            new(4096, string.Format(I18n.T("SvgWidthItem"), 4096))
        };
        CmbSvgWidth.ItemsSource = widthItems;
        CmbSvgWidth.DisplayMemberPath = nameof(SvgDimensionItem.DisplayName);
        CmbSvgWidth.SelectedValuePath = nameof(SvgDimensionItem.Value);

        var selectedWidth = widthItems.FirstOrDefault(w => w.Value == effective.Width);
        if (selectedWidth != null)
        {
            CmbSvgWidth.SelectedItem = selectedWidth;
        }
        else if (effective.Width > 0)
        {
            CmbSvgWidth.Text = string.Format(I18n.T("SvgWidthItem"), effective.Width);
        }
        else
        {
            CmbSvgWidth.SelectedIndex = 0;
        }
    }

    private void OnSvgOptionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        UpdatePreview();
    }

    private void OnSvgWidthLostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
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
                _mediaInfo,
                SelectedVideoQuality.Encoder,
                SelectedRateControl,
                SelectedVideoBitrate
            );

            UpdateEstimatedSizeText(estimatedBytes);
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
            var isSvg = string.Equals(_sourceFormat, "svg", StringComparison.OrdinalIgnoreCase);
            if (isSvg && !AppSettings.SupportsQuality(_targetFormat))
            {
                var key = _batchCount > 1 ? "EstimatedBatchSizeLabel" : "EstimatedFileSizeLabel";
                var w = SelectedSvgWidth > 0 ? SelectedSvgWidth : 1024;
                var estimatedSvgBytes = (long)(w * w * 0.4);
                if (_batchCount > 1) estimatedSvgBytes *= _batchCount;
                var formatted = QualityEstimator.FormatFileSize(estimatedSvgBytes);
                TxtEstimatedSize.Text = string.Format(I18n.T(key), $"~{formatted}");
                return;
            }

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
        if (TxtEstimatedSize == null || BorderEstimatedSize?.Visibility == Visibility.Collapsed) return;

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
        if (_category == "video")
        {
            if (CmbVideoCodec.SelectedItem is CodecItem codecItem)
            {
                SelectedVideoQuality.VideoCodec = codecItem.Id;
            }
            if (CmbEncoder.SelectedItem is EncoderItem encItem)
            {
                SelectedVideoQuality.Encoder = encItem.Id;
            }
            SelectedVideoQuality.RateControl = SelectedRateControl;
            SelectedVideoQuality.VideoQualityCq = (int)SliderVideoCq.Value;
            SelectedVideoQuality.VideoBitrateKbps = (int)SliderVideoBitrate.Value;
            if (CmbAudioBitrate.SelectedItem is BitrateItem abItem)
            {
                SelectedVideoQuality.AudioBitrateKbps = abItem.Value;
            }
            SelectedVideoQuality.AppendQualitySuffix = AppendQualitySuffix;
        }
        else if (_category == "frames")
        {
            if (CmbFramesFormat.SelectedItem is CodecItem item)
            {
                SelectedFramesSetting.ImageFormat = item.Id;
            }
            SelectedFramesSetting.IsRemembered = RememberChoice;
            SelectedFramesSetting.AppendQualitySuffix = AppendQualitySuffix;
        }
        try { DialogResult = true; } catch (InvalidOperationException) { }
        Close();
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        IsResetRequested = true;
        try { DialogResult = true; } catch (InvalidOperationException) { }
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
        {
            BtnCancel_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        try { DialogResult = false; } catch (InvalidOperationException) { }
        Close();
    }
}
