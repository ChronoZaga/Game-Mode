﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Game_Mode
{
    public partial class Form1 : Form
    {
        // Import keybd_event from user32.dll
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        // Import PlaySound from winmm.dll
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

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

        // Sound flags
        private const uint SND_FILENAME = 0x00020000;
        private const uint SND_ASYNC = 0x0001;

        // Virtual key codes
        private const byte VK_VOLUME_UP = 0xAF;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_LWIN = 0x5B; // Left Windows key
        private const byte VK_MENU = 0x12; // Alt key
        private const byte VK_B = 0x42;    // B key
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private readonly string iniPath;

        public Form1()
        {
            InitializeComponent();

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

        private void InitializeNVAPI()
        {
            // Load nvapi64.dll from System32 for x64
            string nvapi64Path = Path.Combine(@"C:\Windows\System32", "nvapi64.dll");
            nvapiHandle = LoadLibrary(nvapi64Path);

            // Get NVAPI function pointers
            NvAPI_Initialize = nvapi_QueryInterface(0x0150E828); // NvAPI_Initialize
            NvAPI_EnumNvidiaDisplayHandle = nvapi_QueryInterface(0x9ABDD40D); // NvAPI_EnumNvidiaDisplayHandle
            NvAPI_SetDVCLevel = nvapi_QueryInterface(0x172409B4); // NvAPI_SetDVCLevel
            NvAPI_GetDVCInfo = nvapi_QueryInterface(0x4085DE45); // NvAPI_GetDVCInfo

            // Initialize NVAPI
            var initializeDelegate = Marshal.GetDelegateForFunctionPointer<NvAPI_InitializeDelegate>(NvAPI_Initialize);
            int status = initializeDelegate();
            if (status == 0) // NVAPI_OK
            {
                nvapiInitialized = true;
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
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
            if (dvcValue < 0) dvcValue = 0;

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
;C:\Path\To\Example1.exe
;C:\Path\To\Example2.exe
;C:\Program Files\Cheat Happens Aurora\Aurora.exe
;C:\Program Files\WindowsApps\NVIDIACorp.NVIDIAControlPanel_8.1.968.0_x64__56jybvy8sckqj\nvcplui.exe
;C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA App.exe

[DesktopModeLaunchAlso]
;C:\Path\To\Example3.exe
;C:\Path\To\Example4.exe
;C:\Program Files\WindowsApps\NVIDIACorp.NVIDIAControlPanel_8.1.968.0_x64__56jybvy8sckqj\nvcplui.exe

[VibranceSettings]
GameVibe=12
DeskVibe=0

[GameSelectorFolders]
Call of Duty
Games
Steam

[GameSelectorExclusions]
;3DMark
Steam
Steam Support Center
";

                File.WriteAllText(iniPath, iniContent);

                // Verify the file was created
                if (!File.Exists(iniPath))
                {
                    MessageBox.Show($"Failed to create INI file at {iniPath}. The application will now close. Try running as Admin once.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create INI file at {iniPath}: {ex.Message} The application will now close. Try running as Admin once.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show($"INI file not found: {iniPath}. The application will now close.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    if (currentSection == "GameModeLaunchAlso" || currentSection == "DesktopModeLaunchAlso")
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
                    if (currentSection == "GameModeLaunchAlso" || currentSection == "DesktopModeLaunchAlso")
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
                ? launchAlsoSettings["GameModeLaunchAlso"].ToDictionary(path => path, _ => "1")
                : new Dictionary<string, string>();
            settings["DesktopModeLaunchAlso"] = launchAlsoSettings.ContainsKey("DesktopModeLaunchAlso")
                ? launchAlsoSettings["DesktopModeLaunchAlso"].ToDictionary(path => path, _ => "1")
                : new Dictionary<string, string>();
            // Store game selector folders and exclusions in settings
            settings["GameSelectorFolders"] = gameSelectorFolders.ToDictionary(folder => folder, _ => "1");
            settings["GameSelectorExclusions"] = gameSelectorExclusions.ToDictionary(exclusion => exclusion, _ => "1");

            return settings;
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
            selectorForm.Icon = System.Drawing.Icon.ExtractAssociatedIcon(@"C:\Windows\System32\joy.cpl");
            selectorForm.Text = "Choose a Game";
            selectorForm.Size = new System.Drawing.Size(300, 400);
            selectorForm.StartPosition = FormStartPosition.Manual;
            // Position in upper right corner
            var screen = Screen.PrimaryScreen.WorkingArea;
            selectorForm.Location = new System.Drawing.Point(screen.Width - selectorForm.Width, 0);
            selectorForm.FormBorderStyle = FormBorderStyle.FixedSingle;
            selectorForm.Opacity = 0.98;
            selectorForm.MaximizeBox = false;
            selectorForm.MinimizeBox = false;

            ListBox listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.One
            };
            foreach (var game in games)
            {
                listBox.Items.Add(new { Display = game.BaseName, Path = game.FullName });
            }
            listBox.DisplayMember = "Display";

            Button okButton = new Button
            {
                Text = "OK",
                Dock = DockStyle.Bottom,
                Height = 30
            };
            okButton.Click += (s, e) =>
            {
                if (listBox.SelectedItem != null)
                {
                    var selected = listBox.SelectedItem as dynamic;
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = selected.Path,
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
                Height = 30
            };
            cancelButton.Click += (s, e) => selectorForm.Close();

            selectorForm.Controls.Add(listBox);
            selectorForm.Controls.Add(okButton);
            selectorForm.Controls.Add(cancelButton);

            // Ensure form is disposed when closed
            selectorForm.FormClosed += (s, e) => selectorForm.Dispose();

            // Show the form modelessly (non-blocking)
            selectorForm.Show();
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

        private void BtnGameMode_Click(object sender, EventArgs e)
        {
            // Read INI settings
            var settings = ReadIniFile();
            var gameModeSettings = settings.ContainsKey("GameMode") ? settings["GameMode"] : new Dictionary<string, string>();
            var vibranceSettings = settings.ContainsKey("VibranceSettings") ? settings["VibranceSettings"] : new Dictionary<string, string>();

            // Set button colors
            bool setButtonColors;
            if (!gameModeSettings.TryGetValue("SetButtonColors", out string setButtonColorsValue) || !bool.TryParse(setButtonColorsValue, out setButtonColors))
                setButtonColors = true;
            if (setButtonColors)
            {
                btnGameMode.BackColor = GetWindowsAccentColor();
                btnDesktopMode.BackColor = System.Drawing.SystemColors.Control;
            }

            // Launch game selector
            bool launchGameSelector;
            if (!gameModeSettings.TryGetValue("LaunchGameSelector", out string launchGameSelectorValue) || !bool.TryParse(launchGameSelectorValue, out launchGameSelector))
                launchGameSelector = true;
            if (launchGameSelector)
            {
                LaunchGameSelector();
            }

            // Set high performance power plan silently
            bool setPowerPlan;
            if (!gameModeSettings.TryGetValue("SetPowerPlan", out string setPowerPlanValue) || !bool.TryParse(setPowerPlanValue, out setPowerPlan))
                setPowerPlan = true;
            if (setPowerPlan)
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
            bool setDigitalVibrance;
            if (!gameModeSettings.TryGetValue("SetDigitalVibrance", out string setDigitalVibranceValue) || !bool.TryParse(setDigitalVibranceValue, out setDigitalVibrance))
                setDigitalVibrance = true;
            if (setDigitalVibrance)
            {
                string gameVibe = vibranceSettings.ContainsKey("GameVibe") ? vibranceSettings["GameVibe"] : "0";
                SetDigitalVibrance(gameVibe);
            }

            // Simulate 50 volume up key presses
            bool adjustVolume;
            if (!gameModeSettings.TryGetValue("AdjustVolume", out string adjustVolumeValue) || !bool.TryParse(adjustVolumeValue, out adjustVolume))
                adjustVolume = true;
            if (adjustVolume)
            {
                for (int i = 0; i < 50; i++)
                {
                    keybd_event(VK_VOLUME_UP, 0, 0, 0); // Key down
                    keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_KEYUP, 0); // Key up
                }
            }

            // Toggle HDR (assumes HDR is off or needs to be enabled)
            bool toggleHDR;
            if (!gameModeSettings.TryGetValue("ToggleHDR", out string toggleHDRValue) || !bool.TryParse(toggleHDRValue, out toggleHDR))
                toggleHDR = true;
            if (toggleHDR)
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
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
            }

            // Play embedded gamemode.wav after 3.5-second delay
            bool playSound;
            if (!gameModeSettings.TryGetValue("PlaySound", out string playSoundValue) || !bool.TryParse(playSoundValue, out playSound))
                playSound = true;
            if (playSound)
            {
                System.Threading.Thread.Sleep(3500);
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = "Game_Mode.gamemode.wav";
                using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                        return;

                    // Create a temporary file path with a unique name
                    string tempPath = Path.Combine(Path.GetTempPath(), $"gamemode_{Guid.NewGuid()}.wav");

                    // Extract the resource to the temp file
                    using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    {
                        resourceStream.CopyTo(fileStream);
                    }

                    if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 0)
                    {
                        PlaySound(tempPath, IntPtr.Zero, SND_FILENAME | SND_ASYNC);

                        // Delay to ensure sound playback releases the file
                        System.Threading.Thread.Sleep(2000);

                        // Clean up the temp file
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                }
            }
        }

        private void BtnDesktopMode_Click(object sender, EventArgs e)
        {
            // Read INI settings
            var settings = ReadIniFile();
            var desktopModeSettings = settings.ContainsKey("DesktopMode") ? settings["DesktopMode"] : new Dictionary<string, string>();
            var vibranceSettings = settings.ContainsKey("VibranceSettings") ? settings["VibranceSettings"] : new Dictionary<string, string>();

            // Set button colors
            bool setButtonColors;
            if (!desktopModeSettings.TryGetValue("SetButtonColors", out string setButtonColorsValue) || !bool.TryParse(setButtonColorsValue, out setButtonColors))
                setButtonColors = true;
            if (setButtonColors)
            {
                btnDesktopMode.BackColor = GetWindowsAccentColor();
                btnGameMode.BackColor = System.Drawing.SystemColors.Control;
            }

            // Set balanced power plan silently
            bool setPowerPlan;
            if (!desktopModeSettings.TryGetValue("SetPowerPlan", out string setPowerPlanValue) || !bool.TryParse(setPowerPlanValue, out setPowerPlan))
                setPowerPlan = true;
            if (setPowerPlan)
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
            bool setDigitalVibrance;
            if (!desktopModeSettings.TryGetValue("SetDigitalVibrance", out string setDigitalVibranceValue) || !bool.TryParse(setDigitalVibranceValue, out setDigitalVibrance))
                setDigitalVibrance = true;
            if (setDigitalVibrance)
            {
                string deskVibe = vibranceSettings.ContainsKey("DeskVibe") ? vibranceSettings["DeskVibe"] : "0";
                SetDigitalVibrance(deskVibe);
            }

            // Simulate 50 volume down key presses
            bool adjustVolume;
            if (!desktopModeSettings.TryGetValue("AdjustVolume", out string adjustVolumeValue) || !bool.TryParse(adjustVolumeValue, out adjustVolume))
                adjustVolume = true;
            if (adjustVolume)
            {
                for (int i = 0; i < 50; i++)
                {
                    keybd_event(VK_VOLUME_DOWN, 0, 0, 0); // Key down
                    keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_KEYUP, 0); // Key up
                }
            }

            // Toggle HDR (assumes HDR is on or needs to be disabled)
            bool toggleHDR;
            if (!desktopModeSettings.TryGetValue("ToggleHDR", out string toggleHDRValue) || !bool.TryParse(toggleHDRValue, out toggleHDR))
                toggleHDR = true;
            if (toggleHDR)
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
                    Arguments = $"\"{iniPath}\"",
                    UseShellExecute = true,
                    Verb = "runas" // Run as admin
                };
                Process.Start(startInfo);
            }
        }
    }
}