using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DeskBoxes
{
    public static class NativeInterop
    {
        public const int GWL_EXSTYLE = -20;
        public const int GWL_HWNDPARENT = -8;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int WM_WINDOWPOSCHANGING = 0x0046;
        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_MINIMIZE = 0xF020;
        public const int WM_HOTKEY = 0x0312;

        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOSENDCHANGING = 0x0400;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_NOREPEAT = 0x4000;
        public const uint VK_B = 0x42;
        public const uint VK_F = 0x46;

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags
        );

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public const int SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_IDLIST = 0x0000;

        public const uint LVM_FIRST = 0x1000;
        public const uint LVM_GETITEMCOUNT = LVM_FIRST + 4;
        public const uint LVM_DELETEITEM = LVM_FIRST + 8;
        public const uint LVM_GETITEMTEXTW = LVM_FIRST + 115;

        public const uint PROCESS_VM_OPERATION = 0x0008;
        public const uint PROCESS_VM_READ = 0x0010;
        public const uint PROCESS_VM_WRITE = 0x0020;
        public const uint MEM_COMMIT = 0x1000;
        public const uint MEM_RESERVE = 0x2000;
        public const uint MEM_RELEASE = 0x8000;
        public const uint PAGE_READWRITE = 0x04;

        // Shell Icon Extraction
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_SYSICONINDEX = 0x000004000;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        public const int SHIL_EXTRALARGE = 0x2; // 48x48

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags
        );

        [DllImport("shell32.dll", EntryPoint = "#727")]
        public static extern int SHGetImageList(int iImageList, ref Guid riid, ref IImageList ppv);

        [ComImport]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IImageList
        {
            [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
            [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
            [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
            [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
            [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
            [PreserveSig] int Draw(IntPtr pimldp);
            [PreserveSig] int Remove(int i);
            [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        private static readonly Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static void PinWindowToDesktop(Window window)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            IntPtr desktopOwner = FindDesktopShellViewParent();
            if (desktopOwner != IntPtr.Zero)
            {
                SetWindowLong(hwnd, GWL_HWNDPARENT, desktopOwner.ToInt32());
            }

            SendToDesktopBottom(hwnd);

            HwndSource source = HwndSource.FromHwnd(hwnd);
            if (source != null)
            {
                source.AddHook(DesktopWindowWndProc);
            }
        }

        public static void SendToDesktopBottom(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            SetWindowPos(
                hwnd,
                HWND_BOTTOM,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOSENDCHANGING
            );
        }

        private static IntPtr FindDesktopShellViewParent()
        {
            IntPtr progman = FindWindow("Progman", null);
            IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
            {
                return progman;
            }

            IntPtr workerW = IntPtr.Zero;
            do
            {
                workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null);
                if (workerW != IntPtr.Zero)
                {
                    defView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (defView != IntPtr.Zero)
                    {
                        return workerW;
                    }
                }
            } while (workerW != IntPtr.Zero);

            return progman;
        }

        private static IntPtr DesktopWindowWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SYSCOMMAND)
            {
                int command = wParam.ToInt32() & 0xFFF0;
                if (command == SC_MINIMIZE)
                {
                    handled = true;
                    return IntPtr.Zero;
                }
            }
            return IntPtr.Zero;
        }

        public static string GetCleanDisplayName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "Unknown";
            try
            {
                string name = Path.GetFileName(path);
                if (string.IsNullOrEmpty(name))
                    return path;

                if (name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFileNameWithoutExtension(name);
                }
                return name;
            }
            catch
            {
                return path;
            }
        }

        public static ImageSource GetFileIcon(string path, bool forceRefresh = false)
        {
            if (string.IsNullOrEmpty(path)) return null;

            ImageSource cached;
            if (!forceRefresh && _iconCache.TryGetValue(path, out cached))
            {
                return cached;
            }

            IntPtr hIcon = IntPtr.Zero;
            try
            {
                bool isDir = Directory.Exists(path);
                bool exists = isDir || File.Exists(path);

                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = SHGFI_SYSICONINDEX;
                uint attr = isDir ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
                if (!exists)
                {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                }

                IntPtr res = SHGetFileInfo(path, attr, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (res != IntPtr.Zero)
                {
                    Guid iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                    IImageList iml = null;
                    int hr = SHGetImageList(SHIL_EXTRALARGE, ref iidImageList, ref iml);
                    if (hr == 0 && iml != null)
                    {
                        iml.GetIcon(shfi.iIcon, 0x00000001, out hIcon);
                    }
                }

                if (hIcon == IntPtr.Zero)
                {
                    SHFILEINFO shfi2 = new SHFILEINFO();
                    uint flags2 = SHGFI_ICON | SHGFI_LARGEICON;
                    if (!exists) flags2 |= SHGFI_USEFILEATTRIBUTES;
                    SHGetFileInfo(path, attr, ref shfi2, (uint)Marshal.SizeOf(shfi2), flags2);
                    hIcon = shfi2.hIcon;
                }

                if (hIcon != IntPtr.Zero)
                {
                    ImageSource img = Imaging.CreateBitmapSourceFromHIcon(
                        hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );
                    img.Freeze();
                    _iconCache[path] = img;
                    return img;
                }
            }
            catch { }
            finally
            {
                if (hIcon != IntPtr.Zero)
                {
                    DestroyIcon(hIcon);
                }
            }

            return null;
        }

        public static void LaunchItem(string path, bool asAdmin = false)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                if (asAdmin)
                {
                    psi.Verb = "runas";
                }
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not open item:\n" + path + "\n\n" + ex.Message,
                    "DeskBoxes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        public static void RevealInExplorer(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", "\"" + path + "\"");
                }
                else if (File.Exists(path))
                {
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
            }
            catch { }
        }

        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRunName = "DeskBoxesApp";

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(AppRunName);
                    return val != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetStartupEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key == null) return;
                    if (enabled)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue(AppRunName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(AppRunName, false);
                    }
                }
            }
            catch { }
        }

        // Desktop Vault & Desktop ListView Concealment
        public static string GetVaultDirectory()
        {
            string dir = Path.Combine(StorageManager.GetAppDataDir(), "BoxedItems");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static bool IsDesktopPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                string parent = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(parent)) return false;

                string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

                if (string.Equals(parent, userDesktop, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parent, publicDesktop, StringComparison.OrdinalIgnoreCase) ||
                    parent.EndsWith("\\Desktop", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static bool ConcealItemFromDesktop(BoxItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.FilePath)) return false;

            string vaultDir = GetVaultDirectory();
            bool modified = false;

            if (IsDesktopPath(item.FilePath))
            {
                string sourcePath = item.FilePath;
                if (string.IsNullOrEmpty(item.OriginalPath))
                {
                    item.OriginalPath = sourcePath;
                    modified = true;
                }

                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(vaultDir, fileName);

                try
                {
                    if (File.Exists(sourcePath))
                    {
                        if (File.Exists(destPath))
                        {
                            File.Delete(destPath);
                        }
                        File.Move(sourcePath, destPath);
                        item.FilePath = destPath;
                        modified = true;
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        if (!Directory.Exists(destPath))
                        {
                            Directory.Move(sourcePath, destPath);
                        }
                        item.FilePath = destPath;
                        modified = true;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    try
                    {
                        if (File.Exists(sourcePath))
                        {
                            File.Copy(sourcePath, destPath, true);
                            item.FilePath = destPath;
                            modified = true;
                        }
                    }
                    catch { }
                }
                catch { }
            }

            string checkDesktopPath = !string.IsNullOrEmpty(item.OriginalPath) ? item.OriginalPath : item.FilePath;
            if (!string.IsNullOrEmpty(checkDesktopPath) && (File.Exists(checkDesktopPath) || Directory.Exists(checkDesktopPath)) && IsDesktopPath(checkDesktopPath))
            {
                string cleanName = GetCleanDisplayName(checkDesktopPath);
                string rawName = Path.GetFileName(checkDesktopPath);
                RemoveIconsFromDesktopListView(new[] { cleanName, rawName });
            }

            return modified;
        }

        public static void RestoreItemToDesktop(BoxItem item)
        {
            if (item == null) return;

            bool needsDesktopRefresh = false;

            if (!string.IsNullOrEmpty(item.OriginalPath))
            {
                string vaultPath = item.FilePath;
                string origPath = item.OriginalPath;

                try
                {
                    if (!string.Equals(vaultPath, origPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(vaultPath))
                        {
                            if (!File.Exists(origPath))
                            {
                                string origDir = Path.GetDirectoryName(origPath);
                                if (!string.IsNullOrEmpty(origDir) && Directory.Exists(origDir))
                                {
                                    File.Move(vaultPath, origPath);
                                }
                            }
                            else
                            {
                                File.Delete(vaultPath);
                                needsDesktopRefresh = true;
                            }
                        }
                        else if (Directory.Exists(vaultPath) && !Directory.Exists(origPath))
                        {
                            Directory.Move(vaultPath, origPath);
                        }
                    }
                }
                catch
                {
                    try
                    {
                        string userDesk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string fallbackPath = Path.Combine(userDesk, Path.GetFileName(origPath));
                        if (File.Exists(vaultPath) && !File.Exists(fallbackPath))
                        {
                            File.Move(vaultPath, fallbackPath);
                            origPath = fallbackPath;
                        }
                    }
                    catch { }
                }

                item.FilePath = origPath;
                item.OriginalPath = null;
            }

            if (needsDesktopRefresh)
            {
                RefreshDesktopShellView();
            }
            else
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static IntPtr FindDesktopShellDefView()
        {
            IntPtr progman = FindWindow("Progman", null);
            IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero) return defView;

            IntPtr workerW = IntPtr.Zero;
            do
            {
                workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null);
                if (workerW != IntPtr.Zero)
                {
                    defView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (defView != IntPtr.Zero) return defView;
                }
            } while (workerW != IntPtr.Zero);

            return IntPtr.Zero;
        }

        public static IntPtr FindDesktopListView()
        {
            IntPtr defView = FindDesktopShellDefView();
            if (defView == IntPtr.Zero) return IntPtr.Zero;
            return FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
        }

        public static void RefreshDesktopShellView()
        {
            IntPtr defView = FindDesktopShellDefView();
            if (defView != IntPtr.Zero)
            {
                PostMessage(defView, 0x0111, new IntPtr(0x7103), IntPtr.Zero);
            }
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        public static void RemoveIconsFromDesktopListView(IEnumerable<string> targetNames)
        {
            if (targetNames == null) return;
            var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in targetNames)
            {
                if (!string.IsNullOrWhiteSpace(n))
                    nameSet.Add(n.Trim());
            }
            if (nameSet.Count == 0) return;

            IntPtr hwndLv = FindDesktopListView();
            if (hwndLv == IntPtr.Zero) return;

            int count = (int)SendMessage(hwndLv, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            if (count <= 0) return;

            uint pid;
            GetWindowThreadProcessId(hwndLv, out pid);
            if (pid == 0) return;

            IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, pid);
            if (hProcess == IntPtr.Zero) return;

            IntPtr remoteMem = IntPtr.Zero;
            try
            {
                remoteMem = VirtualAllocEx(hProcess, IntPtr.Zero, 2048, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (remoteMem == IntPtr.Zero) return;

                IntPtr remoteTextPtr = new IntPtr(remoteMem.ToInt64() + 256);

                for (int i = count - 1; i >= 0; i--)
                {
                    byte[] lvItemBytes = new byte[88];
                    BitConverter.GetBytes((uint)0x0001).CopyTo(lvItemBytes, 0);
                    BitConverter.GetBytes(i).CopyTo(lvItemBytes, 4);
                    BitConverter.GetBytes(0).CopyTo(lvItemBytes, 8);
                    BitConverter.GetBytes(remoteTextPtr.ToInt64()).CopyTo(lvItemBytes, 24);
                    BitConverter.GetBytes(260).CopyTo(lvItemBytes, 32);

                    IntPtr bytesWritten;
                    if (!WriteProcessMemory(hProcess, remoteMem, lvItemBytes, (uint)lvItemBytes.Length, out bytesWritten))
                        continue;

                    int charLen = (int)SendMessage(hwndLv, LVM_GETITEMTEXTW, new IntPtr(i), remoteMem);
                    if (charLen > 0 && charLen < 260)
                    {
                        byte[] textBuf = new byte[charLen * 2];
                        IntPtr bytesRead;
                        if (ReadProcessMemory(hProcess, remoteTextPtr, textBuf, (uint)textBuf.Length, out bytesRead))
                        {
                            string itemText = System.Text.Encoding.Unicode.GetString(textBuf).TrimEnd('\0').Trim();
                            if (nameSet.Contains(itemText))
                            {
                                SendMessage(hwndLv, LVM_DELETEITEM, new IntPtr(i), IntPtr.Zero);
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (remoteMem != IntPtr.Zero)
                    VirtualFreeEx(hProcess, remoteMem, 0, MEM_RELEASE);
                CloseHandle(hProcess);
            }
        }
    }
}
