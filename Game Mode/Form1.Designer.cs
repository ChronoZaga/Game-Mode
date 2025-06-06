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
            this.btnGameMode = new System.Windows.Forms.Button();
            this.btnDesktopMode = new System.Windows.Forms.Button();
            this.SuspendLayout();
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
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 161); // Adjusted to match Size = 300,200 after borders
            this.Controls.Add(this.btnDesktopMode);
            this.Controls.Add(this.btnGameMode);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Game Mode";
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button btnGameMode;
        private System.Windows.Forms.Button btnDesktopMode;

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
    }
}