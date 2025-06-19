using System;
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
            try
            {
                this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(@"C:\Windows\System32\joy.cpl");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set form icon: {ex.Message}");
            }

            // Initialize INI file path (same directory as executable)
            iniPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Game_Mode.ini");

            // Create INI file if it doesn't exist
            CreateIniFileIfNotExists();
        }

        private void CreateIniFileIfNotExists()
        {
            if (File.Exists(iniPath))
                return;

            try
            {
                // Define default INI content with steps ordered by execution
                string iniContent = @"[GameMode]
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

[GameSelectorFolders]
Call of Duty
Games
Steam

[GameSelectorExclusions]
Steam
Steam Support Center
";

                File.WriteAllText(iniPath, iniContent);
                Debug.WriteLine($"Created INI file: {iniPath}");

                // Verify the file was created
                if (!File.Exists(iniPath))
                {
                    MessageBox.Show($"Failed to create INI file at {iniPath}. The application will now close. Try running as Admin once.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create INI file: {ex.Message}");
                MessageBox.Show($"Failed to create INI file at {iniPath}: {ex.Message} The application will now close. Try running as Admin once.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }
        }

        private Dictionary<string, Dictionary<string, bool>> ReadIniFile()
        {
            var settings = new Dictionary<string, Dictionary<string, bool>>();
            var launchAlsoSettings = new Dictionary<string, List<string>>();
            var gameSelectorFolders = new List<string>();
            var gameSelectorExclusions = new List<string>();
            string currentSection = null;

            try
            {
                if (!File.Exists(iniPath))
                {
                    Debug.WriteLine($"INI file not found: {iniPath}");
                    return settings;
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
                            // Initialize lists for folder and exclusion sections
                            if (currentSection == "GameSelectorFolders")
                                gameSelectorFolders = new List<string>();
                            else
                                gameSelectorExclusions = new List<string>();
                        }
                        else
                        {
                            settings[currentSection] = new Dictionary<string, bool>();
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
                            bool enabled = value == "1";
                            settings[currentSection][key] = enabled;
                        }
                    }
                }

                // Store launchAlsoSettings in settings for simplicity
                settings["GameModeLaunchAlso"] = launchAlsoSettings.ContainsKey("GameModeLaunchAlso")
                    ? launchAlsoSettings["GameModeLaunchAlso"].ToDictionary(path => path, _ => true)
                    : new Dictionary<string, bool>();
                settings["DesktopModeLaunchAlso"] = launchAlsoSettings.ContainsKey("DesktopModeLaunchAlso")
                    ? launchAlsoSettings["DesktopModeLaunchAlso"].ToDictionary(path => path, _ => true)
                    : new Dictionary<string, bool>();
                // Store game selector folders and exclusions in settings
                settings["GameSelectorFolders"] = gameSelectorFolders.ToDictionary(folder => folder, _ => true);
                settings["GameSelectorExclusions"] = gameSelectorExclusions.ToDictionary(exclusion => exclusion, _ => true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read INI file: {ex.Message}");
            }

            return settings;
        }

        private System.Drawing.Color GetWindowsAccentColor()
        {
            try
            {
                // Read the accent color from the registry
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get accent color: {ex.Message}");
            }
            // Fallback to a default color if registry read fails
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

        private void RunEmbeddedDVChange(string argument)
        {
            try
            {
                // Get the embedded DVChange.exe resource
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = "Game_Mode.DVChange.exe";
                using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        Debug.WriteLine("DVChange.exe resource not found");
                        return;
                    }

                    // Create a temporary file path
                    string tempPath = Path.Combine(Path.GetTempPath(), "DVChange.exe");

                    // Extract the resource to the temp file
                    using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    {
                        resourceStream.CopyTo(fileStream);
                    }

                    // Run the extracted executable
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = tempPath,
                        Arguments = argument,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process process = Process.Start(startInfo);
                    process.WaitForExit(); // Ensure command completes

                    // Clean up the temp file
                    File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RunEmbeddedDVChange failed: {ex.Message}");
            }
        }

        private void LaunchGameSelector()
        {
            try
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
                        BaseName = Path.GetFileNameWithoutExtension(file),
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
                    Debug.WriteLine("No games found.");
                    return;
                }

                // Create a new form for game selection
                Form selectorForm = new Form();
                // Set the game selector form icon to the gamepad icon from joy.cpl
                try
                {
                    selectorForm.Icon = System.Drawing.Icon.ExtractAssociatedIcon(@"C:\Windows\System32\joy.cpl");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to set game selector form icon: {ex.Message}");
                }

                selectorForm.Text = "Choose a Game";
                selectorForm.Size = new System.Drawing.Size(300, 400);
                selectorForm.StartPosition = FormStartPosition.Manual;
                // Position in upper right corner
                var screen = Screen.PrimaryScreen.WorkingArea;
                selectorForm.Location = new System.Drawing.Point(screen.Width - selectorForm.Width, 0);
                selectorForm.FormBorderStyle = FormBorderStyle.FixedSingle;
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
                        if (selected != null)
                        {
                            try
                            {
                                ProcessStartInfo startInfo = new ProcessStartInfo
                                {
                                    FileName = selected.Path,
                                    UseShellExecute = true
                                };
                                Process.Start(startInfo);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to launch game: {ex.Message}");
                            }
                        }
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
            catch (Exception ex)
            {
                Debug.WriteLine($"LaunchGameSelector failed: {ex.Message}");
            }
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
            try
            {
                // Read INI settings
                var settings = ReadIniFile();
                var gameModeSettings = settings.ContainsKey("GameMode") ? settings["GameMode"] : new Dictionary<string, bool>();

                // Set button colors
                bool setButtonColors;
                if (!gameModeSettings.TryGetValue("SetButtonColors", out setButtonColors))
                    setButtonColors = true;
                if (setButtonColors)
                {
                    btnGameMode.BackColor = GetWindowsAccentColor();
                    btnDesktopMode.BackColor = System.Drawing.SystemColors.Control;
                }

                // Launch game selector
                bool launchGameSelector;
                if (!gameModeSettings.TryGetValue("LaunchGameSelector", out launchGameSelector))
                    launchGameSelector = true;
                if (launchGameSelector)
                {
                    LaunchGameSelector();
                }

                // Set high performance power plan silently
                bool setPowerPlan;
                if (!gameModeSettings.TryGetValue("SetPowerPlan", out setPowerPlan))
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

                // Set NVIDIA Digital Vibrance to 60% (argument 12) using embedded DVChange
                bool setDigitalVibrance;
                if (!gameModeSettings.TryGetValue("SetDigitalVibrance", out setDigitalVibrance))
                    setDigitalVibrance = true;
                if (setDigitalVibrance)
                {
                    RunEmbeddedDVChange("12");
                }

                // Simulate 50 volume up key presses
                bool adjustVolume;
                if (!gameModeSettings.TryGetValue("AdjustVolume", out adjustVolume))
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
                if (!gameModeSettings.TryGetValue("ToggleHDR", out toggleHDR))
                    toggleHDR = true;
                if (toggleHDR)
                {
                    SimulateWinAltB();
                }

                // Launch additional EXEs from GameModeLaunchAlso
                var gameModeLaunchAlso = settings.ContainsKey("GameModeLaunchAlso") ? settings["GameModeLaunchAlso"] : new Dictionary<string, bool>();
                foreach (var exePath in gameModeLaunchAlso.Keys)
                {
                    try
                    {
                        if (File.Exists(exePath))
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo
                            {
                                FileName = exePath,
                                UseShellExecute = true
                            };
                            Process.Start(startInfo);
                            Debug.WriteLine($"Launched additional EXE: {exePath}");
                        }
                        else
                        {
                            Debug.WriteLine($"EXE not found: {exePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to launch additional EXE {exePath}: {ex.Message}");
                    }
                }

                // Play embedded gamemode.wav after 3.5-second delay
                bool playSound;
                if (!gameModeSettings.TryGetValue("PlaySound", out playSound))
                    playSound = true;
                if (playSound)
                {
                    try
                    {
                        System.Threading.Thread.Sleep(3500);
                        Assembly assembly = Assembly.GetExecutingAssembly();
                        string resourceName = "Game_Mode.gamemode.wav";
                        using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (resourceStream == null)
                            {
                                Debug.WriteLine($"gamemode.wav resource not found. Resource name: {resourceName}");
                                return;
                            }

                            // Create a temporary file path with a unique name
                            string tempPath = Path.Combine(Path.GetTempPath(), $"gamemode_{Guid.NewGuid()}.wav");

                            // Extract the resource to the temp file
                            using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                            {
                                resourceStream.CopyTo(fileStream);
                            }

                            // Verify the file exists and has content
                            if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 0)
                            {
                                Debug.WriteLine($"Attempting to play sound from: {tempPath}");
                                bool result = PlaySound(tempPath, IntPtr.Zero, SND_FILENAME | SND_ASYNC);
                                if (!result)
                                {
                                    int errorCode = Marshal.GetLastWin32Error();
                                    Debug.WriteLine($"PlaySound failed with error code: {errorCode}");
                                }
                                else
                                {
                                    Debug.WriteLine($"PlaySound initiated successfully for: {tempPath}");
                                }

                                // Delay to ensure sound playback releases the file
                                System.Threading.Thread.Sleep(2000);

                                // Clean up the temp file
                                try
                                {
                                    if (File.Exists(tempPath))
                                    {
                                        File.Delete(tempPath);
                                        Debug.WriteLine($"Temporary file deleted: {tempPath}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Failed to delete temp file {tempPath}: {ex.Message}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"Temporary file not created or empty: {tempPath}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to play sound: {ex.Message}");
                    }
                }
            }
            catch (Exception)
            {
                // Silently handle errors
            }
        }

        private void BtnDesktopMode_Click(object sender, EventArgs e)
        {
            try
            {
                // Read INI settings
                var settings = ReadIniFile();
                var desktopModeSettings = settings.ContainsKey("DesktopMode") ? settings["DesktopMode"] : new Dictionary<string, bool>();

                // Set button colors
                bool setButtonColors;
                if (!desktopModeSettings.TryGetValue("SetButtonColors", out setButtonColors))
                    setButtonColors = true;
                if (setButtonColors)
                {
                    btnDesktopMode.BackColor = GetWindowsAccentColor();
                    btnGameMode.BackColor = System.Drawing.SystemColors.Control;
                }

                // Set balanced power plan silently
                bool setPowerPlan;
                if (!desktopModeSettings.TryGetValue("SetPowerPlan", out setPowerPlan))
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

                // Set NVIDIA Digital Vibrance to 50% (argument 0) using embedded DVChange
                bool setDigitalVibrance;
                if (!desktopModeSettings.TryGetValue("SetDigitalVibrance", out setDigitalVibrance))
                    setDigitalVibrance = true;
                if (setDigitalVibrance)
                {
                    RunEmbeddedDVChange("0");
                }

                // Simulate 50 volume down key presses
                bool adjustVolume;
                if (!desktopModeSettings.TryGetValue("AdjustVolume", out adjustVolume))
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
                if (!desktopModeSettings.TryGetValue("ToggleHDR", out toggleHDR))
                    toggleHDR = true;
                if (toggleHDR)
                {
                    SimulateWinAltB();
                }

                // Launch additional EXEs from DesktopModeLaunchAlso
                var desktopModeLaunchAlso = settings.ContainsKey("DesktopModeLaunchAlso") ? settings["DesktopModeLaunchAlso"] : new Dictionary<string, bool>();
                foreach (var exePath in desktopModeLaunchAlso.Keys)
                {
                    try
                    {
                        if (File.Exists(exePath))
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo
                            {
                                FileName = exePath,
                                UseShellExecute = true
                            };
                            Process.Start(startInfo);
                            Debug.WriteLine($"Launched additional EXE: {exePath}");
                        }
                        else
                        {
                            Debug.WriteLine($"EXE not found: {exePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to launch additional EXE {exePath}: {ex.Message}");
                    }
                }
            }
            catch (Exception)
            {
                // Silently handle errors
            }
        }

        private void BtnToggleHDR_Click(object sender, EventArgs e)
        {
            try
            {
                // Only toggle HDR
                SimulateWinAltB();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BtnToggleHDR_Click failed: {ex.Message}");
            }
        }

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            try
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
                    Debug.WriteLine($"Opened INI file in Notepad: {iniPath}");
                }
                else
                {
                    Debug.WriteLine($"INI file not found: {iniPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open INI file in Notepad: {ex.Message}");
            }
        }
    }
}