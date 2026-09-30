using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
            Assert.True(window.BtnResetDefaults.IsEnabled);
            Assert.Equal(I18n.T("SettingsTitle"), window.Title);
        });
    }

    [Fact]
    public void Construct_AndMeasure_RendersWithoutTemplateErrors()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            window.Measure(new Size(800, 600));
            window.Arrange(new Rect(0, 0, 800, 600));
            Assert.NotNull(window);
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
            Assert.True(window.BtnResetDefaults.IsEnabled);
        });
    }

    [Fact]
    public void ProfileMenu_ConfiguredWithContextMenuAndOpens()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            Assert.NotNull(window.BtnProfileMenu);
            Assert.NotNull(window.ProfileContextMenu);
            Assert.NotNull(window.CmbProfiles.ContextMenu);
            Assert.Same(window.ProfileContextMenu, window.BtnProfileMenu.ContextMenu);

            Assert.False(window.MenuRename.IsEnabled);
            Assert.False(window.MenuDelete.IsEnabled);
            Assert.True(window.MenuResetDefaults.IsEnabled);
            Assert.True(window.MenuDuplicate.IsEnabled);
            Assert.True(window.MenuExport.IsEnabled);
            Assert.True(window.MenuImport.IsEnabled);

            window.BtnProfileMenu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.True(window.ProfileContextMenu.IsOpen);
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

            var videoCbs = SettingsWindow.FindDescendants<System.Windows.Controls.CheckBox>(window.PanelVideoFormats).ToList();
            var audioCbs = SettingsWindow.FindDescendants<System.Windows.Controls.CheckBox>(window.PanelAudioFormats).ToList();
            var imageCbs = SettingsWindow.FindDescendants<System.Windows.Controls.CheckBox>(window.PanelImageFormats).ToList();

            Assert.NotEmpty(videoCbs);
            Assert.NotEmpty(audioCbs);
            Assert.NotEmpty(imageCbs);

            Assert.All(videoCbs, cb =>
            {
                Assert.NotNull(cb.Tag);
                Assert.True(cb.IsEnabled);
            });
            Assert.All(audioCbs, cb =>
            {
                Assert.NotNull(cb.Tag);
                Assert.True(cb.IsEnabled);
            });
            Assert.All(imageCbs, cb =>
            {
                Assert.NotNull(cb.Tag);
                Assert.True(cb.IsEnabled);
            });
        });
    }

    [Fact]
    public void DefaultProfile_FormatCheckboxes_CanBeModified()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            var defaultProfile = window.CurrentSettings.GetActiveProfile();

            var mp4Checkbox = window.FindFormatCheckBox(window.PanelVideoFormats, "mp4");
            Assert.NotNull(mp4Checkbox);
            Assert.True(mp4Checkbox.IsEnabled);
            Assert.True(mp4Checkbox.IsChecked);

            mp4Checkbox.IsChecked = false;
            Assert.DoesNotContain("mp4", defaultProfile.VideoFormats);

            mp4Checkbox.IsChecked = true;
            Assert.Contains("mp4", defaultProfile.VideoFormats);
        });
    }

    [Fact]
    public void ResetProfileToDefaults_RestoresDefaultFormats()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            var profile = window.CurrentSettings.GetActiveProfile();

            profile.VideoFormats.Clear();
            Assert.Empty(profile.VideoFormats);

            window.ResetProfileToDefaults(profile);

            Assert.NotEmpty(profile.VideoFormats);
            Assert.Contains("mp4", profile.VideoFormats);
            Assert.Contains("mp3", profile.AudioFormats);
            Assert.Contains("png", profile.ImageFormats);
        });
    }

    [Fact]
    public void FormatItems_HaveEditButtonsAndStatusForConfigurableFormats()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            var mp4Btn = window.FindFormatEditButton(window.PanelVideoFormats, "mp4");
            var mp4Status = window.FindFormatStatusBlock(window.PanelVideoFormats, "mp4");
            Assert.NotNull(mp4Btn);
            Assert.NotNull(mp4Status);
            Assert.Equal(I18n.T("QualityStatusAsk"), mp4Status.Text);

            var jpgBtn = window.FindFormatEditButton(window.PanelImageFormats, "jpg");
            var jpgStatus = window.FindFormatStatusBlock(window.PanelImageFormats, "jpg");
            Assert.NotNull(jpgBtn);
            Assert.NotNull(jpgStatus);
            Assert.Equal(I18n.T("QualityStatusAsk"), jpgStatus.Text);

            var pngBtn = window.FindFormatEditButton(window.PanelImageFormats, "png");
            var pngStatus = window.FindFormatStatusBlock(window.PanelImageFormats, "png");
            Assert.Null(pngBtn);
            Assert.Null(pngStatus);
        });
    }

    [Fact]
    public void EditFormatSettings_SavesAndResetsVideoQuality()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            var dialog = new ConversionOptionsDialog("mp4", "video", new VideoQualitySetting
            {
                VideoCodec = "h264",
                RateControl = "cq",
                VideoQualityCq = 21,
                IsRemembered = true
            }, null, true, isSettingsMode: true);

            window.EditFormatSettings("video", "mp4", dialog);

            Assert.True(window.CurrentSettings.TryGetSavedVideoQuality("mp4", out var savedVq));
            Assert.Equal(21, savedVq.VideoQualityCq);
            var statusBlock = window.FindFormatStatusBlock(window.PanelVideoFormats, "mp4");
            Assert.NotNull(statusBlock);
            Assert.Equal(string.Format(I18n.T("QualityStatusVideo"), I18n.T("CodecH264"), 21), statusBlock.Text);
        });
    }

    [Fact]
    public void EditFormatSettings_SavesAndResetsImageQuality()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            var dialog = new ConversionOptionsDialog("jpg", "image", 90, null, true, isSettingsMode: true);
            window.EditFormatSettings("image", "jpg", dialog);

            Assert.True(window.CurrentSettings.TryGetSavedQuality("jpg", out var q));
            Assert.Equal(90, q);
            var statusBlock = window.FindFormatStatusBlock(window.PanelImageFormats, "jpg");
            Assert.NotNull(statusBlock);
            Assert.Equal(string.Format(I18n.T("QualityStatusRemembered"), 90), statusBlock.Text);
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
    public void TabControl_HasThreeExpectedTabs()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            Assert.Equal(3, window.TabsCategory.Items.Count);
        });
    }

    [Fact]
    public void NavigationView_HasThreeMenuItemsAndSyncsWithTabsCategory()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            Assert.Equal(3, window.NavView.MenuItems.Count);
            Assert.True(window.NavItemVideo.IsActive);

            window.TabsCategory.SelectedIndex = 1;
            Assert.True(window.NavItemAudio.IsActive);
            Assert.False(window.NavItemVideo.IsActive);

            window.TabsCategory.SelectedIndex = 2;
            Assert.True(window.NavItemImages.IsActive);

            window.NavItemAudio.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(1, window.TabsCategory.SelectedIndex);
            Assert.True(window.NavItemAudio.IsActive);
            Assert.False(window.NavItemImages.IsActive);

            window.NavItemImages.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(2, window.TabsCategory.SelectedIndex);
            Assert.True(window.NavItemImages.IsActive);

            window.NavItemVideo.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(0, window.TabsCategory.SelectedIndex);
            Assert.True(window.NavItemVideo.IsActive);
        });
    }

    [Fact]
    public void SettingsWindow_BtnClose_HasIsCancelSet()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(new AppSettings());
            Assert.True(window.BtnClose.IsCancel);
        });
    }

    [Fact]
    public void SettingsWindow_PressEscape_HandlesEvent()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(new AppSettings());
            var keyArgs = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            window.RaiseEvent(keyArgs);
            Assert.True(keyArgs.Handled);
        });
    }
}
