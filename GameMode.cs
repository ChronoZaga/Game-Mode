// Game Mode - single-file build
// Build with GameMode-Build.bat (x64). The version number is set in the .bat file.
// Uses the C# compiler built into Windows, which only supports C# 5 - keep newer syntax out
// (no $"..." strings, ?. operators, inline "out string x", or "is int x" patterns).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;


[assembly: AssemblyTitle("Game Mode")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("Game Mode")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("f94acb43-f253-425a-8565-5c2ee5926250")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]

namespace Game_Mode
{
    static class Program
    {
        // Mark the process DPI-aware (system DPI) so Windows doesn't bitmap-stretch the windows
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }

    public class Form1 : Form
    {
        // Import keybd_event from user32.dll
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        // DWM titlebar text color
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        // Hide titlebar icon only
        private const int WS_EX_DLGMODALFRAME = 0x00000001;

        // NVAPI P/Invoke declarations
        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl, PreserveSig = true)]
        private static extern IntPtr nvapi_QueryInterface(uint offset);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvAPI_InitializeDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvAPI_EnumNvidiaDisplayHandleDelegate(uint thisEnum, out IntPtr displayHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvAPI_SetDVCLevelDelegate(IntPtr displayHandle, uint outputId, uint level);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvAPI_GetDVCInfoDelegate(IntPtr displayHandle, uint outputId, ref NV_DISPLAY_DVC_INFO dvcInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct NV_DISPLAY_DVC_INFO
        {
            public uint version;
            public uint currentLevel;
            public uint minLevel;
            public uint maxLevel;
        }

        private const uint NV_DISPLAY_DVC_INFO_VER = (uint)(16 | (1 << 16)); // sizeof(NV_DISPLAY_DVC_INFO) | version
        private const uint NVAPI_MAX_PHYSICAL_GPUS = 64;
        private const uint NVAPI_MAX_DISPLAY_HEADS = 2;

        private static IntPtr nvapiHandle = IntPtr.Zero;
        private static IntPtr NvAPI_Initialize = IntPtr.Zero;
        private static IntPtr NvAPI_EnumNvidiaDisplayHandle = IntPtr.Zero;
        private static IntPtr NvAPI_SetDVCLevel = IntPtr.Zero;
        private static IntPtr NvAPI_GetDVCInfo = IntPtr.Zero;
        private static bool nvapiInitialized = false;

        // Virtual key codes
        private const byte VK_VOLUME_UP = 0xAF;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_LWIN = 0x5B; // Left Windows key
        private const byte VK_MENU = 0x12; // Alt key
        private const byte VK_B = 0x42;    // B key
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private readonly string iniPath;

        // Screen DPI scale (1.0 = 100%), used for sizes that are set in pixels
        private static readonly float dpiScale = GetDpiScale();

        private static float GetDpiScale()
        {
            using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
            {
                return g.DpiX / 96f;
            }
        }

        private static int Dpi(int pixels)
        {
            return (int)Math.Round(pixels * dpiScale);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_DLGMODALFRAME;
                return cp;
            }
        }

        public Form1()
        {
            InitializeComponent();

            // Make titlebar text match the titlebar color
            ApplyTitleBarTextColor();
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            // Set the form icon to the gamepad icon from joy.cpl
            this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(@"C:\Windows\System32\joy.cpl");

            // Initialize INI file path (same directory as executable)
            iniPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "GameMode.ini");

            // Create INI file if it doesn't exist
            CreateIniFileIfNotExists();

            // Initialize NVAPI
            InitializeNVAPI();

            // Register form closed event for cleanup
            this.FormClosed += Form1_FormClosed;
        }

        private void ApplyTitleBarTextColor()
        {
            // Pin the caption to its current color (so it doesn't change when the window is inactive),
            // then paint the title text in that exact same color
            int titleBarColor = GetTitleBarColor();
            DwmSetWindowAttribute(this.Handle, DWMWA_CAPTION_COLOR, ref titleBarColor, sizeof(int));
            DwmSetWindowAttribute(this.Handle, DWMWA_TEXT_COLOR, ref titleBarColor, sizeof(int));

            // Match the window background to the titlebar too
            this.BackColor = System.Drawing.ColorTranslator.FromWin32(titleBarColor);
        }

        // Returns the titlebar color as a COLORREF (0x00BBGGRR)
        private int GetTitleBarColor()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                // "Show accent color on title bars and window borders" is on
                if (key != null)
                {
                    object prevalence = key.GetValue("ColorPrevalence");
                    if (prevalence is int && (int)prevalence == 1)
                    {
                        object accentColor = key.GetValue("AccentColor");
                        if (accentColor is int)
                        {
                            // AccentColor is stored as 0xAABBGGRR; dropping alpha gives a COLORREF
                            return (int)accentColor & 0x00FFFFFF;
                        }
                    }
                }
            }
            // Default light titlebar
            return System.Drawing.ColorTranslator.ToWin32(System.Drawing.Color.White);
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // Re-apply if the accent color or titlebar setting changes while running
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color)
            {
                ApplyTitleBarTextColor();
            }
        }

        private void InitializeNVAPI()
        {
            try
            {
                // Load nvapi64.dll from System32 for x64
                string nvapi64Path = Path.Combine(@"C:\Windows\System32", "nvapi64.dll");
                nvapiHandle = LoadLibrary(nvapi64Path);
                if (nvapiHandle == IntPtr.Zero)
                    return; // No NVIDIA driver installed

                // Get NVAPI function pointers
                NvAPI_Initialize = nvapi_QueryInterface(0x0150E828); // NvAPI_Initialize
                NvAPI_EnumNvidiaDisplayHandle = nvapi_QueryInterface(0x9ABDD40D); // NvAPI_EnumNvidiaDisplayHandle
                NvAPI_SetDVCLevel = nvapi_QueryInterface(0x172409B4); // NvAPI_SetDVCLevel
                NvAPI_GetDVCInfo = nvapi_QueryInterface(0x4085DE45); // NvAPI_GetDVCInfo

                if (NvAPI_Initialize == IntPtr.Zero || NvAPI_EnumNvidiaDisplayHandle == IntPtr.Zero ||
                    NvAPI_SetDVCLevel == IntPtr.Zero || NvAPI_GetDVCInfo == IntPtr.Zero)
                    return;

                // Initialize NVAPI
                var initializeDelegate = Marshal.GetDelegateForFunctionPointer<NvAPI_InitializeDelegate>(NvAPI_Initialize);
                int status = initializeDelegate();
                if (status == 0) // NVAPI_OK
                {
                    nvapiInitialized = true;
                }
            }
            catch
            {
                // NVAPI unavailable (non-NVIDIA GPU or 32-bit process); digital vibrance will be skipped
                nvapiInitialized = false;
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

            if (nvapiHandle != IntPtr.Zero)
            {
                FreeLibrary(nvapiHandle);
                nvapiHandle = IntPtr.Zero;
            }
        }

        private void SetDigitalVibrance(string argument)
        {
            if (!nvapiInitialized)
            {
                return;
            }

            // Parse argument (same as DVChange: 0-63)
            uint dvcValue;
            if (!uint.TryParse(argument, out dvcValue))
            {
                return;
            }

            // Clamp value to 0-63, as per DVChange
            if (dvcValue > 63) dvcValue = 63;

            // Enumerate displays
            var enumDisplayDelegate = Marshal.GetDelegateForFunctionPointer<NvAPI_EnumNvidiaDisplayHandleDelegate>(NvAPI_EnumNvidiaDisplayHandle);
            var setDVCDelegate = Marshal.GetDelegateForFunctionPointer<NvAPI_SetDVCLevelDelegate>(NvAPI_SetDVCLevel);
            var getDVCDelegate = Marshal.GetDelegateForFunctionPointer<NvAPI_GetDVCInfoDelegate>(NvAPI_GetDVCInfo);

            IntPtr[] displayHandles = new IntPtr[NVAPI_MAX_PHYSICAL_GPUS * NVAPI_MAX_DISPLAY_HEADS];
            uint displayCount = 0;
            for (uint i = 0; ; i++)
            {
                IntPtr displayHandle;
                int status = enumDisplayDelegate(i, out displayHandle);
                if (status != 0) // NVAPI_OK = 0
                    break;
                displayHandles[displayCount++] = displayHandle;
            }

            if (displayCount == 0)
            {
                return;
            }

            // Set and verify Digital Vibrance for each display
            NV_DISPLAY_DVC_INFO dvcInfo = new NV_DISPLAY_DVC_INFO { version = NV_DISPLAY_DVC_INFO_VER };
            for (uint i = 0; i < displayCount; i++)
            {
                // Set DVC level
                setDVCDelegate(displayHandles[i], 0, dvcValue);

                // Verify DVC level
                getDVCDelegate(displayHandles[i], 0, ref dvcInfo);
            }
        }

        private void CreateIniFileIfNotExists()
        {
            if (File.Exists(iniPath))
                return;

            try
            {
                // Define default INI content with steps ordered by execution
                string iniContent = @";Created by ChronoZaga
;https://github.com/ChronoZaga/Game-Mode/releases

[GameMode]
SetButtonColors=1
LaunchGameSelector=1
SetPowerPlan=1
SetDigitalVibrance=1
AdjustVolume=1
ToggleHDR=1
PlaySound=1

[DesktopMode]
SetButtonColors=1
SetPowerPlan=1
SetDigitalVibrance=1
AdjustVolume=1
ToggleHDR=1

[GameModeLaunchAlso]
C:\Program Files\Cheat Happens Aurora\Aurora.exe
C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA App.exe
C:\Windows\System32\taskmgr.exe
C:\Program Files\Mozilla Firefox\firefox.exe
C:\Program Files\Tools\Black Window.exe

[DesktopModeLaunchAlso]
C:\Program Files\Tools\EZWinGet.exe
C:\Program Files\KeePass Password Safe 2\KeePass.exe
C:\Program Files\Microsoft OneDrive\OneDrive.vbs

[GameModeKillAlso]
olk
ms-teams
CiscoCollabHost
EZWinGet
Claude
OneDrive
KeePass

[DesktopModeKillAlso]
C:\Program Files\Cheat Happens Aurora\Aurora.exe
C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA App.exe
taskmgr
Black Window

[VibranceSettings]
GameVibe=12
DeskVibe=0

[GameSelectorFolders]
Call of Duty
Games
Steam

[GameSelectorExclusions]
3DMark
Steam
Steam Support Center
";

                File.WriteAllText(iniPath, iniContent);

                // Verify the file was created
                if (!File.Exists(iniPath))
                {
                    MessageBox.Show("Failed to create INI file at " + iniPath + ". The application will now close. Try running as Admin once.","Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to create INI file at " + iniPath + ": " + ex.Message + " The application will now close. Try running as Admin once.","Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }
        }

        private Dictionary<string, Dictionary<string, string>> ReadIniFile()
        {
            var settings = new Dictionary<string, Dictionary<string, string>>();
            var launchAlsoSettings = new Dictionary<string, List<string>>();
            var gameSelectorFolders = new List<string>();
            var gameSelectorExclusions = new List<string>();
            string currentSection = null;

            if (!File.Exists(iniPath))
            {
                MessageBox.Show("INI file not found: " + iniPath + ". The application will now close.","Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }

            foreach (string line in File.ReadAllLines(iniPath))
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";"))
                    continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    if (currentSection == "GameModeLaunchAlso" || currentSection == "DesktopModeLaunchAlso" || currentSection == "GameModeKillAlso" || currentSection == "DesktopModeKillAlso")
                    {
                        launchAlsoSettings[currentSection] = new List<string>();
                    }
                    else if (currentSection == "GameSelectorFolders" || currentSection == "GameSelectorExclusions")
                    {
                        if (currentSection == "GameSelectorFolders")
                            gameSelectorFolders = new List<string>();
                        else
                            gameSelectorExclusions = new List<string>();
                    }
                    else
                    {
                        settings[currentSection] = new Dictionary<string, string>();
                    }
                }
                else if (currentSection != null)
                {
                    if (currentSection == "GameModeLaunchAlso" || currentSection == "DesktopModeLaunchAlso" || currentSection == "GameModeKillAlso" || currentSection == "DesktopModeKillAlso")
                    {
                        if (!string.IsNullOrWhiteSpace(trimmedLine))
                        {
                            launchAlsoSettings[currentSection].Add(trimmedLine);
                        }
                    }
                    else if (currentSection == "GameSelectorFolders" || currentSection == "GameSelectorExclusions")
                    {
                        if (!string.IsNullOrWhiteSpace(trimmedLine))
                        {
                            if (currentSection == "GameSelectorFolders")
                                gameSelectorFolders.Add(trimmedLine);
                            else
                                gameSelectorExclusions.Add(trimmedLine);
                        }
                    }
                    else if (trimmedLine.Contains("="))
                    {
                        string[] parts = trimmedLine.Split(new[] { '=' }, 2);
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();
                        settings[currentSection][key] = value;
                    }
                }
            }

            // Store launchAlsoSettings in settings for simplicity
            settings["GameModeLaunchAlso"] = launchAlsoSettings.ContainsKey("GameModeLaunchAlso")
                ? ToListDictionary(launchAlsoSettings["GameModeLaunchAlso"])
                : new Dictionary<string, string>();
            settings["DesktopModeLaunchAlso"] = launchAlsoSettings.ContainsKey("DesktopModeLaunchAlso")
                ? ToListDictionary(launchAlsoSettings["DesktopModeLaunchAlso"])
                : new Dictionary<string, string>();
            // Store kill lists
            settings["GameModeKillAlso"] = launchAlsoSettings.ContainsKey("GameModeKillAlso")
                ? ToListDictionary(launchAlsoSettings["GameModeKillAlso"])
                : new Dictionary<string, string>();
            settings["DesktopModeKillAlso"] = launchAlsoSettings.ContainsKey("DesktopModeKillAlso")
                ? ToListDictionary(launchAlsoSettings["DesktopModeKillAlso"])
                : new Dictionary<string, string>();
            // Store game selector folders and exclusions in settings
            settings["GameSelectorFolders"] = ToListDictionary(gameSelectorFolders);
            settings["GameSelectorExclusions"] = ToListDictionary(gameSelectorExclusions);

            return settings;
        }

        // Convert an INI list section to a dictionary, ignoring duplicate entries
        private Dictionary<string, string> ToListDictionary(IEnumerable<string> entries)
        {
            return entries
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(entry => entry, _ => "1", StringComparer.OrdinalIgnoreCase);
        }

        // Parse an INI on/off value; accepts 1/0 as well as true/false
        private bool TryParseSetting(string value, out bool result)
        {
            string trimmed = value == null ? null : value.Trim();
            if (trimmed == "1")
            {
                result = true;
                return true;
            }
            if (trimmed == "0")
            {
                result = false;
                return true;
            }
            return bool.TryParse(trimmed, out result);
        }

        // Read an on/off step from an INI section; missing or unreadable values default to on
        private bool IsEnabled(Dictionary<string, string> section, string key)
        {
            string value;
            bool enabled;
            if (!section.TryGetValue(key, out value) || !TryParseSetting(value, out enabled))
                enabled = true;
            return enabled;
        }

        private System.Drawing.Color GetWindowsAccentColor()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                if (key != null)
                {
                    object accentColor = key.GetValue("ColorizationColor");
                    if (accentColor != null)
                    {
                        // Convert the DWORD (ARGB) to Color
                        uint color = (uint)(int)accentColor;
                        int a = (int)((color >> 24) & 0xFF);
                        int r = (int)((color >> 16) & 0xFF);
                        int g = (int)((color >> 8) & 0xFF);
                        int b = (int)(color & 0xFF);
                        return System.Drawing.Color.FromArgb(a, r, g, b);
                    }
                }
            }
            // Fallback to a default color
            return System.Drawing.SystemColors.Control;
        }

        private void SimulateWinAltB()
        {
            // Simulate Win + Alt + B
            keybd_event(VK_LWIN, 0, 0, 0); // Win down
            keybd_event(VK_MENU, 0, 0, 0); // Alt down
            keybd_event(VK_B, 0, 0, 0);    // B down
            System.Threading.Thread.Sleep(50);         // Brief delay to ensure key press registers
            keybd_event(VK_B, 0, KEYEVENTF_KEYUP, 0);    // B up
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0); // Alt up
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0); // Win up
            System.Threading.Thread.Sleep(50);         // Brief delay to ensure toggle completes
        }

        private string RemoveTrademarkSymbols(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            return input.Replace("\u2122", "").Replace("\u00AE", "");
        }

        // Kill processes specified in the INI (section keys are the process names or full paths)
        private void KillProcessesFromSettings(Dictionary<string, Dictionary<string, string>> settings, string sectionKey)
        {
            if (settings == null || !settings.ContainsKey(sectionKey))
                return;

            foreach (var rawEntry in settings[sectionKey].Keys)
            {
                if (string.IsNullOrWhiteSpace(rawEntry))
                    continue;

                string entry = rawEntry.Trim().Trim('"');

                // If entry looks like a path, extract filename
                string imageName = entry;
                if (entry.IndexOfAny(new[] { '\\', ':' }) >= 0)
                {
                    imageName = Path.GetFileName(entry);
                }

                // Ensure we have a file name
                if (string.IsNullOrWhiteSpace(imageName))
                    continue;

                // Derive process name without extension for Process.GetProcessesByName
                string procName = Path.GetFileNameWithoutExtension(imageName);

                try
                {
                    bool anyKilled = false;

                    // Try to find running processes by name and kill them
                    try
                    {
                        var processes = Process.GetProcessesByName(procName);
                        foreach (var p in processes)
                        {
                            try
                            {
                                p.Kill();
                                anyKilled = true;
                            }
                            catch
                            {
                                // ignore individual failures
                            }
                        }
                    }
                    catch
                    {
                        // ignore enumeration errors
                    }

                    // If none matched, fallback to taskkill (/IM expects the image name, including extension)
                    if (!anyKilled)
                    {
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = "taskkill",
                                Arguments = "/IM \"" + imageName + "\" /F",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            Process.Start(psi);
                        }
                        catch
                        {
                            // ignore taskkill failures
                        }
                    }
                }
                catch
                {
                    // swallow any unexpected exceptions to avoid interrupting mode switching
                }
            }
        }

        private void LaunchGameSelector()
        {
            // Read INI settings
            var settings = ReadIniFile();
            var gameSelectorFolders = settings.ContainsKey("GameSelectorFolders")
                ? settings["GameSelectorFolders"].Keys.ToList()
                : new List<string>();
            var gameSelectorExclusions = settings.ContainsKey("GameSelectorExclusions")
                ? settings["GameSelectorExclusions"].Keys.ToList()
                : new List<string>();

            // Define base paths for Common and User Start Menus
            string commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Build list of folders to search
            var paths = new List<string>();
            foreach (string folder in gameSelectorFolders)
            {
                // Add paths for both Common and User Start Menus
                paths.Add(Path.Combine(commonStartMenu, "Programs", folder));
                paths.Add(Path.Combine(userStartMenu, @"Microsoft\Windows\Start Menu\Programs", folder));
            }

            // Collect game shortcuts, excluding specified exclusions
            var games = paths
                .Where(Directory.Exists)
                .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                .Where(IsGameShortcut)
                .Select(file => new
                {
                    BaseName = RemoveTrademarkSymbols(Path.GetFileNameWithoutExtension(file)),
                    FullName = file
                })
                .Where(g => !gameSelectorExclusions.Contains(g.BaseName, StringComparer.OrdinalIgnoreCase))
                .Select(g => new
                {
                    g.BaseName,
                    g.FullName,
                    SortName = GetSortName(g.BaseName)
                })
                .OrderBy(g => g.SortName)
                .GroupBy(g => g.BaseName)
                .Select(g => g.First()) // Remove duplicates by BaseName
                .ToList();

            if (!games.Any())
            {
                return;
            }

            // Create a new form for game selection
            Form selectorForm = new Form();
            selectorForm.SuspendLayout();

            selectorForm.Icon = System.Drawing.Icon.ExtractAssociatedIcon(@"C:\Windows\System32\joy.cpl");
            selectorForm.Text = "Choose a Game";
            selectorForm.Size = new System.Drawing.Size(Dpi(300), Dpi(400));
            selectorForm.StartPosition = FormStartPosition.Manual;

            // Position off-screen first so any initial drawing is not visible
            selectorForm.Location = new System.Drawing.Point(-10000, -10000);

            selectorForm.FormBorderStyle = FormBorderStyle.FixedSingle;
            selectorForm.Opacity = 0.50;
            selectorForm.MaximizeBox = false;
            selectorForm.MinimizeBox = false;

            ListBox listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.One
            };

            listBox.BeginUpdate();
            foreach (var game in games)
            {
                listBox.Items.Add(new { Display = game.BaseName, Path = game.FullName });
            }
            listBox.DisplayMember = "Display";
            listBox.EndUpdate();

            Button okButton = new Button
            {
                Text = "OK",
                Dock = DockStyle.Bottom,
                Height = Dpi(30)
            };
            okButton.Click += (s, e) =>
            {
                if (listBox.SelectedItem != null)
                {
                    var selected = listBox.SelectedItem as dynamic;
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = selected.Path,
                        WorkingDirectory = Path.GetDirectoryName((string)selected.Path),
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    selectorForm.Close();
                }
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                Dock = DockStyle.Bottom,
                Height = Dpi(30)
            };
            cancelButton.Click += (s, e) => selectorForm.Close();

            selectorForm.Controls.Add(listBox);
            selectorForm.Controls.Add(okButton);
            selectorForm.Controls.Add(cancelButton);

            selectorForm.ResumeLayout(false);
            selectorForm.PerformLayout();

            // Ensure form is disposed when closed
            selectorForm.FormClosed += (s, e) => selectorForm.Dispose();

            selectorForm.Shown += (s, e) =>
            {
                selectorForm.Refresh();
                listBox.Refresh();
                okButton.Refresh();
                cancelButton.Refresh();

                // Move onscreen only after the first paint has already happened off-screen
                var screen = Screen.PrimaryScreen.WorkingArea;
                selectorForm.Location = new System.Drawing.Point(screen.Width - selectorForm.Width, 0);
            };

            // Show the form modelessly after it is fully built
            selectorForm.Show();
        }

        // Only list launchable shortcuts; skip hidden/system files (desktop.ini) and uninstallers
        private bool IsGameShortcut(string file)
        {
            string extension = Path.GetExtension(file);
            if (!string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".url", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".appref-ms", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                FileAttributes attributes = File.GetAttributes(file);
                if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    return false;
            }
            catch
            {
                return false;
            }

            return !Path.GetFileNameWithoutExtension(file).StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase);
        }

        private string GetSortName(string baseName)
        {
            string[] words = baseName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1 && (string.Equals(words[0], "A", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(words[0], "The", StringComparison.OrdinalIgnoreCase)))
            {
                return string.Join(" ", words.Skip(1));
            }
            return baseName;
        }

        private void PlayGameModeSoundAsync()
        {
            Task.Run(() =>
            {
                System.Threading.Thread.Sleep(3500);

                // Play the embedded wav straight from the resource stream (runs on this background thread)
                using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Game_Mode.gamemode.wav"))
                {
                    if (resourceStream == null)
                        return;

                    using (var player = new System.Media.SoundPlayer(resourceStream))
                    {
                        player.PlaySync();
                    }
                }
            });
        }

        private void BtnGameMode_Click(object sender, EventArgs e)
        {
            // Read INI settings
            var settings = ReadIniFile();
            var gameModeSettings = settings.ContainsKey("GameMode") ? settings["GameMode"] : new Dictionary<string, string>();
            var vibranceSettings = settings.ContainsKey("VibranceSettings") ? settings["VibranceSettings"] : new Dictionary<string, string>();

            // Set button colors
            if (IsEnabled(gameModeSettings, "SetButtonColors"))
            {
                btnGameMode.BackColor = GetWindowsAccentColor();
                btnDesktopMode.BackColor = System.Drawing.SystemColors.Control;
            }

            // Kill tasks specified in GameModeKillAlso INI section
            KillProcessesFromSettings(settings, "GameModeKillAlso");

            // Launch game selector
            if (IsEnabled(gameModeSettings, "LaunchGameSelector"))
            {
                LaunchGameSelector();
            }

            // Set high performance power plan silently
            if (IsEnabled(gameModeSettings, "SetPowerPlan"))
            {
                ProcessStartInfo powerPlanInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(powerPlanInfo);
            }

            // Set NVIDIA Digital Vibrance using GameVibe setting, default to 0
            if (IsEnabled(gameModeSettings, "SetDigitalVibrance"))
            {
                string gameVibe = vibranceSettings.ContainsKey("GameVibe") ? vibranceSettings["GameVibe"] : "0";
                SetDigitalVibrance(gameVibe);
            }

            // Simulate 50 volume up key presses
            if (IsEnabled(gameModeSettings, "AdjustVolume"))
            {
                for (int i = 0; i < 50; i++)
                {
                    keybd_event(VK_VOLUME_UP, 0, 0, 0); // Key down
                    keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_KEYUP, 0); // Key up
                }
            }

            // Toggle HDR (assumes HDR is off or needs to be enabled)
            if (IsEnabled(gameModeSettings, "ToggleHDR"))
            {
                SimulateWinAltB();
            }

            // Launch additional EXEs from GameModeLaunchAlso
            var gameModeLaunchAlso = settings.ContainsKey("GameModeLaunchAlso") ? settings["GameModeLaunchAlso"] : new Dictionary<string, string>();
            foreach (var exePath in gameModeLaunchAlso.Keys)
            {
                if (File.Exists(exePath))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = Path.GetDirectoryName(exePath), // Keep logs out of GameMode.exe's folder
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
            }

            // Play embedded gamemode.wav after 3.5-second delay without blocking the UI thread
            if (IsEnabled(gameModeSettings, "PlaySound"))
            {
                PlayGameModeSoundAsync();
            }
        }

        private void BtnDesktopMode_Click(object sender, EventArgs e)
        {
            // Read INI settings
            var settings = ReadIniFile();
            var desktopModeSettings = settings.ContainsKey("DesktopMode") ? settings["DesktopMode"] : new Dictionary<string, string>();
            var vibranceSettings = settings.ContainsKey("VibranceSettings") ? settings["VibranceSettings"] : new Dictionary<string, string>();

            // Set button colors
            if (IsEnabled(desktopModeSettings, "SetButtonColors"))
            {
                btnDesktopMode.BackColor = GetWindowsAccentColor();
                btnGameMode.BackColor = System.Drawing.SystemColors.Control;
            }

            // Kill tasks specified in DesktopModeKillAlso INI section
            KillProcessesFromSettings(settings, "DesktopModeKillAlso");

            // Set balanced power plan silently
            if (IsEnabled(desktopModeSettings, "SetPowerPlan"))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(startInfo);
            }

            // Set NVIDIA Digital Vibrance using DeskVibe setting, default to 0
            if (IsEnabled(desktopModeSettings, "SetDigitalVibrance"))
            {
                string deskVibe = vibranceSettings.ContainsKey("DeskVibe") ? vibranceSettings["DeskVibe"] : "0";
                SetDigitalVibrance(deskVibe);
            }

            // Simulate 50 volume down key presses
            if (IsEnabled(desktopModeSettings, "AdjustVolume"))
            {
                for (int i = 0; i < 50; i++)
                {
                    keybd_event(VK_VOLUME_DOWN, 0, 0, 0); // Key down
                    keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_KEYUP, 0); // Key up
                }
            }

            // Toggle HDR (assumes HDR is on or needs to be disabled)
            if (IsEnabled(desktopModeSettings, "ToggleHDR"))
            {
                SimulateWinAltB();
            }

            // Launch additional EXEs from DesktopModeLaunchAlso
            var desktopModeLaunchAlso = settings.ContainsKey("DesktopModeLaunchAlso") ? settings["DesktopModeLaunchAlso"] : new Dictionary<string, string>();
            foreach (var exePath in desktopModeLaunchAlso.Keys)
            {
                if (File.Exists(exePath))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = Path.GetDirectoryName(exePath), // Keep logs out of GameMode.exe's folder
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
            }
        }

        private void BtnToggleHDR_Click(object sender, EventArgs e)
        {
            SimulateWinAltB();
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            if (File.Exists(iniPath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = "\"" + iniPath + "\"",
                    UseShellExecute = true,
                    Verb = "runas" // Run as admin
                };
                try
                {
                    Process.Start(startInfo);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // User declined the UAC prompt
                }
            }
        }

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.btnGameMode = new System.Windows.Forms.Button();
            this.btnDesktopMode = new System.Windows.Forms.Button();
            this.btnToggleHDR = new System.Windows.Forms.Button();
            this.btnSettings = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            //this.BackColor = System.Drawing.Color.DarkGray;
            // 
            // btnGameMode
            // 
            this.btnGameMode.Location = new System.Drawing.Point(15, 30);
            this.btnGameMode.Name = "btnGameMode";
            this.btnGameMode.Size = new System.Drawing.Size(120, 100);
            this.btnGameMode.TabIndex = 0;
            this.btnGameMode.Text = "";
            this.btnGameMode.UseVisualStyleBackColor = true;
            this.btnGameMode.Image = LoadIcoAsBitmap("Game_Mode.game.ico", Dpi(120), Dpi(100)); // Scale to button size
            this.btnGameMode.ImageAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnGameMode.Click += new System.EventHandler(this.BtnGameMode_Click);
            this.toolTip.SetToolTip(this.btnGameMode, "Game Mode");
            // 
            // btnDesktopMode
            // 
            this.btnDesktopMode.Location = new System.Drawing.Point(150, 30);
            this.btnDesktopMode.Name = "btnDesktopMode";
            this.btnDesktopMode.Size = new System.Drawing.Size(120, 100);
            this.btnDesktopMode.TabIndex = 1;
            this.btnDesktopMode.Text = ""; // Removed "Desktop Mode" text
            this.btnDesktopMode.UseVisualStyleBackColor = true;
            this.btnDesktopMode.Image = LoadIcoAsBitmap("Game_Mode.desktop.ico", Dpi(120), Dpi(100)); // Scale to button size
            this.btnDesktopMode.ImageAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnDesktopMode.Click += new System.EventHandler(this.BtnDesktopMode_Click);
            this.toolTip.SetToolTip(this.btnDesktopMode, "Desktop Mode");
            // 
            // btnToggleHDR
            // 
            this.btnToggleHDR.Location = new System.Drawing.Point(127, 140);
            this.btnToggleHDR.Name = "btnToggleHDR";
            this.btnToggleHDR.Size = new System.Drawing.Size(30, 30);
            this.btnToggleHDR.TabIndex = 2;
            this.btnToggleHDR.Text = "";
            this.btnToggleHDR.UseVisualStyleBackColor = true;
            this.btnToggleHDR.Image = LoadSystemIconAsBitmap(@"C:\Windows\System32\shell32.dll", 15, Dpi(24), Dpi(24)); // Smaller monitor icon
            this.btnToggleHDR.ImageAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnToggleHDR.Click += new System.EventHandler(this.BtnToggleHDR_Click);
            this.toolTip.SetToolTip(this.btnToggleHDR, "Toggle HDR");
            // 
            // btnSettings
            // 
            this.btnSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSettings.FlatAppearance.BorderSize = 0;
            this.btnSettings.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.btnSettings.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.btnSettings.Location = new System.Drawing.Point(254, 161);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(30, 30);
            this.btnSettings.TabIndex = 3;
            this.btnSettings.Text = "";
            this.btnSettings.UseVisualStyleBackColor = true;
            this.btnSettings.Image = LoadIcoAsBitmap("Game_Mode.settings.ico", Dpi(16), Dpi(16)); // Changed to embedded resource
            this.btnSettings.ImageAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnSettings.Click += new System.EventHandler(this.BtnSettings_Click);
            this.toolTip.SetToolTip(this.btnSettings, "Settings");
            // 
            // toolTip
            // 
            this.toolTip.AutoPopDelay = 5000;
            this.toolTip.InitialDelay = 500;
            this.toolTip.ReshowDelay = 100;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 191); // Increased height to accommodate new button
            this.Controls.Add(this.btnSettings);
            this.Controls.Add(this.btnToggleHDR);
            this.Controls.Add(this.btnDesktopMode);
            this.Controls.Add(this.btnGameMode);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = true;
            this.Name = "Form1";
            this.Opacity = 0.50; // Set transparency percentage
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Game Mode";
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button btnGameMode;
        private System.Windows.Forms.Button btnDesktopMode;
        private System.Windows.Forms.Button btnToggleHDR;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.ToolTip toolTip;

        private System.Drawing.Bitmap LoadIcoAsBitmap(string resourceName, int targetWidth, int targetHeight)
        {
            try
            {
                // Load the embedded ICO resource
                using (var stream = GetType().Assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Error: Embedded resource '" + resourceName + "' not found.");
                        return null;
                    }
                    byte[] icoData;
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        icoData = ms.ToArray();
                    }

                    // Get the sharpest image in the icon for the target size
                    using (var originalBitmap = GetBestIcoFrame(icoData, Math.Max(targetWidth, targetHeight)))
                    {
                        // Calculate scaling to fit within target dimensions while preserving aspect ratio
                        float aspectRatio = (float)originalBitmap.Width / originalBitmap.Height;
                        int newWidth, newHeight;
                        if (aspectRatio > (float)targetWidth / targetHeight)
                        {
                            newWidth = targetWidth;
                            newHeight = (int)(targetWidth / aspectRatio);
                        }
                        else
                        {
                            newHeight = targetHeight;
                            newWidth = (int)(targetHeight * aspectRatio);
                        }

                        // Create a new bitmap with the scaled size
                        var scaledBitmap = new System.Drawing.Bitmap(newWidth, newHeight);
                        using (var graphics = System.Drawing.Graphics.FromImage(scaledBitmap))
                        {
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            graphics.DrawImage(originalBitmap, 0, 0, newWidth, newHeight);
                        }
                        return scaledBitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading embedded ICO: " + ex.Message);
                return null;
            }
        }

        // Pick the smallest image in the ICO that is at least neededSize (or the largest if none are),
        // decoding PNG-compressed frames directly since System.Drawing.Icon skips them
        private System.Drawing.Bitmap GetBestIcoFrame(byte[] icoData, int neededSize)
        {
            int count = BitConverter.ToUInt16(icoData, 4);
            int best = -1, bestSize = 0;
            for (int i = 0; i < count; i++)
            {
                int size = icoData[6 + i * 16];
                if (size == 0) size = 256;
                bool bestTooSmall = bestSize < neededSize;
                if (best < 0 ||
                    (bestTooSmall && size > bestSize) ||
                    (!bestTooSmall && size >= neededSize && size < bestSize))
                {
                    best = i;
                    bestSize = size;
                }
            }

            int length = (int)BitConverter.ToUInt32(icoData, 6 + best * 16 + 8);
            int offset = (int)BitConverter.ToUInt32(icoData, 6 + best * 16 + 12);

            // PNG signature
            if (icoData[offset] == 0x89 && icoData[offset + 1] == 0x50 && icoData[offset + 2] == 0x4E && icoData[offset + 3] == 0x47)
            {
                using (var pngStream = new MemoryStream(icoData, offset, length))
                using (var png = new System.Drawing.Bitmap(pngStream))
                {
                    return new System.Drawing.Bitmap(png);
                }
            }

            using (var icon = new System.Drawing.Icon(new MemoryStream(icoData), bestSize, bestSize))
            {
                return icon.ToBitmap();
            }
        }

        private System.Drawing.Bitmap LoadSystemIconAsBitmap(string filePath, int iconIndex, int targetWidth, int targetHeight)
        {
            try
            {
                // Extract the icon from the system file
                IntPtr[] hIcon = new IntPtr[1];
                uint result = ExtractIconEx(filePath, iconIndex, hIcon, null, 1);
                if (result == 0 || hIcon[0] == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("Error: Failed to extract icon index " + iconIndex + " from '" + filePath + "'.");
                    return null;
                }

                using (var icon = System.Drawing.Icon.FromHandle(hIcon[0]))
                {
                    // Get the icon's bitmap
                    using (var originalBitmap = icon.ToBitmap())
                    {
                        // Calculate scaling to fit within target dimensions while preserving aspect ratio
                        float aspectRatio = (float)originalBitmap.Width / originalBitmap.Height;
                        int newWidth, newHeight;
                        if (aspectRatio > (float)targetWidth / targetHeight)
                        {
                            newWidth = targetWidth;
                            newHeight = (int)(targetWidth / aspectRatio);
                        }
                        else
                        {
                            newHeight = targetHeight;
                            newWidth = (int)(targetHeight * aspectRatio);
                        }

                        // Create a new bitmap with the scaled size
                        var scaledBitmap = new System.Drawing.Bitmap(newWidth, newHeight);
                        using (var graphics = System.Drawing.Graphics.FromImage(scaledBitmap))
                        {
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.DrawImage(originalBitmap, 0, 0, newWidth, newHeight);
                        }

                        // Destroy the icon handle to prevent resource leaks
                        DestroyIcon(hIcon[0]);
                        return scaledBitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading system icon: " + ex.Message);
                return null;
            }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
