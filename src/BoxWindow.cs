using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace DeskBoxes
{
    public class BoxWindow : Window
    {
        public const double HeaderHeight = 36.0;
        private const string InternalDragFormat = "DeskBoxes.InternalItemDrag";

        public BoxData Data { get; private set; }
        private readonly BoxManager _manager;

        private Border _outerBorder;
        private Border _headerBorder;
        private TextBlock _titleText;
        private TextBox _titleEditBox;
        private TextBlock _countBadge;
        private Button _rollUpButton;
        private Border _folderBar;
        private TextBlock _folderPathText;
        private ScrollViewer _scrollViewer;
        private WrapPanel _itemsPanel;
        private TextBlock _emptyHintText;

        private FileSystemWatcher _folderWatcher;
        private string _currentFolderSubPath;
        private bool _isHoverPeeking;
        private DispatcherTimer _hoverCollapseTimer;

        private Point _dragStartPoint;
        private bool _isDraggingItem;
        private BoxItem _draggedItem;

        // Window drag state
        private bool _isMovingWindow;
        private Point _windowDragStartMouseScreen;
        private double _windowDragStartLeft;
        private double _windowDragStartTop;

        public BoxWindow(BoxData data, BoxManager manager)
        {
            Data = data;
            _manager = manager;
            _currentFolderSubPath = data.FolderPath;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = false;
            ResizeMode = ResizeMode.NoResize;
            AllowDrop = true;

            Left = Data.Left;
            Top = Data.Top;
            Width = Math.Max(200, Data.Width);
            Height = Data.IsRolledUp ? HeaderHeight : Math.Max(120, Data.ExpandedHeight);

            BuildUI();
            ApplyAppearance();
            RefreshItems();
            SetupFolderWatcher();

            _hoverCollapseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(220)
            };
            _hoverCollapseTimer.Tick += (s, e) =>
            {
                _hoverCollapseTimer.Stop();
                if (_isHoverPeeking && !IsMouseOver)
                {
                    EndHoverPeek();
                }
            };

            SourceInitialized += OnSourceInitialized;
            MouseEnter += OnWindowMouseEnter;
            MouseLeave += OnWindowMouseLeave;
            DragOver += OnWindowDragOver;
            Drop += OnWindowDrop;
            Closing += (s, e) => DisposeWatcher();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            NativeInterop.PinWindowToDesktop(this);
        }

        public void EnsureBottomZOrder()
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                NativeInterop.SendToDesktopBottom(helper.Handle);
            }
        }

        private void BuildUI()
        {
            var rootGrid = new Grid();

            _outerBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true
            };

            var mainLayout = new DockPanel { LastChildFill = true };

            // 1. Header Bar
            _headerBorder = new Border
            {
                Height = HeaderHeight,
                CornerRadius = new CornerRadius(9, 9, 0, 0),
                Cursor = Cursors.SizeAll,
                Padding = new Thickness(12, 0, 8, 0)
            };
            _headerBorder.MouseLeftButtonDown += OnHeaderMouseLeftButtonDown;
            _headerBorder.MouseMove += OnHeaderMouseMove;
            _headerBorder.MouseLeftButtonUp += OnHeaderMouseLeftButtonUp;

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var accentIndicator = new TextBlock
            {
                Text = Data.IsFolderBox ? "📂" : "❖",
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(accentIndicator, 0);
            headerGrid.Children.Add(accentIndicator);

            var titleHost = new Grid { VerticalAlignment = VerticalAlignment.Center };
            _titleText = new TextBlock
            {
                Text = Data.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            _titleEditBox = new TextBox
            {
                Text = Data.Title,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Visibility = Visibility.Collapsed,
                Padding = new Thickness(4, 1, 4, 1),
                Background = new SolidColorBrush(Color.FromArgb(220, 20, 24, 33)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
                CaretBrush = Brushes.White
            };
            _titleEditBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitTitleEdit(true);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CommitTitleEdit(false);
                    e.Handled = true;
                }
            };
            _titleEditBox.LostFocus += (s, e) => CommitTitleEdit(true);

            titleHost.Children.Add(_titleText);
            titleHost.Children.Add(_titleEditBox);
            Grid.SetColumn(titleHost, 1);
            headerGrid.Children.Add(titleHost);

            _countBadge = new TextBlock
            {
                FontSize = 11,
                Opacity = 0.65,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 6, 0)
            };
            Grid.SetColumn(_countBadge, 2);
            headerGrid.Children.Add(_countBadge);

            _rollUpButton = CreateHeaderIconButton(Data.IsRolledUp ? "▸" : "▾", "Roll Up / Expand Box (or double-click title bar)");
            _rollUpButton.Click += (s, e) => ToggleRollUp();
            Grid.SetColumn(_rollUpButton, 3);
            headerGrid.Children.Add(_rollUpButton);

            var menuBtn = CreateHeaderIconButton("⋯", "Box Options");
            menuBtn.Click += (s, e) =>
            {
                var menu = BuildBoxContextMenu();
                menu.PlacementTarget = menuBtn;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            };
            Grid.SetColumn(menuBtn, 4);
            headerGrid.Children.Add(menuBtn);

            _headerBorder.Child = headerGrid;
            DockPanel.SetDock(_headerBorder, Dock.Top);
            mainLayout.Children.Add(_headerBorder);

            // 2. Live Folder Box Breadcrumb Bar
            _folderBar = new Border
            {
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromArgb(65, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Visibility = Data.IsFolderBox ? Visibility.Visible : Visibility.Collapsed
            };
            var folderGrid = new Grid();
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            folderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var upBtn = CreateHeaderIconButton("⬆ Up", "Go to parent folder");
            upBtn.FontSize = 11;
            upBtn.Width = 42;
            upBtn.Height = 20;
            upBtn.Click += (s, e) => NavigateFolderUp();
            Grid.SetColumn(upBtn, 0);
            folderGrid.Children.Add(upBtn);

            _folderPathText = new TextBlock
            {
                Text = _currentFolderSubPath ?? "",
                FontSize = 11,
                Opacity = 0.75,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(6, 0, 6, 0)
            };
            Grid.SetColumn(_folderPathText, 1);
            folderGrid.Children.Add(_folderPathText);

            var openExplorerBtn = CreateHeaderIconButton("↗", "Open folder in File Explorer");
            openExplorerBtn.FontSize = 11;
            openExplorerBtn.Width = 24;
            openExplorerBtn.Height = 20;
            openExplorerBtn.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_currentFolderSubPath))
                    NativeInterop.RevealInExplorer(_currentFolderSubPath);
            };
            Grid.SetColumn(openExplorerBtn, 2);
            folderGrid.Children.Add(openExplorerBtn);

            _folderBar.Child = folderGrid;
            DockPanel.SetDock(_folderBar, Dock.Top);
            mainLayout.Children.Add(_folderBar);

            // 3. Body Content
            var bodyGrid = new Grid();

            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(8, 8, 8, 8)
            };

            _itemsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };
            _scrollViewer.Content = _itemsPanel;
            bodyGrid.Children.Add(_scrollViewer);

            _emptyHintText = new TextBlock
            {
                Text = "Drag & drop files, folders, or shortcuts here\nor right-click for options",
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12.5,
                Opacity = 0.55,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            bodyGrid.Children.Add(_emptyHintText);

            mainLayout.Children.Add(bodyGrid);
            _outerBorder.Child = mainLayout;
            rootGrid.Children.Add(_outerBorder);

            AddResizeGrips(rootGrid);

            Content = rootGrid;
            ContextMenu = BuildBoxContextMenu();
        }

        private Button CreateHeaderIconButton(string text, string tooltip)
        {
            var btn = new Button
            {
                Content = text,
                ToolTip = tooltip,
                Width = 26,
                Height = 24,
                Margin = new Thickness(2, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                FontSize = 13,
                Cursor = Cursors.Hand,
                Focusable = false
            };

            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "BtnBorder";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(
                Border.BackgroundProperty,
                new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                "BtnBorder"
            ));
            template.Triggers.Add(hoverTrigger);

            btn.Template = template;
            return btn;
        }

        public void ApplyAppearance()
        {
            Color bgColor = ParseHexColor(Data.ThemeColor, Color.FromRgb(22, 25, 34));
            Color accentColor = ParseHexColor(Data.AccentColor, Color.FromRgb(88, 166, 255));
            double opacity = Math.Max(0.15, Math.Min(0.98, Data.Opacity));

            byte bgAlpha = (byte)(opacity * 255);
            byte headerAlpha = (byte)(Math.Min(0.96, opacity + 0.12) * 255);

            bool isLightTheme = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) > 160;
            Brush textBrush = isLightTheme
                ? new SolidColorBrush(Color.FromRgb(20, 24, 33))
                : Brushes.White;

            _outerBorder.Background = new SolidColorBrush(Color.FromArgb(bgAlpha, bgColor.R, bgColor.G, bgColor.B));
            _outerBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(95, accentColor.R, accentColor.G, accentColor.B));

            _headerBorder.Background = new SolidColorBrush(Color.FromArgb(headerAlpha, bgColor.R, bgColor.G, bgColor.B));
            _headerBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(55, accentColor.R, accentColor.G, accentColor.B));
            _headerBorder.BorderThickness = new Thickness(0, 0, 0, Data.IsRolledUp && !_isHoverPeeking ? 0 : 1);

            if (Data.IsRolledUp && !_isHoverPeeking)
            {
                _headerBorder.CornerRadius = new CornerRadius(9);
            }
            else
            {
                _headerBorder.CornerRadius = new CornerRadius(9, 9, 0, 0);
            }

            _titleText.Foreground = textBrush;
            _countBadge.Foreground = textBrush;
            _folderPathText.Foreground = textBrush;
            _emptyHintText.Foreground = textBrush;
            _rollUpButton.Content = Data.IsRolledUp ? "▸" : "▾";
            _rollUpButton.Foreground = textBrush;
        }

        public void RefreshItems()
        {
            _itemsPanel.Children.Clear();
            _titleText.Text = Data.Title;

            List<BoxItem> displayItems = GetEffectiveItems();
            _countBadge.Text = displayItems.Count.ToString();

            bool showCollapsed = Data.IsRolledUp && !_isHoverPeeking;
            _scrollViewer.Visibility = showCollapsed ? Visibility.Collapsed : Visibility.Visible;
            _folderBar.Visibility = (Data.IsFolderBox && !showCollapsed) ? Visibility.Visible : Visibility.Collapsed;
            _emptyHintText.Visibility = (!showCollapsed && displayItems.Count == 0) ? Visibility.Visible : Visibility.Collapsed;

            if (Data.IsFolderBox)
            {
                _folderPathText.Text = _currentFolderSubPath ?? Data.FolderPath;
                _folderPathText.ToolTip = _currentFolderSubPath ?? Data.FolderPath;
            }

            int iconPx = Math.Max(32, Math.Min(72, _manager.Settings.IconSize));
            double tileWidth = iconPx + 42;
            double tileHeight = _manager.Settings.ShowLabels ? (iconPx + 44) : (iconPx + 16);

            Color bgColor = ParseHexColor(Data.ThemeColor, Color.FromRgb(22, 25, 34));
            bool isLightTheme = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) > 160;
            Brush labelBrush = isLightTheme
                ? new SolidColorBrush(Color.FromRgb(22, 26, 35))
                : Brushes.White;

            foreach (var item in displayItems)
            {
                UIElement tile = CreateItemTile(item, iconPx, tileWidth, tileHeight, labelBrush, isLightTheme);
                _itemsPanel.Children.Add(tile);
            }
        }

        private List<BoxItem> GetEffectiveItems()
        {
            var result = new List<BoxItem>();

            if (Data.IsFolderBox)
            {
                string targetDir = string.IsNullOrEmpty(_currentFolderSubPath) ? Data.FolderPath : _currentFolderSubPath;
                if (!string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir))
                {
                    try
                    {
                        var entries = new List<FileSystemInfo>();
                        var dirInfo = new DirectoryInfo(targetDir);

                        foreach (var d in dirInfo.GetDirectories())
                        {
                            if ((d.Attributes & FileAttributes.Hidden) == 0 && !d.Name.StartsWith("."))
                                entries.Add(d);
                        }
                        foreach (var f in dirInfo.GetFiles())
                        {
                            if ((f.Attributes & FileAttributes.Hidden) == 0 &&
                                !f.Name.StartsWith(".") &&
                                !f.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                            {
                                entries.Add(f);
                            }
                        }

                        IEnumerable<FileSystemInfo> sorted = entries;
                        if (string.Equals(Data.SortBy, "Date", StringComparison.OrdinalIgnoreCase))
                        {
                            sorted = entries
                                .OrderByDescending(e => e is DirectoryInfo)
                                .ThenByDescending(e => e.LastWriteTimeUtc);
                        }
                        else if (string.Equals(Data.SortBy, "Type", StringComparison.OrdinalIgnoreCase))
                        {
                            sorted = entries
                                .OrderByDescending(e => e is DirectoryInfo)
                                .ThenBy(e => e.Extension, StringComparer.OrdinalIgnoreCase)
                                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
                        }
                        else
                        {
                            sorted = entries
                                .OrderByDescending(e => e is DirectoryInfo)
                                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
                        }

                        int idx = 0;
                        foreach (var entry in sorted.Take(150))
                        {
                            result.Add(new BoxItem
                            {
                                Id = entry.FullName,
                                FilePath = entry.FullName,
                                Order = idx++
                            });
                        }
                    }
                    catch { }
                }
                return result;
            }

            if (Data.Items == null) Data.Items = new List<BoxItem>();

            IEnumerable<BoxItem> items = Data.Items;
            if (string.Equals(Data.SortBy, "Name", StringComparison.OrdinalIgnoreCase))
            {
                items = items.OrderBy(i => GetItemDisplayText(i), StringComparer.OrdinalIgnoreCase);
            }
            else if (string.Equals(Data.SortBy, "Date", StringComparison.OrdinalIgnoreCase))
            {
                items = items.OrderByDescending(i =>
                {
                    try
                    {
                        if (File.Exists(i.FilePath)) return File.GetLastWriteTimeUtc(i.FilePath);
                        if (Directory.Exists(i.FilePath)) return Directory.GetLastWriteTimeUtc(i.FilePath);
                    }
                    catch { }
                    return DateTime.MinValue;
                });
            }
            else if (string.Equals(Data.SortBy, "Type", StringComparison.OrdinalIgnoreCase))
            {
                items = items
                    .OrderByDescending(i => Directory.Exists(i.FilePath))
                    .ThenBy(i => Path.GetExtension(i.FilePath ?? ""), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => GetItemDisplayText(i), StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                items = items.OrderBy(i => i.Order);
            }

            return items.ToList();
        }

        private string GetItemDisplayText(BoxItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.CustomLabel))
                return item.CustomLabel;
            return NativeInterop.GetCleanDisplayName(item.FilePath);
        }

        private UIElement CreateItemTile(BoxItem item, int iconPx, double tileWidth, double tileHeight, Brush labelBrush, bool isLightTheme)
        {
            var tileBorder = new Border
            {
                Width = tileWidth,
                Height = tileHeight,
                Margin = new Thickness(3),
                Padding = new Thickness(4, 5, 4, 4),
                CornerRadius = new CornerRadius(7),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = item,
                ToolTip = !string.IsNullOrEmpty(item.OriginalPath) ? item.OriginalPath : item.FilePath
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var iconImage = new Image
            {
                Width = iconPx,
                Height = iconPx,
                Stretch = Stretch.Uniform,
                Source = NativeInterop.GetFileIcon(item.FilePath),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(iconImage, BitmapScalingMode.HighQuality);
            stack.Children.Add(iconImage);

            if (_manager.Settings.ShowLabels)
            {
                var label = new TextBlock
                {
                    Text = GetItemDisplayText(item),
                    Foreground = labelBrush,
                    FontSize = 11.5,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxHeight = 32,
                    Margin = new Thickness(0, 4, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                if (!isLightTheme)
                {
                    label.Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 4,
                        ShadowDepth = 1,
                        Opacity = 0.85
                    };
                }

                stack.Children.Add(label);
            }

            tileBorder.Child = stack;

            tileBorder.MouseEnter += (s, e) =>
            {
                tileBorder.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                tileBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            };
            tileBorder.MouseLeave += (s, e) =>
            {
                tileBorder.Background = Brushes.Transparent;
                tileBorder.BorderBrush = Brushes.Transparent;
            };

            tileBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2 || (_manager.Settings.SingleClickLaunch && e.ClickCount == 1))
                {
                    ActivateItem(item);
                    e.Handled = true;
                    return;
                }

                _dragStartPoint = e.GetPosition(this);
                _isDraggingItem = true;
                _draggedItem = item;
            };

            tileBorder.MouseMove += (s, e) =>
            {
                if (_isDraggingItem && _draggedItem != null && e.LeftButton == MouseButtonState.Pressed)
                {
                    Point pos = e.GetPosition(this);
                    if (Math.Abs(pos.X - _dragStartPoint.X) > 6 || Math.Abs(pos.Y - _dragStartPoint.Y) > 6)
                    {
                        _isDraggingItem = false;
                        var dragData = new DataObject();
                        dragData.SetData(InternalDragFormat, new InternalDragPayload
                        {
                            SourceBoxId = Data.Id,
                            ItemId = _draggedItem.Id,
                            FilePath = _draggedItem.FilePath,
                            CustomLabel = _draggedItem.CustomLabel
                        });
                        if (!string.IsNullOrEmpty(_draggedItem.FilePath))
                        {
                            dragData.SetFileDropList(new System.Collections.Specialized.StringCollection { _draggedItem.FilePath });
                        }
                        var movingRef = _draggedItem;
                        DragDrop.DoDragDrop(tileBorder, dragData, DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
                        _draggedItem = null;

                        if (movingRef != null && !Data.IsFolderBox &&
                            !string.IsNullOrEmpty(movingRef.FilePath) &&
                            !File.Exists(movingRef.FilePath) && !Directory.Exists(movingRef.FilePath))
                        {
                            Data.Items.RemoveAll(i => i.Id == movingRef.Id);
                            RefreshItems();
                            _manager.SaveState();
                        }
                    }
                }
            };

            tileBorder.MouseLeftButtonUp += (s, e) =>
            {
                _isDraggingItem = false;
                _draggedItem = null;
            };

            tileBorder.AllowDrop = true;
            tileBorder.Drop += (s, e) =>
            {
                if (e.Data.GetDataPresent(InternalDragFormat) && !Data.IsFolderBox)
                {
                    var payload = e.Data.GetData(InternalDragFormat) as InternalDragPayload;
                    if (payload != null)
                    {
                        HandleInternalDrop(payload, item);
                        e.Handled = true;
                    }
                }
            };

            tileBorder.ContextMenu = BuildItemContextMenu(item);
            return tileBorder;
        }

        private void ActivateItem(BoxItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return;

            if (Data.IsFolderBox && Directory.Exists(item.FilePath))
            {
                NavigateFolderTo(item.FilePath);
                return;
            }

            NativeInterop.LaunchItem(item.FilePath, false);
        }

        private void NavigateFolderTo(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                _currentFolderSubPath = folderPath;
                SetupFolderWatcher();
                RefreshItems();
            }
        }

        private void NavigateFolderUp()
        {
            if (string.IsNullOrEmpty(_currentFolderSubPath)) return;
            try
            {
                DirectoryInfo parent = Directory.GetParent(_currentFolderSubPath);
                if (parent != null && parent.Exists)
                {
                    _currentFolderSubPath = parent.FullName;
                    SetupFolderWatcher();
                    RefreshItems();
                }
            }
            catch { }
        }

        public void SetupFolderWatcher()
        {
            DisposeWatcher();
            if (!Data.IsFolderBox) return;

            string watchPath = string.IsNullOrEmpty(_currentFolderSubPath) ? Data.FolderPath : _currentFolderSubPath;
            if (string.IsNullOrEmpty(watchPath) || !Directory.Exists(watchPath)) return;

            try
            {
                _folderWatcher = new FileSystemWatcher(watchPath)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                };
                FileSystemEventHandler handler = (s, e) =>
                {
                    Dispatcher.BeginInvoke(new Action(RefreshItems));
                };
                RenamedEventHandler renameHandler = (s, e) =>
                {
                    Dispatcher.BeginInvoke(new Action(RefreshItems));
                };
                _folderWatcher.Created += handler;
                _folderWatcher.Deleted += handler;
                _folderWatcher.Renamed += renameHandler;
                _folderWatcher.EnableRaisingEvents = true;
            }
            catch { }
        }

        private void DisposeWatcher()
        {
            if (_folderWatcher != null)
            {
                try
                {
                    _folderWatcher.EnableRaisingEvents = false;
                    _folderWatcher.Dispose();
                }
                catch { }
                _folderWatcher = null;
            }
        }

        // Roll-up & Hover-Peek
        public void ToggleRollUp()
        {
            _isHoverPeeking = false;
            if (!Data.IsRolledUp)
            {
                Data.ExpandedHeight = Math.Max(140, ActualHeight);
                Data.IsRolledUp = true;
                Height = HeaderHeight;
            }
            else
            {
                Data.IsRolledUp = false;
                Height = Math.Max(140, Data.ExpandedHeight);
            }
            Data.Height = Height;
            ApplyAppearance();
            RefreshItems();
            _manager.SaveState();
        }

        private void OnWindowMouseEnter(object sender, MouseEventArgs e)
        {
            _hoverCollapseTimer.Stop();
            if (Data.IsRolledUp && _manager.Settings.HoverExpandRolledUp && !_isHoverPeeking)
            {
                _isHoverPeeking = true;
                Height = Math.Max(140, Data.ExpandedHeight);
                ApplyAppearance();
                RefreshItems();
            }
        }

        private void OnWindowMouseLeave(object sender, MouseEventArgs e)
        {
            if (_isHoverPeeking)
            {
                _hoverCollapseTimer.Start();
            }
        }

        private void EndHoverPeek()
        {
            _isHoverPeeking = false;
            if (Data.IsRolledUp)
            {
                Height = HeaderHeight;
                ApplyAppearance();
                RefreshItems();
            }
        }

        // Title Editing
        public void StartTitleEdit()
        {
            _titleEditBox.Text = Data.Title;
            _titleText.Visibility = Visibility.Collapsed;
            _titleEditBox.Visibility = Visibility.Visible;
            _titleEditBox.Focus();
            _titleEditBox.SelectAll();
        }

        private void CommitTitleEdit(bool save)
        {
            if (_titleEditBox.Visibility != Visibility.Visible) return;
            if (save && !string.IsNullOrWhiteSpace(_titleEditBox.Text))
            {
                Data.Title = _titleEditBox.Text.Trim();
                _titleText.Text = Data.Title;
                _manager.SaveState();
            }
            _titleEditBox.Visibility = Visibility.Collapsed;
            _titleText.Visibility = Visibility.Visible;
        }

        // Window Dragging & Snapping
        private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            EnsureBottomZOrder();

            if (e.ClickCount == 2)
            {
                ToggleRollUp();
                e.Handled = true;
                return;
            }

            if (_manager.Settings.LockLayout)
                return;

            _isMovingWindow = true;
            _windowDragStartMouseScreen = PointToScreen(e.GetPosition(this));
            _windowDragStartLeft = Left;
            _windowDragStartTop = Top;
            _headerBorder.CaptureMouse();
            e.Handled = true;
        }

        private void OnHeaderMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMovingWindow) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                StopWindowDrag();
                return;
            }

            Point currentScreen = PointToScreen(e.GetPosition(this));
            double dx = currentScreen.X - _windowDragStartMouseScreen.X;
            double dy = currentScreen.Y - _windowDragStartMouseScreen.Y;

            double newLeft = _windowDragStartLeft + dx;
            double newTop = _windowDragStartTop + dy;

            if (_manager.Settings.SnapToGrid)
            {
                _manager.ApplySnapPosition(this, ref newLeft, ref newTop);
            }

            Left = newLeft;
            Top = newTop;
            Data.Left = Left;
            Data.Top = Top;
        }

        private void OnHeaderMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMovingWindow)
            {
                StopWindowDrag();
                e.Handled = true;
            }
        }

        private void StopWindowDrag()
        {
            _isMovingWindow = false;
            _headerBorder.ReleaseMouseCapture();
            EnsureBottomZOrder();
            _manager.SaveState();
        }

        // Drag & Drop Files / Reorder Items
        private void OnWindowDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(InternalDragFormat) || e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnWindowDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(InternalDragFormat))
            {
                var payload = e.Data.GetData(InternalDragFormat) as InternalDragPayload;
                if (payload != null)
                {
                    HandleInternalDrop(payload, null);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    AddFilePaths(files);
                    e.Handled = true;
                }
            }
        }

        private void HandleInternalDrop(InternalDragPayload payload, BoxItem targetBeforeItem)
        {
            if (Data.IsFolderBox) return;

            if (payload.SourceBoxId == Data.Id)
            {
                BoxItem moving = Data.Items.FirstOrDefault(i => i.Id == payload.ItemId);
                if (moving == null) return;

                Data.SortBy = "Manual";
                var ordered = Data.Items.OrderBy(i => i.Order).ToList();
                ordered.Remove(moving);

                if (targetBeforeItem != null)
                {
                    int idx = ordered.IndexOf(targetBeforeItem);
                    if (idx >= 0) ordered.Insert(idx, moving);
                    else ordered.Add(moving);
                }
                else
                {
                    ordered.Add(moving);
                }

                for (int i = 0; i < ordered.Count; i++)
                    ordered[i].Order = i;

                Data.Items = ordered;
                RefreshItems();
                _manager.SaveState();
            }
            else
            {
                _manager.MoveItemBetweenBoxes(payload.SourceBoxId, payload.ItemId, Data.Id, targetBeforeItem);
            }
        }

        public void AddFilePaths(IEnumerable<string> paths)
        {
            if (Data.IsFolderBox)
            {
                string destDir = string.IsNullOrEmpty(_currentFolderSubPath) ? Data.FolderPath : _currentFolderSubPath;
                if (!string.IsNullOrEmpty(destDir) && Directory.Exists(destDir))
                {
                    foreach (var p in paths)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(p);
                            string destFile = Path.Combine(destDir, fileName);
                            if (!string.Equals(p, destFile, StringComparison.OrdinalIgnoreCase) && File.Exists(p) && !File.Exists(destFile))
                            {
                                File.Copy(p, destFile);
                            }
                        }
                        catch { }
                    }
                    RefreshItems();
                }
                return;
            }

            int nextOrder = Data.Items.Count > 0 ? Data.Items.Max(i => i.Order) + 1 : 0;
            bool changed = false;

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                string fileName = Path.GetFileName(path);

                if (Data.Items.Any(i =>
                    string.Equals(i.FilePath, path, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(i.OriginalPath, path, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(i.FilePath ?? ""), fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var newItem = new BoxItem
                {
                    FilePath = path,
                    Order = nextOrder++
                };

                NativeInterop.ConcealItemFromDesktop(newItem);

                Data.Items.Add(newItem);
                changed = true;
            }

            if (changed)
            {
                RefreshItems();
                _manager.SaveState();
            }
        }

        // Resize Grips
        private void AddResizeGrips(Grid rootGrid)
        {
            var rightThumb = CreateResizeThumb(Cursors.SizeWE, HorizontalAlignment.Right, VerticalAlignment.Stretch, 6, double.NaN);
            rightThumb.DragDelta += (s, e) => ResizeBox(0, 0, e.HorizontalChange, 0);
            rootGrid.Children.Add(rightThumb);

            var bottomThumb = CreateResizeThumb(Cursors.SizeNS, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, 6);
            bottomThumb.DragDelta += (s, e) => ResizeBox(0, 0, 0, e.VerticalChange);
            rootGrid.Children.Add(bottomThumb);

            var leftThumb = CreateResizeThumb(Cursors.SizeWE, HorizontalAlignment.Left, VerticalAlignment.Stretch, 6, double.NaN);
            leftThumb.DragDelta += (s, e) => ResizeBox(e.HorizontalChange, 0, -e.HorizontalChange, 0);
            rootGrid.Children.Add(leftThumb);

            var brThumb = CreateResizeThumb(Cursors.SizeNWSE, HorizontalAlignment.Right, VerticalAlignment.Bottom, 14, 14);
            brThumb.DragDelta += (s, e) => ResizeBox(0, 0, e.HorizontalChange, e.VerticalChange);
            rootGrid.Children.Add(brThumb);

            var blThumb = CreateResizeThumb(Cursors.SizeNESW, HorizontalAlignment.Left, VerticalAlignment.Bottom, 14, 14);
            blThumb.DragDelta += (s, e) => ResizeBox(e.HorizontalChange, 0, -e.HorizontalChange, e.VerticalChange);
            rootGrid.Children.Add(blThumb);
        }

        private Thumb CreateResizeThumb(Cursor cursor, HorizontalAlignment hAlign, VerticalAlignment vAlign, double width, double height)
        {
            var thumb = new Thumb
            {
                Cursor = cursor,
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Width = width,
                Height = height,
                Background = Brushes.Transparent,
                Opacity = 0.01
            };
            thumb.DragCompleted += (s, e) =>
            {
                if (_manager.Settings.SnapToGrid)
                {
                    int g = Math.Max(8, _manager.Settings.GridSize);
                    Width = Math.Max(200, Math.Round(Width / g) * g);
                    if (!Data.IsRolledUp)
                    {
                        Height = Math.Max(120, Math.Round(Height / g) * g);
                        Data.ExpandedHeight = Height;
                    }
                    Data.Width = Width;
                    Data.Height = Height;
                }
                EnsureBottomZOrder();
                _manager.SaveState();
            };
            return thumb;
        }

        private void ResizeBox(double dLeft, double dTop, double dWidth, double dHeight)
        {
            if (_manager.Settings.LockLayout) return;

            double newWidth = Width + dWidth;
            if (newWidth >= 200)
            {
                if (dLeft != 0)
                {
                    Left += dLeft;
                    Data.Left = Left;
                }
                Width = newWidth;
                Data.Width = Width;
            }

            if (!Data.IsRolledUp && dHeight != 0)
            {
                double newHeight = Height + dHeight;
                if (newHeight >= 120)
                {
                    Height = newHeight;
                    Data.Height = Height;
                    Data.ExpandedHeight = Height;
                }
            }
        }

        // Context Menus
        private ContextMenu BuildItemContextMenu(BoxItem item)
        {
            var menu = new ContextMenu();

            var openItem = new MenuItem { Header = "Open", FontWeight = FontWeights.SemiBold };
            openItem.Click += (s, e) => ActivateItem(item);
            menu.Items.Add(openItem);

            var runAdmin = new MenuItem { Header = "Run as Administrator" };
            runAdmin.Click += (s, e) => NativeInterop.LaunchItem(item.FilePath, true);
            menu.Items.Add(runAdmin);

            var reveal = new MenuItem { Header = "Open File Location" };
            reveal.Click += (s, e) => NativeInterop.RevealInExplorer(item.FilePath);
            menu.Items.Add(reveal);

            if (!Data.IsFolderBox)
            {
                menu.Items.Add(new Separator());

                var renameItem = new MenuItem { Header = "Rename Shortcut Label..." };
                renameItem.Click += (s, e) =>
                {
                    string current = GetItemDisplayText(item);
                    string updated = PromptInputDialog("Rename Label", "Enter a custom display name for this icon:", current);
                    if (updated != null)
                    {
                        item.CustomLabel = updated.Trim();
                        RefreshItems();
                        _manager.SaveState();
                    }
                };
                menu.Items.Add(renameItem);

                var removeItem = new MenuItem { Header = "↩️ Return to Desktop (Remove from Box)" };
                removeItem.Click += (s, e) =>
                {
                    NativeInterop.RestoreItemToDesktop(item);
                    Data.Items.RemoveAll(i => i.Id == item.Id);
                    RefreshItems();
                    _manager.SaveState();
                };
                menu.Items.Add(removeItem);
            }

            return menu;
        }

        private ContextMenu BuildBoxContextMenu()
        {
            var menu = new ContextMenu();

            var newBox = new MenuItem { Header = "➕ New Box" };
            newBox.Click += (s, e) => _manager.CreateNewBox(Left + 30, Top + 30);
            menu.Items.Add(newBox);

            var newFolderBox = new MenuItem { Header = "📂 New Live Folder Box..." };
            newFolderBox.Click += (s, e) => _manager.CreateFolderBoxInteractive(Left + 30, Top + 30);
            menu.Items.Add(newFolderBox);

            menu.Items.Add(new Separator());

            if (!Data.IsFolderBox)
            {
                var addFiles = new MenuItem { Header = "📥 Add Files or Shortcuts..." };
                addFiles.Click += (s, e) =>
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Add Files or Shortcuts to Box",
                        Multiselect = true,
                        DereferenceLinks = false,
                        Filter = "All Files (*.*)|*.*"
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        AddFilePaths(dlg.FileNames);
                    }
                };
                menu.Items.Add(addFiles);

                var addFolder = new MenuItem { Header = "📁 Add Folder Shortcut..." };
                addFolder.Click += (s, e) =>
                {
                    using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fbd.Description = "Select a folder to add to this box:";
                        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            AddFilePaths(new[] { fbd.SelectedPath });
                        }
                    }
                };
                menu.Items.Add(addFolder);
            }
            else
            {
                var changeFolder = new MenuItem { Header = "📂 Change Linked Folder..." };
                changeFolder.Click += (s, e) =>
                {
                    using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fbd.SelectedPath = Data.FolderPath;
                        fbd.Description = "Choose folder to display in this Live Folder Box:";
                        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            Data.FolderPath = fbd.SelectedPath;
                            _currentFolderSubPath = Data.FolderPath;
                            Data.Title = Path.GetFileName(Data.FolderPath);
                            SetupFolderWatcher();
                            RefreshItems();
                            _manager.SaveState();
                        }
                    }
                };
                menu.Items.Add(changeFolder);
            }

            var renameBox = new MenuItem { Header = "✏️ Rename Box" };
            renameBox.Click += (s, e) => StartTitleEdit();
            menu.Items.Add(renameBox);

            var rollUpItem = new MenuItem
            {
                Header = Data.IsRolledUp ? "▾ Expand Box" : "▸ Roll Up Box",
                InputGestureText = "Double-click header"
            };
            rollUpItem.Click += (s, e) => ToggleRollUp();
            menu.Items.Add(rollUpItem);

            menu.Items.Add(new Separator());

            var themeMenu = new MenuItem { Header = "🎨 Color Theme" };
            foreach (var preset in ThemePreset.Presets)
            {
                var p = preset;
                var item = new MenuItem
                {
                    Header = p.Name,
                    IsChecked = string.Equals(Data.ThemeColor, p.BgHex, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += (s, e) =>
                {
                    Data.ThemeColor = p.BgHex;
                    Data.AccentColor = p.AccentHex;
                    ApplyAppearance();
                    RefreshItems();
                    _manager.SaveState();
                };
                themeMenu.Items.Add(item);
            }
            menu.Items.Add(themeMenu);

            var opacityMenu = new MenuItem { Header = "🔆 Background Opacity" };
            double[] opacities = new[] { 0.25, 0.45, 0.65, 0.75, 0.88, 0.96 };
            foreach (double op in opacities)
            {
                double val = op;
                var opItem = new MenuItem
                {
                    Header = ((int)(val * 100)) + "%",
                    IsChecked = Math.Abs(Data.Opacity - val) < 0.06
                };
                opItem.Click += (s, e) =>
                {
                    Data.Opacity = val;
                    ApplyAppearance();
                    _manager.SaveState();
                };
                opacityMenu.Items.Add(opItem);
            }
            menu.Items.Add(opacityMenu);

            var sortMenu = new MenuItem { Header = "🔠 Sort Icons By" };
            string[] sorts = Data.IsFolderBox
                ? new[] { "Name", "Date", "Type" }
                : new[] { "Manual", "Name", "Date", "Type" };
            foreach (string sMode in sorts)
            {
                string mode = sMode;
                var sItem = new MenuItem
                {
                    Header = mode,
                    IsChecked = string.Equals(Data.SortBy, mode, StringComparison.OrdinalIgnoreCase)
                };
                sItem.Click += (s, e) =>
                {
                    Data.SortBy = mode;
                    RefreshItems();
                    _manager.SaveState();
                };
                sortMenu.Items.Add(sItem);
            }
            menu.Items.Add(sortMenu);

            var sizeMenu = new MenuItem { Header = "📐 Icon Size & Labels" };
            int[] sizes = new[] { 36, 48, 64 };
            string[] sizeNames = new[] { "Small (36px)", "Medium (48px)", "Large (64px)" };
            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                var szItem = new MenuItem
                {
                    Header = sizeNames[i],
                    IsChecked = _manager.Settings.IconSize == sz
                };
                szItem.Click += (s, e) =>
                {
                    _manager.Settings.IconSize = sz;
                    _manager.RefreshAllBoxes();
                    _manager.SaveState();
                };
                sizeMenu.Items.Add(szItem);
            }
            sizeMenu.Items.Add(new Separator());
            var showLabelsItem = new MenuItem
            {
                Header = "Show Icon Labels",
                IsChecked = _manager.Settings.ShowLabels
            };
            showLabelsItem.Click += (s, e) =>
            {
                _manager.Settings.ShowLabels = !_manager.Settings.ShowLabels;
                _manager.RefreshAllBoxes();
                _manager.SaveState();
            };
            sizeMenu.Items.Add(showLabelsItem);
            menu.Items.Add(sizeMenu);

            menu.Items.Add(new Separator());

            var lockItem = new MenuItem
            {
                Header = "🔒 Lock Box Layout",
                IsChecked = _manager.Settings.LockLayout
            };
            lockItem.Click += (s, e) =>
            {
                _manager.Settings.LockLayout = !_manager.Settings.LockLayout;
                _manager.SaveState();
            };
            menu.Items.Add(lockItem);

            var snapItem = new MenuItem
            {
                Header = "🧲 Snap to Grid & Edges",
                IsChecked = _manager.Settings.SnapToGrid
            };
            snapItem.Click += (s, e) =>
            {
                _manager.Settings.SnapToGrid = !_manager.Settings.SnapToGrid;
                _manager.SaveState();
            };
            menu.Items.Add(snapItem);

            var controlCenterItem = new MenuItem { Header = "⚙️ DeskBoxes Control Center..." };
            controlCenterItem.Click += (s, e) => _manager.ShowControlCenter();
            menu.Items.Add(controlCenterItem);

            menu.Items.Add(new Separator());

            var deleteItem = new MenuItem { Header = "🗑️ Delete This Box" };
            deleteItem.Click += (s, e) =>
            {
                if (MessageBox.Show(
                    "Remove the box \"" + Data.Title + "\"?\n(Any desktop icons inside will be returned to your Desktop.)",
                    "Delete Box",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _manager.DeleteBox(Data.Id);
                }
            };
            menu.Items.Add(deleteItem);

            return menu;
        }

        private string PromptInputDialog(string title, string prompt, string defaultVal)
        {
            var win = new Window
            {
                Title = title,
                Width = 360,
                Height = 165,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new SolidColorBrush(Color.FromRgb(24, 28, 38)),
                Foreground = Brushes.White,
                Topmost = true
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            stack.Children.Add(new TextBlock
            {
                Text = prompt,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var box = new TextBox
            {
                Text = defaultVal ?? "",
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 13
            };
            stack.Children.Add(box);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var okBtn = new Button
            {
                Content = "Save",
                Width = 75,
                Height = 28,
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0)
            };
            okBtn.Click += (s, e) => { win.DialogResult = true; };

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 75,
                Height = 28,
                IsCancel = true
            };

            btnRow.Children.Add(okBtn);
            btnRow.Children.Add(cancelBtn);
            stack.Children.Add(btnRow);
            win.Content = stack;

            win.Loaded += (s, e) =>
            {
                box.Focus();
                box.SelectAll();
            };

            return win.ShowDialog() == true ? box.Text : null;
        }

        public static Color ParseHexColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                object obj = ColorConverter.ConvertFromString(hex);
                if (obj is Color) return (Color)obj;
            }
            catch { }
            return fallback;
        }
    }

    [Serializable]
    public class InternalDragPayload
    {
        public string SourceBoxId { get; set; }
        public string ItemId { get; set; }
        public string FilePath { get; set; }
        public string CustomLabel { get; set; }
    }
}
