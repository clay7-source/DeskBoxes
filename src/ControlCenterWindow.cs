using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskBoxes
{
    public class ControlCenterWindow : Window
    {
        private readonly BoxManager _manager;
        private StackPanel _boxListPanel;
        private CheckBox _lockCheck;
        private CheckBox _snapCheck;
        private CheckBox _hoverExpandCheck;
        private CheckBox _singleClickCheck;
        private CheckBox _showLabelsCheck;
        private CheckBox _startupCheck;
        private Slider _globalOpacitySlider;
        private TextBlock _globalOpacityValueText;

        public ControlCenterWindow(BoxManager manager)
        {
            _manager = manager;

            Title = "DeskBoxes Control Center";
            Width = 680;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(17, 20, 28));
            Foreground = Brushes.White;

            BuildUI();
            RefreshBoxList();
        }

        private void BuildUI()
        {
            var rootScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(22, 18, 22, 22)
            };

            var mainStack = new StackPanel();

            var bannerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 32, 46)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(52, 70, 105)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 18)
            };

            var bannerGrid = new Grid();
            bannerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bannerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = "❖ DeskBoxes — Desktop Organizer",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "Double-click any box header to roll it up • Press Ctrl+Alt+B anytime to Quick-Hide all boxes",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(165, 180, 205)),
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(titleStack, 0);
            bannerGrid.Children.Add(titleStack);

            var quickHideBtn = CreateActionButton(
                _manager.Settings.QuickHidden ? "👁 Show Boxes" : "🙈 Quick-Hide All",
                Color.FromRgb(45, 55, 75)
            );
            quickHideBtn.Click += (s, e) =>
            {
                _manager.ToggleQuickHide();
                quickHideBtn.Content = _manager.Settings.QuickHidden ? "👁 Show Boxes" : "🙈 Quick-Hide All";
            };
            Grid.SetColumn(quickHideBtn, 1);
            bannerGrid.Children.Add(quickHideBtn);

            bannerBorder.Child = bannerGrid;
            mainStack.Children.Add(bannerBorder);

            var actionsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 18) };

            var newBoxBtn = CreateActionButton("➕ New Box", Color.FromRgb(37, 99, 235));
            newBoxBtn.Margin = new Thickness(0, 0, 10, 8);
            newBoxBtn.Click += (s, e) =>
            {
                _manager.CreateNewBox(120, 120);
                RefreshBoxList();
            };
            actionsWrap.Children.Add(newBoxBtn);

            var newFolderBoxBtn = CreateActionButton("📂 New Live Folder Box...", Color.FromRgb(14, 116, 144));
            newFolderBoxBtn.Margin = new Thickness(0, 0, 10, 8);
            newFolderBoxBtn.Click += (s, e) =>
            {
                _manager.CreateFolderBoxInteractive(160, 160);
                RefreshBoxList();
            };
            actionsWrap.Children.Add(newFolderBoxBtn);

            var autoSortBtn = CreateActionButton("✨ Auto-Group Desktop Icons", Color.FromRgb(16, 135, 95));
            autoSortBtn.Margin = new Thickness(0, 0, 10, 8);
            autoSortBtn.Click += (s, e) =>
            {
                _manager.AutoGroupDesktopIcons();
                RefreshBoxList();
            };
            actionsWrap.Children.Add(autoSortBtn);

            var tileCascadeBtn = CreateActionButton("📐 Arrange Boxes Neatly", Color.FromRgb(63, 63, 70));
            tileCascadeBtn.Margin = new Thickness(0, 0, 10, 8);
            tileCascadeBtn.Click += (s, e) =>
            {
                _manager.ArrangeBoxesOnGrid();
                RefreshBoxList();
            };
            actionsWrap.Children.Add(tileCascadeBtn);

            var restoreAllBtn = CreateActionButton("↩️ Restore All Icons to Desktop", Color.FromRgb(124, 45, 18));
            restoreAllBtn.Margin = new Thickness(0, 0, 0, 8);
            restoreAllBtn.Click += (s, e) =>
            {
                if (MessageBox.Show(
                    "Move all icons inside your boxes back onto your Windows Desktop?",
                    "Restore Icons to Desktop",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _manager.RestoreAllToDesktop();
                    RefreshBoxList();
                }
            };
            actionsWrap.Children.Add(restoreAllBtn);

            mainStack.Children.Add(actionsWrap);

            mainStack.Children.Add(CreateSectionHeader("YOUR DESKTOP BOXES"));

            _boxListPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            mainStack.Children.Add(_boxListPanel);

            mainStack.Children.Add(CreateSectionHeader("APPEARANCE & THEMES"));

            var appearanceCard = CreateCardBorder();
            var appStack = new StackPanel();

            appStack.Children.Add(new TextBlock
            {
                Text = "Apply Color Theme to All Boxes:",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 225)),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var swatchWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            foreach (var preset in ThemePreset.Presets)
            {
                var p = preset;
                Color c = BoxWindow.ParseHexColor(p.BgHex, Colors.Black);
                Color acc = BoxWindow.ParseHexColor(p.AccentHex, Colors.CornflowerBlue);

                var btn = new Button
                {
                    Content = p.Name,
                    Margin = new Thickness(0, 0, 8, 8),
                    Padding = new Thickness(10, 5, 10, 5),
                    Background = new SolidColorBrush(c),
                    Foreground = (c.R * 0.299 + c.G * 0.587 + c.B * 0.114) > 160 ? Brushes.Black : Brushes.White,
                    BorderBrush = new SolidColorBrush(acc),
                    BorderThickness = new Thickness(1.5),
                    Cursor = Cursors.Hand
                };
                btn.Click += (s, e) =>
                {
                    _manager.ApplyThemeToAll(p.BgHex, p.AccentHex);
                    RefreshBoxList();
                };
                swatchWrap.Children.Add(btn);
            }
            appStack.Children.Add(swatchWrap);

            var opHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            opHeader.Children.Add(new TextBlock
            {
                Text = "Global Box Opacity:",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 225))
            });
            _globalOpacityValueText = new TextBlock
            {
                Text = ((int)(_manager.Settings.DefaultOpacity * 100)) + "%",
                HorizontalAlignment = HorizontalAlignment.Right,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(88, 166, 255))
            };
            opHeader.Children.Add(_globalOpacityValueText);
            appStack.Children.Add(opHeader);

            _globalOpacitySlider = new Slider
            {
                Minimum = 0.15,
                Maximum = 0.98,
                Value = _manager.Settings.DefaultOpacity,
                SmallChange = 0.05,
                LargeChange = 0.1,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _globalOpacitySlider.ValueChanged += (s, e) =>
            {
                if (_globalOpacityValueText != null)
                {
                    _globalOpacityValueText.Text = ((int)(_globalOpacitySlider.Value * 100)) + "%";
                }
                _manager.ApplyOpacityToAll(_globalOpacitySlider.Value);
            };
            appStack.Children.Add(_globalOpacitySlider);

            appearanceCard.Child = appStack;
            mainStack.Children.Add(appearanceCard);

            mainStack.Children.Add(CreateSectionHeader("BEHAVIOR & DESKTOP INTERACTION"));

            var behaviorCard = CreateCardBorder();
            var behStack = new StackPanel();

            _lockCheck = CreateSettingCheckBox(
                "Lock Box Layout (prevent moving or resizing boxes)",
                _manager.Settings.LockLayout,
                val => { _manager.Settings.LockLayout = val; _manager.SaveState(); }
            );
            behStack.Children.Add(_lockCheck);

            _snapCheck = CreateSettingCheckBox(
                "Snap Boxes to Grid (20px) & Neighbor Box Edges",
                _manager.Settings.SnapToGrid,
                val => { _manager.Settings.SnapToGrid = val; _manager.SaveState(); }
            );
            behStack.Children.Add(_snapCheck);

            _hoverExpandCheck = CreateSettingCheckBox(
                "Hover over rolled-up box headers to temporarily peek inside",
                _manager.Settings.HoverExpandRolledUp,
                val => { _manager.Settings.HoverExpandRolledUp = val; _manager.SaveState(); }
            );
            behStack.Children.Add(_hoverExpandCheck);

            _singleClickCheck = CreateSettingCheckBox(
                "Single-click icons to open (instead of double-click)",
                _manager.Settings.SingleClickLaunch,
                val => { _manager.Settings.SingleClickLaunch = val; _manager.SaveState(); }
            );
            behStack.Children.Add(_singleClickCheck);

            _showLabelsCheck = CreateSettingCheckBox(
                "Show text labels underneath icons",
                _manager.Settings.ShowLabels,
                val =>
                {
                    _manager.Settings.ShowLabels = val;
                    _manager.RefreshAllBoxes();
                    _manager.SaveState();
                }
            );
            behStack.Children.Add(_showLabelsCheck);

            _startupCheck = CreateSettingCheckBox(
                "Start DeskBoxes automatically when Windows logs in",
                NativeInterop.IsStartupEnabled(),
                val => NativeInterop.SetStartupEnabled(val)
            );
            behStack.Children.Add(_startupCheck);

            behaviorCard.Child = behStack;
            mainStack.Children.Add(behaviorCard);

            rootScroll.Content = mainStack;
            Content = rootScroll;
        }

        public void RefreshBoxList()
        {
            if (_boxListPanel == null) return;
            _boxListPanel.Children.Clear();

            foreach (var b in _manager.Settings.Boxes.ToList())
            {
                var box = b;
                var rowBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(25, 30, 42)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(45, 53, 72)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var nameBlock = new TextBlock
                {
                    Text = (box.IsFolderBox ? "📂 " : "❖ ") + box.Title,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13.5,
                    Foreground = Brushes.White
                };
                infoStack.Children.Add(nameBlock);

                string sub = box.IsFolderBox
                    ? ("Live Folder Box → " + box.FolderPath)
                    : (box.Items.Count + " shortcuts/items • Position (" + (int)box.Left + ", " + (int)box.Top + ")");
                infoStack.Children.Add(new TextBlock
                {
                    Text = sub,
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 165, 190)),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                Grid.SetColumn(infoStack, 0);
                grid.Children.Add(infoStack);

                var rollBtn = CreateSmallButton(box.IsRolledUp ? "Expand" : "Roll Up");
                rollBtn.Click += (s, e) =>
                {
                    var win = _manager.GetWindow(box.Id);
                    if (win != null) win.ToggleRollUp();
                    RefreshBoxList();
                };
                Grid.SetColumn(rollBtn, 1);
                grid.Children.Add(rollBtn);

                var visBtn = CreateSmallButton(box.IsHidden ? "Show" : "Hide");
                visBtn.Click += (s, e) =>
                {
                    _manager.ToggleBoxVisibility(box.Id);
                    RefreshBoxList();
                };
                Grid.SetColumn(visBtn, 2);
                grid.Children.Add(visBtn);

                var delBtn = CreateSmallButton("Delete");
                delBtn.Foreground = new SolidColorBrush(Color.FromRgb(251, 113, 133));
                delBtn.Click += (s, e) =>
                {
                    if (MessageBox.Show(
                        "Delete box \"" + box.Title + "\"?\n(Any desktop items inside will be restored to your Desktop.)",
                        "Delete Box",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        _manager.DeleteBox(box.Id);
                        RefreshBoxList();
                    }
                };
                Grid.SetColumn(delBtn, 3);
                grid.Children.Add(delBtn);

                rowBorder.Child = grid;
                _boxListPanel.Children.Add(rowBorder);
            }
        }

        private TextBlock CreateSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 140, 175)),
                Margin = new Thickness(2, 4, 0, 8)
            };
        }

        private Border CreateCardBorder()
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 28, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 53, 72)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 18)
            };
        }

        private Button CreateActionButton(string text, Color bg)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(14, 8, 14, 8),
                Background = new SolidColorBrush(bg),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12.5,
                Cursor = Cursors.Hand
            };
        }

        private Button CreateSmallButton(string text)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(6, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(38, 45, 62)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(65, 75, 98)),
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private CheckBox CreateSettingCheckBox(string label, bool isChecked, Action<bool> onChanged)
        {
            var cb = new CheckBox
            {
                Content = new TextBlock
                {
                    Text = label,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    Margin = new Thickness(4, 0, 0, 0)
                },
                IsChecked = isChecked,
                Margin = new Thickness(0, 5, 0, 5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            cb.Checked += (s, e) => onChanged(true);
            cb.Unchecked += (s, e) => onChanged(false);
            return cb;
        }
    }
}
