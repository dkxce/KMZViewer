using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;

namespace KMZRebuilder
{
    public partial class SelectionViewer_Filter : Form
    {
        KMZ_Viewer.KMZViewerForm parent;
        XmlDocument xd;
        
        int total;
        int filtered;
        List<int> ids = new List<int>();
        public List<PointF> LoadedRoute = new List<PointF>();
        public List<PointF> LoadedPoly = new List<PointF>();

        public SelectionViewer_Filter(Form parent, XmlDocument xd)
        {
            if (parent is KMZ_Viewer.KMZViewerForm)
                this.parent = (KMZ_Viewer.KMZViewerForm)parent;
            this.xd = xd;
            
            InitializeComponent();
            marksFilter.SelectedIndex = 0;

            this.Text = "KMZRebuilder Selection Filter";

            Reset();
        }

        public void Up()
        {
            label1.Text = String.Format("Total placemarks: {0}", total);
            filtered = ids.Count;
            int todel = total - filtered;
            string del_text = "delete";
            label3.Text = todel > 0 ? String.Format("Placemarks with filter: {0} to keep, {1} to " + del_text, filtered, todel) : "---";
            button9.Enabled = button11.Enabled = todel > 0;
            label3.ForeColor = total == filtered ? Color.Black : Color.Maroon;
        }

        private XmlNode GetXMLNode(int objectindex, out XmlNode layer)
        {
            layer = null;
            if(objectindex < 0) return null;
            if (objectindex >= this.parent.objects.Items.Count) return null;
            layer = xd.SelectNodes("kml/Document/Folder")[int.Parse(this.parent.objects.Items[objectindex].SubItems[1].Text)];
            return layer.SelectNodes("Placemark")[int.Parse(this.parent.objects.Items[objectindex].SubItems[2].Text)];
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Regex reg = new Regex(textBox1.Text.Trim(),checkBox1.Checked ? RegexOptions.IgnoreCase : RegexOptions.None);
            
            ids.Clear();
            for (int i = 0; i < this.parent.objects.Items.Count; i++)
            {
                bool reg_name = false;
                bool reg_desc = false;

                XmlNode layer;
                XmlNode placemark = GetXMLNode(i, out layer);
                XmlNode nn = placemark.SelectSingleNode("name");
                if (nn != null)
                {
                    if (nn.HasChildNodes)
                    {
                        string nam = nn.ChildNodes[0].Value;
                        if (reg.IsMatch(nam)) reg_name = true;
                    };
                };

                string description = "";
                try { description = placemark.SelectSingleNode("description").ChildNodes[0].Value; }
                catch { };
                if ((!String.IsNullOrEmpty(description)) && (reg.IsMatch(description)))
                    reg_desc = true;

                bool add = false;

                if ((ApplyTo.SelectedIndex == 0) && reg_name) add = true;               // name only
                if ((ApplyTo.SelectedIndex == 1) && reg_desc) add = true;               // desc only
                if ((ApplyTo.SelectedIndex == 2) && (reg_name || reg_desc)) add = true; // name OR desc
                if ((ApplyTo.SelectedIndex == 3) && (reg_name && reg_desc)) add = true; // name AND desc
                if ((ApplyTo.SelectedIndex == 4) && (reg_name != reg_desc)) add = true; // name AND desc

                if (add) ids.Add(i);               
            };            
            Up();
            button6.Enabled = true;
        }

        private void Reset()
        {
            ids.Clear();
            for (int i = 0; i < this.parent.objects.Items.Count; i++) ids.Add(i);
            total = this.parent.objects.Items.Count;
            filtered = ids.Count;
            Up();
            button6.Enabled = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Regex reg = new Regex(textBox1.Text.Trim(), checkBox1.Checked ? RegexOptions.IgnoreCase : RegexOptions.None);

            ids.Clear();
            for (int i = 0; i < this.parent.objects.Items.Count; i++)
            {
                bool reg_name = false;
                bool reg_desc = false;

                XmlNode layer;
                XmlNode placemark = GetXMLNode(i, out layer);
                XmlNode nn = placemark.SelectSingleNode("name");
                if (nn != null)
                {
                    if (nn.HasChildNodes)
                    {
                        string nam = nn.ChildNodes[0].Value;
                        if (reg.IsMatch(nam)) reg_name = true;
                    };
                };

                string description = "";
                try { description = placemark.SelectSingleNode("description").ChildNodes[0].Value; }
                catch { };
                if ((!String.IsNullOrEmpty(description)) && (reg.IsMatch(description)))
                    reg_desc = true;

                bool add = false;

                if ((ApplyTo.SelectedIndex == 0) && reg_name) add = true;               // name only
                if ((ApplyTo.SelectedIndex == 1) && reg_desc) add = true;               // desc only
                if ((ApplyTo.SelectedIndex == 2) && (reg_name || reg_desc)) add = true; // name OR desc
                if ((ApplyTo.SelectedIndex == 3) && (reg_name && reg_desc)) add = true; // name AND desc
                if ((ApplyTo.SelectedIndex == 4) && (reg_name != reg_desc)) add = true; // name AND desc

                if (!add) ids.Add(i);
            };            
            Up();
            button6.Enabled = true;
        }

        private void button6_Click(object sender, EventArgs e)
        {            
            Reset();            
        }

        private void button5_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = ".kml,.gpx";
            ofd.Filter = "KML, GPX & SHP files (*.kml;*.gpx;*.shp)|*.kml;*.gpx;*.shp";
            if (ofd.ShowDialog() == DialogResult.OK)
                loadroute(ofd.FileName);
            ofd.Dispose();
        }

        private void loadroute(string filename)
        {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InstalledUICulture;
            System.Globalization.NumberFormatInfo ni = (System.Globalization.NumberFormatInfo)ci.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";

            routeFileBox.Text = filename;
            LoadedRoute.Clear();

            System.IO.FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            System.IO.StreamReader sr = new StreamReader(fs);
            if (System.IO.Path.GetExtension(filename).ToLower() == ".kml")
            {
                string file = sr.ReadToEnd();
                int si = file.IndexOf("<coordinates>");
                int ei = file.IndexOf("</coordinates>");
                string co = file.Substring(si + 13, ei - si - 13).Trim().Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
                string[] arr = co.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                if ((arr != null) && (arr.Length > 0))
                    for (int i = 0; i < arr.Length; i++)
                    {
                        string[] xyz = arr[i].Split(new string[] { "," }, StringSplitOptions.None);
                        LoadedRoute.Add(new PointF(float.Parse(xyz[0], ni), float.Parse(xyz[1], ni)));
                    };
            };
            if (System.IO.Path.GetExtension(filename).ToLower() == ".gpx")
            {
                string file = sr.ReadToEnd();
                int si = 0;
                int ei = 0;
                si = file.IndexOf("<rtept", ei);
                ei = file.IndexOf(">", si);
                while (si > 0)
                {
                    string rtept = file.Substring(si + 7, ei - si - 7).Replace("\"", "").Replace("/", "").Trim();
                    int ssi = rtept.IndexOf("lat=");
                    int sse = rtept.IndexOf(" ", ssi);
                    if (sse < 0) sse = rtept.Length;
                    string lat = rtept.Substring(ssi + 4, sse - ssi - 4);
                    ssi = rtept.IndexOf("lon=");
                    sse = rtept.IndexOf(" ", ssi);
                    if (sse < 0) sse = rtept.Length;
                    string lon = rtept.Substring(ssi + 4, sse - ssi - 4);
                    LoadedRoute.Add(new PointF(float.Parse(lon, ni), float.Parse(lat, ni)));

                    si = file.IndexOf("<rtept", ei);
                    if (si > 0)
                        ei = file.IndexOf(">", si);
                };
            };
            if (Path.GetExtension(filename).ToLower() == ".shp")
            {
                fs.Position = 32;
                int tof = fs.ReadByte();
                if ((tof == 3))
                {
                    fs.Position = 104;
                    byte[] ba = new byte[4];
                    fs.Read(ba, 0, ba.Length);
                    if (BitConverter.IsLittleEndian) Array.Reverse(ba);
                    int len = BitConverter.ToInt32(ba, 0) * 2;
                    fs.Read(ba, 0, ba.Length);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                    tof = BitConverter.ToInt32(ba, 0);
                    if ((tof == 3))
                    {
                        fs.Position += 32;
                        fs.Read(ba, 0, ba.Length);
                        if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                        if (BitConverter.ToInt32(ba, 0) == 1)
                        {
                            fs.Read(ba, 0, ba.Length);
                            if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                            int pCo = BitConverter.ToInt32(ba, 0);
                            fs.Read(ba, 0, ba.Length);
                            if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                            if (BitConverter.ToInt32(ba, 0) == 0)
                            {
                                ba = new byte[8];
                                for (int i = 0; i < pCo; i++)
                                {
                                    PointF ap = new PointF();
                                    fs.Read(ba, 0, ba.Length);
                                    if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                                    ap.X = (float)BitConverter.ToDouble(ba, 0);
                                    fs.Read(ba, 0, ba.Length);
                                    if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                                    ap.Y = (float)BitConverter.ToDouble(ba, 0);
                                    LoadedRoute.Add(ap);
                                };
                            };
                        };
                    };
                };
            };
            sr.Close();
            fs.Close();

            label7.Text = "Route (path) has " + LoadedRoute.Count.ToString() + " points";
            marksFilter.Enabled = LoadedRoute.Count > 1;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            dosort(true);
            button6.Enabled = true;
        }

        private void dosort(bool inside)
        {
            if (marksFilter.SelectedIndex == -1) return;
            if (LoadedRoute.Count < 2) return;

            double rad = (double)inrad.Value;

            ids.Clear();
            for (int itm = 0; itm < this.parent.objects.Items.Count; itm++)
            {
                bool skip = true;

                XmlNode layer;
                XmlNode placemark = GetXMLNode(itm, out layer);
                XmlNode cn = placemark.SelectSingleNode("Point/coordinates");
                if (cn != null)
                {
                    string[] llz = cn.ChildNodes[0].Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    if (llz.Length > 2)
                    {
                        double x = double.Parse(llz[0], System.Globalization.CultureInfo.InvariantCulture);
                        double y = double.Parse(llz[1], System.Globalization.CultureInfo.InvariantCulture);

                        double length2 = double.MaxValue;
                        int side2 = 0;
                        for (int i = 1; i < LoadedRoute.Count; i++)
                        {
                            PointF op;
                            int side;
                            double d = DistanceFromPointToLine(new PointF((float)x, (float)y), LoadedRoute[i - 1], LoadedRoute[i], out op, out side);
                            if (d < length2)
                            {
                                length2 = d;
                                side2 = side;
                            };
                        };

                        if (length2 <= rad)
                        {
                            if (marksFilter.SelectedIndex < 1)
                                skip = false;
                            else
                                if ((marksFilter.SelectedIndex == 1) && (side2 <= 0))
                                    skip = false;
                                else
                                    if ((marksFilter.SelectedIndex == 2) && (side2 > 0)) skip = false;
                        };
                    };
                };

                if ((inside) && (!skip)) ids.Add(itm);
                if ((!inside) && (skip)) ids.Add(itm);
            };
            Up();
        }

        /// <summary>
        ///     Расчет расстояния от точки до линии
        /// </summary>
        /// <param name="pt">Искомая точка</param>
        /// <param name="lineStart">Нач точка линии</param>
        /// <param name="lineEnd">Кон точка линии</param>
        /// <param name="pointOnLine">точка на линии ближайшая к искомой</param>
        /// <param name="side">С какой стороны линии находится искомая точка (+ слева, - справа)</param>
        /// <returns>метры</returns>
        private static float DistanceFromPointToLine(PointF pt, PointF lineStart, PointF lineEnd, out PointF pointOnLine, out int side)
        {
            float dx = lineEnd.X - lineStart.X;
            float dy = lineEnd.Y - lineStart.Y;

            if ((dx == 0) && (dy == 0))
            {
                // line is a point
                // линия может быть с нулевой длиной после анализа TRA
                pointOnLine = lineStart;
                side = 0;
                //dx = pt.X - lineStart.X;
                //dy = pt.Y - lineStart.Y;                
                //return Math.Sqrt(dx * dx + dy * dy);
                return Utils.GetLengthMeters(pt.Y, pt.X, pointOnLine.Y, pointOnLine.X, false);
            };

            side = Math.Sign((lineEnd.X - lineStart.X) * (pt.Y - lineStart.Y) - (lineEnd.Y - lineStart.Y) * (pt.X - lineStart.X));

            // Calculate the t that minimizes the distance.
            float t = ((pt.X - lineStart.X) * dx + (pt.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);

            // See if this represents one of the segment's
            // end points or a point in the middle.
            if (t < 0)
            {
                pointOnLine = new PointF(lineStart.X, lineStart.Y);
                dx = pt.X - lineStart.X;
                dy = pt.Y - lineStart.Y;
            }
            else if (t > 1)
            {
                pointOnLine = new PointF(lineEnd.X, lineEnd.Y);
                dx = pt.X - lineEnd.X;
                dy = pt.Y - lineEnd.Y;
            }
            else
            {
                pointOnLine = new PointF(lineStart.X + t * dx, lineStart.Y + t * dy);
                dx = pt.X - pointOnLine.X;
                dy = pt.Y - pointOnLine.Y;
            };

            //return Math.Sqrt(dx * dx + dy * dy);
            return Utils.GetLengthMeters(pt.Y, pt.X, pointOnLine.Y, pointOnLine.X, false);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            dosort(false);
            button6.Enabled = true;
        }

        // APPLY
        private void button9_Click(object sender, EventArgs e)
        {
            if (this.parent != null)
            {
                Dictionary<int, List<int>> lps = new Dictionary<int, List<int>>();

                for (int i = 0; i < this.parent.objects.Items.Count; i++)
                    if (!ids.Contains(i))
                    {
                        int layer = int.Parse(this.parent.objects.Items[i].SubItems[1].Text);
                        int placm = int.Parse(this.parent.objects.Items[i].SubItems[2].Text);
                        
                        if (!lps.ContainsKey(layer))
                            lps.Add(layer, new List<int>(new int[] { placm }));
                        else
                            lps[layer].Add(placm);
                    };

                foreach (KeyValuePair<int, List<int>> lp in lps)
                {
                    lp.Value.Sort();
                    for (int i = lp.Value.Count - 1; i >= 0; i--)
                        delete_object(lp.Key, lp.Value[i]);
                };

                this.Enabled = false;
                try
                {
                    parent.DrawCheckedLayers();
                }
                catch { };
                this.Enabled = true;
                this.Focus();
                Reset();
            };
        }

        private void delete_object(int layer, int placemark)
        {
            XmlNode xf = xd.SelectNodes("kml/Document/Folder")[layer];
            XmlNodeList cnt = xf.SelectNodes("Placemark");
            XmlNode xp = cnt[placemark];
            xp.ParentNode.RemoveChild(xp);
        }

        private void button10_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = ".kml";
            ofd.Filter = "KML & ESRI Shape files (*.kml;*.shp)|*.kml;*.shp";
            if (ofd.ShowDialog() == DialogResult.OK)
                loadpoly(ofd.FileName);
            ofd.Dispose();
        }

        private void loadpoly(string filename)
        {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InstalledUICulture;
            System.Globalization.NumberFormatInfo ni = (System.Globalization.NumberFormatInfo)ci.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";

            textBox2.Text = filename;
            LoadedPoly.Clear();

            System.IO.FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            if (Path.GetExtension(filename).ToLower() == ".kml")
            {
                System.IO.StreamReader sr = new StreamReader(fs);
                {
                    string file = sr.ReadToEnd();
                    int si = file.IndexOf("<coordinates>");
                    int ei = file.IndexOf("</coordinates>");
                    string co = file.Substring(si + 13, ei - si - 13).Trim().Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
                    string[] arr = co.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    if ((arr != null) && (arr.Length > 0))
                        for (int i = 0; i < arr.Length; i++)
                        {
                            string[] xyz = arr[i].Split(new string[] { "," }, StringSplitOptions.None);
                            LoadedPoly.Add(new PointF(float.Parse(xyz[0], ni), float.Parse(xyz[1], ni)));
                        };
                };
                sr.Close();
            };
            if (Path.GetExtension(filename).ToLower() == ".shp")
            {
                fs.Position = 32;
                int tof = fs.ReadByte();
                if ((tof == 5))
                {
                    fs.Position = 104;
                    byte[] ba = new byte[4];
                    fs.Read(ba, 0, ba.Length);
                    if (BitConverter.IsLittleEndian) Array.Reverse(ba);
                    int len = BitConverter.ToInt32(ba, 0) * 2;
                    fs.Read(ba, 0, ba.Length);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                    tof = BitConverter.ToInt32(ba, 0);
                    if ((tof == 5))
                    {
                        fs.Position += 32;
                        fs.Read(ba, 0, ba.Length);
                        if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                        if (BitConverter.ToInt32(ba, 0) == 1)
                        {
                            fs.Read(ba, 0, ba.Length);
                            if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                            int pCo = BitConverter.ToInt32(ba, 0);
                            fs.Read(ba, 0, ba.Length);
                            if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                            if (BitConverter.ToInt32(ba, 0) == 0)
                            {
                                ba = new byte[8];
                                for (int i = 0; i < pCo; i++)
                                {
                                    PointF ap = new PointF();
                                    fs.Read(ba, 0, ba.Length);
                                    if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                                    ap.X = (float)BitConverter.ToDouble(ba, 0);
                                    fs.Read(ba, 0, ba.Length);
                                    if (!BitConverter.IsLittleEndian) Array.Reverse(ba);
                                    ap.Y = (float)BitConverter.ToDouble(ba, 0);
                                    LoadedPoly.Add(ap);
                                };
                            };
                        };
                    };
                };
            };
            fs.Close();

            label8.Text = "Polygon has " + LoadedPoly.Count.ToString() + " points";
            marksFilter.Enabled = LoadedPoly.Count > 1;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            dopoly(true);
            button6.Enabled = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            dopoly(false);
            button6.Enabled = true;
        }

        private void dopoly(bool inside)
        {
            if (LoadedPoly.Count < 2) return;
            PointF[] poly = LoadedPoly.ToArray();

            double rad = (double)inrad.Value;

            ids.Clear();
            for (int itm = 0; itm < this.parent.objects.Items.Count; itm++)
            {
                bool skip = true;

                XmlNode layer;
                XmlNode placemark = GetXMLNode(itm, out layer);
                XmlNode cn = placemark.SelectSingleNode("Point/coordinates");
                if (cn != null)
                {
                    string[] llz = cn.ChildNodes[0].Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    if (llz.Length > 2)
                    {
                        double x = double.Parse(llz[0], System.Globalization.CultureInfo.InvariantCulture);
                        double y = double.Parse(llz[1], System.Globalization.CultureInfo.InvariantCulture);

                        if (PointInPolygon(new PointF((float)x, (float)y), poly, 1E-09))
                            skip = false;
                    };
                };

                if ((inside) && (!skip)) ids.Add(itm);
                if ((!inside) && (skip)) ids.Add(itm);
            };
            Up();
        }

        private static bool PointInPolygon(PointF point, PointF[] polygon, double EPS)
        {
            int count, up;
            count = 0;
            for (int i = 0; i < polygon.Length - 1; i++)
            {
                up = CRS(point, polygon[i], polygon[i + 1], EPS);
                if (up >= 0)
                    count += up;
                else
                    break;
            };
            up = CRS(point, polygon[polygon.Length - 1], polygon[0], EPS);
            if (up >= 0)
                return Convert.ToBoolean((count + up) & 1);
            else
                return false;
        }

        private static int CRS(PointF P, PointF A1, PointF A2, double EPS)
        {
            double x;
            int res = 0;
            if (Math.Abs(A1.Y - A2.Y) < EPS)
            {
                if ((Math.Abs(P.Y - A1.Y) < EPS) && ((P.X - A1.X) * (P.X - A2.X) < 0.0)) res = -1;
                return res;
            };
            if ((A1.Y - P.Y) * (A2.Y - P.Y) > 0.0) return res;
            x = A2.X - (A2.Y - P.Y) / (A2.Y - A1.Y) * (A2.X - A1.X);
            if (Math.Abs(x - P.X) < EPS)
            {
                res = -1;
            }
            else
            {
                if (x < P.X)
                {
                    res = 1;
                    if ((Math.Abs(A1.Y - P.Y) < EPS) && (A1.Y < A2.Y)) res = 0;
                    else
                        if ((Math.Abs(A2.Y - P.Y) < EPS) && (A2.Y < A1.Y)) res = 0;
                };
            };
            return res;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://en.wikipedia.org/wiki/Regular_expression");
        }

        private void button11_Click(object sender, EventArgs e)
        {
            button9_Click(sender, e);
            Close();
        }

        private void Selection_Filter_Load(object sender, EventArgs e)
        {
            ApplyTo.SelectedIndex = 0;
        }

        private void button12_Click(object sender, EventArgs e)
        {
            contextMenuStrip1.Show(button12, new Point(0, 0));
        }

        private void defaultRegexForNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBox1.Text = @"^([_\w\s\.\,\-\№\#\:\/\\\!\?" + "\"" + @"'`\*\^\(\)\[\]\@\$\%\+]*)$";
            ApplyTo.SelectedIndex = 0;
        }

        private void descriptionWebSiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBox1.Text = @"website=([^\r\n]+)";
            ApplyTo.SelectedIndex = 1;
        }

        private void descriptionEmailToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBox1.Text = @"email=([^\r\n]+)";
            ApplyTo.SelectedIndex = 1;
        }

        private void descriptionPhoneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            textBox1.Text = @"phone=([^\r\n]+)";
            ApplyTo.SelectedIndex = 1;
        }
    }

    public class Utils
    {
        // Рассчет расстояния       
        #region LENGTH
        public static float GetLengthMeters(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            // use fastest
            float result = (float)GetLengthMetersD(StartLat, StartLong, EndLat, EndLong, radians);

            if (float.IsNaN(result))
            {
                result = (float)GetLengthMetersC(StartLat, StartLong, EndLat, EndLong, radians);
                if (float.IsNaN(result))
                {
                    result = (float)GetLengthMetersE(StartLat, StartLong, EndLat, EndLong, radians);
                    if (float.IsNaN(result))
                        result = 0;
                };
            };

            return result;
        }

        // Slower
        public static uint GetLengthMetersA(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            double D2R = Math.PI / 180;     // Преобразование градусов в радианы

            double a = 6378137.0000;     // WGS-84 Equatorial Radius (a)
            double f = 1 / 298.257223563;  // WGS-84 Flattening (f)
            double b = (1 - f) * a;      // WGS-84 Polar Radius
            double e2 = (2 - f) * f;      // WGS-84 Квадрат эксцентричности эллипсоида  // 1-(b/a)^2

            // Переменные, используемые для вычисления смещения и расстояния
            double fPhimean;                           // Средняя широта
            double fdLambda;                           // Разница между двумя значениями долготы
            double fdPhi;                           // Разница между двумя значениями широты
            double fAlpha;                           // Смещение
            double fRho;                           // Меридианский радиус кривизны
            double fNu;                           // Поперечный радиус кривизны
            double fR;                           // Радиус сферы Земли
            double fz;                           // Угловое расстояние от центра сфероида
            double fTemp;                           // Временная переменная, использующаяся в вычислениях

            // Вычисляем разницу между двумя долготами и широтами и получаем среднюю широту
            // предположительно что расстояние между точками << радиуса земли
            if (!radians)
            {
                fdLambda = (StartLong - EndLong) * D2R;
                fdPhi = (StartLat - EndLat) * D2R;
                fPhimean = ((StartLat + EndLat) / 2) * D2R;
            }
            else
            {
                fdLambda = StartLong - EndLong;
                fdPhi = StartLat - EndLat;
                fPhimean = (StartLat + EndLat) / 2;
            };

            // Вычисляем меридианные и поперечные радиусы кривизны средней широты
            fTemp = 1 - e2 * (sqr(Math.Sin(fPhimean)));
            fRho = (a * (1 - e2)) / Math.Pow(fTemp, 1.5);
            fNu = a / (Math.Sqrt(1 - e2 * (Math.Sin(fPhimean) * Math.Sin(fPhimean))));

            // Вычисляем угловое расстояние
            if (!radians)
            {
                fz = Math.Sqrt(sqr(Math.Sin(fdPhi / 2.0)) + Math.Cos(EndLat * D2R) * Math.Cos(StartLat * D2R) * sqr(Math.Sin(fdLambda / 2.0)));
            }
            else
            {
                fz = Math.Sqrt(sqr(Math.Sin(fdPhi / 2.0)) + Math.Cos(EndLat) * Math.Cos(StartLat) * sqr(Math.Sin(fdLambda / 2.0)));
            };
            fz = 2 * Math.Asin(fz);

            // Вычисляем смещение
            if (!radians)
            {
                fAlpha = Math.Cos(EndLat * D2R) * Math.Sin(fdLambda) * 1 / Math.Sin(fz);
            }
            else
            {
                fAlpha = Math.Cos(EndLat) * Math.Sin(fdLambda) * 1 / Math.Sin(fz);
            };
            fAlpha = Math.Asin(fAlpha);

            // Вычисляем радиус Земли
            fR = (fRho * fNu) / (fRho * sqr(Math.Sin(fAlpha)) + fNu * sqr(Math.Cos(fAlpha)));
            // Получаем расстояние
            return (uint)Math.Round(Math.Abs(fz * fR));
        }
        // Slowest
        public static uint GetLengthMetersB(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            double fPhimean, fdLambda, fdPhi, fAlpha, fRho, fNu, fR, fz, fTemp, Distance,
                D2R = Math.PI / 180,
                a = 6378137.0,
                e2 = 0.006739496742337;
            if (radians) D2R = 1;

            fdLambda = (StartLong - EndLong) * D2R;
            fdPhi = (StartLat - EndLat) * D2R;
            fPhimean = (StartLat + EndLat) / 2.0 * D2R;

            fTemp = 1 - e2 * Math.Pow(Math.Sin(fPhimean), 2);
            fRho = a * (1 - e2) / Math.Pow(fTemp, 1.5);
            fNu = a / Math.Sqrt(1 - e2 * Math.Sin(fPhimean) * Math.Sin(fPhimean));

            fz = 2 * Math.Asin(Math.Sqrt(Math.Pow(Math.Sin(fdPhi / 2.0), 2) +
              Math.Cos(EndLat * D2R) * Math.Cos(StartLat * D2R) * Math.Pow(Math.Sin(fdLambda / 2.0), 2)));
            fAlpha = Math.Asin(Math.Cos(EndLat * D2R) * Math.Sin(fdLambda) / Math.Sin(fz));
            fR = fRho * fNu / (fRho * Math.Pow(Math.Sin(fAlpha), 2) + fNu * Math.Pow(Math.Cos(fAlpha), 2));
            Distance = fz * fR;

            return (uint)Math.Round(Distance);
        }
        // Average
        public static uint GetLengthMetersC(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            double D2R = Math.PI / 180;
            if (radians) D2R = 1;
            double dDistance = Double.MinValue;
            double dLat1InRad = StartLat * D2R;
            double dLong1InRad = StartLong * D2R;
            double dLat2InRad = EndLat * D2R;
            double dLong2InRad = EndLong * D2R;

            double dLongitude = dLong2InRad - dLong1InRad;
            double dLatitude = dLat2InRad - dLat1InRad;

            // Intermediate result a.
            double a = Math.Pow(Math.Sin(dLatitude / 2.0), 2.0) +
                       Math.Cos(dLat1InRad) * Math.Cos(dLat2InRad) *
                       Math.Pow(Math.Sin(dLongitude / 2.0), 2.0);

            // Intermediate result c (great circle distance in Radians).
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            const double kEarthRadiusKms = 6378137.0000;
            dDistance = kEarthRadiusKms * c;

            return (uint)Math.Round(dDistance);
        }
        // Fastest
        public static double GetLengthMetersD(double sLat, double sLon, double eLat, double eLon, bool radians)
        {
            double EarthRadius = 6378137.0;

            double lon1 = radians ? sLon : DegToRad(sLon);
            double lon2 = radians ? eLon : DegToRad(eLon);
            double lat1 = radians ? sLat : DegToRad(sLat);
            double lat2 = radians ? eLat : DegToRad(eLat);

            return EarthRadius * (Math.Acos(Math.Sin(lat1) * Math.Sin(lat2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(lon1 - lon2)));
        }
        // Fastest
        public static double GetLengthMetersE(double sLat, double sLon, double eLat, double eLon, bool radians)
        {
            double EarthRadius = 6378137.0;

            double lon1 = radians ? sLon : DegToRad(sLon);
            double lon2 = radians ? eLon : DegToRad(eLon);
            double lat1 = radians ? sLat : DegToRad(sLat);
            double lat2 = radians ? eLat : DegToRad(eLat);

            /* This algorithm is called Sinnott's Formula */
            double dlon = (lon2) - (lon1);
            double dlat = (lat2) - (lat1);
            double a = Math.Pow(Math.Sin(dlat / 2), 2.0) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dlon / 2), 2.0);
            double c = 2 * Math.Asin(Math.Sqrt(a));
            return EarthRadius * c;
        }
        private static double sqr(double val)
        {
            return val * val;
        }
        public static double DegToRad(double deg)
        {
            return (deg / 180.0 * Math.PI);
        }
        #endregion LENGTH
    }    

}