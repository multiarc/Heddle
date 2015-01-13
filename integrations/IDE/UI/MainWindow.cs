using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDE.Helpers;

namespace IDE.UI {
    public partial class MainWindow: Form {
        public MainWindow ()
        {
            InitializeComponent();
            InitTabContextMenu();
        }

        private void MenuOpenClick (object sender, EventArgs e)
        {
            OpenFileDialog.CheckFileExists = true;
            OpenFileDialog.CheckPathExists = true;
            OpenFileDialog.Multiselect = true;
            if (OpenFileDialog.ShowDialog() == DialogResult.OK) {
                foreach (string fileName in OpenFileDialog.FileNames) {
                    string tabName = Path.GetFileName(fileName);
                    string text = FileHelper.ReadTextFile(fileName);
                    OpenCodeInNewTab(text, tabName, fileName);
                }
            }
        }

        private void DocumentsDragDrop (object sender, DragEventArgs e)
        {
            string[] dataFormats = e.Data.GetFormats(true);
            if (dataFormats.Contains(DataFormats.FileDrop)) {
                var fileNames = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (fileNames != null) {
                    foreach (string fileName in fileNames) {
                        string tabName = Path.GetFileName(fileName);
                        string text = FileHelper.ReadTextFile(fileName);
                        OpenCodeInNewTab(text, tabName, fileName);
                    }
                }
            } else if (dataFormats.Contains(DataFormats.Html) || dataFormats.Contains(DataFormats.Text) || dataFormats.Contains(DataFormats.UnicodeText)
                       || dataFormats.Contains(DataFormats.OemText) || dataFormats.Contains(DataFormats.StringFormat)
                       || dataFormats.Contains(DataFormats.Rtf)) {
                var text = e.Data.GetData(DataFormats.UnicodeText, true) as string;
                if (text != null)
                    OpenCodeInNewTab(text, "Untitled", null);
            }
        }

        private void ReloadWithEncodingClick (object sender, EventArgs e)
        {
            var item = sender as MenuItem;
            if (item != null && item.Tag is Encoding) {
                var textBox = Documents.SelectedTab.Tag as RichTextBox;
                if (textBox != null && textBox.Tag is TextBoxContext && !string.IsNullOrEmpty((textBox.Tag as TextBoxContext).FileName))
                    textBox.Text = FileHelper.ReadTextFile((textBox.Tag as TextBoxContext).FileName, item.Tag as Encoding);
            }
        }

        private void CloseTabMenuClick (object sender, EventArgs e)
        {
            Documents.TabPages.Remove(Documents.SelectedTab);
        }

        private void CloseTabClick (object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
                Documents.TabPages.Remove(Documents.SelectedTab);
        }

        private void CopyTextMenuClick (object sender, EventArgs e)
        {
            if (Documents.SelectedTab != null && Documents.SelectedTab.Tag is RichTextBox) {
                var textBox = (RichTextBox) Documents.SelectedTab.Tag;
                if (!string.IsNullOrEmpty(textBox.SelectedText))
                    textBox.Copy();
                else
                    Clipboard.SetData(DataFormats.Text, textBox.Lines[textBox.GetLineFromCharIndex(textBox.SelectionStart)]);
            }
        }

        private void DocumentsDragEnter (object sender, DragEventArgs e)
        {
            string[] dataFormats = e.Data.GetFormats(true);
            if (dataFormats.Contains(DataFormats.FileDrop) || dataFormats.Contains(DataFormats.Html) || dataFormats.Contains(DataFormats.Text)
                || dataFormats.Contains(DataFormats.UnicodeText) || dataFormats.Contains(DataFormats.OemText)
                || dataFormats.Contains(DataFormats.StringFormat) || dataFormats.Contains(DataFormats.Rtf))
                e.Effect = DragDropEffects.Copy;
        }

        private void MenuQuitClick (object sender, EventArgs e)
        {
            Close();
        }

        private void MenuCloseClick (object sender, EventArgs e)
        {
            Documents.TabPages.Remove(Documents.SelectedTab);
        }

        private void MenuNewClick (object sender, EventArgs e)
        {
            OpenCodeInNewTab("", "Untitled", "");
        }

        private void MenuSaveClick (object sender, EventArgs e)
        {
            if (Documents.SelectedTab != null && Documents.SelectedTab.Tag is RichTextBox) {
                var textBox = (RichTextBox) Documents.SelectedTab.Tag;
                var context = (TextBoxContext) textBox.Tag;
                SaveToFile(string.Join("\r\n", textBox.Lines), context);
            }
        }

        private void MenuSaveAsClick (object sender, EventArgs e)
        {
            if (Documents.SelectedTab != null && Documents.SelectedTab.Tag is RichTextBox) {
                var textBox = (RichTextBox) Documents.SelectedTab.Tag;
                var context = (TextBoxContext) textBox.Tag;
                SaveToFileAs(string.Join("\r\n", textBox.Lines), context);
            }
        }

        private void MainWindowFormClosing (object sender, FormClosingEventArgs e)
        {
            bool yesForAll = false;
            foreach (TabPage tabPage in Documents.TabPages) {
                if (tabPage.Tag is RichTextBox) {
                    var textBox = (RichTextBox) Documents.SelectedTab.Tag;
                    var context = (TextBoxContext) textBox.Tag;
                    if (context.IsChanged) {
                        if (yesForAll) {
                            SaveToFile(textBox.Text, context);
                            continue;
                        }
                        var confirmDialog = new SaveConfirmDialog
                        {
                            LabelFileName =
                            {
                                Text = tabPage.Text
                            }
                        };
                        if (Documents.TabCount == 1) {
                            confirmDialog.ButtonYesForAll.Enabled = false;
                            confirmDialog.ButtonNoForAll.Enabled = false;
                        }
                        DialogResult result = confirmDialog.ShowDialog();
                        confirmDialog.Dispose();
                        switch (result) {
                                //Yes for all
                            case DialogResult.OK:
                                yesForAll = true;
                                SaveToFile(textBox.Text, context);
                                break;
                                //No for all
                            case DialogResult.Ignore:
                                return;
                            case DialogResult.No:
                                continue;
                            case DialogResult.Yes:
                                SaveToFile(textBox.Text, context);
                                break;
                            default:
                                e.Cancel = true;
                                return;
                        }
                        Documents.TabPages.Remove(tabPage);
                    }
                }
            }
        }
    }
}