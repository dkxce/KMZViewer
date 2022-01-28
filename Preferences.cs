using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Windows.Forms;

namespace KMZ_Viewer
{
    [Serializable]
    public class Preferences : XMLSaved<Preferences>
    {
        [XmlArray("configuration")]
        [XmlArrayItem("property")]
        public List<Property> Properties;

        [XmlIgnore]
        private bool DefaultsIsLoaded = false;

        [XmlIgnore]
        public string this[string name]
        {
            get
            {
                LoadDefaults();
                if (Properties.Count == 0) return "";
                foreach (Property prop in Properties)
                    if (prop.name == name)
                        return prop.value;
                return "";
            }
            set
            {
                LoadDefaults();
                foreach (Property prop in Properties)
                    if (prop.name == name)
                    {
                        prop.value = value;
                        return;
                    };
                Properties.Add(new Property(name, value));
            }
        }

        public bool GetBoolValue(string name)
        {
            LoadDefaults();
            if (Properties.Count == 0) return false;
            foreach (Property prop in Properties)
                if (prop.name == name)
                {
                    string pv = prop.value;
                    if (String.IsNullOrEmpty(pv)) return false;
                    pv = pv.ToLower();
                    return (pv == "1") || (pv == "yes") || (pv == "true");
                };
            return false;
        }

        private void LoadDefaults()
        {
            if (DefaultsIsLoaded) return;
            if (Properties == null) Properties = new List<Property>();
            if ((Properties.Count > 0) && (String.IsNullOrEmpty(Properties[0].comm))) Properties = new List<Property>();
            if (!this.Contains("gpi_localization")) Properties.Add(new Property("gpi_localization", "EN", 0, "2-symbols string, Language, ISO-639 code"));
            if (!this.Contains("gpireader_save_media")) Properties.Add(new Property("gpireader_save_media", "no", 1, "Save media from GPI"));
            if (!this.Contains("gpireader_poi_image_from_jpeg")) Properties.Add(new Property("gpireader_poi_image_from_jpeg", "no", 1, "If yes - POI image sets from JPEG, if no - from Bitmap"));
            DefaultsIsLoaded = true;
        }

        private bool Contains(string name)
        {
            foreach (Property prop in Properties)
                if (prop.name == name) return true;
            return false;
        }

        public static Preferences Load()
        {
            string fName = KMZViewerForm.CurrentDirectory() + @"\KMZViewer.config";
            if (File.Exists(fName))
            {
                try { return Preferences.Load(fName); }
                catch { };
            };
            return new Preferences();
        }

        public void Save()
        {
            string fName = KMZViewerForm.CurrentDirectory() + @"\KMZViewer.config";
            this.LoadDefaults();
            try { Preferences.Save(fName, this); }
            catch { };
        }

        [Serializable]
        public class Property
        {
            [XmlAttribute]
            public string name;
            [XmlAttribute]
            public byte cat; // 0 - string, 1 - boolean, 2 - number, 3 - disable
            [XmlAttribute]
            public string comm;
            [XmlAttribute]
            public ushort min = ushort.MinValue;
            [XmlAttribute]
            public ushort max = ushort.MaxValue;
            [XmlText]
            public string value;

            public Property() { }
            public Property(string name) { this.name = name; }
            public Property(string name, string value) { this.name = name; this.value = value; }
            public Property(string name, string value, byte cat) { this.name = name; this.value = value; this.cat = cat; }
            public Property(string name, string value, byte cat, string comm) { this.name = name; this.value = value; this.cat = cat; this.comm = comm; }
            public Property(string name, string value, byte cat, string comm, byte min, byte max) { this.name = name; this.value = value; this.cat = cat; this.comm = comm; this.min = min; this.max = max; }

            public override string ToString()
            {
                return String.Format("{0} = {1}", name, value);
            }
        }

        public void ShowChangeDialog()
        {
            Form form = new Form();
            form.StartPosition = FormStartPosition.CenterParent;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.ShowIcon = false;
            form.ShowInTaskbar = false;
            form.Width = 400;
            form.Height = 420;
            form.Text = "Предпочтения";
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            Label lab = new Label();
            form.Controls.Add(lab);
            lab.Text = "Дважды кликните или нажмите пробел/ввод для изменения:";
            lab.AutoSize = true;
            lab.Left = 8;
            lab.Top = 5;
            Label hel = new Label();
            form.Controls.Add(hel);
            hel.Text = "-- параметр не выбран --";
            hel.Width = form.Width - 26;
            hel.Height = 32;
            hel.Left = 8;
            hel.Top = 25 + form.Height - 116;
            ListBox lb = new ListBox();
            form.Controls.Add(lb);
            lb.Width = form.Width - 26;
            lb.Left = 10;
            lb.Top = 25;
            lb.Height = form.Height - 110;
            lb.BorderStyle = BorderStyle.FixedSingle;
            foreach (Property prop in Properties) lb.Items.Add(prop);
            lb.DoubleClick += (delegate(object sender, EventArgs e) { OnChangeItem(lb); });
            lb.KeyPress += (delegate(object sender, KeyPressEventArgs e) { if ((e.KeyChar == (char)32) || (e.KeyChar == (char)13)) OnChangeItem(lb); });
            lb.SelectedIndexChanged += (delegate(object sender, EventArgs e) { if (lb.SelectedIndex < 0) hel.Text = "-- параметр не выбран --"; else hel.Text = ((Property)lb.SelectedItem).comm; });
            Button okbtn = new Button();
            form.Controls.Add(okbtn);
            okbtn.Left = form.Width / 2 - okbtn.Width / 2;
            okbtn.Top = lb.Top + lb.Height + 26;
            okbtn.Text = "OK";
            okbtn.Click += (delegate(object sender, EventArgs e) { form.Close(); });
            form.ShowDialog();
            form.Dispose();            
        }

        private void OnChangeItem(ListBox lb)
        {
            int si = lb.SelectedIndex;
            if (si >= 0)
            {
                Property p = (Property)lb.SelectedItem;
                if (p.cat == 3) return;
                string caption = "Изменить значение";
                string nval = p.value;
                if (p.cat == 1) // boolean value
                {
                    int ifl = 0;
                    List<string> yn = new List<string>(new string[] { "no", "yes" });
                    if (nval == "yes") ifl = 1;
                    if (InputBox.Show(caption, p.name + ":", yn.ToArray(), ref ifl) == DialogResult.OK)
                    {
                        p.value = yn[ifl];
                        this[p.name] = p.value;
                        lb.Items[si] = p;
                    };
                }
                else if (p.cat == 2) // number value
                {
                    int ifl = int.Parse(nval);
                    if (InputBox.Show(caption, p.name + ":", ref ifl, p.min, p.max) == DialogResult.OK)
                    {
                        p.value = ifl.ToString();
                        this[p.name] = p.value;
                        lb.Items[si] = p;
                    };
                }
                else // text value
                {
                    if (InputBox.Show(caption, p.name + ":", ref nval, "R^.{" + p.min + "," + p.max + "}$") == DialogResult.OK)
                    {
                        p.value = nval.Trim();
                        this[p.name] = p.value;
                        lb.Items[si] = p;
                    };
                };
            };
        }
    }
}
