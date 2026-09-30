using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JustConvert.Core;
using JustConvert.Core.Windows;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace JustConvert.Cli.UI;

public partial class SettingsWindow : FluentWindow
{
    private AppSettings _settings;
    private readonly ConverterRegistry _registry = new();

    private string? _draggedFormat;
    private string? _draggedCategory;
    private Panel? _draggedTargetPanel;
    private Point _draggedStartPoint;
    private Grid? _draggedRow;
    private bool _isDragging;

    private static readonly string[] AllVideoFormats =
    [
        "mp4", "webm", "mkv", "mov", "gif",
        "frames",
        "mp3", "wav", "flac", "aac", "m4a", "opus",
        "reencode"
    ];

    private static readonly string[] AllAudioFormats =
    [
        "mp3", "aac", "m4a", "wav", "flac", "ogg", "opus", "aiff", "reencode"
    ];

    private static readonly string[] AllImageFormats =
    [
        "png", "jpg", "webp", "ico", "bmp", "gif", "jp2", "tiff", "tga", "pcx", "ppm", "avif", "reencode"
    ];

    internal AppSettings CurrentSettings => _settings;

    public Wpf.Ui.Controls.MenuItem BtnRename => MenuRename;
    public Wpf.Ui.Controls.MenuItem BtnDelete => MenuDelete;
    public Wpf.Ui.Controls.MenuItem BtnResetDefaults => MenuResetDefaults;
    public Wpf.Ui.Controls.MenuItem BtnDuplicate => MenuDuplicate;
    public Wpf.Ui.Controls.MenuItem BtnExport => MenuExport;
    public Wpf.Ui.Controls.MenuItem BtnImport => MenuImport;

    public SettingsWindow() : this(AppSettings.Load())
    {
    }

    internal SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        _settings.EnsureDefaultProfile();

        InitializeComponent();

        CmbProfiles.ContextMenu = BtnProfileMenu.ContextMenu;

        Title = I18n.T("SettingsTitle");
        AppTitleBar.Title = Title;

        if (!DesignerProperties.GetIsInDesignMode(this))
        {
            ApplicationThemeManager.ApplySystemTheme();
            ApplicationAccentColorManager.ApplySystemAccent();
            ApplicationThemeManager.Apply(this);
            SystemThemeWatcher.Watch(this);

            RefreshProfilesList();
        }
        else
        {
            _settings = new AppSettings();
            PopulateFormatList(PanelVideoFormats, "video", ["mp4", "webm", "mkv"], ["mp4"]);
            PopulateFormatList(PanelAudioFormats, "audio", ["mp3", "wav", "flac"], ["mp3"]);
            PopulateFormatList(PanelImageFormats, "image", ["png", "jpg", "webp"], ["png"]);
            MenuResetDefaults.Header = I18n.T("BtnResetDefaults");
        }
    }

    private void RefreshProfilesList()
    {
        CmbProfiles.ItemsSource = null;
        CmbProfiles.ItemsSource = _settings.Profiles;
        CmbProfiles.SelectedItem = _settings.GetActiveProfile();
    }

    private void OnProfileMenuClick(object sender, RoutedEventArgs e)
    {
        if (BtnProfileMenu.ContextMenu != null)
        {
            BtnProfileMenu.ContextMenu.PlacementTarget = BtnProfileMenu;
            BtnProfileMenu.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            BtnProfileMenu.ContextMenu.IsOpen = true;
        }
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_settings == null || CmbProfiles.SelectedItem is not MenuProfile profile) return;

        _settings.ActiveProfileId = profile.Id;

        var isDefault = profile.Id == "default";
        MenuRename.IsEnabled = !isDefault;
        MenuDelete.IsEnabled = !isDefault && _settings.Profiles.Count > 1;
        PopulateCategoryPanels(profile);
    }

    private void PopulateCategoryPanels(MenuProfile profile)
    {
        PopulateFormatList(PanelVideoFormats, "video", GetOrderedFormats(profile.VideoFormats, AllVideoFormats, "video"), profile.VideoFormats);
        PopulateFormatList(PanelAudioFormats, "audio", GetOrderedFormats(profile.AudioFormats, AllAudioFormats, "audio"), profile.AudioFormats);
        PopulateFormatList(PanelImageFormats, "image", GetOrderedFormats(profile.ImageFormats, AllImageFormats, "image"), profile.ImageFormats);
    }

    internal static List<string> GetOrderedFormats(IEnumerable<string> profileFormats, IEnumerable<string> allKnownFormats, string? category = null)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sepCounter = 1;

        var profileList = profileFormats.ToList();
        var hasExplicitSeparators = profileList.Any(ClassicContextMenuManager.IsSeparator);

        if (hasExplicitSeparators)
        {
            foreach (var f in profileList)
            {
                var clean = f.TrimStart('.').ToLowerInvariant();
                if (ClassicContextMenuManager.IsSeparator(clean))
                {
                    result.Add($"separator:{sepCounter++}");
                }
                else if (allKnownFormats.Contains(clean, StringComparer.OrdinalIgnoreCase) && seen.Add(clean))
                {
                    result.Add(clean);
                }
            }
            return result;
        }
        else
        {
            int? prevGroup = null;
            foreach (var f in profileList)
            {
                var clean = f.TrimStart('.').ToLowerInvariant();
                if (allKnownFormats.Contains(clean, StringComparer.OrdinalIgnoreCase) && seen.Add(clean))
                {
                    if (category != null)
                    {
                        var group = ClassicContextMenuManager.GetFormatGroup(clean, category);
                        if (prevGroup.HasValue && group != prevGroup.Value)
                        {
                            result.Add($"separator:{sepCounter++}");
                        }
                        prevGroup = group;
                    }
                    result.Add(clean);
                }
            }
        }

        int? lastGroup = null;
        if (category != null && result.Count > 0)
        {
            var lastItem = result.LastOrDefault(x => !ClassicContextMenuManager.IsSeparator(x));
            if (lastItem != null)
            {
                lastGroup = ClassicContextMenuManager.GetFormatGroup(lastItem, category);
            }
        }

        foreach (var format in allKnownFormats)
        {
            var clean = format.TrimStart('.').ToLowerInvariant();
            if (seen.Add(clean))
            {
                if (!hasExplicitSeparators && category != null)
                {
                    var group = ClassicContextMenuManager.GetFormatGroup(clean, category);
                    if (lastGroup.HasValue && group != lastGroup.Value)
                    {
                        result.Add($"separator:{sepCounter++}");
                    }
                    lastGroup = group;
                }
                result.Add(clean);
            }
        }

        return result;
    }

    private void PopulateFormatList(Panel targetPanel, string category, IEnumerable<string> formats, List<string> activeFormats)
    {
        targetPanel.Children.Clear();

        var displayList = formats.Select(f => f.TrimStart('.').ToLowerInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        for (var i = 0; i < displayList.Count; i++)
        {
            var itemKey = displayList[i];
            var isSeparator = ClassicContextMenuManager.IsSeparator(itemKey);

            var row = new Grid
            {
                Margin = isSeparator ? new Thickness(0, 4, 8, 4) : new Thickness(0, 3, 8, 3),
                Tag = itemKey
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dragHandle = new Wpf.Ui.Controls.Button
            {
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.ReOrderDotsVertical20 },
                ToolTip = I18n.T(isSeparator ? "TooltipDragSeparator" : "TooltipDragToReorder"),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
                Cursor = Cursors.SizeAll,
                Tag = itemKey
            };

            AttachDragEvents(dragHandle, row, itemKey, category, targetPanel, displayList, activeFormats);

            Grid.SetColumn(dragHandle, 0);
            row.Children.Add(dragHandle);

            if (isSeparator)
            {
                var sepPanel = new Grid
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                var sepLine = new System.Windows.Controls.Separator
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0)
                };
                sepPanel.Children.Add(sepLine);
                Grid.SetColumn(sepPanel, 1);
                row.Children.Add(sepPanel);

                var actionsStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var placeholder = new Wpf.Ui.Controls.Button
                {
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Edit20 },
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Hidden,
                    Focusable = false,
                    IsHitTestVisible = false
                };
                actionsStack.Children.Add(placeholder);

                var deleteButton = new Wpf.Ui.Controls.Button
                {
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Delete20 },
                    ToolTip = I18n.T("TooltipDeleteSeparator"),
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = itemKey
                };
                deleteButton.Click += (s, _) =>
                {
                    DeleteSeparator(category, itemKey);
                };
                actionsStack.Children.Add(deleteButton);

                Grid.SetColumn(actionsStack, 2);
                row.Children.Add(actionsStack);

                var sepMenu = new ContextMenu();
                var itemDelSep = new Wpf.Ui.Controls.MenuItem
                {
                    Header = I18n.T("MenuDeleteSeparator"),
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Delete20 }
                };
                itemDelSep.Click += (s, _) =>
                {
                    DeleteSeparator(category, itemKey);
                };
                sepMenu.Items.Add(itemDelSep);
                row.ContextMenu = sepMenu;
            }
            else
            {
                var format = itemKey;
                var isConfigurable = HasConfigurableSettings(category, format);

                var contentStack = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var checkBox = new System.Windows.Controls.CheckBox
                {
                    Content = I18n.GetSubMenuTitle(format),
                    IsChecked = activeFormats.Contains(format, StringComparer.OrdinalIgnoreCase),
                    Tag = format
                };

                checkBox.Checked += (s, _) =>
                {
                    SyncActiveFormats(targetPanel, displayList, activeFormats);
                };

                checkBox.Unchecked += (s, _) =>
                {
                    activeFormats.RemoveAll(f => string.Equals(f, format, StringComparison.OrdinalIgnoreCase));
                };

                contentStack.Children.Add(checkBox);

                System.Windows.Controls.TextBlock? statusText = null;
                if (isConfigurable)
                {
                    statusText = new System.Windows.Controls.TextBlock
                    {
                        Text = GetFormatStatus(category, format),
                        FontSize = 11,
                        Foreground = (TryFindResource("TextFillColorSecondaryBrush") as System.Windows.Media.Brush) ?? System.Windows.Media.Brushes.Gray,
                        Margin = new Thickness(26, 2, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Tag = format
                    };
                    contentStack.Children.Add(statusText);
                }

                Grid.SetColumn(contentStack, 1);
                row.Children.Add(contentStack);

                var actionsStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                if (isConfigurable)
                {
                    var editButton = new Wpf.Ui.Controls.Button
                    {
                        Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Edit20 },
                        ToolTip = I18n.T("BtnEditFormatSettings"),
                        Padding = new Thickness(6, 4, 6, 4),
                        Margin = new Thickness(4, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = format
                    };
                    editButton.Click += (s, _) =>
                    {
                        OnEditFormatSettings(category, format, statusText);
                    };
                    actionsStack.Children.Add(editButton);
                }
                else
                {
                    var placeholder = new Wpf.Ui.Controls.Button
                    {
                        Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Edit20 },
                        Padding = new Thickness(6, 4, 6, 4),
                        Margin = new Thickness(4, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Visibility = Visibility.Hidden,
                        Focusable = false,
                        IsHitTestVisible = false
                    };
                    actionsStack.Children.Add(placeholder);
                }

                var deleteButton = new Wpf.Ui.Controls.Button
                {
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Delete20 },
                    ToolTip = I18n.T("TooltipDeleteFormat"),
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = format
                };
                deleteButton.Click += (s, _) =>
                {
                    DeleteFormat(category, format);
                };
                actionsStack.Children.Add(deleteButton);

                Grid.SetColumn(actionsStack, 2);
                row.Children.Add(actionsStack);

                var itemMenu = new ContextMenu();
                var itemAddSep = new Wpf.Ui.Controls.MenuItem
                {
                    Header = I18n.T("MenuAddSeparatorAfter"),
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.DividerShort20 }
                };
                var currentItemKey = itemKey;
                itemAddSep.Click += (s, _) =>
                {
                    var curList = GetDisplayedFormats(targetPanel);
                    var idx = curList.IndexOf(currentItemKey);
                    AddSeparator(category, idx >= 0 ? idx : null);
                };
                itemMenu.Items.Add(itemAddSep);

                var itemDel = new Wpf.Ui.Controls.MenuItem
                {
                    Header = I18n.T("MenuDeleteFormat"),
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Delete20 }
                };
                itemDel.Click += (s, _) =>
                {
                    DeleteFormat(category, currentItemKey);
                };
                itemMenu.Items.Add(itemDel);

                row.ContextMenu = itemMenu;
            }

            targetPanel.Children.Add(row);
        }
    }

    private void AttachDragEvents(
        Wpf.Ui.Controls.Button dragHandle,
        Grid row,
        string itemKey,
        string category,
        Panel targetPanel,
        List<string> displayList,
        List<string> activeFormats)
    {
        var capturedItem = itemKey;

        dragHandle.PreviewMouseLeftButtonDown += (s, e) =>
        {
            _draggedFormat = capturedItem;
            _draggedCategory = category;
            _draggedTargetPanel = targetPanel;
            _draggedStartPoint = e.GetPosition(targetPanel);
            _draggedRow = row;
            _isDragging = false;
            dragHandle.CaptureMouse();
            e.Handled = true;
        };

        dragHandle.PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedFormat == capturedItem && _draggedRow != null)
            {
                var currentPoint = e.GetPosition(targetPanel);
                if (!_isDragging && Math.Abs(currentPoint.Y - _draggedStartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;
                    _draggedRow.Opacity = 0.65;
                }

                if (_isDragging)
                {
                    var fromIndex = targetPanel.Children.IndexOf(_draggedRow);
                    var targetIndex = GetDynamicTargetIndex(targetPanel, _draggedRow, currentPoint);

                    if (targetIndex >= 0 && targetIndex != fromIndex && targetIndex < targetPanel.Children.Count)
                    {
                        targetPanel.Children.RemoveAt(fromIndex);
                        targetPanel.Children.Insert(targetIndex, _draggedRow);

                        var item = displayList[fromIndex];
                        displayList.RemoveAt(fromIndex);
                        displayList.Insert(targetIndex, item);
                    }

                    if (targetPanel.Parent is ScrollViewer scrollViewer)
                    {
                        var mouseInScroll = e.GetPosition(scrollViewer);
                        if (mouseInScroll.Y < 30)
                        {
                            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 8);
                        }
                        else if (mouseInScroll.Y > scrollViewer.ActualHeight - 30)
                        {
                            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 8);
                        }
                    }
                }
            }
        };

        dragHandle.PreviewMouseLeftButtonUp += (s, e) =>
        {
            if (_draggedFormat == capturedItem)
            {
                if (_isDragging)
                {
                    SyncActiveFormats(targetPanel, displayList, activeFormats);
                }
                CleanupDrag();
                dragHandle.ReleaseMouseCapture();
                e.Handled = true;
            }
        };

        dragHandle.LostMouseCapture += (s, e) =>
        {
            if (_draggedFormat == capturedItem)
            {
                if (_isDragging)
                {
                    SyncActiveFormats(targetPanel, displayList, activeFormats);
                }
                CleanupDrag();
            }
        };

        dragHandle.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                if (ClassicContextMenuManager.IsSeparator(capturedItem))
                {
                    DeleteSeparator(category, capturedItem);
                }
                else
                {
                    DeleteFormat(category, capturedItem);
                }
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Alt || Keyboard.Modifiers == ModifierKeys.Control)
            {
                var curIdx = displayList.IndexOf(capturedItem);
                if (e.Key == Key.Up && curIdx > 0)
                {
                    MoveFormatItem(targetPanel, category, displayList, activeFormats, curIdx, -1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down && curIdx < displayList.Count - 1)
                {
                    MoveFormatItem(targetPanel, category, displayList, activeFormats, curIdx, 1);
                    e.Handled = true;
                }
            }
        };
    }

    private void CleanupDrag()
    {
        if (_draggedRow != null)
        {
            _draggedRow.Opacity = 1.0;
            _draggedRow = null;
        }
        _draggedFormat = null;
        _isDragging = false;
        _draggedTargetPanel = null;
    }

    internal static int GetDynamicTargetIndex(Panel targetPanel, FrameworkElement draggedRow, Point mousePos)
    {
        var fromIndex = targetPanel.Children.IndexOf(draggedRow);
        if (fromIndex < 0) return -1;

        var count = targetPanel.Children.Count;
        if (count <= 1) return fromIndex;

        for (int i = count - 1; i > fromIndex; i--)
        {
            if (targetPanel.Children[i] is FrameworkElement row)
            {
                try
                {
                    var top = row.TransformToAncestor(targetPanel).Transform(new Point(0, 0)).Y;
                    var mid = top + (row.ActualHeight / 2.0);
                    if (mousePos.Y > mid)
                    {
                        return i;
                    }
                }
                catch { }
            }
        }

        for (int i = 0; i < fromIndex; i++)
        {
            if (targetPanel.Children[i] is FrameworkElement row)
            {
                try
                {
                    var top = row.TransformToAncestor(targetPanel).Transform(new Point(0, 0)).Y;
                    var mid = top + (row.ActualHeight / 2.0);
                    if (mousePos.Y < mid)
                    {
                        return i;
                    }
                }
                catch { }
            }
        }

        return fromIndex;
    }

    private void MoveFormatItem(Panel targetPanel, string category, List<string> displayList, List<string> activeFormats, int fromIndex, int offset)
    {
        var toIndex = fromIndex + offset;
        if (toIndex < 0 || toIndex >= displayList.Count || fromIndex == toIndex) return;

        var row = targetPanel.Children[fromIndex];
        targetPanel.Children.RemoveAt(fromIndex);
        targetPanel.Children.Insert(toIndex, row);

        var format = displayList[fromIndex];
        displayList.RemoveAt(fromIndex);
        displayList.Insert(toIndex, format);

        SyncActiveFormats(targetPanel, displayList, activeFormats);

        var nextHandle = FindFormatDragHandle(targetPanel, format) ?? FindSeparatorDragHandle(targetPanel, format);
        nextHandle?.Focus();
    }

    private void SyncActiveFormats(Panel targetPanel, List<string> displayList, List<string> activeFormats)
    {
        var activeSet = new HashSet<string>(activeFormats, StringComparer.OrdinalIgnoreCase);
        activeFormats.Clear();
        foreach (var itemKey in displayList)
        {
            if (ClassicContextMenuManager.IsSeparator(itemKey))
            {
                activeFormats.Add("separator");
            }
            else
            {
                var cb = FindFormatCheckBox(targetPanel, itemKey);
                var isChecked = cb != null ? cb.IsChecked == true : activeSet.Contains(itemKey);
                if (isChecked)
                {
                    activeFormats.Add(itemKey);
                }
            }
        }
    }

    private void OnNavItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is NavigationViewItem item && int.TryParse(item.Tag?.ToString(), out var index))
        {
            TabsCategory.SelectedIndex = index;
        }
    }

    private void OnNavViewSelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (TabsCategory == null) return;
        if (sender.SelectedItem is NavigationViewItem item && int.TryParse(item.Tag?.ToString(), out var index))
        {
            if (TabsCategory.SelectedIndex != index)
            {
                TabsCategory.SelectedIndex = index;
            }
        }
    }

    private void OnCategoryTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != TabsCategory) return;
        if (NavView?.MenuItems == null) return;
        var index = TabsCategory.SelectedIndex;
        for (var i = 0; i < NavView.MenuItems.Count; i++)
        {
            if (NavView.MenuItems[i] is NavigationViewItem item)
            {
                item.IsActive = (i == index);
            }
        }
    }

    private void OnDuplicateProfileClick(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is not MenuProfile current) return;

        var defaultName = $"{current.Name} ({I18n.T("ProfileCopySuffix")})";
        var newName = InputDialog.Show(this, I18n.T("PromptNewProfileName"), I18n.T("SettingsTitle"), defaultName);
        if (string.IsNullOrWhiteSpace(newName)) return;

        var clone = current.Clone(newName.Trim());

        _settings.Profiles.Add(clone);
        _settings.ActiveProfileId = clone.Id;
        _settings.Save();

        RefreshProfilesList();
    }

    private void OnRenameProfileClick(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is not MenuProfile current || current.Id == "default") return;

        var result = InputDialog.Show(this, I18n.T("PromptRenameProfile"), I18n.T("SettingsTitle"), current.Name);
        if (!string.IsNullOrWhiteSpace(result) && result != current.Name)
        {
            current.Name = result.Trim();
            _settings.Save();
            RefreshProfilesList();
        }
    }

    private void OnDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is not MenuProfile current || current.Id == "default" || _settings.Profiles.Count <= 1) return;

        var message = string.Format(I18n.T("ConfirmDeleteProfile"), current.Name);
        if (MessageDialog.ShowConfirm(this, message, I18n.T("SettingsTitle")))
        {
            _settings.Profiles.Remove(current);
            _settings.ActiveProfileId = "default";
            _settings.Save();
            RefreshProfilesList();
        }
    }

    private void OnResetDefaultsClick(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is not MenuProfile current) return;

        if (!MessageDialog.ShowConfirm(this, I18n.T("ConfirmResetDefaults"), I18n.T("SettingsTitle"))) return;

        ResetProfileToDefaults(current);
    }

    internal void ResetProfileToDefaults(MenuProfile profile)
    {
        var factoryDefault = AppSettings.CreateDefaultProfile();
        profile.VideoFormats = [.. factoryDefault.VideoFormats];
        profile.AudioFormats = [.. factoryDefault.AudioFormats];
        profile.ImageFormats = [.. factoryDefault.ImageFormats];
        _settings.Save();
        PopulateCategoryPanels(profile);
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            FileName = "JustConvert-Settings.json",
            Title = I18n.T("BtnExport")
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                _settings.Export(sfd.FileName);
                TxtApplyStatus.Text = I18n.T("SettingsExportSuccess");
            }
            catch (Exception ex)
            {
                MessageDialog.ShowError(this, ex.Message, I18n.T("TitleError"));
            }
        }
    }

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = I18n.T("BtnImport")
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                var imported = AppSettings.Import(ofd.FileName);
                _settings = imported;
                _settings.Save();
                RefreshProfilesList();
                ApplySettingsToSystem();
                TxtApplyStatus.Text = I18n.T("SettingsImportSuccess");
            }
            catch
            {
                MessageDialog.ShowError(this, I18n.T("SettingsImportError"), I18n.T("TitleError"));
            }
        }
    }

    private static bool HasConfigurableSettings(string category, string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        if (category == "video")
        {
            return fmt is "mp4" or "webm" or "mkv" or "mov" or "reencode" or "frames" or "mp3" or "aac" or "m4a" or "opus";
        }
        if (category == "audio")
        {
            return fmt is "mp3" or "aac" or "m4a" or "ogg" or "opus" or "reencode";
        }
        if (category == "image")
        {
            return AppSettings.SupportsQuality(fmt);
        }
        return false;
    }

    private static string GetCodecDisplayName(string codec) => codec.ToLowerInvariant() switch
    {
        "h264" => I18n.T("CodecH264"),
        "h265" => I18n.T("CodecH265"),
        "vp9" => I18n.T("CodecVp9"),
        "av1" => I18n.T("CodecAv1"),
        "prores422" => I18n.T("CodecProRes"),
        "copy" => I18n.T("CodecCopy"),
        _ => codec.ToUpperInvariant()
    };

    internal string GetFormatStatus(string category, string format)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();

        if (category == "video")
        {
            if (fmt == "frames")
            {
                return _settings.FramesSetting.IsRemembered
                    ? string.Format(I18n.T("FramesStatusRemembered"), _settings.FramesSetting.ImageFormat.ToUpperInvariant())
                    : I18n.T("QualityStatusAsk");
            }
            if (fmt is "mp3" or "aac" or "m4a" or "opus")
            {
                if (_settings.TryGetSavedAudioQuality(fmt, out var bitrate))
                {
                    return bitrate <= 0
                        ? I18n.T("QualityStatusAudioAuto")
                        : string.Format(I18n.T("QualityStatusAudio"), bitrate);
                }
                return I18n.T("QualityStatusAsk");
            }
            if (_settings.TryGetSavedVideoQuality(fmt, out var vq))
            {
                var codecName = GetCodecDisplayName(vq.VideoCodec);
                if (string.Equals(vq.VideoCodec, "copy", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Format(I18n.T("QualityStatusVideoCopy"), codecName);
                }
                if (string.Equals(vq.RateControl, "cbr", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Format(I18n.T("QualityStatusVideoCbr"), codecName, vq.VideoBitrateKbps);
                }
                if (string.Equals(vq.RateControl, "vbr", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Format(I18n.T("QualityStatusVideoVbr"), codecName, vq.VideoBitrateKbps);
                }
                return string.Format(I18n.T("QualityStatusVideo"), codecName, vq.VideoQualityCq);
            }
            return I18n.T("QualityStatusAsk");
        }

        if (category == "audio")
        {
            if (_settings.TryGetSavedAudioQuality(fmt, out var bitrate))
            {
                return bitrate <= 0
                    ? I18n.T("QualityStatusAudioAuto")
                    : string.Format(I18n.T("QualityStatusAudio"), bitrate);
            }
            return I18n.T("QualityStatusAsk");
        }

        if (category == "image")
        {
            if (_settings.TryGetSavedQuality(fmt, out var quality))
            {
                return string.Format(I18n.T("QualityStatusRemembered"), quality);
            }
            return I18n.T("QualityStatusAsk");
        }

        return string.Empty;
    }

    private void OnEditFormatSettings(string category, string format, System.Windows.Controls.TextBlock? statusBlock)
    {
        var fmt = format.TrimStart('.').ToLowerInvariant();
        ConversionOptionsDialog dialog;

        if (category == "video")
        {
            if (fmt == "frames")
            {
                dialog = new ConversionOptionsDialog("frames", "frames", _settings.GetEffectiveFramesSetting(), null, _settings.AppendQualitySuffix, isSettingsMode: true);
            }
            else if (fmt is "mp3" or "aac" or "m4a" or "opus")
            {
                dialog = new ConversionOptionsDialog(fmt, "audio", _settings.GetEffectiveAudioQuality(fmt), null, _settings.AppendQualitySuffix, isSettingsMode: true);
            }
            else
            {
                dialog = new ConversionOptionsDialog(fmt, "video", _settings.GetEffectiveVideoQuality(fmt), null, _settings.AppendQualitySuffix, isSettingsMode: true);
            }
        }
        else if (category == "audio")
        {
            dialog = new ConversionOptionsDialog(fmt, "audio", _settings.GetEffectiveAudioQuality(fmt), null, _settings.AppendQualitySuffix, isSettingsMode: true);
        }
        else
        {
            dialog = new ConversionOptionsDialog(fmt, "image", _settings.GetEffectiveQuality(fmt), null, _settings.AppendQualitySuffix, isSettingsMode: true);
        }

        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            ApplyDialogResult(category, fmt, dialog);
            if (statusBlock != null)
            {
                statusBlock.Text = GetFormatStatus(category, fmt);
            }
        }
    }

    internal void ApplyDialogResult(string category, string fmt, ConversionOptionsDialog dialog)
    {
        if (category == "video")
        {
            if (fmt == "frames")
            {
                if (dialog.IsResetRequested || !dialog.RememberChoice)
                {
                    _settings.ResetFramesSetting();
                }
                else
                {
                    var s = dialog.SelectedFramesSetting;
                    s.IsRemembered = true;
                    _settings.SetFramesSetting(s);
                }
            }
            else if (fmt is "mp3" or "aac" or "m4a" or "opus")
            {
                if (dialog.IsResetRequested || !dialog.RememberChoice)
                {
                    _settings.ResetAudioQuality(fmt);
                }
                else
                {
                    _settings.SetAudioQuality(fmt, dialog.SelectedAudioBitrate, true);
                }
            }
            else
            {
                if (dialog.IsResetRequested || !dialog.RememberChoice)
                {
                    _settings.ResetVideoQuality(fmt);
                }
                else
                {
                    var s = dialog.SelectedVideoQuality;
                    s.IsRemembered = true;
                    _settings.SetVideoQuality(fmt, s);
                }
            }
        }
        else if (category == "audio")
        {
            if (dialog.IsResetRequested || !dialog.RememberChoice)
            {
                _settings.ResetAudioQuality(fmt);
            }
            else
            {
                _settings.SetAudioQuality(fmt, dialog.SelectedAudioBitrate, true);
            }
        }
        else if (category == "image")
        {
            if (dialog.IsResetRequested || !dialog.RememberChoice)
            {
                _settings.ResetQuality(fmt);
            }
            else
            {
                _settings.SetQuality(fmt, dialog.SelectedImageQuality, true);
            }
        }

        _settings.AppendQualitySuffix = dialog.AppendQualitySuffix;
        _settings.Save();
    }

    internal void EditFormatSettings(string category, string format, ConversionOptionsDialog dialog)
    {
        ApplyDialogResult(category, format, dialog);
        var panel = GetCategoryPanel(category);
        var statusBlock = FindFormatStatusBlock(panel, format);
        if (statusBlock != null)
        {
            statusBlock.Text = GetFormatStatus(category, format);
        }
    }

    internal Panel GetCategoryPanel(string category) => category switch
    {
        "video" => PanelVideoFormats,
        "audio" => PanelAudioFormats,
        _ => PanelImageFormats
    };

    internal System.Windows.Controls.CheckBox? FindFormatCheckBox(Panel panel, string format)
    {
        return FindDescendants<System.Windows.Controls.CheckBox>(panel)
            .FirstOrDefault(cb => string.Equals(cb.Tag as string, format, StringComparison.OrdinalIgnoreCase));
    }

    internal Wpf.Ui.Controls.Button? FindFormatEditButton(Panel panel, string format)
    {
        return FindDescendants<Wpf.Ui.Controls.Button>(panel)
            .FirstOrDefault(b => string.Equals(b.Tag as string, format, StringComparison.OrdinalIgnoreCase)
                              && b.Visibility == Visibility.Visible
                              && b.Icon is Wpf.Ui.Controls.SymbolIcon si && si.Symbol == Wpf.Ui.Controls.SymbolRegular.Edit20);
    }

    internal Wpf.Ui.Controls.Button? FindFormatDragHandle(Panel panel, string format)
    {
        return FindDescendants<Wpf.Ui.Controls.Button>(panel)
            .FirstOrDefault(b => string.Equals(b.Tag as string, format, StringComparison.OrdinalIgnoreCase)
                              && b.Icon is Wpf.Ui.Controls.SymbolIcon si && si.Symbol == Wpf.Ui.Controls.SymbolRegular.ReOrderDotsVertical20);
    }

    internal System.Windows.Controls.TextBlock? FindFormatStatusBlock(Panel panel, string format)
    {
        return FindDescendants<System.Windows.Controls.TextBlock>(panel)
            .FirstOrDefault(tb => string.Equals(tb.Tag as string, format, StringComparison.OrdinalIgnoreCase));
    }

    internal Wpf.Ui.Controls.Button? FindSeparatorDragHandle(Panel panel, int separatorIndex = 0)
    {
        return FindDescendants<Wpf.Ui.Controls.Button>(panel)
            .Where(b => b.Tag is string tag && ClassicContextMenuManager.IsSeparator(tag)
                     && b.Icon is Wpf.Ui.Controls.SymbolIcon si && si.Symbol == Wpf.Ui.Controls.SymbolRegular.ReOrderDotsVertical20)
            .ElementAtOrDefault(separatorIndex);
    }

    internal Wpf.Ui.Controls.Button? FindSeparatorDragHandle(Panel panel, string separatorKey)
    {
        return FindDescendants<Wpf.Ui.Controls.Button>(panel)
            .FirstOrDefault(b => string.Equals(b.Tag as string, separatorKey, StringComparison.OrdinalIgnoreCase)
                              && b.Icon is Wpf.Ui.Controls.SymbolIcon si && si.Symbol == Wpf.Ui.Controls.SymbolRegular.ReOrderDotsVertical20);
    }

    internal Wpf.Ui.Controls.Button? FindSeparatorDeleteButton(Panel panel, string? separatorKey = null, int separatorIndex = 0)
    {
        var buttons = FindDescendants<Wpf.Ui.Controls.Button>(panel)
            .Where(b => b.Tag is string tag && ClassicContextMenuManager.IsSeparator(tag)
                     && b.Icon is Wpf.Ui.Controls.SymbolIcon si && si.Symbol == Wpf.Ui.Controls.SymbolRegular.Delete20);
        if (separatorKey != null)
        {
            return buttons.FirstOrDefault(b => string.Equals(b.Tag as string, separatorKey, StringComparison.OrdinalIgnoreCase));
        }
        return buttons.ElementAtOrDefault(separatorIndex);
    }

    internal Wpf.Ui.Controls.Button? FindFormatDeleteButton(Panel panel, string format)
    {
        return FindDescendants<Wpf.Ui.Controls.Button>(panel)
            .FirstOrDefault(b => string.Equals(b.Tag as string, format, StringComparison.OrdinalIgnoreCase)
                              && b.Icon is Wpf.Ui.Controls.SymbolIcon si && si.Symbol == Wpf.Ui.Controls.SymbolRegular.Delete20);
    }

    internal List<string> GetDisplayedFormats(Panel panel)
    {
        return panel.Children.OfType<Grid>()
            .Select(row => row.Tag as string)
            .Where(f => !string.IsNullOrEmpty(f))
            .Select(f => f!)
            .ToList();
    }

    private void OnAddMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string category)
        {
            var menu = BuildAddContextMenu(category, fe);
            menu.PlacementTarget = fe;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    internal ContextMenu BuildAddContextMenu(string category, FrameworkElement? placementTarget = null)
    {
        var menu = new ContextMenu();
        if (placementTarget != null)
        {
            menu.PlacementTarget = placementTarget;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        }

        var itemAddSep = new Wpf.Ui.Controls.MenuItem
        {
            Header = I18n.T("MenuAddSeparator"),
            Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.DividerShort20 }
        };
        itemAddSep.Click += (s, _) => AddSeparator(category);
        menu.Items.Add(itemAddSep);

        var panel = GetCategoryPanel(category);
        var displayed = new HashSet<string>(GetDisplayedFormats(panel), StringComparer.OrdinalIgnoreCase);

        var formatMenu = new Wpf.Ui.Controls.MenuItem
        {
            Header = I18n.T("MenuAddFormat"),
            Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Apps20 }
        };

        var availableFormats = GetCategoryAllFormats(category)
            .Where(f => !IsActionItem(f) && !displayed.Contains(f))
            .ToList();

        if (availableFormats.Count > 0)
        {
            foreach (var fmt in availableFormats)
            {
                var subItem = new Wpf.Ui.Controls.MenuItem
                {
                    Header = I18n.GetSubMenuTitle(fmt)
                };
                var capturedFormat = fmt;
                subItem.Click += (s, _) => AddFormat(category, capturedFormat);
                formatMenu.Items.Add(subItem);
            }
        }
        else
        {
            formatMenu.Items.Add(new Wpf.Ui.Controls.MenuItem
            {
                Header = I18n.T("MenuAllFormatsAdded"),
                IsEnabled = false
            });
        }
        menu.Items.Add(formatMenu);

        var actionMenu = new Wpf.Ui.Controls.MenuItem
        {
            Header = I18n.T("MenuAddAction"),
            Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Flash20 }
        };

        var availableActions = GetCategoryActions(category)
            .Where(a => !displayed.Contains(a))
            .ToList();

        if (availableActions.Count > 0)
        {
            foreach (var act in availableActions)
            {
                var subItem = new Wpf.Ui.Controls.MenuItem
                {
                    Header = I18n.GetSubMenuTitle(act)
                };
                var capturedAction = act;
                subItem.Click += (s, _) => AddFormat(category, capturedAction);
                actionMenu.Items.Add(subItem);
            }
        }
        else
        {
            actionMenu.Items.Add(new Wpf.Ui.Controls.MenuItem
            {
                Header = I18n.T("MenuAllActionsAdded"),
                IsEnabled = false
            });
        }
        menu.Items.Add(actionMenu);

        return menu;
    }

    internal void AddFormat(string category, string format, int? insertAfterIndex = null)
    {
        var panel = GetCategoryPanel(category);
        var activeFormats = GetCategoryActiveFormats(category);
        var displayList = GetDisplayedFormats(panel);
        var cleanFormat = format.TrimStart('.').ToLowerInvariant();

        if (displayList.Contains(cleanFormat, StringComparer.OrdinalIgnoreCase)) return;

        var insertIndex = insertAfterIndex.HasValue
            ? Math.Clamp(insertAfterIndex.Value + 1, 0, displayList.Count)
            : displayList.Count;

        displayList.Insert(insertIndex, cleanFormat);
        if (!activeFormats.Contains(cleanFormat, StringComparer.OrdinalIgnoreCase))
        {
            activeFormats.Add(cleanFormat);
        }
        SyncActiveFormats(panel, displayList, activeFormats);

        var scrollViewer = panel.Parent as ScrollViewer;
        var scrollOffset = scrollViewer?.VerticalOffset ?? 0;

        PopulateFormatList(panel, category, displayList, activeFormats);

        if (!insertAfterIndex.HasValue)
        {
            scrollViewer?.ScrollToBottom();
        }
        else
        {
            scrollViewer?.ScrollToVerticalOffset(scrollOffset);
        }

        var handle = FindFormatDragHandle(panel, cleanFormat);
        handle?.Focus();
    }

    internal void DeleteFormat(string category, string format)
    {
        var panel = GetCategoryPanel(category);
        var activeFormats = GetCategoryActiveFormats(category);
        var displayList = GetDisplayedFormats(panel);
        var cleanFormat = format.TrimStart('.').ToLowerInvariant();

        var index = displayList.FindIndex(k => string.Equals(k, cleanFormat, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;

        displayList.RemoveAt(index);
        activeFormats.RemoveAll(f => string.Equals(f, cleanFormat, StringComparison.OrdinalIgnoreCase));
        SyncActiveFormats(panel, displayList, activeFormats);

        var scrollViewer = panel.Parent as ScrollViewer;
        var scrollOffset = scrollViewer?.VerticalOffset ?? 0;

        PopulateFormatList(panel, category, displayList, activeFormats);
        scrollViewer?.ScrollToVerticalOffset(scrollOffset);
    }

    internal void AddSeparator(string category, int? insertAfterIndex = null)
    {
        var panel = GetCategoryPanel(category);
        var activeFormats = GetCategoryActiveFormats(category);
        var displayList = GetDisplayedFormats(panel);

        var maxSepId = displayList
            .Where(ClassicContextMenuManager.IsSeparator)
            .Select(s => s.Split(':') is { Length: > 1 } parts && int.TryParse(parts[1], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        var newSepKey = $"separator:{maxSepId + 1}";

        var insertIndex = insertAfterIndex.HasValue
            ? Math.Clamp(insertAfterIndex.Value + 1, 0, displayList.Count)
            : displayList.Count;

        displayList.Insert(insertIndex, newSepKey);
        SyncActiveFormats(panel, displayList, activeFormats);

        var scrollViewer = panel.Parent as ScrollViewer;
        var scrollOffset = scrollViewer?.VerticalOffset ?? 0;

        PopulateFormatList(panel, category, displayList, activeFormats);

        if (!insertAfterIndex.HasValue)
        {
            scrollViewer?.ScrollToBottom();
        }
        else
        {
            scrollViewer?.ScrollToVerticalOffset(scrollOffset);
        }

        var handle = FindSeparatorDragHandle(panel, newSepKey);
        handle?.Focus();
    }

    internal void DeleteSeparator(string category, string separatorKey)
    {
        var panel = GetCategoryPanel(category);
        var activeFormats = GetCategoryActiveFormats(category);
        var displayList = GetDisplayedFormats(panel);

        var index = displayList.FindIndex(k => string.Equals(k, separatorKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;

        displayList.RemoveAt(index);
        SyncActiveFormats(panel, displayList, activeFormats);

        var scrollViewer = panel.Parent as ScrollViewer;
        var scrollOffset = scrollViewer?.VerticalOffset ?? 0;

        PopulateFormatList(panel, category, displayList, activeFormats);

        scrollViewer?.ScrollToVerticalOffset(scrollOffset);
    }

    internal void MoveFormat(string category, string format, int targetIndex)
    {
        var panel = GetCategoryPanel(category);
        var activeFormats = GetCategoryActiveFormats(category);
        var displayList = GetDisplayedFormats(panel);
        var fromIndex = displayList.FindIndex(f => string.Equals(f, format, StringComparison.OrdinalIgnoreCase));
        if (fromIndex >= 0 && targetIndex >= 0 && targetIndex < displayList.Count && fromIndex != targetIndex)
        {
            MoveFormatItem(panel, category, displayList, activeFormats, fromIndex, targetIndex - fromIndex);
        }
    }

    internal List<string> GetCategoryActiveFormats(string category)
    {
        var profile = _settings.GetActiveProfile();
        return category switch
        {
            "video" => profile.VideoFormats,
            "audio" => profile.AudioFormats,
            _ => profile.ImageFormats
        };
    }

    internal static string[] GetCategoryAllFormats(string category) => category switch
    {
        "video" => AllVideoFormats,
        "audio" => AllAudioFormats,
        _ => AllImageFormats
    };

    internal static bool IsActionItem(string format) =>
        string.Equals(format, "frames", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format, "reencode", StringComparison.OrdinalIgnoreCase);

    internal static string[] GetCategoryActions(string category) => category switch
    {
        "video" => ["frames", "reencode"],
        "audio" => ["reencode"],
        "image" => ["reencode"],
        _ => []
    };

    internal static IEnumerable<T> FindDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is T match) yield return match;
                foreach (var desc in FindDescendants<T>(child)) yield return desc;
            }
        }
        else if (parent is ContentControl cc && cc.Content is DependencyObject contentChild)
        {
            if (contentChild is T match) yield return match;
            foreach (var desc in FindDescendants<T>(contentChild)) yield return desc;
        }
        else if (parent is Decorator dec && dec.Child is DependencyObject decChild)
        {
            if (decChild is T match) yield return match;
            foreach (var desc in FindDescendants<T>(decChild)) yield return desc;
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        _settings.Save();
        ApplySettingsToSystem();
        TxtApplyStatus.Text = I18n.T("SettingsAppliedSuccess");
    }

    private void ApplySettingsToSystem()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "just-convert.exe");
            new ClassicContextMenuManager(_registry).Register(exePath);
        }
        catch { }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_isDragging && e.Key == Key.Escape)
        {
            if (_draggedTargetPanel != null && _draggedCategory != null)
            {
                var activeProfile = _settings.GetActiveProfile();
                PopulateCategoryPanels(activeProfile);
            }
            CleanupDrag();
            e.Handled = true;
            return;
        }
        if (!e.Handled && e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
