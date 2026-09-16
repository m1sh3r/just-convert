using System.Windows;
using System.Windows.Controls.Primitives;
using JustConvert.Cli.UI;
using JustConvert.Core;
using Xunit;

namespace JustConvert.Tests;

public class UiPresetEditorAndInputTests
{
    [Fact]
    public void InputDialog_Construct_InitializesPromptAndDefaultValue()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new InputDialog("Enter profile name:", "Rename Profile", "Standard");

            Assert.Equal("Enter profile name:", dialog.TxtPrompt.Text);
            Assert.Equal("Standard", dialog.TxtInput.Text);
            Assert.Equal("Standard", dialog.InputText);
            Assert.Equal("Rename Profile", dialog.Title);
            Assert.Equal("Rename Profile", dialog.AppTitleBar.Title);
        });
    }

    [Fact]
    public void InputDialog_InputText_TrimsWhitespace()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new InputDialog("Prompt", "Title");
            dialog.TxtInput.Text = "   Custom Profile Name   ";

            Assert.Equal("Custom Profile Name", dialog.InputText);
        });
    }

    [Fact]
    public void InputDialog_ClickOkAndCancel_ClosesWindow()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new InputDialog("Prompt", "Title", "Initial");
            dialog.BtnOk.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            var cancelDialog = new InputDialog("Prompt", "Title", "Initial");
            cancelDialog.BtnCancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
    }

    [Fact]
    public void PresetEditorDialog_Construct_NewPreset_InitializesVideoDefaults()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new PresetEditorDialog(null);

            Assert.NotNull(dialog.Preset);
            Assert.Equal(I18n.T("PresetTitleNew"), dialog.Title);
            Assert.Equal("video", dialog.CmbCategory.SelectedValue);
            Assert.Equal(Visibility.Visible, dialog.PanelVideoSettings.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelAudioSettings.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelImageSettings.Visibility);
            Assert.Equal(dialog.Preset.VideoQualityCq, (int)dialog.SliderVideoCq.Value);
            Assert.Equal($"CQ {dialog.Preset.VideoQualityCq}", dialog.TxtVideoCq.Text);
        });
    }

    [Fact]
    public void PresetEditorDialog_Construct_ExistingAudioPreset_PopulatesControls()
    {
        StaTestRunner.Run(() =>
        {
            var initial = new CustomPreset
            {
                Name = "Podcast Voice",
                Category = "audio",
                ContainerFormat = "mp3",
                AudioBitrateKbps = 256,
                AppendSuffix = false
            };

            var dialog = new PresetEditorDialog(initial);

            Assert.Equal(I18n.T("PresetTitleEdit"), dialog.Title);
            Assert.Equal("Podcast Voice", dialog.TxtPresetName.Text);
            Assert.Equal("audio", dialog.CmbCategory.SelectedValue);
            Assert.Equal(Visibility.Visible, dialog.PanelAudioSettings.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelVideoSettings.Visibility);
            Assert.Equal(256, (int)dialog.SliderAudioBitrate.Value);
            Assert.False(dialog.ChkAppendSuffix.IsChecked);
        });
    }

    [Fact]
    public void PresetEditorDialog_SwitchCategory_UpdatesPanelsAndContainers()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new PresetEditorDialog(null);

            dialog.CmbCategory.SelectedValue = "image";

            Assert.Equal(Visibility.Visible, dialog.PanelImageSettings.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelVideoSettings.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelAudioSettings.Visibility);
            Assert.NotNull(dialog.CmbContainer.ItemsSource);

            dialog.CmbCategory.SelectedValue = "audio";

            Assert.Equal(Visibility.Visible, dialog.PanelAudioSettings.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelVideoSettings.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.PanelImageSettings.Visibility);
        });
    }

    [Fact]
    public void PresetEditorDialog_SliderChanges_UpdateProperties()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new PresetEditorDialog(null);

            dialog.SliderVideoCq.Value = 19;
            Assert.Equal(19, dialog.Preset.VideoQualityCq);
            Assert.Equal("CQ 19", dialog.TxtVideoCq.Text);

            dialog.CmbCategory.SelectedValue = "audio";
            dialog.SliderAudioBitrate.Value = 320;
            Assert.Equal(320, dialog.Preset.AudioBitrateKbps);
            Assert.Equal($"320 {I18n.T("UnitKB")}/s", dialog.TxtAudioBitrate.Text);

            dialog.CmbCategory.SelectedValue = "image";
            dialog.SliderImageQuality.Value = 88;
            Assert.Equal(88, dialog.Preset.ImageQuality);
            Assert.Equal("88%", dialog.TxtImageQuality.Text);
        });
    }

    [Fact]
    public void PresetEditorDialog_Save_PopulatesPresetModel()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new PresetEditorDialog(null);

            dialog.TxtPresetName.Text = "  Custom WebM High  ";
            dialog.CmbCategory.SelectedValue = "video";
            dialog.CmbContainer.SelectedValue = "webm";
            dialog.SliderVideoCq.Value = 28;
            dialog.ChkAppendSuffix.IsChecked = true;

            dialog.BtnSave.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal("Custom WebM High", dialog.Preset.Name);
            Assert.Equal("video", dialog.Preset.Category);
            Assert.Equal("webm", dialog.Preset.ContainerFormat);
            Assert.Equal(28, dialog.Preset.VideoQualityCq);
            Assert.True(dialog.Preset.AppendSuffix);
        });
    }

    [Fact]
    public void PresetEditorDialog_Save_WithEmptyName_FallsBackToDefaultTitle()
    {
        StaTestRunner.Run(() =>
        {
            var dialog = new PresetEditorDialog(null);

            dialog.TxtPresetName.Text = "   ";
            dialog.BtnSave.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal(I18n.T("PresetTitleNew"), dialog.Preset.Name);
        });
    }
}
