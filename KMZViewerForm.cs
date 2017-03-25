using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Windows.Forms;
using System.Drawing.Imaging;

using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;


namespace KMZ_Viewer
{
    public partial class KMZViewerForm : Form
    {
        public NaviMapNet.MapLayer mapContent = null;
        public NaviMapNet.MapLayer mapSelect = null;
        public ToolTip mapTootTip = new ToolTip();
        private bool firstboot = true;

        public string[] args = null;
        private string fileName = "";
        private string fileExt = "";
        private XmlDocument xd = null;

        public KMZViewerForm(string[] args)
        {
            this.args = args;

            Init();

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            Text += " " + fvi.FileVersion + " by " + fvi.CompanyName;

            MapViewer.UserDefinedGetTileUrl += new NaviMapNet.NaviMapNetViewer.GetTilePathCall(UserDefinedGetTileUrl);
        }

        public static string GetTempPath()
        {
            return System.IO.Path.GetTempPath() + @"KMZViewer\";
        }

        private static void RecreateTemp()
        {
            string path = GetTempPath();
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(path+@"images\");
        }

        private void Init()
        {
            InitializeComponent();
            mapTootTip.ShowAlways = true;            

            mapSelect = new NaviMapNet.MapLayer("mapSelect");
            MapViewer.MapLayers.Add(mapSelect);
            mapContent = new NaviMapNet.MapLayer("mapContent");
            MapViewer.MapLayers.Add(mapContent);            

            MapViewer.NotFoundTileColor = Color.LightYellow;
            MapViewer.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_Mapnik;
            MapViewer.WebRequestTimeout = 10000;
            MapViewer.ZoomID = 10;
            MapViewer.OnMapUpdate = new NaviMapNet.NaviMapNetViewer.MapEvent(MapUpdate);
            //MapViewer.UserDefinedGetTileUrl = new NaviMapNet.NaviMapNetViewer.GetTilePathCall(this.GetTilePath);
            MapViewer.DrawMap = true;

            iStorages.Items.Add("No Map");

            iStorages.Items.Add("OSM Mapnik Render Tiles");
            iStorages.Items.Add("OSM OpenVkarte Render Tiles");
            iStorages.Items.Add("Wikimapia");

            iStorages.Items.Add("OpenTopoMaps");
            iStorages.Items.Add("Sputnik.ru");
            iStorages.Items.Add("RUMAP");
            iStorages.Items.Add("2GIS");
            iStorages.Items.Add("ArcGIS ESRI");

            iStorages.Items.Add("Nokia-Ovi");
            iStorages.Items.Add("OviMap");
            iStorages.Items.Add("OviMap Sputnik");
            iStorages.Items.Add("OviMap Relief");
            iStorages.Items.Add("OviMap Hybrid");

            iStorages.Items.Add("Kosmosnimki.ru ScanEx 1");
            iStorages.Items.Add("Kosmosnimki.ru ScanEx 2");
            iStorages.Items.Add("Kosmosnimki.ru IRS Sat");

            iStorages.Items.Add("Google Map");
            iStorages.Items.Add("Google Sat");

            iStorages.Items.Add("-- SAS Planet Cache --");
            iStorages.Items.Add("-- User-Defined Url --");

            iStorages.SelectedIndex = 1;
        }

        private void iStorages_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (iStorages.SelectedIndex == 0)
            {
                MapViewer.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.Custom_UserDefined;
                MapViewer.ImageSourceType = NaviMapNet.NaviMapNetViewer.ImageSourceTypes.tiles;
                MapViewer.ImageSourceUrl = "";
            };
            if (iStorages.SelectedIndex == 1)
                MapViewer.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_Mapnik;
            if (iStorages.SelectedIndex == 2)
                MapViewer.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_Openvkarte;
            if (iStorages.SelectedIndex == 3)
                MapViewer.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.OSM_Wikimapia;
            if (iStorages.SelectedIndex > 3)
            {
                MapViewer.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.Custom_UserDefined;
                MapViewer.ImageSourceType = NaviMapNet.NaviMapNetViewer.ImageSourceTypes.tiles;
                MapViewer.ImageSourceProjection = NaviMapNet.NaviMapNetViewer.ImageSourceProjections.EPSG3857;
            };
            if (iStorages.SelectedIndex == 4)
                MapViewer.ImageSourceUrl = "http://a.tile.opentopomap.org/{z}/{x}/{y}.png";
            if (iStorages.SelectedIndex == 5)
                MapViewer.ImageSourceUrl = "http://tiles.maps.sputnik.ru/{z}/{x}/{y}.png";
            if (iStorages.SelectedIndex == 6)
                MapViewer.ImageSourceUrl = "http://tile.digimap.ru/rumap/{z}/{x}/{y}.png";
            if (iStorages.SelectedIndex == 7)
                MapViewer.ImageSourceUrl = "https://tile1.maps.2gis.com/tiles?x={x}&y={y}&z={z}&v=1.1";
            if (iStorages.SelectedIndex == 8)
                MapViewer.ImageSourceUrl = "http://services.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}.png";
            if (iStorages.SelectedIndex == 9)
                MapViewer.ImageSourceUrl = "http://maptile.mapplayer1.maps.svc.ovi.com/maptiler/maptile/newest/normal.day/{z}/{x}/{y}/256/png8";
            if (iStorages.SelectedIndex == 10)
                MapViewer.ImageSourceUrl = "http://1.maptile.lbs.ovi.com/maptiler/v2/maptile/newest/normal.day/{z}/{x}/{y}/256/png8?lg=RUS&token=fee2f2a877fd4a429f17207a57658582&appId=nokiaMaps";
            if (iStorages.SelectedIndex == 11)
                MapViewer.ImageSourceUrl = "http://1.maptile.lbs.ovi.com/maptiler/v2/maptile/newest/satellite.day/{z}/{x}/{y}/256/png8?lg=RUS&token=fee2f2a877fd4a429f17207a57658582&appId=nokiaMaps";
            if (iStorages.SelectedIndex == 12)
                MapViewer.ImageSourceUrl = "http://1.maptile.lbs.ovi.com/maptiler/v2/maptile/newest/hybrid.day/{z}/{x}/{y}/256/png8?lg=RUS&token=fee2f2a877fd4a429f17207a57658582&appId=nokiaMaps";
            if (iStorages.SelectedIndex == 13)
                MapViewer.ImageSourceUrl = "http://1.maptile.lbs.ovi.com/maptiler/v2/maptile/newest/terrain.day/{z}/{x}/{y}/256/png8?lg=RUS&token=fee2f2a877fd4a429f17207a57658582&appId=nokiaMaps";
            if (iStorages.SelectedIndex == 14)
                MapViewer.ImageSourceUrl = "http://maps.kosmosnimki.ru/TileService.ashx?Request=gettile&LayerName=04C9E7CE82C34172910ACDBF8F1DF49A&apikey=7BDJ6RRTHH&crs=epsg:3857&z={z}&x={x}&y={y}";
            if (iStorages.SelectedIndex == 15)
                MapViewer.ImageSourceUrl = "http://maps.kosmosnimki.ru/TileService.ashx?Request=gettile&LayerName=04C9E7CE82C34172910ACDBF8F1DF49A&apikey=7BDJ6RRTHH&crs=epsg:3857&z={z}&x={x}&y={y}";
            if (iStorages.SelectedIndex == 16)
                MapViewer.ImageSourceUrl = "http://irs.gis-lab.info/?layers=irs&request=GetTile&z={z}&x={x}&y={y}";
            if (iStorages.SelectedIndex == 17)
                MapViewer.ImageSourceUrl = "http://mts0.google.com/vt/lyrs=m@177000000&hl=ru&src=app&x={x}&s=&y={y}&z={z}&s=Ga";
            if (iStorages.SelectedIndex == 18)
                MapViewer.ImageSourceUrl = "http://mts0.google.com/vt/lyrs=h@177000000&hl=ru&src=app&x={x}&s=&y={y}&z={z}&s=G";
            if (iStorages.SelectedIndex == 20)
                MapViewer.ImageSourceUrl = udu.Text;
            
            MapViewer.ReloadMap();
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            MapViewer.DrawTilesBorder = !MapViewer.DrawTilesBorder;
            toolStripMenuItem4.Checked = MapViewer.DrawTilesBorder;
            MapViewer.ReloadMap();
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            MapViewer.DrawTilesXYZ = !MapViewer.DrawTilesXYZ;
            toolStripMenuItem5.Checked = MapViewer.DrawTilesXYZ;
            MapViewer.ReloadMap();
        }

        private void MapUpdate()
        {
            toolStripStatusLabel1.Text = "Last Requested File: " + MapViewer.LastRequestedFile;
            toolStripStatusLabel2.Text = MapViewer.CenterDegreesLat.ToString().Replace(",", ".");
            toolStripStatusLabel3.Text = MapViewer.CenterDegreesLon.ToString().Replace(",", ".");
        }

        private Timer mmTimer = null;
        private void MapViewer_MouseMove(object sender, MouseEventArgs e)
        {
            locate = false;
            if (e.Button != MouseButtons.None) mapTootTip.Hide(this);

            if (mmTimer != null)
                mmTimer.Enabled = false;
            else
            {
                mmTimer = new Timer();
                mmTimer.Interval = 800;
                mmTimer.Tick += new EventHandler(mmTimer_Tick);
            };
            mmTimer.Start();
            
            PointF m = MapViewer.MousePositionDegrees;
            toolStripStatusLabel4.Text = m.Y.ToString().Replace(",", ".");
            toolStripStatusLabel5.Text = m.X.ToString().Replace(",", ".");
        }

        private void mmTimer_Tick(object sender, EventArgs e)
        {
            mmTimer.Enabled = false;
            if (mapContent.ObjectsCount == 0) return;

            try
            {
                Point f = this.PointToScreen(new Point(0, 0));
                Point p = Cursor.Position;
                Point s = new Point(p.X - f.X, p.Y - f.Y);

                Point current = MapViewer.MousePositionPixels;
                PointF sCenter = MapViewer.PixelsToDegrees(current);
                PointF sFrom = MapViewer.PixelsToDegrees(new Point(current.X - 5, current.Y + 5));
                PointF sTo = MapViewer.PixelsToDegrees(new Point(current.X + 5, current.Y - 5));
                NaviMapNet.MapObject[] objs = mapContent.Select(new RectangleF(sFrom, new SizeF(sTo.X - sFrom.X, sTo.Y - sTo.X)));
                if ((objs != null) && (objs.Length > 0))
                {
                    uint len = uint.MaxValue;
                    int ind = 0;
                    for (int i = 0; i < objs.Length; i++)
                    {
                        uint tl = GetLengthMetersC(sCenter.Y, sCenter.X, objs[i].Center.Y, objs[i].Center.X, false);
                        if (tl < len) { len = tl; ind = i; };
                    };

                    mapTootTip.Show(objs[ind].Name, this, s.X, s.Y, 5000);
                }
                else
                    mapTootTip.Hide(this);
            }
            catch { };
        }

        private void objects_DoubleClick(object sender, EventArgs e)
        {
            if (objects.SelectedItems.Count == 0) return;
            
            NaviMapNet.MapObject mo = mapContent[objects.SelectedIndices[0]];            
            MapViewer.CenterDegrees = mo.Center;
            SelectOnMap(objects.SelectedIndices[0]);
        }

        private void MapViewer_MouseClick(object sender, MouseEventArgs e)
        {
            if (!locate) return;                     
            if (mapContent.ObjectsCount == 0) return;
            Point clicked = MapViewer.MousePositionPixels;
            PointF sCenter = MapViewer.PixelsToDegrees(clicked);
            PointF sFrom = MapViewer.PixelsToDegrees(new Point(clicked.X - 5, clicked.Y + 5));
            PointF sTo = MapViewer.PixelsToDegrees(new Point(clicked.X + 5, clicked.Y - 5));
            NaviMapNet.MapObject[] objs = mapContent.Select(new RectangleF(sFrom, new SizeF(sTo.X - sFrom.X, sTo.Y - sFrom.Y)));
            if ((objs != null) && (objs.Length > 0))
            {
                uint len = uint.MaxValue;
                int ind = 0;
                for (int i = 0; i < objs.Length; i++)
                {
                    uint tl = GetLengthMetersC(sCenter.Y, sCenter.X, objs[i].Center.Y, objs[i].Center.X, false);
                    if (tl < len) { len = tl; ind = i; };
                };

                if((objects.SelectedIndices.Count == 0) || (objects.SelectedIndices[0] != objs[ind].Index))
                {                    
                    objects.Items[objs[ind].Index].Selected = true;
                    objects.Items[objs[ind].Index].Focused = true;
                };

                SelectOnMap(objs[ind].Index);
            };
        }

        private static uint GetLengthMetersC(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
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

        private Color prevSIC = Color.White;
        private ListViewItem prevSII = null;
        private void objects_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (prevSII != null)
            {
                prevSII.BackColor = prevSIC;
                prevSII = null;
            };
            if (mapSelect.ObjectsCount > 0)
            {
                mapSelect.Clear();
                MapViewer.DrawOnMapData();
            };
            if (objects.SelectedItems.Count == 0) return;

            objects.EnsureVisible(objects.SelectedIndices[0]);
            prevSII = objects.SelectedItems[0];
            prevSIC = objects.SelectedItems[0].BackColor;
            objects.SelectedItems[0].BackColor = Color.Red;

            NaviMapNet.MapObject mo = mapContent[objects.SelectedIndices[0]];

            tName.Text = objects.SelectedItems[0].SubItems[0].Text;
            tLat.Text = objects.SelectedItems[0].SubItems[4].Text;
            tLon.Text = objects.SelectedItems[0].SubItems[5].Text;
            tDesc.Text = objects.SelectedItems[0].SubItems[6].Text;
        }

        private void SelectOnMap(int id)
        {
            if (id < 0) return;

            mapSelect.Clear();
            if (mapContent[id].ObjectType == NaviMapNet.MapObjectType.mPoint)
            {
                NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(mapContent[id].Center);
                mp.Name = "Selected";
                mp.SizePixels = new Size(22, 22);
                mp.Squared = false;
                mp.Color = Color.Red;
                mapSelect.Add(mp);
                MapViewer.DrawOnMapData();
            };
        }

        private void objects_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != Convert.ToChar(Keys.Enter)) return;
            if (objects.SelectedIndices.Count == 0) return;            
            SelectOnMap(objects.SelectedIndices[0]);
        }

        private void îòêðûòüÔàéëToolStripMenuItem_Click(object sender, EventArgs e)
        {            
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select file";
            ofd.Filter = "KMZ/KML files (*.kml;*.kmz)|*.kml;*.kmz";
            if (ofd.ShowDialog() == DialogResult.OK)
                OpenFile(ofd.FileName);
            ofd.Dispose();
        }

        private void OpenFile(string filename)
        {
            this.fileName = filename;
            this.fileExt = Path.GetExtension(this.fileName).ToLower();
            RecreateTemp();
            if (this.fileExt == ".kml")
                File.Copy(filename, GetTempPath() + "doc.kml");
            else
                UnZipKMZ(filename, GetTempPath());

            OpenKML();
            LoadLayers();
            DrawCheckedLayers();
        }

        private void LoadLayers()
        {            
            // get doc name
            string kmldocName = "";
            string kmldocDesc = "";
            {
                XmlNode xn;
                xn = xd.SelectSingleNode("kml/Document/name");
                try { if (xn != null) kmldocName = xn.ChildNodes[0].Value; }
                catch { };
                xn = xd.SelectSingleNode("kml/Document/description");
                try { if (xn != null) kmldocDesc = xn.ChildNodes[0].Value; }
                catch { };
            };

            // get layers
            layers.Items.Clear();
            objects.Items.Clear();
            mapContent.Clear();
            images.Images.Clear();            
            {
                foreach (XmlNode xn in xd.SelectNodes("kml/Document/Folder"))
                {
                    XmlNode xn2 = xn.SelectSingleNode("name");
                    string lname = "";
                    try { if (xn2 != null) lname = xn2.ChildNodes[0].Value; }
                    catch { };
                    XmlNodeList nl2 = xn.SelectNodes("Placemark");
                    int p = (nl2 == null ? 0 : nl2.Count);
                    layers.Items.Add(String.Format("{1:00}: {0}",lname, layers.Items.Count));
                };
            };            
        }

        private void OpenKML()
        {
            FileStream fs = new FileStream(GetTempPath() + "doc.kml", FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            string xml = sr.ReadToEnd();
            sr.Close();
            fs.Close();

            xml = RemoveXMLNamespaces(xml);
            this.xd = new XmlDocument();
            this.xd.LoadXml(xml);

            XmlDocument kmlDoc = this.xd;

            // Âûòÿãèâàåì âñå âëîæåíûå ïàïêè (Folder) â äîêóìåíò (kml/Document)
            {
                XmlNode doc = kmlDoc.SelectSingleNode("kml/Document");
                XmlNode lp = null;
                XmlNode ln = null;
                XmlNode n;
                while ((n = kmlDoc.SelectSingleNode("kml/Document/Folder/Folder")) != null)
                {
                    XmlNode p = n.ParentNode;
                    p.RemoveChild(n);

                    if (lp != p)
                        doc.InsertAfter(n, p);
                    else
                        doc.InsertAfter(n, ln);

                    lp = p;
                    ln = n;
                };
            };

            // if no folders in kml
            {
                XmlNode doc = kmlDoc.SelectSingleNode("kml/Document/Folder");
                if (doc == null)
                {
                    doc = kmlDoc.SelectSingleNode("kml/Document");
                    string txt = doc.InnerXml;
                    XmlNode ns = kmlDoc.CreateElement("Folder");
                    ns.InnerXml = txt;
                    doc.AppendChild(ns);
                };
            };

            //âûòÿãèâàåì âñå îáúåêòû áåç ïàïêè â ïàïêó
            {
                XmlNode doc = kmlDoc.SelectSingleNode("kml/Document");
                XmlNodeList nl = kmlDoc.SelectNodes("kml/Document/Placemark");
                if (nl.Count > 0)
                {
                    XmlNode ns = kmlDoc.CreateElement("Folder");
                    XmlNode nn = kmlDoc.CreateElement("name");
                    nn.AppendChild(kmlDoc.CreateTextNode("No in folder [" + nl.Count.ToString() + "]"));
                    ns.AppendChild(nn);
                    doc.AppendChild(ns);
                    foreach (XmlNode n in nl)
                    {
                        doc.RemoveChild(n);
                        ns.AppendChild(n);
                    };
                };
            }

            // move "Placemark/MultiGeometry/LineString" -> "Placemark/LineString"
            {
                foreach (XmlNode xn in kmlDoc.SelectNodes("kml/Document/Folder/Placemark/MultiGeometry/LineString"))
                {
                    XmlNode xnp = xn.ParentNode.ParentNode;
                    XmlNode xnm = xn.ParentNode;
                    xnm.RemoveChild(xn);
                    xnp.RemoveChild(xnm);
                    xnp.AppendChild(xn);
                };
            };

            // move styles from kml/Document/Folder to kml/Document/styleUrl
            // move styleUrls from kml/Document/Folder/styleUrl to kml/Document/styleUrl
            {
                //styles
                string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                Random random = (new Random());
                string pref = new String(new char[] { chars[random.Next(chars.Length)], chars[random.Next(chars.Length)], chars[random.Next(chars.Length)] });

                XmlNodeList nl;
                List<string> sl = new List<string>();
                nl = kmlDoc.SelectNodes("kml/Document/Folder/Placemark/Style");
                foreach (XmlNode n in nl)
                {
                    string ort = n.InnerXml;
                    int index = sl.IndexOf(ort);
                    if (index < 0)
                    {
                        sl.Add(ort);
                        index = sl.Count - 1;
                        XmlNode ns = kmlDoc.CreateElement("Style");
                        ns.Attributes.Append(kmlDoc.CreateAttribute("id"));
                        ns.Attributes["id"].Value = "style" + pref + index.ToString();
                        ns.InnerXml = ort;
                        n.ParentNode.ParentNode.ParentNode.AppendChild(ns);
                    };
                    XmlNode n2 = kmlDoc.CreateElement("styleUrl");
                    n2.AppendChild(kmlDoc.CreateTextNode("#style" + pref + index.ToString()));
                    n.ParentNode.ReplaceChild(n2, n);
                };

                //Style
                sl.Clear();
                nl = kmlDoc.SelectNodes("kml/Document/Folder/Style");
                foreach (XmlNode n in nl)
                {
                    XmlNode p = n.ParentNode.ParentNode;
                    n.ParentNode.RemoveChild(n);
                    string ort = n.OuterXml;
                    if (sl.IndexOf(ort) < 0)
                    {
                        sl.Add(ort);
                        p.AppendChild(n);
                    };
                };


                //styleMaps
                sl.Clear();
                nl = kmlDoc.SelectNodes("kml/Document/Folder/StyleMap");
                foreach (XmlNode n in nl)
                {
                    XmlNode p = n.ParentNode.ParentNode;
                    n.ParentNode.RemoveChild(n);
                    string ort = n.OuterXml;
                    if (sl.IndexOf(ort) < 0)
                    {
                        sl.Add(ort);
                        p.AppendChild(n);
                    };
                };
            };

            // remove not used styles
            {
                XmlNodeList sex = kmlDoc.SelectNodes("kml/Document/Folder/Placemark/styleUrl");
                XmlNodeList sml = kmlDoc.SelectNodes("kml/Document/StyleMap");
                XmlNodeList ssl = kmlDoc.SelectNodes("kml/Document/Style");

                List<string> sl = new List<string>();
                foreach (XmlNode n in sml)
                {
                    string sn = "#" + n.Attributes["id"].Value;
                    bool ex = false;
                    foreach (XmlNode scu in sex)
                        if (scu.ChildNodes[0].Value == sn)
                            ex = true;
                    if (!ex)
                    {
                        n.ParentNode.RemoveChild(n);
                        foreach (XmlNode xn2 in n.SelectNodes("Pair/styleUrl"))
                        {
                            string su = xn2.ChildNodes[0].Value;
                            if (su.IndexOf("#") == 0) su = su.Remove(0, 1);
                            sl.Add(su);
                        };
                    };
                };
                foreach (XmlNode n in ssl)
                {
                    if (n.Attributes["id"] == null)
                        n.ParentNode.RemoveChild(n);
                    else
                    {
                        string sn = n.Attributes["id"].Value;
                        bool ex = false;
                        foreach (XmlNode scu in sml)
                            if (n.Attributes["id"].Value == sn)
                                ex = true;
                        string snh = "#" + sn;
                        if (!ex)
                            foreach (XmlNode scu in sex)
                                if (scu.ChildNodes[0].Value == snh)
                                    ex = true;
                        if (!ex) 
                            sl.Add(sn);
                    };
                };
                if (sl.Count > 0)
                    foreach (string ss in sl)
                    {
                        XmlNode n = kmlDoc.SelectSingleNode("kml/Document/Style[@id='" + ss + "']");
                        n.ParentNode.RemoveChild(n);
                    };
            };

            // get no icons and no name placemarks
            int noicons = 0;
            {
                foreach (XmlNode pm in kmlDoc.SelectNodes("kml/Document/Folder/Placemark"))
                {
                    if (pm.SelectSingleNode("name") == null)
                    {
                        XmlNode nn = kmlDoc.CreateElement("name");
                        nn.AppendChild(kmlDoc.CreateTextNode("NoName"));
                        pm.AppendChild(nn);
                    }
                    else if (pm.SelectSingleNode("name").ChildNodes.Count == 0)
                    {
                        XmlNode nn = pm.SelectSingleNode("name");
                        nn.AppendChild(kmlDoc.CreateTextNode("NoName"));
                        pm.AppendChild(nn);
                    };

                    if (pm.SelectSingleNode("Point") == null) continue;

                    XmlNode su = pm.SelectSingleNode("styleUrl");
                    if (su != null)
                    {
                        string sut = su.ChildNodes[0].Value.ToLower();
                        if (sut.IndexOf("root:") == 0)
                        {
                            su.ParentNode.RemoveChild(su);
                            su = null;
                        };
                    };

                    if ((pm.SelectSingleNode("Style") == null) && (su == null))
                    {
                        su = kmlDoc.CreateElement("styleUrl");
                        su.AppendChild(kmlDoc.CreateTextNode("#noicon"));
                        XmlNode ncn = pm.SelectSingleNode("descrption");
                        if (ncn == null) ncn = pm.SelectSingleNode("name");
                        pm.InsertAfter(su, ncn);
                        noicons++;
                    };
                };

                if ((noicons > 0) && (kmlDoc.SelectSingleNode("kml/Document/Style[@id='noicon']") == null))
                {
                    XmlNode su = kmlDoc.CreateElement("Style");
                    su.Attributes.Append(kmlDoc.CreateAttribute("id"));
                    su.Attributes["id"].Value = "noicon";
                    su.InnerXml = "<IconStyle><Icon><href>images/noicon.png</href></Icon></IconStyle>";
                    kmlDoc.SelectSingleNode("kml/Document").AppendChild(su);
                };
            };

            // delete multi names, move style before coord
            {
                XmlNodeList nl = kmlDoc.SelectNodes("kml/Document/Folder");
                foreach (XmlNode n in nl)
                {
                    XmlNodeList subl = n.SelectNodes("name");
                    if (subl.Count > 1)
                        for (int i = subl.Count - 1; i > 0; i--)
                            n.RemoveChild(subl[i]);

                    subl = n.SelectNodes("Placemark");
                    foreach (XmlNode nn in subl)
                    {
                        XmlNodeList ssl = nn.SelectNodes("name");
                        if (ssl.Count > 1)
                            for (int i = ssl.Count - 1; i > 0; i--)
                                nn.RemoveChild(ssl[i]);

                        XmlNode scn = nn.SelectSingleNode("styleUrl");
                        XmlNode ncn = nn.SelectSingleNode("descrption");
                        if (ncn == null) ncn = nn.SelectSingleNode("name");
                        if (ncn == null) ncn = nn.ChildNodes[0];
                        if (scn != null)
                        {
                            nn.RemoveChild(scn);
                            nn.InsertAfter(scn, ncn);
                        };
                    };
                };
            };

            // set empty icons
            {
                foreach (XmlNode xn in xd.SelectNodes("kml/Document/Style/IconStyle/Icon/href"))
                {
                    string href = xn.ChildNodes[0].Value;
                    if (!Uri.IsWellFormedUriString(href, UriKind.Absolute))
                        if (!File.Exists(GetTempPath() + href))
                        {
                            href = href.Replace("/", @"\");
                            if (href.LastIndexOf(@"\") >= 0)
                                href = @"images\" + href.Substring(href.LastIndexOf(@"\"));
                            else
                                href = @"images\" + href;
                            while (href.IndexOf(@"\\") >= 0) href = href.Replace(@"\\", @"\");
                            xn.ChildNodes[0].Value = href;
                            File.Copy(CurrentDirectory() + @"noi.png", GetTempPath() + href);
                        };
                }
            };
        }

        public static void UnZipKMZ(string archiveFilenameIn, string outFolder)
        {
            ZipFile zf = null;
            try
            {
                FileStream fs = File.OpenRead(archiveFilenameIn);
                zf = new ZipFile(fs);
                foreach (ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile) continue; // Ignore directories
                    String entryFileName = zipEntry.Name;
                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    byte[] buffer = new byte[4096];     // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    // Manipulate the output filename here as desired.
                    String fullZipToPath = Path.Combine(outFolder, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (FileStream streamWriter = File.Create(fullZipToPath))
                    {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    };
                };
            }
            finally
            {
                if (zf != null)
                {
                    zf.IsStreamOwner = true; // Makes close also shut the underlying stream
                    zf.Close(); // Ensure we release resources
                }
            }
        }

        public static string RemoveXMLNamespaces(string xml)
        {
            string outerXml = xml;
            { // "
                string xmlnsPattern = "\\s+xmlns\\s*(:\\w)?\\s*=\\s*\\\"(?<url>[^\\\"]*)\\\"";
                MatchCollection matchCol = Regex.Matches(outerXml, xmlnsPattern);
                foreach (Match match in matchCol)
                    outerXml = outerXml.Replace(match.ToString(), "");
            };
            {// '
                string xmlnsPattern = "\\s+xmlns\\s*(:\\w)?\\s*=\\s*\\\'(?<url>[^\\\']*)\\\'";
                MatchCollection matchCol = Regex.Matches(outerXml, xmlnsPattern);
                foreach (Match match in matchCol)
                    outerXml = outerXml.Replace(match.ToString(), "");
            };
            return outerXml;
        }

        public static string CurrentDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
            // return Application.StartupPath;
            // return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // return System.IO.Directory.GetCurrentDirectory();
            // return Environment.CurrentDirectory;
            // return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            // return System.IO.Path.GetDirectory(Application.ExecutablePath);
        }

        public void DrawCheckedLayers()
        {
            System.Globalization.CultureInfo gi = System.Globalization.CultureInfo.InstalledUICulture;
            System.Globalization.NumberFormatInfo ni = (System.Globalization.NumberFormatInfo)gi.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";
            //
            objects.Items.Clear();
            mapContent.Clear();
            images.Images.Clear();
            //
            Hashtable imList = new Hashtable();
            toolStripStatusLabel1.Text = String.Format("Loading",0);
            int ttld = 0;
            int ttlc = 0;
            //
            if(layers.Items.Count > 0)
            {
                for (int ci = 0; ci < layers.Items.Count; ci++)
                    if (layers.GetItemChecked(ci))
                    {
                        XmlNode xn = xd.SelectNodes("kml/Document/Folder")[ci];
                        XmlNodeList xns = xn.SelectNodes("Placemark");
                        ttlc += xns.Count;
                    };

                for (int ci = 0; ci < layers.Items.Count; ci++)
                {
                    if (layers.GetItemChecked(ci))
                    {
                        XmlNode xn = xd.SelectNodes("kml/Document/Folder")[ci];
                        XmlNodeList xns = xn.SelectNodes("Placemark");
                        if (xns.Count > 0)
                        {
                            for (int i = 0; i < xns.Count; i++)
                            {
                                toolStripStatusLabel1.Text = String.Format("Wait, creating {0} of {1} placemark", ++ttld, ttlc);
                                if((i % 35) == 0)statusStrip2.Update();

                                string name = "";
                                try { name = xns[i].SelectSingleNode("name").ChildNodes[0].Value; }
                                catch { };
                                string description = "";
                                try { description = xns[i].SelectSingleNode("description").ChildNodes[0].Value; }
                                catch { };

                                //LINE
                                XmlNode cn = xns[i].SelectSingleNode("LineString/coordinates");
                                if (cn != null)
                                {
                                    string[] llza = cn.ChildNodes[0].Value.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                                    string styleUrl = "";
                                    if (xns[i].SelectSingleNode("styleUrl") != null) styleUrl = xns[i].SelectSingleNode("styleUrl").ChildNodes[0].Value;
                                    if (styleUrl.IndexOf("#") == 0) styleUrl = styleUrl.Remove(0, 1);

                                    Color lineColor = Color.FromArgb(255, Color.Blue);
                                    int lineWidth = 3;

                                    XmlNode sn = null;
                                    if (styleUrl != "")
                                    {
                                        string firstsid = styleUrl;
                                        XmlNodeList pk = xd.SelectNodes("kml/Document/StyleMap[@id='" + styleUrl + "']/Pair/key");
                                        if (pk.Count > 0)
                                            for (int n = 0; n < pk.Count; n++)
                                            {
                                                XmlNode cns = pk[n];
                                                if ((cns.ChildNodes[0].Value != "normal") && (n > 0)) continue;
                                                if (cns.ParentNode.SelectSingleNode("styleUrl") == null) continue;
                                                firstsid = cns.ParentNode.SelectSingleNode("styleUrl").ChildNodes[0].Value;
                                                if (firstsid.IndexOf("#") == 0) firstsid = firstsid.Remove(0, 1);
                                            };
                                        try
                                        {
                                            sn = xd.SelectSingleNode("kml/Document/Style[@id='" + firstsid + "']/LineStyle");
                                        }
                                        catch { };
                                    }
                                    else
                                        sn = xns[i].SelectSingleNode("Style/LineStyle");
                                    if (sn != null)
                                    {
                                        string colval = sn.SelectSingleNode("color").ChildNodes[0].Value;
                                        try
                                        {
                                            lineColor = Color.FromName(colval);
                                            if (colval.Length == 8)
                                            {
                                                lineColor = Color.FromArgb(
                                                    Convert.ToInt32(colval.Substring(0, 2), 16),
                                                    Convert.ToInt32(colval.Substring(2, 2), 16),
                                                    Convert.ToInt32(colval.Substring(4, 2), 16),
                                                    Convert.ToInt32(colval.Substring(6, 2), 16)
                                                    );
                                            };
                                        }
                                        catch { };
                                        string widval = sn.SelectSingleNode("width").ChildNodes[0].Value;
                                        try
                                        {
                                            lineWidth = (int)double.Parse(widval, ni);
                                            if (lineWidth < 3) lineWidth = 3;
                                        }
                                        catch { };
                                    };

                                    List<PointF> xy = new List<PointF>();
                                    foreach (string llzi in llza)
                                    {
                                        string[] llz = llzi.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                                        xy.Add(new PointF(float.Parse(llz[0], ni), float.Parse(llz[1], ni)));
                                    };

                                    NaviMapNet.MapPolyLine ml = new NaviMapNet.MapPolyLine(xy.ToArray());
                                    ml.Name = name;
                                    ml.UserData = new object[] { ci, i, name, description };
                                    ml.Color = lineColor;
                                    ml.Width = lineWidth;

                                    mapContent.Add(ml);
                                    ListViewItem lvi = objects.Items.Add(ml.Name, -1);
                                    lvi.SubItems.Add(String.Format("{0:00}", ci));
                                    lvi.SubItems.Add(String.Format("{0:00}", i));
                                    lvi.SubItems.Add("Line");                                    
                                    lvi.SubItems.Add("-");
                                    lvi.SubItems.Add("-");
                                    lvi.SubItems.Add(description);                                    
                                };
                                // POINT
                                cn = xns[i].SelectSingleNode("Point/coordinates");
                                if (cn != null)
                                {
                                    string[] llz = cn.ChildNodes[0].Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                                    string styleUrl = "";
                                    string href = "";
                                    if (xns[i].SelectSingleNode("styleUrl") != null) styleUrl = xns[i].SelectSingleNode("styleUrl").ChildNodes[0].Value;
                                    if (styleUrl.IndexOf("#") == 0) styleUrl = styleUrl.Remove(0, 1);

                                    if (styleUrl != "")
                                    {
                                        string firstsid = styleUrl;
                                        XmlNodeList pk = xd.SelectNodes("kml/Document/StyleMap[@id='" + styleUrl + "']/Pair/key");
                                        if (pk.Count > 0)
                                            for (int n = 0; n < pk.Count; n++)
                                            {
                                                XmlNode cns = pk[n];
                                                if ((cns.ChildNodes[0].Value != "normal") && (n > 0)) continue;
                                                if (cns.ParentNode.SelectSingleNode("styleUrl") == null) continue;
                                                firstsid = cns.ParentNode.SelectSingleNode("styleUrl").ChildNodes[0].Value;
                                                if (firstsid.IndexOf("#") == 0) firstsid = firstsid.Remove(0, 1);
                                            };
                                        try
                                        {
                                            XmlNode nts = xd.SelectSingleNode("kml/Document/Style[@id='" + firstsid + "']/IconStyle/Icon/href");
                                            href = nts.ChildNodes[0].Value;
                                        }
                                        catch { };
                                    };

                                    NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(double.Parse(llz[1], ni), double.Parse(llz[0], ni));
                                    mp.Name = name;
                                    mp.UserData = new object[] { ci, i, name, description };
                                    mp.SizePixels = new Size(16, 16);
                                    int ii = -1;
                                    if (imList.ContainsKey(href))
                                        ii = (int)imList[href];
                                    else
                                    {
                                        if (href == "")
                                            imList.Add(href, -1);
                                        else
                                        {
                                            Image im = null;
                                            if (Uri.IsWellFormedUriString(href, UriKind.Absolute))
                                            {
                                                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(href);

                                                try
                                                {
                                                    using (System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse())
                                                    using (Stream stream = response.GetResponseStream())
                                                        im = Bitmap.FromStream(stream);
                                                }
                                                catch
                                                { im = null; };
                                            }
                                            else
                                            {
                                                try { im = Image.FromFile(GetTempPath() + href); }
                                                catch { im = null; };
                                            };

                                            if (im != null)
                                            {
                                                images.Images.Add(href, (Image)new Bitmap(im));
                                                im.Dispose();
                                                imList.Add(href, ii = images.Images.Count - 1);
                                            }
                                            else
                                                imList.Add(href, ii = -1);
                                        };
                                    };
                                    if (ii >= 0)
                                    {
                                        mp.Color = Color.Transparent;
                                        mp.Squared = true;
                                        mp.Img = images.Images[ii];
                                    }
                                    else
                                    {
                                        mp.Color = Color.Purple;
                                        mp.Squared = false;
                                    };

                                    mapContent.Add(mp);
                                    ListViewItem lvi = objects.Items.Add(String.Format("{0}", mp.Name, mp.Center.Y.ToString().Replace(",", "."), mp.Center.X.ToString().Replace(",", ".")), ii);
                                    lvi.SubItems.Add(String.Format("{0:00}", ci));
                                    lvi.SubItems.Add(String.Format("{0:00}", i));
                                    lvi.SubItems.Add("Point");
                                    lvi.SubItems.Add(mp.Center.Y.ToString(ni));
                                    lvi.SubItems.Add(mp.Center.X.ToString(ni));
                                    lvi.SubItems.Add(description);
                                };
                            };
                        };
                    };
                };
            };
            //
            toolStripStatusLabel1.Text = String.Format("{0} placemarks created", ttld);
            MapViewer.DrawOnMapData();            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DrawCheckedLayers();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (layers.Items.Count == 0) return;
            for (int ci = 0; ci < layers.Items.Count; ci++)
                layers.SetItemChecked(ci,true);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (layers.Items.Count == 0) return;
            for (int ci = 0; ci < layers.Items.Count; ci++)
                layers.SetItemChecked(ci,false);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (layers.Items.Count == 0) return;
            for (int ci = 0; ci < layers.Items.Count; ci++)
                layers.SetItemChecked(ci,!layers.GetItemChecked(ci));
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            hideOnMapToolStripMenuItem.Enabled =
                editPlacemarkXMLToolStripMenuItem.Enabled = 
                    editPlacemarkXMLToolStripMenuItem1.Enabled =
                        deletePlacemarkToolStripMenuItem.Enabled = 
                            objects.SelectedIndices.Count > 0;

            hideOnMapToolStripMenuItem.Checked = !mapContent[objects.SelectedIndices[0]].Visible;
        }

        private void editPlacemarkXMLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.SelectedIndices.Count == 0) return; 

            System.Globalization.CultureInfo gi = System.Globalization.CultureInfo.InstalledUICulture;
            System.Globalization.NumberFormatInfo ni = (System.Globalization.NumberFormatInfo)gi.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";
                       
            string name = objects.SelectedItems[0].SubItems[0].Text;
            int l = int.Parse(objects.SelectedItems[0].SubItems[1].Text);
            int p = int.Parse(objects.SelectedItems[0].SubItems[2].Text);
            bool point = objects.SelectedItems[0].SubItems[3].Text == "Point";
            string lat = objects.SelectedItems[0].SubItems[4].Text;
            string lon = objects.SelectedItems[0].SubItems[5].Text;
            string desc = objects.SelectedItems[0].SubItems[6].Text;
            XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
            XmlNode xp = xf.SelectNodes("Placemark")[p];

            DialogResult dr = InputXY(point, ref name, ref lat, ref lon, ref desc);
            if (dr == DialogResult.OK)
            {
                objects.SelectedItems[0].SubItems[0].Text = name;
                
                XmlNode n;
                n = xp.SelectSingleNode("name");
                if (n == null) xp.AppendChild(n = xd.CreateElement("name"));
                if (n.ChildNodes.Count == 0)
                    n.AppendChild(xd.CreateTextNode(name));
                else
                    n.ChildNodes[0].Value = name;

                if (point)
                {
                    n = xp.SelectSingleNode("Point/coordinates");
                    n.ChildNodes[0].Value = String.Format("{0},{1},0", lon, lat);
                    objects.SelectedItems[0].SubItems[4].Text = lat;
                    objects.SelectedItems[0].SubItems[5].Text = lon;
                    NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)mapContent[objects.SelectedIndices[0]];
                    mp.Points[0].X = float.Parse(lon, ni);
                    mp.Points[0].Y = float.Parse(lat, ni);
                    MapViewer.DrawOnMapData();
                };

                objects.SelectedItems[0].SubItems[6].Text = desc;

                n = xp.SelectSingleNode("description");
                if (n == null) xp.AppendChild(n = xd.CreateElement("description"));
                if (n.ChildNodes.Count == 0)
                    n.AppendChild(xd.CreateTextNode(desc));
                else
                    n.ChildNodes[0].Value = desc;
            };
        }

        public static DialogResult InputXY(bool changeXY, ref string value, ref string lat, ref string lon, ref string desc)
        {
            Form form = new Form();
            Label nameText = new Label();
            Label xText = new Label();
            Label yText = new Label();
            Label dText = new Label();
            TextBox nameBox = new TextBox();
            TextBox xBox = new TextBox();
            TextBox yBox = new TextBox();
            TextBox dBox = new TextBox();
            dBox.Multiline = true;
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = "Change placemark";
            nameText.Text = "Name:";
            nameBox.Text = value;
            xText.Text = "Longitude:";
            xBox.Text = lon;
            yText.Text = "Latitude:";
            yBox.Text = lat;
            dText.Text = "Description:";
            dBox.Text = desc;

            if (!changeXY) xBox.Enabled = yBox.Enabled = false;

            xBox.KeyPress += new KeyPressEventHandler(xy_KeyPress);
            yBox.KeyPress += new KeyPressEventHandler(xy_KeyPress);

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            nameText.SetBounds(9, 10, 372, 13);
            nameBox.SetBounds(12, 26, 372, 20);
            yText.SetBounds(9, 50, 372, 13);
            yBox.SetBounds(12, 66, 372, 20);
            xText.SetBounds(9, 90, 372, 13);
            xBox.SetBounds(12, 106, 372, 20);
            dText.SetBounds(9, 130, 372, 13);
            dBox.SetBounds(12, 146, 372, 80);

            buttonOk.SetBounds(228, 237, 75, 23);
            buttonCancel.SetBounds(309, 237, 75, 23);

            nameText.AutoSize = true;
            nameBox.Anchor = nameBox.Anchor | AnchorStyles.Right;
            yBox.Anchor = yBox.Anchor | AnchorStyles.Right;
            xBox.Anchor = xBox.Anchor | AnchorStyles.Right;
            dBox.Anchor = dBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 270);
            form.Controls.AddRange(new Control[] { nameText, nameBox, yText, yBox, xText, xBox, dText, dBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, nameText.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            form.Dispose();
            if (dialogResult == DialogResult.OK)
                value = nameBox.Text;
            lat = yBox.Text;
            lon = xBox.Text;
            desc = dBox.Text;
            return dialogResult;
        }

        private static void xy_KeyPress(object sender, KeyPressEventArgs e)
        {
            // allows 0-9, backspace, and decimal, and -
            if (((e.KeyChar < 48 || e.KeyChar > 57) && e.KeyChar != 8 && e.KeyChar != 46 && e.KeyChar != 45))
            {
                e.Handled = true;
                return;
            }

            // checks to make sure only 1 decimal is allowed
            if (e.KeyChar == 46)
            {
                if ((sender as TextBox).Text.IndexOf(e.KeyChar) != -1)
                    e.Handled = true;
            }

            // checks to make sure only 1 - is allowed
            if (e.KeyChar == 45)
            {
                if ((sender as TextBox).SelectionStart != 0)
                    e.Handled = true;
                if ((sender as TextBox).Text.IndexOf(e.KeyChar) != -1)
                    e.Handled = true;
            }
        }

        public static DialogResult InputXML(ref string value)
        {
            Form form = new Form();
            TextBox dBox = new TextBox();
            dBox.Multiline = true;
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = "Change placemark XML";
            dBox.Text = value;
            dBox.BorderStyle = BorderStyle.FixedSingle;
            dBox.WordWrap = true;
            
            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            dBox.SetBounds(9, 10, 372, 216);

            buttonOk.SetBounds(228, 237, 75, 23);
            buttonCancel.SetBounds(309, 237, 75, 23);

            dBox.Anchor = dBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 270);
            form.Controls.AddRange(new Control[] { dBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, dBox.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            form.Dispose();
            if (dialogResult == DialogResult.OK)
                value = dBox.Text;
            return dialogResult;
        }

        public static DialogResult InputBox(string title, string promptText, ref string value)
        {
            return InputBox(title, promptText, ref value, null);
        }

        public static DialogResult InputBox(string title, string promptText, ref string value, Bitmap icon)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();
            PictureBox picture = new PictureBox();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;
            if (icon != null) picture.Image = icon;
            picture.SizeMode = PictureBoxSizeMode.StretchImage;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);
            picture.SetBounds(12, 72, 22, 22);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel, picture });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            if (picture.Image != null) picture.Image.Dispose();
            form.Dispose();
            value = textBox.Text;
            return dialogResult;
        }


        private void editPlacemarkXMLToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (objects.SelectedIndices.Count == 0) return; 

            System.Globalization.CultureInfo gi = System.Globalization.CultureInfo.InstalledUICulture;
            System.Globalization.NumberFormatInfo ni = (System.Globalization.NumberFormatInfo)gi.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";

            int l = int.Parse(objects.SelectedItems[0].SubItems[1].Text);
            int p = int.Parse(objects.SelectedItems[0].SubItems[2].Text);
            bool point = objects.SelectedItems[0].SubItems[3].Text == "Point";
            XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
            XmlNode xp = xf.SelectNodes("Placemark")[p];
            string xml = xp.InnerXml;
            
            DialogResult dr = InputXML(ref xml);
            if (dr == DialogResult.OK)
            {
                xp.InnerXml = xml;
                XmlNode n;
                NaviMapNet.MapObject mo = (NaviMapNet.MapObject)mapContent[objects.SelectedIndices[0]];

                string name = "";
                n = xp.SelectSingleNode("name");
                if ((n != null) && (n.ChildNodes.Count > 0)) name = n.ChildNodes[0].Value;
                objects.SelectedItems[0].SubItems[0].Text = name;
                mo.Name = name;
                string desc = "";
                n = xp.SelectSingleNode("description");
                if ((n == null) && (n.ChildNodes.Count > 0)) desc = n.ChildNodes[0].Value;
                objects.SelectedItems[0].SubItems[6].Text = desc;

                if (point)
                {
                    n = xp.SelectSingleNode("Point/coordinates");
                    string[] llz = n.ChildNodes[0].Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    objects.SelectedItems[0].SubItems[4].Text = llz[1];
                    objects.SelectedItems[0].SubItems[5].Text = llz[0];
                    NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)mapContent[objects.SelectedIndices[0]];
                    mp.Points[0].X = float.Parse(llz[0], ni);
                    mp.Points[0].Y = float.Parse(llz[1], ni);
                    MapViewer.DrawOnMapData();
                };                
            };
        }

        private void deletePlacemarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.SelectedIndices.Count == 0) return; 

            if (MessageBox.Show("Do you really want to delete current placemark?", "Delete data", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No) return;

            int d = objects.SelectedIndices[0];
            int l = int.Parse(objects.SelectedItems[0].SubItems[1].Text);
            int p = int.Parse(objects.SelectedItems[0].SubItems[2].Text);
            bool point = objects.SelectedItems[0].SubItems[3].Text == "Point";
            XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
            XmlNode xp = xf.SelectNodes("Placemark")[p];

            NaviMapNet.MapObject mo = (NaviMapNet.MapObject)mapContent[objects.SelectedIndices[0]];            
            objects.Items.RemoveAt(objects.SelectedIndices[0]);
            xp.ParentNode.RemoveChild(xp);
            mapContent.Remove(mo.Index);
            MapViewer.DrawOnMapData();

            XmlNodeList nl = xf.SelectNodes("Placemark");
            if ((nl.Count > 0) && (p < nl.Count) && (d < objects.Items.Count))
                for (int i = p; i < nl.Count; i++)
                    objects.Items[d + i - p].SubItems[2].Text = String.Format("{0:00}",i);
        }

        private void contextMenuStrip2_Opening(object sender, CancelEventArgs e)
        {
            Point point = layers.PointToClient(Cursor.Position);
            int index = layers.IndexFromPoint(point);
            layers.SelectedIndex = index;

            deleteToolStripMenuItem.Enabled =
                renameLayerToolStripMenuItem.Enabled = 
                    layers.SelectedIndices.Count > 0;
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (layers.SelectedIndices.Count == 0) return;
            if (MessageBox.Show("Do you really want to delete current layer?", "Delete data", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No) return;
            
            int l = layers.SelectedIndices[0];
            layers.Items.RemoveAt(l);
            XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
            xf.ParentNode.RemoveChild(xf);
            bool[] chl = new bool[layers.Items.Count];
            if (chl.Length > 0)
                for (int i = 0; i < chl.Length; i++)
                    chl[i] = layers.GetItemChecked(i);
            LoadLayers();
            if (chl.Length > 0)
                for (int i = 0; i < chl.Length; i++)
                    layers.SetItemChecked(i, chl[i]);
            DrawCheckedLayers();
        }

        private void renameLayerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (layers.SelectedIndices.Count == 0) return;
            int l = layers.SelectedIndices[0];
            XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
            XmlNode xn2 = xf.SelectSingleNode("name");
            string lname = "";
            try { if (xn2 != null) lname = xn2.ChildNodes[0].Value; }
            catch { };

            DialogResult dr = InputBox("Layer", "Change layer name:", ref lname);
            if (dr == DialogResult.OK)
            {
                layers.Items[l] = String.Format("{1:00}: {0}", lname, l);
                if (xn2 == null) xf.AppendChild(xn2 = xd.CreateElement("name"));
                if (xn2.ChildNodes.Count > 0)
                    xn2.ChildNodes[0].Value = lname;
                else
                    xn2.AppendChild(xd.CreateTextNode(lname));
            };
        }

        private void ñîõðàíèòüÑëîèÂÔàéëToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (layers.Items.Count == 0) return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save to file";
            if (this.fileExt == ".kml")
            {
                sfd.Filter = "KML files (*.kml)|*.kml";
                sfd.DefaultExt = ".kml";
            }
            else
            {
                sfd.Filter = "KMZ files (*.kmz)|*.kmz";
                sfd.DefaultExt = ".kmz";
            };
            string fn = "";
            if (sfd.ShowDialog() == DialogResult.OK) fn = sfd.FileName;                
            sfd.Dispose();
            if (fn == "") return;
            SaveTo(fn);
        }

        private void SaveTo(string filename)
        {
            xd.Save(GetTempPath() + "doc.kml");
            string fExt = Path.GetExtension(filename).ToLower();
            if (fExt == ".kml")
                File.Copy(GetTempPath() + "doc.kml", filename, true);
            else
                CreateZIP(filename, GetTempPath());
        }

        // https://github.com/icsharpcode/SharpZipLib/wiki/Zip-Samples#Create_a_Zip_fromto_a_memory_stream_or_byte_array_1
        private void CreateZIP(string filename, string folder)
        {
            FileStream fsOut = File.Create(filename);
            ZipOutputStream zipStream = new ZipOutputStream(fsOut);
            zipStream.SetLevel(3); //0-9, 9 being the highest level of compression
            // zipStream.Password = password;  // optional. Null is the same as not setting. Required if using AES.
            CompressFolder(folder, zipStream, folder.Length);
            zipStream.IsStreamOwner = true; // Makes the Close also Close the underlying stream
            zipStream.Close();
        }

        // Recurses down the folder structure
        private void CompressFolder(string path, ZipOutputStream zipStream, int folderOffset)
        {
            string[] files = Directory.GetFiles(path);

            foreach (string filename in files)
            {

                FileInfo fi = new FileInfo(filename);

                string entryName = filename.Substring(folderOffset); // Makes the name in zip based on the folder
                entryName = ZipEntry.CleanName(entryName); // Removes drive from name and fixes slash direction
                ZipEntry newEntry = new ZipEntry(entryName);
                newEntry.DateTime = fi.LastWriteTime; // Note the zip format stores 2 second granularity

                // Specifying the AESKeySize triggers AES encryption. Allowable values are 0 (off), 128 or 256.
                // A password on the ZipOutputStream is required if using AES.
                //   newEntry.AESKeySize = 256;

                // To permit the zip to be unpacked by built-in extractor in WinXP and Server2003, WinZip 8, Java, and other older code,
                // you need to do one of the following: Specify UseZip64.Off, or set the Size.
                // If the file may be bigger than 4GB, or you do not need WinXP built-in compatibility, you do not need either,
                // but the zip will be in Zip64 format which not all utilities can understand.
                //   zipStream.UseZip64 = UseZip64.Off;
                newEntry.Size = fi.Length;

                zipStream.PutNextEntry(newEntry);

                // Zip the file in buffered chunks
                // the "using" will close the stream even if an exception occurs
                byte[] buffer = new byte[4096];
                using (FileStream streamReader = File.OpenRead(filename))
                {
                    StreamUtils.Copy(streamReader, zipStream, buffer);
                }
                zipStream.CloseEntry();
            }
            string[] folders = Directory.GetDirectories(path);
            foreach (string folder in folders)
            {
                CompressFolder(folder, zipStream, folderOffset);
            }
        }

        private void hideOnMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.SelectedIndices.Count == 0) return;

            bool vis = mapContent[objects.SelectedIndices[0]].Visible;
            vis = !vis;
            mapContent[objects.SelectedIndices[0]].Visible = vis;
            objects.Items[objects.SelectedIndices[0]].Font =
                new Font(objects.Items[objects.SelectedIndices[0]].Font, vis ? FontStyle.Regular : FontStyle.Strikeout);

            MapViewer.DrawOnMapData();
        }

        private string UserDefinedGetTileUrl(int x, int y, int z)
        {
            if (iStorages.SelectedIndex == 19) return SASPlanetCache(x, y, z + 1);
            return "";
        }

        private string SASPlanetCache(int x, int y, int z)
        {
            string basedir = String.Format(@"{1}\z{0}", z, spcd.Text);
            if (!Directory.Exists(basedir)) return "none";

            string xDir = "x" + x.ToString();
            string[] xdirs = Directory.GetDirectories(basedir);
            if ((xdirs == null) || (xdirs.Length == 0)) return "none";
            foreach (string xdir in xdirs)
            {
                string xx = xdir + @"\x" + x.ToString();
                if (Directory.Exists(xx))
                {
                    string[] ydirs = Directory.GetDirectories(xx);
                    if ((ydirs == null) || (ydirs.Length == 0)) return "none";
                    foreach (string ydir in ydirs)
                    {
                        string yy = ydir + @"\y" + y.ToString() + ".png";
                        if (File.Exists(yy))
                            return yy;
                    };
                };
            };

            return "none";
        }

        private void udu_TextChanged(object sender, EventArgs e)
        {
            if (iStorages.SelectedIndex == 20)
                MapViewer.ImageSourceUrl = udu.Text;
        }

        private void KMZViewerForm_Load(object sender, EventArgs e)
        {
            if ((args != null) && (args.Length > 0))
            {
                if (File.Exists(args[0]))
                    OpenFile(args[0]);
            };
        }

        private void KMZViewerForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null) return;
            if (files.Length == 0) return;
            if (files.Length > 1) return;
            OpenFile(files[0]);
        }

        private void KMZViewerForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private bool locate = false;
        private void MapViewer_MouseDown(object sender, MouseEventArgs e)
        {
            locate = true;
        }
    }
}