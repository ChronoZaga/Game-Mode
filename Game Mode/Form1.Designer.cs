using System;

namespace Game_Mode
{
    partial class Form1
    {
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
            //this.BackColor = System.Drawing.Color.Gray;
            // 
            // btnGameMode
            // 
            this.btnGameMode.Location = new System.Drawing.Point(15, 30);
            this.btnGameMode.Name = "btnGameMode";
            this.btnGameMode.Size = new System.Drawing.Size(120, 100);
            this.btnGameMode.TabIndex = 0;
            this.btnGameMode.Text = "";
            this.btnGameMode.UseVisualStyleBackColor = true;
            this.btnGameMode.Image = LoadIcoAsBitmap("Game_Mode.game.ico", 120, 100); // Scale to button size
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
            this.btnDesktopMode.Image = LoadIcoAsBitmap("Game_Mode.desktop.ico", 120, 100); // Scale to button size
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
            this.btnToggleHDR.Image = LoadSystemIconAsBitmap(@"C:\Windows\System32\shell32.dll", 15, 24, 24); // Smaller monitor icon
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
            this.btnSettings.Image = LoadIcoAsBitmap("Game_Mode.settings.ico", 16, 16); // Changed to embedded resource
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
            this.Name = "Form1";
            this.Opacity = 0.98; // Set transparency percentage
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
                        System.Diagnostics.Debug.WriteLine($"Error: Embedded resource '{resourceName}' not found.");
                        return null;
                    }
                    using (var icon = new System.Drawing.Icon(stream))
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
                            return scaledBitmap;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading embedded ICO: {ex.Message}");
                return null;
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
                    System.Diagnostics.Debug.WriteLine($"Error: Failed to extract icon index {iconIndex} from '{filePath}'.");
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
                System.Diagnostics.Debug.WriteLine($"Error loading system icon: {ex.Message}");
                return null;
            }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}