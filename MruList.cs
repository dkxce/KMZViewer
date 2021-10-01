using System;
using System.Collections.Generic;
using System.Text;

using System.Windows.Forms;
using System.IO;

namespace KMZ_Viewer
{
    public class MruPathList
    {
        private bool isDirList = false;

        private string MRUListSavedFileName;
        private int MRUFilesCount;
        private List<FileInfo> MRUFilesInfos;
        private ToolStripMenuItem MyMenu;

        private bool UseSeparator = false;
        private ToolStripSeparator Separator = null;
        private ToolStripMenuItem[] MenuItems;

        // Raised when the user selects a file from the MRU list.
        public delegate void FileSelectedEventHandler(string file_name);
        public event FileSelectedEventHandler FileSelected;

        public int Count { get { return MRUFilesInfos.Count; } }

        // Constructors
        #region Constructors
        public MruPathList(string MRUFileName, ToolStripMenuItem menu)
        {
            this.Init(MRUFileName, menu, 10, false);
        }
        public MruPathList(string MRUFileName, ToolStripMenuItem menu, int num_files)
        {
            this.Init(MRUFileName, menu, num_files, false);
        }
        public MruPathList(string MRUFileName, ToolStripMenuItem menu, bool isDirList)
        {
            this.Init(MRUFileName, menu, 10, isDirList);
        }
        public MruPathList(string MRUFileName, ToolStripMenuItem menu, int num_files, bool isDirList)
        {
            this.Init(MRUFileName, menu, num_files, isDirList);
        }
        #endregion Constructors

        // Init
        private void Init(string MRUFileName, ToolStripMenuItem menu, int num_files, bool isDirList)
        {
            this.MRUListSavedFileName = MRUFileName;
            this.isDirList = isDirList;

            MyMenu = menu;
            MRUFilesCount = num_files;
            MRUFilesInfos = new List<FileInfo>();

            // Make a separator
            Separator = new ToolStripSeparator();
            Separator.Visible = false;
            if (UseSeparator) MyMenu.DropDownItems.Add(Separator);

            // Make the menu items we may later need.
            MenuItems = new ToolStripMenuItem[MRUFilesCount + 1];
            for (int i = 0; i < MRUFilesCount; i++)
            {
                MenuItems[i] = new ToolStripMenuItem();
                MenuItems[i].Visible = false;
                MyMenu.DropDownItems.Add(MenuItems[i]);
            };

            // Reload items from the registry.
            LoadFiles();

            // Display the items.
            ShowFiles();
        }

        private void LoadFiles()
        {
            string filemru = this.MRUListSavedFileName;
            if (!File.Exists(filemru)) return; 

            FileStream fs = new FileStream(filemru, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.GetEncoding(1251));
            while (!sr.EndOfStream)
            {
                string fullpath = sr.ReadLine();
                if (String.IsNullOrEmpty(fullpath)) continue;
                if (fullpath.StartsWith("#")) continue;
                if (fullpath.StartsWith("@")) continue;
                if (!isDirList)
                {
                    if (File.Exists(fullpath))
                        MRUFilesInfos.Add(new FileInfo(fullpath));
                }
                else
                {
                    if (Directory.Exists(fullpath))
                        MRUFilesInfos.Add(new FileInfo(fullpath));
                };
                        
            };
            sr.Close();
            fs.Close();
        }

        // Save the current items in the Registry.
        private void SaveFiles()
        {            
            string filemru = this.MRUListSavedFileName;
            if (filemru == null) return;
            FileStream fs = new FileStream(filemru, FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
            sw.WriteLine("#");
            sw.WriteLine("# MRU Path List");
            sw.WriteLine("#");
            sw.WriteLine("# One path per line");
            sw.WriteLine("#");
            foreach (FileInfo file_info in MRUFilesInfos)
                sw.WriteLine(file_info.FullName);
            sw.Close();
            fs.Close();            
        }

        // Remove a file's info from the list.
        private void RemoveFileInfo(string file_name)
        {
            // Remove occurrences of the file's information from the list.
            for (int i = MRUFilesInfos.Count - 1; i >= 0; i--)
            {
                if (MRUFilesInfos[i].FullName == file_name) MRUFilesInfos.RemoveAt(i);
            }
        }

        // Add a file to the list, rearranging if necessary.
        public void AddFile(string file_name)
        {
            // Remove the file from the list.
            RemoveFileInfo(file_name);

            // Add the file to the beginning of the list.
            MRUFilesInfos.Insert(0, new FileInfo(file_name));

            // If we have too many items, remove the last one.
            if (MRUFilesInfos.Count > MRUFilesCount) MRUFilesInfos.RemoveAt(MRUFilesCount);

            // Display the files.
            ShowFiles();

            // Update the Registry.
            SaveFiles();
        }

        // Remove a file from the list, rearranging if necessary.
        public void RemoveFile(string file_name)
        {
            // Remove the file from the list.
            RemoveFileInfo(file_name);

            // Display the files.
            ShowFiles();

            // Update the Registry.
            SaveFiles();
        }

        public delegate string FormatMenuItemTextEvent(int index, FileInfo FileInfo, ref System.Drawing.Color TextColor);
        public event FormatMenuItemTextEvent FormatMenuItem;

        // Display the files in the menu items.
        private void ShowFiles()
        {
            Separator.Visible = (MRUFilesInfos.Count > 0);
            for (int i = 0; i < MRUFilesInfos.Count; i++)
            {
                string mit = string.Format("&{0}: `{1}`", i+1, MRUFilesInfos[i].Name);
                mit +=  " at .. " + MRUFilesInfos[i].FullName.Remove(MRUFilesInfos[i].FullName.Length - MRUFilesInfos[i].Name.Length);
                while (mit.Length > 90) mit = mit.Remove(mit.IndexOf("` at .. ") + 8, 1);

                if (FormatMenuItem != null)
                {
                    System.Drawing.Color color = MenuItems[i].ForeColor = System.Drawing.Color.Black;
                    string MIT = FormatMenuItem(i + 1, MRUFilesInfos[i], ref color);
                    mit = String.IsNullOrEmpty(MIT) ? mit : MIT;
                    MenuItems[i].ForeColor = color;
                };

                MenuItems[i].Text = mit;                
                MenuItems[i].Visible = true;
                MenuItems[i].Tag = MRUFilesInfos[i];
                MenuItems[i].Click -= File_Click;
                MenuItems[i].Click += File_Click;
            }
            for (int i = MRUFilesInfos.Count; i < MRUFilesCount; i++)
            {
                MenuItems[i].Visible = false;
                MenuItems[i].Click -= File_Click;
            }
        }

        public void UpdateNames()
        {
            ShowFiles();
        }

        // The user selected a file from the menu.
        private void File_Click(object sender, EventArgs e)
        {
            // Don't bother if no one wants to catch the event.
            if (FileSelected != null)
            {
                // Get the corresponding FileInfo object.
                ToolStripMenuItem menu_item = sender as ToolStripMenuItem;
                FileInfo file_info = menu_item.Tag as FileInfo;

                // Raise the event.
                FileSelected(file_info.FullName);
            }
        }
    }
}
