using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace DeskBoxes
{
    public class BoxItem
    {
        public string Id { get; set; }
        public string FilePath { get; set; }
        public string OriginalPath { get; set; }
        public string CustomLabel { get; set; }
        public int Order { get; set; }

        public BoxItem()
        {
            Id = Guid.NewGuid().ToString("N");
        }
    }

    public class BoxData
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double ExpandedHeight { get; set; }
        public bool IsRolledUp { get; set; }
        public bool IsHidden { get; set; }

        // Live Folder Box settings
        public bool IsFolderBox { get; set; }
        public string FolderPath { get; set; }
        public string SortBy { get; set; } // "Manual", "Name", "Date", "Type"

        // Appearance
        public string ThemeColor { get; set; } // Hex RGB e.g. "#161922"
        public string AccentColor { get; set; } // Hex RGB e.g. "#58A6FF"
        public double Opacity { get; set; } // 0.15 to 0.98

        public List<BoxItem> Items { get; set; }

        public BoxData()
        {
            Id = Guid.NewGuid().ToString("N");
            Title = "New Box";
            Left = 100;
            Top = 100;
            Width = 360;
            Height = 260;
            ExpandedHeight = 260;
            IsRolledUp = false;
            IsHidden = false;
            IsFolderBox = false;
            FolderPath = "";
            SortBy = "Manual";
            ThemeColor = "#161922";
            AccentColor = "#58A6FF";
            Opacity = 0.72;
            Items = new List<BoxItem>();
        }
    }

    public class AppSettings
    {
        public bool LockLayout { get; set; }
        public bool SnapToGrid { get; set; }
        public int GridSize { get; set; }
        public bool HoverExpandRolledUp { get; set; }
        public bool SingleClickLaunch { get; set; }
        public bool QuickHidden { get; set; }
        public int IconSize { get; set; } // 36, 48, 64
        public bool ShowLabels { get; set; }
        public double DefaultOpacity { get; set; }
        public string DefaultThemeColor { get; set; }
        public List<BoxData> Boxes { get; set; }

        public AppSettings()
        {
            LockLayout = false;
            SnapToGrid = true;
            GridSize = 20;
            HoverExpandRolledUp = true;
            SingleClickLaunch = false;
            QuickHidden = false;
            IconSize = 48;
            ShowLabels = true;
            DefaultOpacity = 0.72;
            DefaultThemeColor = "#161922";
            Boxes = new List<BoxData>();
        }
    }

    public static class ThemePreset
    {
        public class Preset
        {
            public string Name;
            public string BgHex;
            public string AccentHex;

            public Preset(string name, string bg, string accent)
            {
                Name = name;
                BgHex = bg;
                AccentHex = accent;
            }
        }

        public static readonly Preset[] Presets = new Preset[]
        {
            new Preset("Obsidian Glass", "#161922", "#58A6FF"),
            new Preset("Midnight Ocean", "#0F1E36", "#38BDF8"),
            new Preset("Emerald Slate", "#0F2922", "#34D399"),
            new Preset("Royal Amethyst", "#221533", "#C084FC"),
            new Preset("Crimson Dusk", "#2D141C", "#FB7185"),
            new Preset("Warm Amber", "#291D12", "#FBBF24"),
            new Preset("Pure Charcoal", "#18181B", "#A1A1AA"),
            new Preset("Frost Light", "#DCE4F0", "#2563EB")
        };
    }

    public static class StorageManager
    {
        public static string GetAppDataDir()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(localAppData, "DeskBoxes");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private static string GetStateFilePath()
        {
            return Path.Combine(GetAppDataDir(), "boxes-state.json");
        }

        public static AppSettings Load()
        {
            string stateFile = GetStateFilePath();

            try
            {
                if (File.Exists(stateFile))
                {
                    string json = File.ReadAllText(stateFile);
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var settings = serializer.Deserialize<AppSettings>(json);
                    if (settings != null && settings.Boxes != null && settings.Boxes.Count > 0)
                    {
                        return settings;
                    }
                }
            }
            catch
            {
                // Fall back to default settings
            }

            return CreateDefaultSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                string json = serializer.Serialize(settings);
                File.WriteAllText(GetStateFilePath(), json);
            }
            catch { }
        }

        public static AppSettings CreateDefaultSettings()
        {
            var settings = new AppSettings();

            // 1. Starter Box (empty by default so new users can drag & drop what they want)
            var mainBox = new BoxData
            {
                Title = "Quick Launch",
                Left = 60,
                Top = 60,
                Width = 360,
                Height = 250,
                ExpandedHeight = 250,
                ThemeColor = "#161922",
                AccentColor = "#58A6FF",
                Opacity = 0.75
            };
            settings.Boxes.Add(mainBox);

            // 2. Downloads Live Folder Box
            string downloadsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads"
            );
            if (!Directory.Exists(downloadsPath))
                downloadsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var downloadsBox = new BoxData
            {
                Title = "Downloads",
                Left = 440,
                Top = 60,
                Width = 380,
                Height = 250,
                ExpandedHeight = 250,
                IsFolderBox = true,
                FolderPath = downloadsPath,
                SortBy = "Date",
                ThemeColor = "#0F1E36",
                AccentColor = "#38BDF8",
                Opacity = 0.75
            };
            settings.Boxes.Add(downloadsBox);

            return settings;
        }
    }
}
