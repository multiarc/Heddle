namespace IDE.UI
{
    partial class MainWindow
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
            this.MainMenu = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuNew = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuOpen = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuSave = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuSaveAs = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuClose = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuQuit = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.Documents = new System.Windows.Forms.TabControl();
            this.SaveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.MainMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // MainMenu
            // 
            this.MainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.MainMenu.Location = new System.Drawing.Point(0, 0);
            this.MainMenu.Name = "MainMenu";
            this.MainMenu.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
            this.MainMenu.Size = new System.Drawing.Size(1194, 24);
            this.MainMenu.TabIndex = 0;
            this.MainMenu.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.MenuNew,
            this.MenuOpen,
            this.MenuSave,
            this.MenuSaveAs,
            this.MenuClose,
            this.MenuQuit});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // MenuNew
            // 
            this.MenuNew.Name = "MenuNew";
            this.MenuNew.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.T)));
            this.MenuNew.Size = new System.Drawing.Size(186, 22);
            this.MenuNew.Text = "New";
            this.MenuNew.Click += new System.EventHandler(this.MenuNewClick);
            // 
            // MenuOpen
            // 
            this.MenuOpen.Name = "MenuOpen";
            this.MenuOpen.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.MenuOpen.Size = new System.Drawing.Size(186, 22);
            this.MenuOpen.Text = "Open";
            this.MenuOpen.ToolTipText = "Open existing template";
            this.MenuOpen.Click += new System.EventHandler(this.MenuOpenClick);
            // 
            // MenuSave
            // 
            this.MenuSave.Name = "MenuSave";
            this.MenuSave.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.MenuSave.Size = new System.Drawing.Size(186, 22);
            this.MenuSave.Text = "Save";
            this.MenuSave.ToolTipText = "Save editing template";
            this.MenuSave.Click += new System.EventHandler(this.MenuSaveClick);
            // 
            // MenuSaveAs
            // 
            this.MenuSaveAs.Name = "MenuSaveAs";
            this.MenuSaveAs.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.S)));
            this.MenuSaveAs.Size = new System.Drawing.Size(186, 22);
            this.MenuSaveAs.Text = "Save As";
            this.MenuSaveAs.ToolTipText = "Save editing template as...";
            this.MenuSaveAs.Click += new System.EventHandler(this.MenuSaveAsClick);
            // 
            // MenuClose
            // 
            this.MenuClose.Name = "MenuClose";
            this.MenuClose.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.W)));
            this.MenuClose.Size = new System.Drawing.Size(186, 22);
            this.MenuClose.Text = "Close";
            this.MenuClose.ToolTipText = "Close editing template";
            this.MenuClose.Click += new System.EventHandler(this.MenuCloseClick);
            // 
            // MenuQuit
            // 
            this.MenuQuit.Name = "MenuQuit";
            this.MenuQuit.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Q)));
            this.MenuQuit.Size = new System.Drawing.Size(186, 22);
            this.MenuQuit.Text = "Quit";
            this.MenuQuit.ToolTipText = "Quit IDE";
            this.MenuQuit.Click += new System.EventHandler(this.MenuQuitClick);
            // 
            // OpenFileDialog
            // 
            this.OpenFileDialog.DefaultExt = "tmpl";
            this.OpenFileDialog.Filter = "Template|*.tmpl|HTML|*.html|Text|*.txt|All Files|*.*";
            // 
            // Documents
            // 
            this.Documents.AllowDrop = true;
            this.Documents.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.Documents.Cursor = System.Windows.Forms.Cursors.Hand;
            this.Documents.HotTrack = true;
            this.Documents.Location = new System.Drawing.Point(0, 24);
            this.Documents.Multiline = true;
            this.Documents.Name = "Documents";
            this.Documents.SelectedIndex = 0;
            this.Documents.Size = new System.Drawing.Size(1194, 773);
            this.Documents.TabIndex = 1;
            this.Documents.DragDrop += new System.Windows.Forms.DragEventHandler(this.DocumentsDragDrop);
            this.Documents.DragEnter += new System.Windows.Forms.DragEventHandler(this.DocumentsDragEnter);
            // 
            // SaveFileDialog
            // 
            this.SaveFileDialog.DefaultExt = "tmpl";
            this.SaveFileDialog.Filter = "Template|*.tmpl|HTML|*.html|Text|*.txt|All Files|*.*";
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1194, 800);
            this.Controls.Add(this.Documents);
            this.Controls.Add(this.MainMenu);
            this.DoubleBuffered = true;
            this.MainMenuStrip = this.MainMenu;
            this.Name = "MainWindow";
            this.Text = "Text Templating Engine";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainWindowFormClosing);
            this.MainMenu.ResumeLayout(false);
            this.MainMenu.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip MainMenu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem MenuOpen;
        private System.Windows.Forms.ToolStripMenuItem MenuSave;
        private System.Windows.Forms.ToolStripMenuItem MenuSaveAs;
        private System.Windows.Forms.ToolStripMenuItem MenuClose;
        private System.Windows.Forms.ToolStripMenuItem MenuQuit;
        private System.Windows.Forms.OpenFileDialog OpenFileDialog;
        private System.Windows.Forms.ToolStripMenuItem MenuNew;
        private System.Windows.Forms.TabControl Documents;
        private System.Windows.Forms.SaveFileDialog SaveFileDialog;
    }
}

