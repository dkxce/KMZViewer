namespace KMZViewer
{
    partial class SelectIcon
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
            this.secondary = new System.Windows.Forms.PictureBox();
            this.primary = new System.Windows.Forms.PictureBox();
            this.button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.secondary)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.primary)).BeginInit();
            this.SuspendLayout();
            // 
            // secondary
            // 
            this.secondary.Cursor = System.Windows.Forms.Cursors.Hand;
            this.secondary.Location = new System.Drawing.Point(12, 169);
            this.secondary.Name = "secondary";
            this.secondary.Size = new System.Drawing.Size(384, 144);
            this.secondary.TabIndex = 1;
            this.secondary.TabStop = false;
            this.secondary.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.secondary_MouseDoubleClick);
            this.secondary.MouseClick += new System.Windows.Forms.MouseEventHandler(this.secondary_MouseClick);
            // 
            // primary
            // 
            this.primary.Cursor = System.Windows.Forms.Cursors.Hand;
            this.primary.Location = new System.Drawing.Point(12, 12);
            this.primary.Name = "primary";
            this.primary.Size = new System.Drawing.Size(384, 144);
            this.primary.TabIndex = 0;
            this.primary.TabStop = false;
            this.primary.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.primary_MouseDoubleClick);
            this.primary.MouseClick += new System.Windows.Forms.MouseEventHandler(this.primary_MouseClick);
            // 
            // button1
            // 
            this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button1.Location = new System.Drawing.Point(167, 327);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 2;
            this.button1.Text = "OK";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // SelectIcon
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(407, 363);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.secondary);
            this.Controls.Add(this.primary);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SelectIcon";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Выберите иконку";
            ((System.ComponentModel.ISupportInitialize)(this.secondary)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.primary)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.PictureBox primary;
        public System.Windows.Forms.PictureBox secondary;
        private System.Windows.Forms.Button button1;

    }
}