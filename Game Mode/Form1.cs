using System;
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

        private void LaunchNvidiaControlPanel()
        {
            try
            {
                // Attempt to launch NVIDIA Control Panel using the package family name
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "shell:appsFolder\\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel",
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(startInfo);
            }
            catch (Exception)
            {
                // Silently handle errors, consistent with existing error handling
            }
        }

        private void LaunchNvidiaApp()
        {
            try
            {
                // Attempt to launch NVIDIA App from the updated installation path
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = @"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA App.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(startInfo);
            }
            catch (Exception)
            {
                // Silently handle errors, consistent with existing error handling
            }
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
                // Define paths to search for game shortcuts
                string[] paths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Steam"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), @"Programs\Steam"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Call of Duty"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), @"Programs\Call of Duty"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Games"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), @"Programs\Games")
                };

                // Collect game shortcuts, excluding "Steam" and "Steam Support Center"
                var games = paths
                    .Where(Directory.Exists)
                    .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    .Select(file => new
                    {
                        BaseName = Path.GetFileNameWithoutExtension(file),
                        FullName = file
                    })
                    .Where(g => g.BaseName != "Steam" && g.BaseName != "Steam Support Center")
                    .OrderBy(g => g.BaseName)
                    .GroupBy(g => g.BaseName)
                    .Select(g => g.First()) // Remove duplicates by BaseName
                    .ToList();

                if (!games.Any())
                {
                    Debug.WriteLine("No games found.");
                    return;
                }

                // Create a new form for game selection
                using (Form selectorForm = new Form())
                {
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
                    selectorForm.StartPosition = FormStartPosition.CenterParent;
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
                            selectorForm.DialogResult = DialogResult.OK;
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

                    if (selectorForm.ShowDialog() == DialogResult.OK)
                    {
                        var selected = listBox.SelectedItem as dynamic;
                        if (selected != null)
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo
                            {
                                FileName = selected.Path,
                                UseShellExecute = true
                            };
                            Process.Start(startInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LaunchGameSelector failed: {ex.Message}");
            }
        }

        private void BtnGameMode_Click(object sender, EventArgs e)
        {
            try
            {
                // Set button colors
                btnGameMode.BackColor = GetWindowsAccentColor();
                btnDesktopMode.BackColor = System.Drawing.SystemColors.Control;

                // Set high performance power plan silently
                ProcessStartInfo powerPlanInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(powerPlanInfo);

                /* Commented out to disable monitor timeout changes
                // Set monitor timeout to 0 for AC power silently
                ProcessStartInfo monitorAcInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "-x -monitor-timeout-ac 0",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(monitorAcInfo);

                // Set monitor timeout to 0 for DC power silently
                ProcessStartInfo monitorDcInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "-x -monitor-timeout-dc 0",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(monitorDcInfo);
                */

                // Set NVIDIA Digital Vibrance to 60% (argument 12) using embedded DVChange
                RunEmbeddedDVChange("12");

                // Simulate 50 volume up key presses
                for (int i = 0; i < 50; i++)
                {
                    keybd_event(VK_VOLUME_UP, 0, 0, 0); // Key down
                    keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_KEYUP, 0); // Key up
                }

                // Toggle HDR (assumes HDR is off or needs to be enabled)
                SimulateWinAltB();

                // Launch NVIDIA Control Panel
                LaunchNvidiaControlPanel();

                // Launch NVIDIA App
                LaunchNvidiaApp();

                // Play embedded gamemode.wav after 3.5-second delay
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

                // Launch game selector
                LaunchGameSelector();
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
                // Set button colors
                btnDesktopMode.BackColor = GetWindowsAccentColor();
                btnGameMode.BackColor = System.Drawing.SystemColors.Control;

                // Set balanced power plan silently
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(startInfo);

                // Set NVIDIA Digital Vibrance to 50% (argument 0) using embedded DVChange
                RunEmbeddedDVChange("0");

                // Simulate 50 volume down key presses
                for (int i = 0; i < 50; i++)
                {
                    keybd_event(VK_VOLUME_DOWN, 0, 0, 0); // Key down
                    keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_KEYUP, 0); // Key up
                }

                // Toggle HDR (assumes HDR is on or needs to be disabled)
                SimulateWinAltB();

                // Launch NVIDIA Control Panel
                LaunchNvidiaControlPanel();
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
    }
}