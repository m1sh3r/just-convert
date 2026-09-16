using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using JustConvert.Core;
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

    public PresetEditorDialog() : this(null)
    {
    }

    public PresetEditorDialog(CustomPreset? initial)
    {
        _isUpdating = true;
        _isEdit = initial != null;
        Preset = initial != null ? new CustomPreset
        {
            Id = initial.Id,
            Name = initial.Name,
            Category = initial.Category,
            ContainerFormat = initial.ContainerFormat,
            VideoCodec = initial.VideoCodec,
            Encoder = initial.Encoder,
            VideoQualityCq = initial.VideoQualityCq,
            AudioCodec = initial.AudioCodec,
            AudioBitrateKbps = initial.AudioBitrateKbps,
            ImageQuality = initial.ImageQuality,
            AppendSuffix = initial.AppendSuffix
        } : new CustomPreset();

        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            Title = I18n.T("PresetTitleNew");
            AppTitleBar.Title = Title;
            BtnCancel.Content = I18n.T("BtnCancel");
            BtnSave.Content = I18n.T("BtnApply");
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

            SliderVideoCq.Value = Math.Clamp(Preset.VideoQualityCq, 15, 35);
            TxtVideoCq.Text = $"CQ {Preset.VideoQualityCq}";

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

    private void UpdateCategoryDisplay(string category)
    {
        PanelVideoSettings.Visibility = category == "video" ? Visibility.Visible : Visibility.Collapsed;
        PanelAudioSettings.Visibility = category == "audio" ? Visibility.Visible : Visibility.Collapsed;
        PanelImageSettings.Visibility = category == "image" ? Visibility.Visible : Visibility.Collapsed;
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
    }

    private void OnVideoCodecChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || Preset == null) return;
        var codec = (CmbVideoCodec.SelectedItem as ComboItem)?.Id ?? "h264";
        Preset.VideoCodec = codec;
        _isUpdating = true;
        PopulateEncoders();
        _isUpdating = false;
    }

    private void OnVideoCqChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating || Preset == null || TxtVideoCq == null) return;
        var val = (int)e.NewValue;
        Preset.VideoQualityCq = val;
        TxtVideoCq.Text = $"CQ {val}";
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
        Preset.AppendSuffix = ChkAppendSuffix.IsChecked == true;

        if (Preset.Category == "video")
        {
            Preset.VideoCodec = (CmbVideoCodec.SelectedItem as ComboItem)?.Id ?? "h264";
            Preset.Encoder = (CmbEncoder.SelectedItem as ComboItem)?.Id ?? "auto";
            Preset.VideoQualityCq = (int)SliderVideoCq.Value;
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
