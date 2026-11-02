using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DeskBoxes
{
    public class BoxManager
    {
        private const int HOTKEY_ID_QUICKHIDE_B = 9001;
        private const int HOTKEY_ID_QUICKHIDE_F = 9002;

        public AppSettings Settings { get; private set; }
        private readonly Dictionary<string, BoxWindow> _windows = new Dictionary<string, BoxWindow>();
        private System.Windows.Forms.NotifyIcon _trayIcon;
        private ControlCenterWindow _controlCenter;
        private Window _hotkeyMessageWindow;
        private DispatcherTimer _desktopConcealTimer;

        public void Initialize()
        {
            Settings = StorageManager.Load();

            ConcealAllBoxedDesktopItems();

            SetupTrayIcon();
            SetupGlobalHotkeyWindow();

            foreach (var boxData in Settings.Boxes)
            {
                SpawnBoxWindow(boxData);
            }

            _desktopConcealTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.5)
            };
            _desktopConcealTimer.Tick += (s, e) => EnforceDesktopConcealment();
            _desktopConcealTimer.Start();
        }

        public void ConcealAllBoxedDesktopItems()
        {
            bool anyModified = false;
            foreach (var box in Settings.Boxes)
            {
                if (box.IsFolderBox || box.Items == null) continue;
                foreach (var item in box.Items)
                {
                    if (NativeInterop.ConcealItemFromDesktop(item))
                    {
                        anyModified = true;
                    }
                }
            }
            if (anyModified)
            {
                SaveState();
            }
        }

        private void EnforceDesktopConcealment()
        {
            if (Settings == null || Settings.Boxes == null) return;
            var namesToHide = new List<string>();
            foreach (var box in Settings.Boxes)
            {
                if (box.IsFolderBox || box.Items == null) continue;
                foreach (var item in box.Items)
                {
                    string checkPath = !string.IsNullOrEmpty(item.OriginalPath) ? item.OriginalPath : item.FilePath;
                    if (!string.IsNullOrEmpty(checkPath) &&
                        NativeInterop.IsDesktopPath(checkPath) &&
                        (File.Exists(checkPath) || Directory.Exists(checkPath)))
                    {
                        namesToHide.Add(NativeInterop.GetCleanDisplayName(checkPath));
                        namesToHide.Add(Path.GetFileName(checkPath));
                    }
                }
            }
            if (namesToHide.Count > 0)
            {
                NativeInterop.RemoveIconsFromDesktopListView(namesToHide);
            }
        }

        public void RestoreAllToDesktop()
        {
            foreach (var box in Settings.Boxes)
            {
                if (box.IsFolderBox || box.Items == null) continue;
                foreach (var item in box.Items)
                {
                    NativeInterop.RestoreItemToDesktop(item);
                }
                box.Items.Clear();
            }
            RefreshAllBoxes();
            SaveState();
            NativeInterop.RefreshDesktopShellView();
        }

        private void SpawnBoxWindow(BoxData data)
        {
            if (_windows.ContainsKey(data.Id))
                return;

            var win = new BoxWindow(data, this);
            _windows[data.Id] = win;

            if (!data.IsHidden && !Settings.QuickHidden)
            {
                win.Show();
                win.EnsureBottomZOrder();
            }
        }

        public BoxWindow GetWindow(string boxId)
        {
            BoxWindow win;
            _windows.TryGetValue(boxId, out win);
            return win;
        }

        public BoxData CreateNewBox(double left, double top, string title = null)
        {
            var box = new BoxData
            {
                Title = string.IsNullOrWhiteSpace(title) ? ("Box " + (Settings.Boxes.Count + 1)) : title,
                Left = left,
                Top = top,
                Width = 360,
                Height = 250,
                ExpandedHeight = 250,
                ThemeColor = Settings.DefaultThemeColor ?? "#161922",
                AccentColor = "#58A6FF",
                Opacity = Settings.DefaultOpacity
            };

            Settings.Boxes.Add(box);
            SpawnBoxWindow(box);
            SaveState();
            return box;
        }

        public void CreateFolderBoxInteractive(double left, double top)
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                fbd.Description = "Choose a folder on your PC to mirror live inside a Live Folder Box:";
                fbd.ShowNewFolderButton = true;
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && Directory.Exists(fbd.SelectedPath))
                {
                    string folderName = Path.GetFileName(fbd.SelectedPath);
                    if (string.IsNullOrWhiteSpace(folderName))
                        folderName = fbd.SelectedPath;

                    var folderBox = new BoxData
                    {
                        Title = folderName,
                        Left = left,
                        Top = top,
                        Width = 380,
                        Height = 280,
                        ExpandedHeight = 280,
                        IsFolderBox = true,
                        FolderPath = fbd.SelectedPath,
                        SortBy = "Name",
                        ThemeColor = "#0F1E36",
                        AccentColor = "#38BDF8",
                        Opacity = Settings.DefaultOpacity
                    };

                    Settings.Boxes.Add(folderBox);
                    SpawnBoxWindow(folderBox);
                    SaveState();
                }
            }
        }

        public void DeleteBox(string boxId)
        {
            var boxData = Settings.Boxes.FirstOrDefault(b => b.Id == boxId);
            if (boxData != null && !boxData.IsFolderBox && boxData.Items != null)
            {
                foreach (var item in boxData.Items)
                {
                    NativeInterop.RestoreItemToDesktop(item);
                }
            }

            BoxWindow win;
            if (_windows.TryGetValue(boxId, out win))
            {
                win.Close();
                _windows.Remove(boxId);
            }
            Settings.Boxes.RemoveAll(b => b.Id == boxId);
            SaveState();
        }

        public void ToggleBoxVisibility(string boxId)
        {
            var data = Settings.Boxes.FirstOrDefault(b => b.Id == boxId);
            if (data == null) return;

            data.IsHidden = !data.IsHidden;
            BoxWindow win;
            if (_windows.TryGetValue(boxId, out win))
            {
                if (data.IsHidden || Settings.QuickHidden)
                {
                    win.Hide();
                }
                else
                {
                    win.Show();
                    win.EnsureBottomZOrder();
                }
            }
            SaveState();
        }

        public void ToggleQuickHide()
        {
            Settings.QuickHidden = !Settings.QuickHidden;
            foreach (var kvp in _windows)
            {
                if (Settings.QuickHidden || kvp.Value.Data.IsHidden)
                {
                    kvp.Value.Hide();
                }
                else
                {
                    kvp.Value.Show();
                    kvp.Value.EnsureBottomZOrder();
                }
            }
            SaveState();

            if (_controlCenter != null && _controlCenter.IsLoaded)
            {
                _controlCenter.RefreshBoxList();
            }
        }

        public void MoveItemBetweenBoxes(string sourceBoxId, string itemId, string targetBoxId, BoxItem targetBeforeItem)
        {
            var sourceBox = Settings.Boxes.FirstOrDefault(b => b.Id == sourceBoxId);
            var targetBox = Settings.Boxes.FirstOrDefault(b => b.Id == targetBoxId);
            if (sourceBox == null || targetBox == null || targetBox.IsFolderBox) return;

            var movingItem = sourceBox.Items.FirstOrDefault(i => i.Id == itemId);
            if (movingItem == null) return;

            if (!sourceBox.IsFolderBox)
            {
                sourceBox.Items.Remove(movingItem);
            }
            else
            {
                movingItem = new BoxItem
                {
                    FilePath = movingItem.FilePath,
                    CustomLabel = movingItem.CustomLabel
                };
            }

            if (!targetBox.Items.Any(i => string.Equals(i.FilePath, movingItem.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                var ordered = targetBox.Items.OrderBy(i => i.Order).ToList();
                if (targetBeforeItem != null)
                {
                    int idx = ordered.IndexOf(targetBeforeItem);
                    if (idx >= 0) ordered.Insert(idx, movingItem);
                    else ordered.Add(movingItem);
                }
                else
                {
                    ordered.Add(movingItem);
                }

                for (int i = 0; i < ordered.Count; i++)
                    ordered[i].Order = i;

                targetBox.Items = ordered;
            }

            BoxWindow srcWin;
            if (_windows.TryGetValue(sourceBoxId, out srcWin))
                srcWin.RefreshItems();
            BoxWindow dstWin;
            if (_windows.TryGetValue(targetBoxId, out dstWin))
                dstWin.RefreshItems();

            SaveState();
        }

        public void ApplyThemeToAll(string bgHex, string accentHex)
        {
            Settings.DefaultThemeColor = bgHex;
            foreach (var b in Settings.Boxes)
            {
                b.ThemeColor = bgHex;
                b.AccentColor = accentHex;
            }
            foreach (var win in _windows.Values)
            {
                win.ApplyAppearance();
                win.RefreshItems();
            }
            SaveState();
        }

        public void ApplyOpacityToAll(double opacity)
        {
            Settings.DefaultOpacity = opacity;
            foreach (var b in Settings.Boxes)
            {
                b.Opacity = opacity;
            }
            foreach (var win in _windows.Values)
            {
                win.ApplyAppearance();
            }
            SaveState();
        }

        public void RefreshAllBoxes()
        {
            foreach (var win in _windows.Values)
            {
                win.ApplyAppearance();
                win.RefreshItems();
            }
        }

        public void ApplySnapPosition(BoxWindow movingWin, ref double newLeft, ref double newTop)
        {
            const double edgeSnapThreshold = 14.0;
            const double boxGap = 12.0;

            double w = movingWin.ActualWidth;
            double h = movingWin.ActualHeight;

            bool snappedX = false;
            bool snappedY = false;

            Rect workArea = SystemParameters.WorkArea;
            if (Math.Abs(newLeft - (workArea.Left + boxGap)) < edgeSnapThreshold)
            {
                newLeft = workArea.Left + boxGap;
                snappedX = true;
            }
            else if (Math.Abs((newLeft + w) - (workArea.Right - boxGap)) < edgeSnapThreshold)
            {
                newLeft = workArea.Right - boxGap - w;
                snappedX = true;
            }

            if (Math.Abs(newTop - (workArea.Top + boxGap)) < edgeSnapThreshold)
            {
                newTop = workArea.Top + boxGap;
                snappedY = true;
            }
            else if (Math.Abs((newTop + h) - (workArea.Bottom - boxGap)) < edgeSnapThreshold)
            {
                newTop = workArea.Bottom - boxGap - h;
                snappedY = true;
            }

            foreach (var other in _windows.Values)
            {
                if (other == movingWin || !other.IsVisible) continue;

                double oL = other.Left;
                double oT = other.Top;
                double oR = oL + other.ActualWidth;
                double oB = oT + other.ActualHeight;

                if (!snappedX)
                {
                    if (Math.Abs(newLeft - oL) < edgeSnapThreshold) { newLeft = oL; snappedX = true; }
                    else if (Math.Abs((newLeft + w) - oR) < edgeSnapThreshold) { newLeft = oR - w; snappedX = true; }
                    else if (Math.Abs(newLeft - (oR + boxGap)) < edgeSnapThreshold) { newLeft = oR + boxGap; snappedX = true; }
                    else if (Math.Abs((newLeft + w) - (oL - boxGap)) < edgeSnapThreshold) { newLeft = oL - boxGap - w; snappedX = true; }
                }

                if (!snappedY)
                {
                    if (Math.Abs(newTop - oT) < edgeSnapThreshold) { newTop = oT; snappedY = true; }
                    else if (Math.Abs((newTop + h) - oB) < edgeSnapThreshold) { newTop = oB - h; snappedY = true; }
                    else if (Math.Abs(newTop - (oB + boxGap)) < edgeSnapThreshold) { newTop = oB + boxGap; snappedY = true; }
                    else if (Math.Abs((newTop + h) - (oT - boxGap)) < edgeSnapThreshold) { newTop = oT - boxGap - h; snappedY = true; }
                }
            }

            int g = Math.Max(8, Settings.GridSize);
            if (!snappedX)
                newLeft = Math.Round(newLeft / g) * g;
            if (!snappedY)
                newTop = Math.Round(newTop / g) * g;
        }

        public void ArrangeBoxesOnGrid()
        {
            Rect workArea = SystemParameters.WorkArea;
            double startX = workArea.Left + 40;
            double startY = workArea.Top + 40;
            double gap = 16;

            double curX = startX;
            double curY = startY;
            double rowMaxH = 0;

            foreach (var box in Settings.Boxes)
            {
                if (box.IsHidden) continue;
                if (curX + box.Width > workArea.Right - 20 && curX > startX)
                {
                    curX = startX;
                    curY += rowMaxH + gap;
                    rowMaxH = 0;
                }

                box.Left = curX;
                box.Top = curY;

                BoxWindow win;
                if (_windows.TryGetValue(box.Id, out win))
                {
                    win.Left = box.Left;
                    win.Top = box.Top;
                }

                double h = box.IsRolledUp ? BoxWindow.HeaderHeight : box.ExpandedHeight;
                if (h > rowMaxH) rowMaxH = h;
                curX += box.Width + gap;
            }

            SaveState();
        }

        public void AutoGroupDesktopIcons()
        {
            try
            {
                string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

                var allEntries = new List<string>();
                if (Directory.Exists(userDesktop))
                {
                    allEntries.AddRange(Directory.GetDirectories(userDesktop));
                    allEntries.AddRange(Directory.GetFiles(userDesktop));
                }
                if (Directory.Exists(publicDesktop))
                {
                    allEntries.AddRange(Directory.GetDirectories(publicDesktop));
                    allEntries.AddRange(Directory.GetFiles(publicDesktop));
                }

                var apps = new List<string>();
                var folders = new List<string>();
                var docs = new List<string>();

                foreach (string p in allEntries.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileName(p);
                    if (string.IsNullOrEmpty(name) || name.StartsWith(".") || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (Directory.Exists(p))
                    {
                        folders.Add(p);
                    }
                    else
                    {
                        string ext = Path.GetExtension(p).ToLowerInvariant();
                        if (ext == ".lnk" || ext == ".exe" || ext == ".url" || ext == ".bat" || ext == ".cmd")
                            apps.Add(p);
                        else
                            docs.Add(p);
                    }
                }

                if (apps.Count > 0)
                    CreateOrUpdateCategoryBox("Applications & Shortcuts", apps, "#161922", "#58A6FF", 60, 60);
                if (folders.Count > 0)
                    CreateOrUpdateCategoryBox("Desktop Folders", folders, "#0F2922", "#34D399", 460, 60);
                if (docs.Count > 0)
                    CreateOrUpdateCategoryBox("Files & Documents", docs, "#221533", "#C084FC", 60, 360);

                ArrangeBoxesOnGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error scanning Desktop items: " + ex.Message, "DeskBoxes", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CreateOrUpdateCategoryBox(string title, List<string> paths, string themeHex, string accentHex, double defaultLeft, double defaultTop)
        {
            var box = Settings.Boxes.FirstOrDefault(b => !b.IsFolderBox && string.Equals(b.Title, title, StringComparison.OrdinalIgnoreCase));
            if (box == null)
            {
                box = new BoxData
                {
                    Title = title,
                    Left = defaultLeft,
                    Top = defaultTop,
                    Width = 380,
                    Height = 260,
                    ExpandedHeight = 260,
                    ThemeColor = themeHex,
                    AccentColor = accentHex,
                    Opacity = Settings.DefaultOpacity
                };
                Settings.Boxes.Add(box);
                SpawnBoxWindow(box);
            }

            BoxWindow win;
            if (_windows.TryGetValue(box.Id, out win))
            {
                win.AddFilePaths(paths);
            }
        }

        public void ShowControlCenter()
        {
            if (_controlCenter == null || !_controlCenter.IsLoaded)
            {
                _controlCenter = new ControlCenterWindow(this);
                _controlCenter.Show();
            }
            else
            {
                _controlCenter.RefreshBoxList();
                _controlCenter.Activate();
                if (_controlCenter.WindowState == WindowState.Minimized)
                    _controlCenter.WindowState = WindowState.Normal;
            }
        }

        public void SaveState()
        {
            StorageManager.Save(Settings);
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = CreateCustomTrayIcon(),
                Text = "DeskBoxes — Desktop Organizer (Ctrl+Alt+B to Quick-Hide)",
                Visible = true
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();

            var openCenter = new System.Windows.Forms.ToolStripMenuItem("⚙️ DeskBoxes Control Center...");
            openCenter.Font = new Font(openCenter.Font, System.Drawing.FontStyle.Bold);
            openCenter.Click += (s, e) => ShowControlCenter();
            menu.Items.Add(openCenter);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var newBoxItem = new System.Windows.Forms.ToolStripMenuItem("➕ New Box");
            newBoxItem.Click += (s, e) => CreateNewBox(120, 120);
            menu.Items.Add(newBoxItem);

            var newFolderBoxItem = new System.Windows.Forms.ToolStripMenuItem("📂 New Live Folder Box...");
            newFolderBoxItem.Click += (s, e) => CreateFolderBoxInteractive(160, 160);
            menu.Items.Add(newFolderBoxItem);

            var autoGroupItem = new System.Windows.Forms.ToolStripMenuItem("✨ Auto-Group Desktop Icons");
            autoGroupItem.Click += (s, e) => AutoGroupDesktopIcons();
            menu.Items.Add(autoGroupItem);

            var arrangeItem = new System.Windows.Forms.ToolStripMenuItem("📐 Arrange Boxes Neatly");
            arrangeItem.Click += (s, e) => ArrangeBoxesOnGrid();
            menu.Items.Add(arrangeItem);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var quickHideItem = new System.Windows.Forms.ToolStripMenuItem("🙈 Quick-Hide / Show Boxes (Ctrl+Alt+B)");
            quickHideItem.Click += (s, e) => ToggleQuickHide();
            menu.Items.Add(quickHideItem);

            var lockItem = new System.Windows.Forms.ToolStripMenuItem("🔒 Lock Box Layout");
            lockItem.Click += (s, e) =>
            {
                Settings.LockLayout = !Settings.LockLayout;
                SaveState();
            };
            menu.Opening += (s, e) =>
            {
                lockItem.Checked = Settings.LockLayout;
                quickHideItem.Checked = Settings.QuickHidden;
            };
            menu.Items.Add(lockItem);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("❌ Exit DeskBoxes");
            exitItem.Click += (s, e) => Shutdown();
            menu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    ShowControlCenter();
                }
            };
        }

        private Icon CreateCustomTrayIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);

                using (var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 22, 28, 42)))
                using (var borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 88, 166, 255), 2f))
                using (var cellBrush = new SolidBrush(System.Drawing.Color.FromArgb(230, 88, 166, 255)))
                {
                    g.FillRectangle(bgBrush, 2, 2, 28, 28);
                    g.DrawRectangle(borderPen, 2, 2, 28, 28);

                    g.FillRectangle(cellBrush, 6, 6, 8, 8);
                    g.FillRectangle(cellBrush, 18, 6, 8, 8);
                    g.FillRectangle(cellBrush, 6, 18, 8, 8);
                    g.FillRectangle(cellBrush, 18, 18, 8, 8);
                }

                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private void SetupGlobalHotkeyWindow()
        {
            _hotkeyMessageWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Visibility = Visibility.Hidden
            };

            var helper = new WindowInteropHelper(_hotkeyMessageWindow);
            helper.EnsureHandle();

            IntPtr hwnd = helper.Handle;
            if (hwnd != IntPtr.Zero)
            {
                NativeInterop.RegisterHotKey(
                    hwnd,
                    HOTKEY_ID_QUICKHIDE_B,
                    NativeInterop.MOD_CONTROL | NativeInterop.MOD_ALT | NativeInterop.MOD_NOREPEAT,
                    NativeInterop.VK_B
                );
                NativeInterop.RegisterHotKey(
                    hwnd,
                    HOTKEY_ID_QUICKHIDE_F,
                    NativeInterop.MOD_CONTROL | NativeInterop.MOD_ALT | NativeInterop.MOD_NOREPEAT,
                    NativeInterop.VK_F
                );

                HwndSource source = HwndSource.FromHwnd(hwnd);
                if (source != null)
                {
                    source.AddHook((IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                    {
                        if (msg == NativeInterop.WM_HOTKEY)
                        {
                            int id = wParam.ToInt32();
                            if (id == HOTKEY_ID_QUICKHIDE_B || id == HOTKEY_ID_QUICKHIDE_F)
                            {
                                ToggleQuickHide();
                                handled = true;
                            }
                        }
                        return IntPtr.Zero;
                    });
                }
            }
        }

        public void Shutdown()
        {
            SaveState();

            if (_hotkeyMessageWindow != null)
            {
                var helper = new WindowInteropHelper(_hotkeyMessageWindow);
                if (helper.Handle != IntPtr.Zero)
                {
                    NativeInterop.UnregisterHotKey(helper.Handle, HOTKEY_ID_QUICKHIDE_B);
                    NativeInterop.UnregisterHotKey(helper.Handle, HOTKEY_ID_QUICKHIDE_F);
                }
            }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            Application.Current.Shutdown();
        }
    }

    public static class Program
    {
        private static Mutex _singleInstanceMutex;

        [STAThread]
        public static void Main(string[] args)
        {
            // Terminate any older running instance of DeskBoxes
            try
            {
                int currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var oldProcs = System.Diagnostics.Process.GetProcessesByName("DeskBoxes");
                foreach (var proc in oldProcs)
                {
                    if (proc.Id != currentPid)
                    {
                        try
                        {
                            proc.Kill();
                            proc.WaitForExit(1500);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "DeskBoxesApp_Mutex_v1", out createdNew);

            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            var manager = new BoxManager();
            app.Startup += (s, e) =>
            {
                manager.Initialize();
                if (args != null && args.Any(a => a.Equals("--control-center", StringComparison.OrdinalIgnoreCase)))
                {
                    manager.ShowControlCenter();
                }
            };

            app.Run();
        }
    }
}
