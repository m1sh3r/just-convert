using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using JustConvert.Core;
using JustConvert.Core.Converters.Tools;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public partial class PresetEditorDialog : FluentWindow
{
    private readonly bool _isEdit;
    private bool _isUpdating;

    public CustomPreset Preset { get; private set; }

    private record ComboItem(string Id, string DisplayName);
    private record BitrateItem(int Value, string DisplayName);

    public PresetEditorDialog() : this(null, false)
    {
    }

    public PresetEditorDialog(CustomPreset? initial, bool isNew = false)
    {
        _isUpdating = true;
        _isEdit = initial != null && !isNew;
        if (initial != null)
        {
            Preset = new CustomPreset
            {
                Id = isNew ? Guid.NewGuid().ToString("N")[..8] : initial.Id,
                Name = initial.Name,
                Category = initial.Category,
                ContainerFormat = initial.ContainerFormat,
                PresetType = initial.PresetType,
                IsCustomCommand = initial.IsCustomCommand,
                CustomArguments = initial.CustomArguments,
                InputExtensions = initial.InputExtensions,
                Order = initial.Order,
                VideoCodec = initial.VideoCodec,
                Encoder = initial.Encoder,
                RateControl = initial.RateControl,
                VideoQualityCq = initial.VideoQualityCq,
                VideoBitrateKbps = initial.VideoBitrateKbps,
                AudioCodec = initial.AudioCodec,
                AudioBitrateKbps = initial.AudioBitrateKbps,
                ImageQuality = initial.ImageQuality,
                AppendSuffix = initial.AppendSuffix
            };
        }
        else
        {
            Preset = new CustomPreset();
        }

        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            Title = I18n.T("PresetTitleNew");
            AppTitleBar.Title = Title;
            BtnCancel.Content = I18n.T("BtnCancel");
            BtnSave.Content = I18n.T("BtnApply");
            BtnValidateCommand.Content = I18n.T("BtnValidateCommand");
            RadioPresetQuick.Content = I18n.T("PresetTypeQuick");
            RadioPresetTemplate.Content = I18n.T("PresetTypeTemplate");
            RadioModeStandard.Content = I18n.T("PresetModeStandard");
            RadioModeCustom.Content = I18n.T("PresetModeCustom");
            RadioExtAll.Content = I18n.T("InputExtensionsAll");
            RadioExtCustom.Content = I18n.T("InputExtensionsCustom");
            CmbCategory.ItemsSource = new[] { "Видео", "Аудио", "Изображения" };
            CmbCategory.SelectedIndex = 0;
            CmbContainer.ItemsSource = new[] { "MP4 (.mp4)", "MKV (.mkv)", "WebM (.webm)" };
            CmbContainer.SelectedIndex = 0;
            CmbVideoCodec.ItemsSource = new[] { "H.264 / AVC", "H.265 / HEVC", "AV1" };
            CmbVideoCodec.SelectedIndex = 0;
            CmbEncoder.ItemsSource = new[] { "CPU (libx264)", "NVIDIA NVENC" };
            CmbEncoder.SelectedIndex = 0;
            CmbAudioBitrate.ItemsSource = new[] { "128 kbps", "192 kbps", "256 kbps", "320 kbps" };
            CmbAudioBitrate.SelectedIndex = 1;
            return;
        }

        ApplicationThemeManager.ApplySystemTheme();
        ApplicationAccentColorManager.ApplySystemAccent();
        ApplicationThemeManager.Apply(this);
        SystemThemeWatcher.Watch(this);

        Title = _isEdit ? I18n.T("PresetTitleEdit") : I18n.T("PresetTitleNew");
        AppTitleBar.Title = Title;

        InitUI();
    }

    private void InitUI()
    {
        _isUpdating = true;

        TxtPresetName.Text = Preset.Name;
        ChkAppendSuffix.IsChecked = Preset.AppendSuffix;

        if (string.Equals(Preset.PresetType, "template", StringComparison.OrdinalIgnoreCase))
        {
            RadioPresetTemplate.IsChecked = true;
        }
        else
        {
            RadioPresetQuick.IsChecked = true;
        }

        if (Preset.IsCustomCommand)
        {
            RadioModeCustom.IsChecked = true;
        }
        else
        {
            RadioModeStandard.IsChecked = true;
        }

        TxtCustomArgs.Text = Preset.CustomArguments;

        if (!string.IsNullOrWhiteSpace(Preset.InputExtensions))
        {
            RadioExtCustom.IsChecked = true;
            TxtInputExtensions.Text = Preset.InputExtensions;
            TxtInputExtensions.IsEnabled = true;
        }
        else
        {
            RadioExtAll.IsChecked = true;
            TxtInputExtensions.Text = string.Empty;
            TxtInputExtensions.IsEnabled = false;
        }

        CmbCategory.ItemsSource = new List<ComboItem>
        {
            new("video", I18n.T("CategoryVideo")),
            new("audio", I18n.T("CategoryAudio")),
            new("image", I18n.T("CategoryImages"))
        };
        CmbCategory.DisplayMemberPath = nameof(ComboItem.DisplayName);
        CmbCategory.SelectedValuePath = nameof(ComboItem.Id);

        var selCat = Preset.Category.ToLowerInvariant();
        CmbCategory.SelectedValue = selCat;

        PopulateContainers(selCat);
        PopulateCategoryPanels(selCat);

        _isUpdating = false;
        UpdateCategoryDisplay(selCat);
        UpdateModeDisplay();
        UpdateCommandPreview();
    }

    private void PopulateContainers(string category)
    {
        var containers = category switch
        {
            "audio" => new List<ComboItem>
            {
                new("mp3", "MP3"),
                new("aac", "AAC"),
                new("m4a", "M4A"),
                new("wav", "WAV"),
                new("flac", "FLAC"),
                new("ogg", "OGG"),
                new("opus", "OPUS")
            },
            "image" => new List<ComboItem>
            {
                new("jpg", "JPEG"),
                new("png", "PNG"),
                new("webp", "WEBP"),
                new("avif", "AVIF"),
                new("jp2", "JPEG 2000")
            },
            _ => new List<ComboItem>
            {
                new("mp4", "MP4"),
                new("webm", "WEBM"),
                new("mkv", "MKV"),
                new("mov", "MOV")
            }
        };

        CmbContainer.ItemsSource = containers;
        CmbContainer.DisplayMemberPath = nameof(ComboItem.DisplayName);
        CmbContainer.SelectedValuePath = nameof(ComboItem.Id);

        var match = containers.FirstOrDefault(c => string.Equals(c.Id, Preset.ContainerFormat, StringComparison.OrdinalIgnoreCase)) ?? containers[0];
        CmbContainer.SelectedItem = match;
    }

    private void PopulateCategoryPanels(string category)
    {
        if (category == "video")
        {
            var codecs = new List<ComboItem>
            {
                new("h264", I18n.T("CodecH264")),
                new("h265", I18n.T("CodecH265")),
                new("av1", I18n.T("CodecAv1")),
                new("vp9", I18n.T("CodecVp9")),
                new("prores422", I18n.T("CodecProRes")),
                new("copy", I18n.T("CodecCopy"))
            };
            CmbVideoCodec.ItemsSource = codecs;
            CmbVideoCodec.DisplayMemberPath = nameof(ComboItem.DisplayName);
            CmbVideoCodec.SelectedValuePath = nameof(ComboItem.Id);
            var matchCodec = codecs.FirstOrDefault(c => string.Equals(c.Id, Preset.VideoCodec, StringComparison.OrdinalIgnoreCase)) ?? codecs[0];
            CmbVideoCodec.SelectedItem = matchCodec;

            PopulateEncoders();
            PopulateRateControls();

            SliderVideoCq.Value = Math.Clamp(Preset.VideoQualityCq, 15, 35);
            TxtVideoCq.Text = $"CQ {Preset.VideoQualityCq}";

            SliderVideoBitrate.Value = Math.Clamp(Preset.VideoBitrateKbps, 500, 50000);
            TxtVideoBitrate.Text = $"{Preset.VideoBitrateKbps} {I18n.T("BitrateUnitKbps")}";

            UpdateVideoRateControlVisibility();

            var bitrates = new List<BitrateItem>
            {
                new(128, "128 " + I18n.T("UnitKB") + "/s"),
                new(192, "192 " + I18n.T("UnitKB") + "/s"),
                new(256, "256 " + I18n.T("UnitKB") + "/s"),
                new(320, "320 " + I18n.T("UnitKB") + "/s")
            };
            CmbAudioBitrate.ItemsSource = bitrates;
            CmbAudioBitrate.DisplayMemberPath = nameof(BitrateItem.DisplayName);
            CmbAudioBitrate.SelectedValuePath = nameof(BitrateItem.Value);
            var matchBitrate = bitrates.FirstOrDefault(b => b.Value == Preset.AudioBitrateKbps) ?? bitrates[1];
            CmbAudioBitrate.SelectedItem = matchBitrate;
        }
        else if (category == "audio")
        {
            SliderAudioBitrate.Value = Math.Clamp(Preset.AudioBitrateKbps, 64, 320);
            TxtAudioBitrate.Text = $"{Preset.AudioBitrateKbps} " + I18n.T("UnitKB") + "/s";
        }
        else
        {
            SliderImageQuality.Value = Math.Clamp(Preset.ImageQuality, 1, 100);
            TxtImageQuality.Text = $"{Preset.ImageQuality}%";
        }
    }

    private void PopulateEncoders()
    {
        var currentCodec = (CmbVideoCodec.SelectedItem as ComboItem)?.Id ?? Preset.VideoCodec;
        var encoders = new List<ComboItem>
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
        CmbEncoder.DisplayMemberPath = nameof(ComboItem.DisplayName);
        CmbEncoder.SelectedValuePath = nameof(ComboItem.Id);

        var match = encoders.FirstOrDefault(e => string.Equals(e.Id, Preset.Encoder, StringComparison.OrdinalIgnoreCase)) ?? encoders[0];
        CmbEncoder.SelectedItem = match;
    }

    private void PopulateRateControls()
    {
        var items = new List<ComboItem>
        {
            new("cq", I18n.T("RateControlCq")),
            new("vbr", I18n.T("RateControlVbr")),
            new("cbr", I18n.T("RateControlCbr"))
        };

        CmbRateControl.ItemsSource = items;
        CmbRateControl.DisplayMemberPath = nameof(ComboItem.DisplayName);
        CmbRateControl.SelectedValuePath = nameof(ComboItem.Id);

        var rc = Preset.RateControl?.ToLowerInvariant() ?? "cq";
        var match = items.FirstOrDefault(i => i.Id.Equals(rc, StringComparison.OrdinalIgnoreCase)) ?? items[0];
        CmbRateControl.SelectedItem = match;
    }

    private void UpdateVideoRateControlVisibility()
    {
        var isSpecial = Preset.VideoCodec is "copy" or "prores422" or "prores4444";
        if (GridRateControl != null) GridRateControl.Visibility = isSpecial ? Visibility.Collapsed : Visibility.Visible;

        if (isSpecial)
        {
            if (PanelVideoCq != null) PanelVideoCq.Visibility = Visibility.Collapsed;
            if (PanelVideoBitrate != null) PanelVideoBitrate.Visibility = Visibility.Collapsed;
            return;
        }

        var rc = (Preset.RateControl ?? "cq").ToLowerInvariant();
        if (rc is "vbr" or "cbr")
        {
            if (PanelVideoCq != null) PanelVideoCq.Visibility = Visibility.Collapsed;
            if (PanelVideoBitrate != null)
            {
                PanelVideoBitrate.Visibility = Visibility.Visible;
                if (TxtVideoBitrate != null && SliderVideoBitrate != null)
                {
                    TxtVideoBitrate.Text = $"{(int)SliderVideoBitrate.Value} {I18n.T("BitrateUnitKbps")}";
                }
            }
        }
        else
        {
            if (PanelVideoCq != null) PanelVideoCq.Visibility = Visibility.Visible;
            if (PanelVideoBitrate != null) PanelVideoBitrate.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateCategoryDisplay(string category)
    {
        if (PanelVideoSettings == null || PanelAudioSettings == null || PanelImageSettings == null) return;
        PanelVideoSettings.Visibility = category == "video" ? Visibility.Visible : Visibility.Collapsed;
        PanelAudioSettings.Visibility = category == "audio" ? Visibility.Visible : Visibility.Collapsed;
        PanelImageSettings.Visibility = category == "image" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateModeDisplay()
    {
        if (PanelStandardSettings == null || PanelCustomSettings == null) return;
        var isCustom = RadioModeCustom?.IsChecked == true;
        PanelStandardSettings.Visibility = isCustom ? Visibility.Collapsed : Visibility.Visible;
        PanelCustomSettings.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateCommandPreview()
    {
        if (TxtCommandPreview == null) return;
        var container = (CmbContainer?.SelectedItem as ComboItem)?.Id ?? Preset?.ContainerFormat ?? "mp4";
        var args = TxtCustomArgs?.Text?.Trim() ?? string.Empty;
        var argsStr = string.IsNullOrWhiteSpace(args) ? "[arguments]" : args;
        TxtCommandPreview.Text = $"ffmpeg -i \"<input>\" {argsStr} \"<output>.{container}\"";
    }

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || Preset == null) return;
        var cat = (CmbCategory.SelectedItem as ComboItem)?.Id ?? "video";
        Preset.Category = cat;
        _isUpdating = true;
        PopulateContainers(cat);
        PopulateCategoryPanels(cat);
        _isUpdating = false;
        UpdateCategoryDisplay(cat);
        UpdateCommandPreview();
    }

    private void OnContainerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || Preset == null) return;
        UpdateCommandPreview();
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        UpdateModeDisplay();
    }

    private void OnInputExtChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating || TxtInputExtensions == null) return;
        TxtInputExtensions.IsEnabled = RadioExtCustom.IsChecked == true;
    }

    private void OnCustomArgsChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;
        UpdateCommandPreview();
    }

    private async void BtnValidateCommand_Click(object sender, RoutedEventArgs e)
    {
        var args = TxtCustomArgs.Text?.Trim() ?? string.Empty;
        var cat = (CmbCategory.SelectedItem as ComboItem)?.Id ?? "video";

        BtnValidateCommand.IsEnabled = false;
        InfoBarValidation.IsOpen = true;
        InfoBarValidation.Severity = InfoBarSeverity.Informational;
        InfoBarValidation.Message = I18n.T("PresetValidating");

        try
        {
            var res = await CustomPresetValidator.ValidateFfmpegArgumentsAsync(args, cat);
            InfoBarValidation.Severity = res.IsValid ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            InfoBarValidation.Message = res.Message;
        }
        finally
        {
            BtnValidateCommand.IsEnabled = true;
        }
    }

    private void OnVideoCodecChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || Preset == null) return;
        var codec = (CmbVideoCodec.SelectedItem as ComboItem)?.Id ?? "h264";
        Preset.VideoCodec = codec;
        _isUpdating = true;
        PopulateEncoders();
        _isUpdating = false;
        UpdateVideoRateControlVisibility();
    }

    private void OnRateControlChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || Preset == null) return;
        var rc = (CmbRateControl.SelectedItem as ComboItem)?.Id ?? "cq";
        Preset.RateControl = rc;
        UpdateVideoRateControlVisibility();
    }

    private void OnVideoCqChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating || Preset == null || TxtVideoCq == null) return;
        var val = (int)e.NewValue;
        Preset.VideoQualityCq = val;
        TxtVideoCq.Text = $"CQ {val}";
    }

    private void OnVideoBitrateChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating || Preset == null || TxtVideoBitrate == null) return;
        var val = (int)e.NewValue;
        Preset.VideoBitrateKbps = val;
        TxtVideoBitrate.Text = $"{val} {I18n.T("BitrateUnitKbps")}";
    }

    private void OnAudioBitrateChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating || Preset == null || TxtAudioBitrate == null) return;
        var val = (int)e.NewValue;
        Preset.AudioBitrateKbps = val;
        TxtAudioBitrate.Text = $"{val} " + I18n.T("UnitKB") + "/s";
    }

    private void OnImageQualityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating || Preset == null || TxtImageQuality == null) return;
        var val = (int)e.NewValue;
        Preset.ImageQuality = val;
        TxtImageQuality.Text = $"{val}%";
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtPresetName.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = I18n.T("PresetTitleNew");
        }

        Preset.Name = name;
        Preset.Category = (CmbCategory.SelectedItem as ComboItem)?.Id ?? "video";
        Preset.ContainerFormat = (CmbContainer.SelectedItem as ComboItem)?.Id ?? "mp4";
        Preset.PresetType = RadioPresetQuick.IsChecked == true ? "quick" : "template";
        Preset.IsCustomCommand = RadioModeCustom.IsChecked == true;
        Preset.CustomArguments = TxtCustomArgs.Text?.Trim() ?? string.Empty;
        Preset.InputExtensions = RadioExtCustom.IsChecked == true ? TxtInputExtensions.Text?.Trim() ?? string.Empty : string.Empty;
        Preset.AppendSuffix = ChkAppendSuffix.IsChecked == true;

        if (Preset.Category == "video")
        {
            Preset.VideoCodec = (CmbVideoCodec.SelectedItem as ComboItem)?.Id ?? "h264";
            Preset.Encoder = (CmbEncoder.SelectedItem as ComboItem)?.Id ?? "auto";
            Preset.RateControl = (CmbRateControl.SelectedItem as ComboItem)?.Id ?? "cq";
            Preset.VideoQualityCq = (int)SliderVideoCq.Value;
            Preset.VideoBitrateKbps = (int)SliderVideoBitrate.Value;
            if (CmbAudioBitrate.SelectedItem is BitrateItem b)
            {
                Preset.AudioBitrateKbps = b.Value;
            }
        }
        else if (Preset.Category == "audio")
        {
            Preset.AudioBitrateKbps = (int)SliderAudioBitrate.Value;
        }
        else
        {
            Preset.ImageQuality = (int)SliderImageQuality.Value;
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
