using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace IDE.UI {
    public partial class MainWindow {
        private void InitTabContextMenu ()
        {
            var contextMenu = new ContextMenu();
            var item = new MenuItem("Close");
            item.Click += CloseTabMenuClick;
            item.Shortcut = Shortcut.CtrlW;
            contextMenu.MenuItems.Add(item);
            item = new MenuItem("Reload with encoding");
            var subItem = new MenuItem("UTF-8")
            {
                Tag = Encoding.UTF8
            };
            subItem.Click += ReloadWithEncodingClick;
            item.MenuItems.Add(subItem);
            subItem = new MenuItem("ASCII")
            {
                Tag = Encoding.ASCII
            };
            subItem.Click += ReloadWithEncodingClick;
            item.MenuItems.Add(subItem);
            subItem = new MenuItem("UTF32")
            {
                Tag = Encoding.UTF32
            };
            subItem.Click += ReloadWithEncodingClick;
            item.MenuItems.Add(subItem);
            subItem = new MenuItem("UTF7")
            {
                Tag = Encoding.UTF7
            };
            subItem.Click += ReloadWithEncodingClick;
            item.MenuItems.Add(subItem);
            subItem = new MenuItem("Unicode")
            {
                Tag = Encoding.Unicode
            };
            subItem.Click += ReloadWithEncodingClick;
            item.MenuItems.Add(subItem);
            subItem = new MenuItem("BigEndianUnicode")
            {
                Tag = Encoding.BigEndianUnicode
            };
            subItem.Click += ReloadWithEncodingClick;
            item.MenuItems.Add(subItem);
            subItem = new MenuItem("Default")
            {
                Tag = Encoding.Default
            };
            subItem.Click += ReloadWithEncodingClick;
            item.MenuItems.Add(subItem);
            contextMenu.MenuItems.Add(item);
            Documents.ContextMenu = contextMenu;
            Documents.MouseClick += CloseTabClick;
        }

        private void OpenCodeInNewTab (string text, string tabName, string fileName)
        {
            var tab = new TabPage(tabName)
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(5),
                UseVisualStyleBackColor = true,
                AllowDrop = true,
                Margin = new Padding(0)
            };
            tab.DragEnter += DocumentsDragEnter;
            tab.DragDrop += DocumentsDragDrop;
            RichTextBox textBox = InitNewTextBox(text, fileName);
            tab.Tag = textBox;
            Documents.TabPages.Add(tab);
            textBox.Size = tab.Size;
            tab.Controls.Add(textBox);
        }

        private void InitRichTextBoxMenu (RichTextBox textBox)
        {
            var menu = new ContextMenu();
            var item = new MenuItem("Copy");
            item.Click += CopyTextMenuClick;
            item.Shortcut = Shortcut.CtrlC;
            menu.MenuItems.Add(item);
            textBox.ContextMenu = menu;
        }

        private RichTextBox InitNewTextBox (string text, string fileName)
        {
            var textBox = new RichTextBox
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(3),
                Margin = new Padding(0),
                Multiline = true,
                AllowDrop = true,
                Font = new Font(new FontFamily("Consolas"), 10),
                WordWrap = false,
                TabStop = false,
                AcceptsTab = true
            };
            var context = new TextBoxContext(fileName);
            textBox.Tag = context;
            textBox.TextChanged += context.Changed;
            textBox.DragEnter += DocumentsDragEnter;
            textBox.DragDrop += DocumentsDragDrop;
            if (!string.IsNullOrEmpty(text))
                textBox.Text = text;
            InitRichTextBoxMenu(textBox);
            return textBox;
        }

        private void SaveToFile (string text, TextBoxContext context)
        {
            if (string.IsNullOrEmpty(context.FileName))
                SaveToFileAs(text, context);
            else
                context.Save(text);
        }

        private void SaveToFileAs (string text, TextBoxContext context)
        {
            SaveFileDialog.CheckPathExists = true;
            SaveFileDialog.CheckFileExists = false;
            if (SaveFileDialog.ShowDialog() == DialogResult.OK) {
                context.Save(text, SaveFileDialog.FileName);
                Documents.SelectedTab.Text = Path.GetFileName(SaveFileDialog.FileName);
            }
        }
    }
}