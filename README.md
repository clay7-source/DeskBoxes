# ❖ DeskBoxes

**DeskBoxes** is an ultra-lightweight (~80 KB), zero-dependency desktop icon organizer for Windows 10 & 11. Group your shortcuts, games, apps, and folders into translucent glass boxes pinned directly to your desktop layer.

---

## ✨ Features

- **Featherweight & Portable (~80 KB Single `.exe`)**
  - Built against the native Windows `.NET Framework 4.x` and Win32 Shell APIs (`user32.dll`, `shell32.dll`) already included in every installation of Windows 10 and 11.
  - Zero external `.dll` files, installers, or background services.
- **True Desktop-Layer Pinning**
  - Stays anchored behind normal application windows, hidden from `Alt+Tab` and the taskbar, and immune to `Win+D` (*Show Desktop*).
- **Automatic Desktop Icon Concealment**
  - Dragging an icon from your Windows Desktop into a Box automatically moves/conceals it from the outside desktop so you never see duplicate icons.
  - Right-click any icon (`↩️ Return to Desktop`), drag it back out onto the desktop, or delete a box to restore icons back to your Desktop at any time.
- **Title-Bar Roll-Up & Hover Peek**
  - **Double-click any box header** (or click `▾`/`▸`) to roll the box up into a compact title bar.
  - Hovering over a rolled-up box header temporarily expands it so you can launch an icon, then automatically collapses when your cursor leaves.
- **📂 Live Folder Boxes**
  - Mirror any folder on your PC (such as `Downloads`, `Documents`, or a project directory) live on your desktop with real-time `FileSystemWatcher` updates and subfolder navigation (`⬆ Up` & `↗ Open in Explorer`).
- **Custom Themes, Opacity & Magnetic Snapping**
  - **8 Built-in Color Themes**: *Obsidian Glass*, *Midnight Ocean*, *Emerald Slate*, *Royal Amethyst*, *Crimson Dusk*, *Warm Amber*, *Pure Charcoal*, and *Frost Light*.
  - Per-box and global **Opacity Control** (`15%` to `98%`).
  - **Snap-to-Grid (20px) & Magnetic Edge Alignment** with neighboring boxes and screen borders.
  - **Quick-Hide (`Ctrl + Alt + B`)**: Instantly toggle all desktop boxes on or off via hotkey or the System Tray icon.

---

## 🚀 Getting Started

### Option 1: Download `DeskBoxes.exe`
Download **[`DeskBoxes.exe` from the Latest Release](https://github.com/clay7-source/DeskBoxes/releases/latest)** and run it.
- **Left-click** the System Tray icon to open the **DeskBoxes Control Center**.
- **Right-click** inside any Box for theme, opacity, sorting, and layout options.
- Settings and boxed desktop shortcuts are stored cleanly in `%LOCALAPPDATA%\DeskBoxes`.

### Option 2: Build from Source (No Visual Studio Required!)
DeskBoxes compiles in ~1 second using the C# compiler (`csc.exe`) built into Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

---

## 📄 License

Licensed under the [MIT License](LICENSE).
