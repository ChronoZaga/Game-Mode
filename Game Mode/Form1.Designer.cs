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
            this.btnGameMode.Text = "Game Mode";
            this.btnGameMode.UseVisualStyleBackColor = true;
            this.btnGameMode.Click += new System.EventHandler(this.BtnGameMode_Click);
            // 
            // btnDesktopMode
            // 
            this.btnDesktopMode.Location = new System.Drawing.Point(150, 30);
            this.btnDesktopMode.Name = "btnDesktopMode";
            this.btnDesktopMode.Size = new System.Drawing.Size(120, 100);
            this.btnDesktopMode.TabIndex = 1;
            this.btnDesktopMode.Text = "Desktop Mode";
            this.btnDesktopMode.UseVisualStyleBackColor = true;
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
    }
}