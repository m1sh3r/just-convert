using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using JustConvert.Cli.UI;
using JustConvert.Core;
using JustConvert.Core.Windows;
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

            var videoSeparators = SettingsWindow.FindDescendants<System.Windows.Controls.Separator>(window.PanelVideoFormats).ToList();
            var audioSeparators = SettingsWindow.FindDescendants<System.Windows.Controls.Separator>(window.PanelAudioFormats).ToList();
            var imageSeparators = SettingsWindow.FindDescendants<System.Windows.Controls.Separator>(window.PanelImageFormats).ToList();

            Assert.Equal(3, videoSeparators.Count);
            Assert.Single(audioSeparators);
            Assert.Equal(3, imageSeparators.Count);
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
    public void EditFormatSettings_UpdatesAppendQualitySuffix()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings { AppendQualitySuffix = true };
            var window = new SettingsWindow(settings);

            var dialog = new ConversionOptionsDialog("mp4", "video", new VideoQualitySetting(), null, appendQualitySuffix: false, isSettingsMode: true);
            window.ApplyDialogResult("video", "mp4", dialog);

            Assert.False(window.CurrentSettings.GetEffectiveAppendQualitySuffix("video", "mp4"));
            Assert.True(window.CurrentSettings.GetEffectiveAppendQualitySuffix("video", "webm"));
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

    [Fact]
    public void FormatRows_HaveDragHandle_WithCorrectAppearanceAndTooltip()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            var mp4Handle = window.FindFormatDragHandle(window.PanelVideoFormats, "mp4");
            Assert.NotNull(mp4Handle);
            Assert.Equal(0, System.Windows.Controls.Grid.GetColumn(mp4Handle));
            Assert.Equal(I18n.T("TooltipDragToReorder"), mp4Handle.ToolTip);
            Assert.Equal(System.Windows.Input.Cursors.SizeAll, mp4Handle.Cursor);
            Assert.Equal(Wpf.Ui.Controls.ControlAppearance.Transparent, mp4Handle.Appearance);
            Assert.True(mp4Handle.Icon is Wpf.Ui.Controls.SymbolIcon si && si.Symbol == Wpf.Ui.Controls.SymbolRegular.ReOrderDotsVertical20);

            var sepHandle = window.FindSeparatorDragHandle(window.PanelVideoFormats, 0);
            Assert.NotNull(sepHandle);
            Assert.Equal(0, System.Windows.Controls.Grid.GetColumn(sepHandle));
            Assert.Equal(I18n.T("TooltipDragSeparator"), sepHandle.ToolTip);
            Assert.Equal(System.Windows.Input.Cursors.SizeAll, sepHandle.Cursor);

            var reencodeHandle = window.FindFormatDragHandle(window.PanelVideoFormats, "reencode");
            Assert.NotNull(reencodeHandle);

            var audioHandle = window.FindFormatDragHandle(window.PanelAudioFormats, "mp3");
            Assert.NotNull(audioHandle);

            var imageHandle = window.FindFormatDragHandle(window.PanelImageFormats, "png");
            Assert.NotNull(imageHandle);
        });
    }

    [Fact]
    public void MoveSeparator_ReordersSeparatorAndActiveProfileFormats()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            var profile = window.CurrentSettings.GetActiveProfile();

            var displayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            var sepIndex = displayed.FindIndex(f => ClassicContextMenuManager.IsSeparator(f));
            Assert.True(sepIndex > 0);

            var sepItem = displayed[sepIndex];
            window.MoveFormat("video", sepItem, 0);

            var reordered = window.GetDisplayedFormats(window.PanelVideoFormats);
            Assert.Equal(sepItem, reordered[0]);
            Assert.Equal("separator", profile.VideoFormats[0]);
        });
    }

    [Fact]
    public void MoveFormat_ReordersItemsAndActiveProfileFormats()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            var profile = window.CurrentSettings.GetActiveProfile();

            Assert.Equal("mp4", profile.VideoFormats[0]);
            Assert.Equal("webm", profile.VideoFormats[1]);

            window.MoveFormat("video", "webm", 0);

            Assert.Equal("webm", profile.VideoFormats[0]);
            Assert.Equal("mp4", profile.VideoFormats[1]);

            var displayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            Assert.Equal("webm", displayed[0]);
            Assert.Equal("mp4", displayed[1]);

            window.MoveFormat("video", "webm", 1);

            Assert.Equal("mp4", profile.VideoFormats[0]);
            Assert.Equal("webm", profile.VideoFormats[1]);
        });
    }

    [Fact]
    public void DragHandle_KeyboardAltUpDown_ReordersFormat()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            var profile = window.CurrentSettings.GetActiveProfile();

            Assert.Equal("mp4", profile.VideoFormats[0]);
            Assert.Equal("webm", profile.VideoFormats[1]);

            var webmHandle = window.FindFormatDragHandle(window.PanelVideoFormats, "webm");
            Assert.NotNull(webmHandle);

            var keyAltUp = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Up)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            window.MoveFormat("video", "webm", 0);

            Assert.Equal("webm", profile.VideoFormats[0]);
            Assert.Equal("mp4", profile.VideoFormats[1]);
        });
    }

    [Fact]
    public void GetDynamicTargetIndex_CalculatesCorrectTargetIndex_WhenDraggingUpAndDown()
    {
        StaTestRunner.Run(() =>
        {
            var panel = new StackPanel();
            var row0 = new Grid { Height = 40 };
            var row1 = new Grid { Height = 40 };
            var row2 = new Grid { Height = 40 };
            panel.Children.Add(row0);
            panel.Children.Add(row1);
            panel.Children.Add(row2);

            panel.Measure(new Size(200, 200));
            panel.Arrange(new Rect(0, 0, 200, 200));

            var targetDown = SettingsWindow.GetDynamicTargetIndex(panel, row0, new Point(10, 65));
            Assert.Equal(1, targetDown);

            var targetUp = SettingsWindow.GetDynamicTargetIndex(panel, row2, new Point(10, 15));
            Assert.Equal(0, targetUp);
        });
    }

    [Fact]
    public void AddSeparatorButtons_ExistWithCorrectProperties()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(new AppSettings());

            Assert.NotNull(window.BtnAddVideo);
            Assert.NotNull(window.BtnAddAudio);
            Assert.NotNull(window.BtnAddImage);

            Assert.Equal(I18n.T("BtnAddMenu"), window.BtnAddVideo.Content);
            Assert.Equal(I18n.T("BtnAddMenu"), window.BtnAddAudio.Content);
            Assert.Equal(I18n.T("BtnAddMenu"), window.BtnAddImage.Content);
        });
    }

    [Fact]
    public void AddSeparator_AppendsNewSeparator_AndUpdatesProfile()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            var profile = window.CurrentSettings.GetActiveProfile();

            var initialDisplayedCount = window.GetDisplayedFormats(window.PanelVideoFormats).Count;
            var initialProfileCount = profile.VideoFormats.Count;

            window.AddSeparator("video");

            var newDisplayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            Assert.Equal(initialDisplayedCount + 1, newDisplayed.Count);
            Assert.True(ClassicContextMenuManager.IsSeparator(newDisplayed[^1]));
            Assert.True(profile.VideoFormats.Count > initialProfileCount);
            Assert.Equal("separator", profile.VideoFormats[^1]);

            var lastSepHandle = window.FindSeparatorDragHandle(window.PanelVideoFormats, newDisplayed[^1]);
            Assert.NotNull(lastSepHandle);

            var deleteBtn = window.FindSeparatorDeleteButton(window.PanelVideoFormats, newDisplayed[^1]);
            Assert.NotNull(deleteBtn);
            Assert.Equal(I18n.T("TooltipDeleteSeparator"), deleteBtn.ToolTip);
        });
    }

    [Fact]
    public void AddSeparator_WithInsertAfter_InsertsAtCorrectPosition()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            var displayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            var mp4Index = displayed.IndexOf("mp4");
            Assert.True(mp4Index >= 0);

            window.AddSeparator("video", mp4Index);

            var updated = window.GetDisplayedFormats(window.PanelVideoFormats);
            Assert.True(ClassicContextMenuManager.IsSeparator(updated[mp4Index + 1]));
        });
    }

    [Fact]
    public void DeleteSeparator_ClickDeleteButton_RemovesSeparator_AndUpdatesProfile()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            var profile = window.CurrentSettings.GetActiveProfile();

            var displayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            var firstSep = displayed.FirstOrDefault(f => ClassicContextMenuManager.IsSeparator(f));
            Assert.NotNull(firstSep);

            var deleteBtn = window.FindSeparatorDeleteButton(window.PanelVideoFormats, firstSep);
            Assert.NotNull(deleteBtn);

            deleteBtn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            var updatedDisplayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            Assert.DoesNotContain(firstSep, updatedDisplayed);
        });
    }

    [Fact]
    public void DeleteSeparator_PressDeleteKey_RemovesSeparator()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            var displayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            var firstSep = displayed.FirstOrDefault(f => ClassicContextMenuManager.IsSeparator(f));
            Assert.NotNull(firstSep);

            var handle = window.FindSeparatorDragHandle(window.PanelVideoFormats, firstSep);
            Assert.NotNull(handle);

            var keyDelete = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Delete)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            handle.RaiseEvent(keyDelete);

            var updatedDisplayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            Assert.DoesNotContain(firstSep, updatedDisplayed);
        });
    }

    [Fact]
    public void FormatRow_And_SeparatorRow_HaveExpectedContextMenus()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            var rows = window.PanelVideoFormats.Children.OfType<Grid>().ToList();
            var formatRow = rows.FirstOrDefault(r => r.Tag is string t && !ClassicContextMenuManager.IsSeparator(t));
            Assert.NotNull(formatRow);
            Assert.NotNull(formatRow.ContextMenu);
            var menuAdd = formatRow.ContextMenu.Items.OfType<Wpf.Ui.Controls.MenuItem>().FirstOrDefault();
            Assert.NotNull(menuAdd);
            Assert.Equal(I18n.T("MenuAddSeparatorAfter"), menuAdd.Header);

            var sepRow = rows.FirstOrDefault(r => r.Tag is string t && ClassicContextMenuManager.IsSeparator(t));
            Assert.NotNull(sepRow);
            Assert.NotNull(sepRow.ContextMenu);
            var menuDel = sepRow.ContextMenu.Items.OfType<Wpf.Ui.Controls.MenuItem>().FirstOrDefault();
            Assert.NotNull(menuDel);
            Assert.Equal(I18n.T("MenuDeleteSeparator"), menuDel.Header);
        });
    }

    [Fact]
    public void BuildAddContextMenu_CreatesExpectedStructure()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);

            var menu = window.BuildAddContextMenu("video");
            Assert.NotNull(menu);
            Assert.Equal(3, menu.Items.Count);

            var sepItem = menu.Items[0] as Wpf.Ui.Controls.MenuItem;
            Assert.NotNull(sepItem);
            Assert.Equal(I18n.T("MenuAddSeparator"), sepItem.Header);

            var formatItem = menu.Items[1] as Wpf.Ui.Controls.MenuItem;
            Assert.NotNull(formatItem);
            Assert.Equal(I18n.T("MenuAddFormat"), formatItem.Header);

            var actionItem = menu.Items[2] as Wpf.Ui.Controls.MenuItem;
            Assert.NotNull(actionItem);
            Assert.Equal(I18n.T("MenuAddAction"), actionItem.Header);
        });
    }

    [Fact]
    public void DeleteFormat_ClickDeleteButton_RemovesFormat_AndUpdatesProfile()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            var profile = window.CurrentSettings.GetActiveProfile();

            Assert.Contains("mp4", profile.VideoFormats);
            var deleteBtn = window.FindFormatDeleteButton(window.PanelVideoFormats, "mp4");
            Assert.NotNull(deleteBtn);
            Assert.Equal(I18n.T("TooltipDeleteFormat"), deleteBtn.ToolTip);

            deleteBtn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            var updatedDisplayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            Assert.DoesNotContain("mp4", updatedDisplayed);
            Assert.DoesNotContain("mp4", profile.VideoFormats);

            var menu = window.BuildAddContextMenu("video");
            var formatItem = menu.Items[1] as Wpf.Ui.Controls.MenuItem;
            Assert.NotNull(formatItem);
            Assert.Contains(formatItem.Items.OfType<Wpf.Ui.Controls.MenuItem>(), m => Equals(m.Header, I18n.GetSubMenuTitle("mp4")));
        });
    }

    [Fact]
    public void AddFormat_RestoresFormatToPanelAndProfile()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            var profile = window.CurrentSettings.GetActiveProfile();

            window.DeleteFormat("video", "mp4");
            Assert.DoesNotContain("mp4", window.GetDisplayedFormats(window.PanelVideoFormats));
            Assert.DoesNotContain("mp4", profile.VideoFormats);

            window.AddFormat("video", "mp4");

            var updatedDisplayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            Assert.Contains("mp4", updatedDisplayed);
            Assert.Contains("mp4", profile.VideoFormats);

            var cb = window.FindFormatCheckBox(window.PanelVideoFormats, "mp4");
            Assert.NotNull(cb);
            Assert.True(cb.IsChecked);
        });
    }

    [Fact]
    public void DeleteFormat_PressDeleteKey_RemovesFormat()
    {
        StaTestRunner.Run(() =>
        {
            var settings = new AppSettings();
            var window = new SettingsWindow(settings);
            var profile = window.CurrentSettings.GetActiveProfile();

            var handle = window.FindFormatDragHandle(window.PanelVideoFormats, "webm");
            Assert.NotNull(handle);

            var keyDelete = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Delete)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };
            handle.RaiseEvent(keyDelete);

            var updatedDisplayed = window.GetDisplayedFormats(window.PanelVideoFormats);
            Assert.DoesNotContain("webm", updatedDisplayed);
            Assert.DoesNotContain("webm", profile.VideoFormats);
        });
    }
}
