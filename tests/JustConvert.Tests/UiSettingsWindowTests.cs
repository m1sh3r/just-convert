using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using JustConvert.Cli.UI;
using JustConvert.Core;
using Xunit;

namespace JustConvert.Tests;

public class UiSettingsWindowTests
{
    [Fact]
    public void Construct_InitializesProfilesAndDefaultProfile()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            Assert.NotNull(window.CmbProfiles.ItemsSource);
            Assert.NotNull(window.CmbProfiles.SelectedItem);
            Assert.False(window.BtnRename.IsEnabled);
            Assert.False(window.BtnDelete.IsEnabled);
            Assert.Equal(I18n.T("ProfileReadOnlyNotice"), window.TxtProfileNotice.Text);
            Assert.Equal(I18n.T("SettingsTitle"), window.Title);
        });
    }

    [Fact]
    public void Construct_WithCustomProfile_EnablesRenameAndDeleteButtons()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var customProfile = new MenuProfile
            {
                Id = "custom_profile",
                Name = "My Custom Profile",
                IsReadOnly = false,
                VideoFormats = ["mp4", "webm"],
                AudioFormats = ["mp3"],
                ImageFormats = ["png"]
            };
            settings.Profiles.Add(customProfile);
            settings.ActiveProfileId = "custom_profile";

            var window = new SettingsWindow(settings);

            Assert.True(window.BtnRename.IsEnabled);
            Assert.True(window.BtnDelete.IsEnabled);
            Assert.Equal(I18n.T("ProfileCustomNotice"), window.TxtProfileNotice.Text);
        });
    }

    [Fact]
    public void FormatCheckboxes_ArePopulatedInPanels()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            Assert.NotEmpty(window.PanelVideoFormats.Children);
            Assert.NotEmpty(window.PanelAudioFormats.Children);
            Assert.NotEmpty(window.PanelImageFormats.Children);

            Assert.All(window.PanelVideoFormats.Children.OfType<System.Windows.Controls.CheckBox>(), cb => Assert.NotNull(cb.Tag));
            Assert.All(window.PanelAudioFormats.Children.OfType<System.Windows.Controls.CheckBox>(), cb => Assert.NotNull(cb.Tag));
            Assert.All(window.PanelImageFormats.Children.OfType<System.Windows.Controls.CheckBox>(), cb => Assert.NotNull(cb.Tag));
        });
    }

    [Fact]
    public void QualitySliders_UpdateQualitySettingsAndStatusText()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            window.SliderQualityJpg.Value = 77;

            Assert.True(window.CurrentSettings.TryGetSavedQuality("jpg", out var quality));
            Assert.Equal(77, quality);
            Assert.Equal(string.Format(I18n.T("QualityStatusRemembered"), 77), window.TxtQualityStatusJpg.Text);
        });
    }

    [Fact]
    public void ResetQualityButton_ClearsSavedQuality()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            settings.SetQuality("jpg", 75, true);

            var window = new SettingsWindow(settings);
            Assert.True(window.CurrentSettings.TryGetSavedQuality("jpg", out _));

            window.BtnResetQualityJpg.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.False(window.CurrentSettings.TryGetSavedQuality("jpg", out _));
            Assert.Equal(I18n.T("QualityStatusAsk"), window.TxtQualityStatusJpg.Text);
            Assert.Equal(AppSettings.GetDefaultQuality("jpg"), (int)window.SliderQualityJpg.Value);
        });
    }

    [Fact]
    public void RemuxControls_UpdateRemuxSetting()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            window.CmbSettingsRemuxContainer.SelectedItem = "mkv";
            window.ChkSettingsRemuxFastStart.IsChecked = false;
            window.ChkSettingsRemuxSubtitles.IsChecked = true;

            Assert.Equal("mkv", window.CurrentSettings.RemuxSetting.TargetContainer);
            Assert.False(window.CurrentSettings.RemuxSetting.FastStart);
            Assert.True(window.CurrentSettings.RemuxSetting.CopySubtitles);
        });
    }

    [Fact]
    public void ResetRemuxButton_ClearsRemuxSetting()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            settings.SetRemuxSetting(new RemuxSetting { TargetContainer = "mkv", IsRemembered = true });

            var window = new SettingsWindow(settings);
            window.BtnResetRemux.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.False(window.CurrentSettings.RemuxSetting.IsRemembered);
            Assert.Equal(I18n.T("QualityStatusAsk"), window.TxtRemuxStatus.Text);
        });
    }

    [Fact]
    public void AppendQualitySuffixCheckbox_TogglesSetting()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            window.ChkAppendQualitySuffix.IsChecked = false;
            Assert.False(window.CurrentSettings.AppendQualitySuffix);

            window.ChkAppendQualitySuffix.IsChecked = true;
            Assert.True(window.CurrentSettings.AppendQualitySuffix);
        });
    }

    [Fact]
    public void PresetsList_SelectionTogglesEditAndDeleteButtons()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            settings.CustomPresets.Add(new CustomPreset { Name = "WebM Low", Category = "video" });

            var window = new SettingsWindow(settings);

            Assert.False(window.BtnEditPreset.IsEnabled);
            Assert.False(window.BtnDeletePreset.IsEnabled);

            window.ListPresets.SelectedIndex = 0;
            Assert.True(window.BtnEditPreset.IsEnabled);
            Assert.True(window.BtnDeletePreset.IsEnabled);

            window.ListPresets.SelectedIndex = -1;
            Assert.False(window.BtnEditPreset.IsEnabled);
            Assert.False(window.BtnDeletePreset.IsEnabled);
        });
    }

    [Fact]
    public void TabControl_HasFourExpectedTabs()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            Assert.Equal(4, window.TabsCategory.Items.Count);
        });
    }
}
