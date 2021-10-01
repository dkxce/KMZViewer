using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace KMZ_Viewer
{
    public class TNCState : XMLSaved<TNCState>
    {
        public string CONFIG_VERSION = "2.0";
        public string PREFIX_BEACON = "=";
        public string SUFFIX_OBJECT = "*";
        public string SUFFIX_ITEM = "!";

        public string CURRENT_LAT { get { return CURRENT_DATA[0]; } set { CURRENT_DATA[0] = value; } }
        public string CURRENT_LON { get { return CURRENT_DATA[1]; } set { CURRENT_DATA[1] = value; } }
        public string CURRENT_ALT { get { return CURRENT_DATA[2]; } set { CURRENT_DATA[2] = value; } }
        public string CURRENT_ICO { get { return CURRENT_DATA[3]; } set { CURRENT_DATA[3] = value; } }
        public string CURRENT_COM { get { return CURRENT_DATA[4]; } set { CURRENT_DATA[4] = value; } }
        public string CURRENT_STT { get { return CURRENT_DATA[5]; } set { CURRENT_DATA[5] = value; } }
        public string CURRENT_MES { get { return CURRENT_DATA[6]; } set { CURRENT_DATA[6] = value; } }        
        public string CURRENT_TYP { get { return CURRENT_DATA[7]; } set { CURRENT_DATA[7] = value; } }

        public string SOURCE_CALLSIGN = "NOCALL-10";
        public string DESTINATION_CALLSIGN = "APRS";
        public string DIGIPATH = "WIDE1-1,WIDE2-2";
        public string PAYLOAD = "";

        public string[] LIST_OF_DESTINATION = new string[] { "APRS", "APUV98", "Heard", "EMAIL" };
        public string[] LIST_OF_DIGIPATH = new string[] { "", "WIDE", "WIDE1-1", "WIDE2-1", "WIDE1-1,WIDE2-1", "WIDE1-1,WIDE2-2" };
        public string[] LIST_OF_SYMBOL = new string[] { "/[" };
        public string[] LIST_OF_COMMENT = new string[0];
        public string[] LIST_OF_MESSAGE = new string[0];
        public string[] LIST_OF_STATE = new string[0];
        public string[] LIST_OF_PAYLOAD = new string[0];

        [XmlIgnore]
        internal string[] CURRENT_DATA = new string[8] { "55.55 N", "37.37 E", "0", "/y", "Send by KMZViewer Software", "On Air", "I'm using KMZViewer Software", "BEACON" };
        [XmlIgnore]
        public string[] P_WAY
        {
            get
            {
                if (String.IsNullOrEmpty(DIGIPATH)) return null;
                return DIGIPATH.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }
        [XmlIgnore]
        public string P_LAT
        {
            get
            {
                string res = CURRENT_DATA[0];
                double rdb = KMZRebuilder.LatLonParser.Parse(res, true);
                return Math.Truncate(rdb).ToString("00") + ((rdb - Math.Truncate(rdb)) * 60).ToString("00.00").Replace(",", ".") + (rdb > 0 ? "N" : "S");
            }
        }
        [XmlIgnore]
        public string P_LON
        {
            get
            {
                string res = CURRENT_DATA[1];
                double rdb = KMZRebuilder.LatLonParser.Parse(res, false);
                return Math.Truncate(rdb).ToString("000") + ((rdb - Math.Truncate(rdb)) * 60).ToString("00.00").Replace(",", ".") + (rdb > 0 ? "E" : "W");
            }
        }
        [XmlIgnore]
        public string P_ALT
        {
            get
            {
                string res = CURRENT_DATA[2];
                double alt = 0;
                double.TryParse(res, out alt);
                alt = alt / 0.3048;
                res = ((int)Math.Round(alt)).ToString();
                while (res.Length < 6) res = "0" + res;
                return res;
            }
        }
    }

    public static class TNCSub
    {
        private static string cd = KMZ_Viewer.KMZViewerForm.CurrentDirectory() + @"\TNCPRESETS\";

        public static string[][] GetPresets()
        {
            try
            {
                if (!System.IO.Directory.Exists(cd))
                    System.IO.Directory.CreateDirectory(cd);
                if (!System.IO.Directory.Exists(cd))
                    return null;
            }
            catch { return null; };
            
            string[] files = System.IO.Directory.GetFiles(cd, "*.tncx");
            if (files == null) return null;
            if (files.Length == 0) return null;

            Regex rx = new Regex(@"\<\!--\sNAME:\s(.*?)\s--\>");
            List<string[]> res = new List<string[]>();
            for (int i = 0; i < files.Length; i++)
            {
                string fn = System.IO.Path.GetFileNameWithoutExtension(files[i]);
                string pn = fn;
                try
                {
                    System.IO.FileStream fs = new System.IO.FileStream(files[i], System.IO.FileMode.Open, System.IO.FileAccess.Read);
                    System.IO.StreamReader sr = new System.IO.StreamReader(fs, System.Text.Encoding.UTF8);
                    string config = sr.ReadToEnd();
                    sr.Close();
                    fs.Close();
                    Match mx = rx.Match(config);
                    if (mx.Success) pn = mx.Groups[1].Value;
                    res.Add(new string[] { files[i], fn, pn });
                }
                catch { };
            };
            return res.ToArray();
        }

        public static void SetPresets(TNCState preset, string name)
        {
            while (name.IndexOf("--") >= 0) name = name.Replace("--", "-");
            try
            {
                if (!System.IO.Directory.Exists(cd))
                    System.IO.Directory.CreateDirectory(cd);
                if (!System.IO.Directory.Exists(cd))
                    return;
            }
            catch { return; };

            string file = String.Format("{1}{0:yyyyMMddHHmmss}UTC.tncx", DateTime.UtcNow, cd);
            string text = TNCState.Save(preset);
            text = text.Replace("</TNCState>", "<!-- NAME: " + name + " -->\r\n</TNCState>");
            try
            {
                System.IO.FileStream fs = new System.IO.FileStream(file, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                System.IO.StreamWriter sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);
                sw.Write(text);
                sw.Close();
                fs.Close();
            }
            catch { };
        }

        public static void UpdatePresets(TNCState preset, string name, string file)
        {
            while (name.IndexOf("--") >= 0) name = name.Replace("--", "-");
            string text = TNCState.Save(preset);
            text = text.Replace("</TNCState>", "<!-- NAME: " + name + " -->\r\n</TNCState>");
            try
            {
                System.IO.FileStream fs = new System.IO.FileStream(file, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                System.IO.StreamWriter sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);
                sw.Write(text);
                sw.Close();
                fs.Close();
            }
            catch { };
        }
    }
}
