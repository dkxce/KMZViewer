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
using System.Xml.Serialization;
using System.Windows.Forms;
using System.Drawing.Imaging;

using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace KMZ_Viewer
{
    public partial class KMZViewerForm : Form
    {
        // P/Invoke constants
        private const int WM_SYSCOMMAND = 0x112;
        private const int MF_STRING = 0x0;
        private const int MF_SEPARATOR = 0x800;
        private const int XP_OPENFILE = 0xA801;
        private const int XP_OPENLAYER = 0xA802;
        private const int XP_OPENLAYERS = 0xA803;
        
        // P/Invoke declarations
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool InsertMenu(IntPtr hMenu, int uPosition, int uFlags, int uIDNewItem, string lpNewItem);

        private int SYSMENU_ABOUT_ID = 0x1;
        private int SYSMENU_WGSFormX = 0x2;
        private int SYSMENU_FROM_PBF = 0x3;

        MruPathList mruD;
        MruPathList mruF;
        MruPathList mruT;
        State state;
        string SASCacheMapPath = @"C:\Program Files\SASPlanet\cache\osmmapMapnik";
        string UserDefinedURL = "http://tile.openstreetmap.org/{z}/{x}/{y}.png";
        string UserDefindedFile = @"C:\nofile.mbtiles";

        private System.Threading.Mutex mtxTrace = new System.Threading.Mutex();
        private System.Threading.Mutex mtxCrafts = new System.Threading.Mutex();

        private APRSCFG aprs_cfg = null;
        public Preferences Properties = Preferences.Load();

        public NaviMapNet.MapLayer mapContent = null;
        public NaviMapNet.MapLayer mapAPRS = null;
        public NaviMapNet.MapLayer mapAPRSTrace = null;
        public NaviMapNet.MapLayer mapTrace = null;
        public NaviMapNet.MapLayer mapCrafts = null;
        public NaviMapNet.MapLayer mapSelect = null;
        public ToolTip mapTootTip = new ToolTip();
        private bool firstboot = true;

        private GetRouter groute = null;
        public NaviMapNet.MapLayer mapRoute = null;
        public NaviMapNet.MapPoint mapRStart = null;
        public NaviMapNet.MapPoint mapRFinish = null;
        public NaviMapNet.MapPolyLine mapRVector = null;
        private KMZRebuilder.MultiPointRouteForm mapRMulti = null;

        public string[] args = null;
        private string fileName = "";
        private string fileExt = "";
        private XmlDocument xd = null;

        public static PointInRegionUtils PIRU = new PointInRegionUtils();

        public FlightRadarGrabber FRG = new FlightRadarGrabber();
        public Image[] FRGI = null;
        public Color[] FRGC = new Color[] { Color.AliceBlue, Color.AntiqueWhite, Color.Aqua, Color.Beige, Color.BlueViolet, Color.Brown, Color.BurlyWood, Color.Chocolate, Color.Coral, Color.Crimson, Color.Cyan, Color.DeepSkyBlue, Color.Firebrick, Color.Gold, Color.Gray, Color.Green, Color.GreenYellow, Color.Indigo, Color.Ivory, Color.Khaki, Color.Lavender, Color.LightGreen, Color.LightSteelBlue, Color.MistyRose, Color.Moccasin, Color.Olive, Color.Orange, Color.Orchid, Color.PaleGreen, Color.Peru, Color.RoyalBlue, Color.Salmon, Color.SeaGreen, Color.SeaShell, Color.Silver, Color.SteelBlue, Color.Tan, Color.Tomato, Color.Yellow };
        private Random FRGCR = new Random();
        public Font FRGFont = new Font("MS Sans Serif", 7);
        private const byte cAirCraftNormal = 0;
        private const byte cAirCraftBad = 1;
        private const byte cAirCraftOldBad = 2;
        private const byte cAirCraftOld = 3;
        System.Media.SoundPlayer FR24_BEEP_SND = new System.Media.SoundPlayer(global::KMZViewer.Properties.Resources.beep);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Get a handle to a copy of this form's system (window) menu
            IntPtr hSysMenu = GetSystemMenu(this.Handle, false);
            AppendMenu(hSysMenu, MF_SEPARATOR, 0, string.Empty);
            AppendMenu(hSysMenu, MF_STRING, SYSMENU_WGSFormX, "Lat && Lon Converter ...");
            AppendMenu(hSysMenu, MF_STRING, SYSMENU_FROM_PBF, "KMZ POI From OSM Converter ...");
            AppendMenu(hSysMenu, MF_SEPARATOR, 0, string.Empty);
            AppendMenu(hSysMenu, MF_STRING, SYSMENU_ABOUT_ID, "&О Программе ...");
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg == WM_SYSCOMMAND) && ((int)m.WParam == SYSMENU_WGSFormX))
            {
                (new KMZRebuilder.WGSFormX()).Show();
            };
            if ((m.Msg == WM_SYSCOMMAND) && ((int)m.WParam == SYSMENU_FROM_PBF))
            {
                try
                {
                    if (File.Exists(CurrentDirectory() + @"\KMZPOIfromOSM.exe"))
                        System.Diagnostics.Process.Start(CurrentDirectory() + @"\KMZPOIfromOSM.exe");
                }
                catch { };
            };
            if ((m.Msg == WM_SYSCOMMAND) && ((int)m.WParam == SYSMENU_ABOUT_ID))
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                string text = fvi.ProductName + " " + fvi.FileVersion + " by " + fvi.CompanyName + "\r\n";
                text += "\r\n";
                text += "Supports:\r\n";
                text += " - GPI Import  (Garmin Points of Interests)\r\n";
                text += " - OSM Search RU  (openstreetmap.ru)\r\n";
                text += " - FlightRadar24  (www.flightradar24.com)\r\n";
                text += " - APRS-IS TCP/IP  (My Soft: SimpleAPRSserver, APRSAIR)\r\n";
                text += " - APRS TNC AGWPE TCP/IP  (AGWPE Engine, SoundModem by UZ7HO)\r\n";
                text += " - APRS TNC KISS TCP/IP  (AGWPE Engine, SoundModem by UZ7HO)\r\n";
                text += " - APRS TNC KISS Serial  (Lanchonlh HG-UV98)\r\n";
                text += " - dkxce Route Engine\r\n";
                text += " - Raster MBTiles\r\n";
                text += "\r\n";
                text += fvi.LegalCopyright + "\r\n";
                #if  NOFR24
                text += "Build: NOFR24\r\n";
                #else
                text += "Build: NORMAL\r\n";
                #endif
                try
                {
                    string[] dnst = DNS.DNSLookUp.Get_TXT("kmztools.dkxce.linkpc.net");
                    foreach (string dt in dnst)
                        if (dt.StartsWith("vwr: about:"))
                            text += "\r\n" + dt.Substring(11).Trim();
                }
                catch (Exception ex)
                {
                };
                MessageBox.Show(text, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            if (m.Msg == ProcDataExchange.WM_COPYDATA)
            {
                ProcDataExchange.COPYDATASTRUCT copyData = (ProcDataExchange.COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(ProcDataExchange.COPYDATASTRUCT));
                if (copyData.DataType == XP_OPENFILE)
                {
                    string fn = null;
                    try { fn = copyData.DataUTF8String; } catch { };
                    if (!String.IsNullOrEmpty(fn))
                        if (File.Exists(fn))
                        {
                            if(this.fileName != fn)
                                OpenFile(fn);
                            button2_Click(button2, null);
                            button1_Click(button1, null);
                        };
                };
                if (copyData.DataType == XP_OPENLAYER)
                {
                    string fn = null;
                    int layer = -1;
                    try 
                    {
                        byte[] data = copyData.Data;
                        fn = System.Text.Encoding.UTF8.GetString(data, 4, data.Length - 4);
                        layer = BitConverter.ToInt32(data, 0);
                    }
                    catch { };
                    if (!String.IsNullOrEmpty(fn))
                        if (File.Exists(fn))
                        {
                            if(this.fileName != fn)
                                OpenFile(fn);
                            if ((layer >= 0) && (layer < layers.Items.Count))
                            {
                                for (int ci = 0; ci < layers.Items.Count; ci++)
                                    layers.SetItemChecked(ci, false);
                                layers.SetItemChecked(layer, true);
                                DrawCheckedLayers();
                                tabControl1.SelectedIndex = 1;
                            };
                        };
                };
                if (copyData.DataType == XP_OPENLAYERS)
                {
                    string fn = null;
                    int[] lays = new int[0];
                    try
                    {
                        byte[] data = copyData.Data;
                        int fl = BitConverter.ToInt32(data, 0);
                        fn = System.Text.Encoding.UTF8.GetString(data, 4, fl);
                        lays = new int[BitConverter.ToInt32(data, 4 + fl)];
                        for (int i = 0; i < lays.Length; i++)
                            lays[i] = BitConverter.ToInt32(data, 4 + fl + 4 + 4 * i);
                    }
                    catch { };
                    if (!String.IsNullOrEmpty(fn))
                        if (File.Exists(fn))
                        {
                            if (this.fileName != fn)
                                OpenFile(fn);
                            if (lays.Length > 0)
                            {
                                for (int ci = 0; ci < layers.Items.Count; ci++)
                                    layers.SetItemChecked(ci, false);
                                for (int i = 0; i < lays.Length; i++)
                                    layers.SetItemChecked(lays[i], true);
                                DrawCheckedLayers();
                                tabControl1.SelectedIndex = 1;
                            };
                        };
                };     
            };
        }

        public KMZViewerForm(string[] args)
        {
            this.args = args;

            FileAss.SetFileAssociation("kmz", "KMZFile", "Open in KMZViewer", CurrentDirectory() + @"\KMZViewer.exe");
            FileAss.SetFileAssociation("kml", "KMLFile", "Open in KMZViewer", CurrentDirectory() + @"\KMZViewer.exe");
            FileAss.SetFileAssociation("wpt", "WPTFile", "Open in KMZViewer", CurrentDirectory() + @"\KMZViewer.exe");
            FileAss.SetFileAssociation("gpx", "GPXFile", "Open in KMZViewer", CurrentDirectory() + @"\KMZViewer.exe");
            FileAss.SetFileAssociation("gpi", "GPXFile", "Open in KMZViewer", CurrentDirectory() + @"\KMZViewer.exe");
            FileAss.SetFileAssociation("gdb", "GDBFile", "Open in KMZViewer", CurrentDirectory() + @"\KMZViewer.exe");
            FileAss.SetFileAssociation("dat", "DATFile", "Open in KMZViewer", CurrentDirectory() + @"\KMZViewer.exe");
            FileAss.SetFileAssociation("fit", "FITFile", "Open in KMZViewer", CurrentDirectory() + @"\KMZViewer.exe");
            FileAss.UpdateExplorer();

            Init();

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);

            this.Text = "KMZ Viewer XP FR24/APRS/TNC/RT";
            #if  NOFR24
            tabControl1.TabPages.RemoveAt(5);
            this.Text = "KMZ Viewer XP APRS/TNC";
            #endif
            this.Text += " " + fvi.FileVersion + " by " + fvi.CompanyName;

            MapViewer.UserDefinedGetTileUrl += new NaviMapNet.NaviMapNetViewer.GetTilePathCall(UserDefinedGetTileUrl);    
        
            string fn = CurrentDirectory()+@"\KMZViewer.stt";
            if (File.Exists(fn)) state = State.Load(fn);

            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\", true)
                    .CreateSubKey("KMZViewer"))
                    key.SetValue("Handle", this.Handle.ToInt32());
            }
            catch { };

            // ProcDataExchange.EnableReceivingData(this.Handle);
        }

        public string ClearLastSlash(string file_name)
        {
            if (file_name.Substring(file_name.Length - 1) == @"\")
                return file_name.Remove(file_name.Length - 1);
            return file_name;
        }

        private void mruD_DirSelected(string file_name)
        {
            SASCacheMapPath = file_name;
            mruD.AddFile(file_name);
            if (iStorages.SelectedIndex == (iStorages.Items.Count - 1))
                iStorages_SelectedIndexChanged(this, null);
            else
                iStorages.SelectedIndex = iStorages.Items.Count - 1;
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

        [Serializable]
        public class MapStore
        {
            public string Name;
            public string Url;
            public string CacheDir;
            public NaviMapNet.NaviMapNetViewer.MapServices Service = NaviMapNet.NaviMapNetViewer.MapServices.Custom_UserDefined;
            public NaviMapNet.NaviMapNetViewer.ImageSourceTypes Source = NaviMapNet.NaviMapNetViewer.ImageSourceTypes.tiles;
            public NaviMapNet.NaviMapNetViewer.ImageSourceProjections Projection = NaviMapNet.NaviMapNetViewer.ImageSourceProjections.EPSG3857;

            public override string ToString()
            {
                return Name;
            }

            public MapStore() { }
            public MapStore(string Name) { this.Name = Name; }
            public MapStore(string Name, string Url, string Cache)
            {
                this.Name = Name;
                this.Url = Url;
                this.CacheDir = Cache;
            }
            public MapStore(string Name, string Url, NaviMapNet.NaviMapNetViewer.MapServices Service)
            {
                this.Name = Name;
                this.Url = Url;
                this.Service = Service;
            }
        }

        private void Init()
        {
            InitializeComponent();
            mapTootTip.ShowAlways = true;

            mapTrace = new NaviMapNet.MapLayer("mapTrace");
            MapViewer.MapLayers.Add(mapTrace);
            mapAPRSTrace = new NaviMapNet.MapLayer("mapAPRSTrace");
            MapViewer.MapLayers.Add(mapAPRSTrace);

            mapCrafts = new NaviMapNet.MapLayer("mapCrafts");
            MapViewer.MapLayers.Add(mapCrafts);            
            mapAPRS = new NaviMapNet.MapLayer("mapAPRS");
            MapViewer.MapLayers.Add(mapAPRS);

            mapRoute = new NaviMapNet.MapLayer("mapRoute");
            MapViewer.MapLayers.Add(mapRoute);
            mapSelect = new NaviMapNet.MapLayer("mapSelect");
            MapViewer.MapLayers.Add(mapSelect);
            mapContent = new NaviMapNet.MapLayer("mapContent");
            MapViewer.MapLayers.Add(mapContent);            
            

            // LOAD NO MAP
            iStorages.Items.Add(new MapStore("[[*** No Map ***]]","",null));

            // LOAD MAPS FROM FILE
            string mf = CurrentDirectory() + @"\KMZViewer.maps";
            if (File.Exists(mf))
            {
                MapStore[] mss = XMLSaved<MapStore[]>.Load(mf);
                if ((mss != null) && (mss.Length > 0))
                    iStorages.Items.AddRange(mss);
            };
            
            //iStorages.Items.Add(new MapStore("OSM Mapnik", "http://tile.openstreetmap.org/{z}/{x}/{y}.png", "OSMMapnik"));
            //iStorages.Items.Add(new MapStore("OSM OpenVkarte", "http://tile.xn--pnvkarte-m4a.de/tilegen/{z}/{x}/{y}.png", "OSMOpenVkarte"));
            //iStorages.Items.Add(new MapStore("OSM Wikimapia", "http://i{w}.wikimapia.org/?lng=1&x={x}&y={y}&zoom={z}", "OSMWiki"));

            //iStorages.Items.Add(new MapStore("OpenTopoMaps", "http://a.tile.opentopomap.org/{z}/{x}/{y}.png", "OpenTopoMaps"));
            //iStorages.Items.Add(new MapStore("Sputnik.ru", "http://tiles.maps.sputnik.ru/{z}/{x}/{y}.png", "Sputnik"));
            //iStorages.Items.Add(new MapStore("RUMAP", "http://tile.digimap.ru/rumap/{z}/{x}/{y}.png", "RUMAP"));
            //iStorages.Items.Add(new MapStore("2GIS", "https://tile1.maps.2gis.com/tiles?x={x}&y={y}&z={z}&v=1.1", "2GIS"));
            //iStorages.Items.Add(new MapStore("ArcGIS ESRI", "http://services.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}.png", "ArcGIS"));

            //iStorages.Items.Add(new MapStore("OviMap: Nokia", "http://maptile.mapplayer1.maps.svc.ovi.com/maptiler/maptile/newest/normal.day/{z}/{x}/{y}/256/png8", "OviNokia"));
            //iStorages.Items.Add(new MapStore("OviMap: Default", "http://1.maptile.lbs.ovi.com/maptiler/v2/maptile/newest/normal.day/{z}/{x}/{y}/256/png8?lg=RUS&token=fee2f2a877fd4a429f17207a57658582&appId=nokiaMaps", "OviDefault"));
            //iStorages.Items.Add(new MapStore("OviMap: Sputnik", "http://1.maptile.lbs.ovi.com/maptiler/v2/maptile/newest/satellite.day/{z}/{x}/{y}/256/png8?lg=RUS&token=fee2f2a877fd4a429f17207a57658582&appId=nokiaMaps", "OviSputnik"));
            //iStorages.Items.Add(new MapStore("OviMap: Relief", "http://1.maptile.lbs.ovi.com/maptiler/v2/maptile/newest/hybrid.day/{z}/{x}/{y}/256/png8?lg=RUS&token=fee2f2a877fd4a429f17207a57658582&appId=nokiaMaps", "OviRelief"));
            //iStorages.Items.Add(new MapStore("OviMap: Hybrid", "http://1.maptile.lbs.ovi.com/maptiler/v2/maptile/newest/terrain.day/{z}/{x}/{y}/256/png8?lg=RUS&token=fee2f2a877fd4a429f17207a57658582&appId=nokiaMaps", "OviHybrid"));

            //iStorages.Items.Add(new MapStore("Kosmosnimki.ru: ScanEx 1", "http://maps.kosmosnimki.ru/TileService.ashx?Request=gettile&LayerName=04C9E7CE82C34172910ACDBF8F1DF49A&apikey=7BDJ6RRTHH&crs=epsg:3857&z={z}&x={x}&y={y}", "ScanEx1"));
            //iStorages.Items.Add(new MapStore("Kosmosnimki.ru: ScanEx 2", "http://maps.kosmosnimki.ru/TileService.ashx?Request=gettile&LayerName=04C9E7CE82C34172910ACDBF8F1DF49A&apikey=7BDJ6RRTHH&crs=epsg:3857&z={z}&x={x}&y={y}", "ScanEx2"));
            //iStorages.Items.Add(new MapStore("Kosmosnimki.ru: IRS Sat", "http://irs.gis-lab.info/?layers=irs&request=GetTile&z={z}&x={x}&y={y}", "IRSSat"));

            //iStorages.Items.Add(new MapStore("Google: Map", "http://mts0.google.com/vt/lyrs=m@177000000&hl=ru&src=app&x={x}&s=&y={y}&z={z}&s=Ga", "GoogleMap"));
            //iStorages.Items.Add(new MapStore("Google: Sat", "http://mts0.google.com/vt/lyrs=h@177000000&hl=ru&src=app&x={x}&s=&y={y}&z={z}&s=G", "GoogleSat"));

            //iStorages.Items.Add(new MapStore("Here: Normal Transit Day", "http://1.base.maps.api.here.com/maptile/2.1/maptile/newest/normal.day.transit/{z}/{x}/{y}/256/png8?app_id=xWVIueSv6JL0aJ5xqTxb&app_code=djPZyynKsbTjIUDOBcHZ2g&lg=rus&ppi=72", "HereNTD"));
            //iStorages.Items.Add(new MapStore("Here: Normal Day", "http://1.base.maps.api.here.com/maptile/2.1/maptile/newest/normal.day/{z}/{x}/{y}/256/png8?app_id=xWVIueSv6JL0aJ5xqTxb&app_code=djPZyynKsbTjIUDOBcHZ2g&lg=rus&ppi=72", "HereND"));

            // LOAD USER-DEFINED MAPS
            iStorages.Items.Add(new MapStore("[[*** MBTiles file ***]]", "", NaviMapNet.NaviMapNetViewer.MapServices.Custom_MBTiles));
            iStorages.Items.Add(new MapStore("[[*** User-Defined Url ***]]", "", "URLDefined"));
            iStorages.Items.Add(new MapStore("[[*** SAS Planet Cache Path ***]]","","SASPlanet"));            

            MapViewer.NotFoundTileColor = Color.LightYellow;
            MapViewer.ImageSourceService = NaviMapNet.NaviMapNetViewer.MapServices.Custom_LocalFiles;
            MapViewer.ImageSourceUrl = @"C:\Program Files\SASPlanet\cache\osmmapMapnik\";
            MapViewer.WebRequestTimeout = 10000;
            MapViewer.ZoomID = 10;
            MapViewer.OnMapUpdate = new NaviMapNet.NaviMapNetViewer.MapEvent(MapUpdate);
            //MapViewer.UserDefinedGetTileUrl = new NaviMapNet.NaviMapNetViewer.GetTilePathCall(this.GetTilePath);                        

            //iStorages.SelectedIndex = iStorages.Items.Count - 1;
            //MapViewer.DrawMap = true;
            //MapViewer.ReloadMap();

            //List<MapStore> iss = new List<MapStore>();
            //for (int i = 1; i < iStorages.Items.Count - 3; i++)
            //    iss.Add((MapStore)iStorages.Items[i]);
            //XMLSaved<MapStore[]>.Save(CurrentDirectory() + @"\KMZViewer.maps", iss.ToArray());

            LoadTNC(false);
        }

        private void iStorages_SelectedIndexChanged(object sender, EventArgs e)
        {
            MapStore iS = (MapStore)iStorages.SelectedItem;

            if (iStorages.SelectedIndex < (iStorages.Items.Count - 1))
            {
                iCache.SelectedIndex = -1;
                iCache.BackColor = Color.FromArgb(224, 224, 224);
                iStorages.BackColor = Color.White;
            }
            else
            {
                iCache.BackColor = Color.White;
                iStorages.BackColor = Color.FromArgb(224, 224, 224);
            };

            MapViewer.ImageSourceService = iS.Service;
            MapViewer.ImageSourceType = iS.Source;
            MapViewer.ImageSourceProjection = iS.Projection;

            if (iStorages.SelectedIndex < (iStorages.Items.Count - 1))
            {
                MapViewer.UseDiskCache = true;
                MapViewer.UserDefinedMapName = iS.CacheDir;

                if (iStorages.SelectedIndex == (iStorages.Items.Count - 2))
                    MapViewer.ImageSourceUrl = UserDefinedURL;
                else if (iStorages.SelectedIndex == (iStorages.Items.Count - 3))
                {
                    MapViewer.UseDiskCache = false;
                    MapViewer.ImageSourceUrl = UserDefindedFile;
                }
                else
                    MapViewer.ImageSourceUrl = iS.Url;
            };            

            if (iStorages.SelectedIndex == (iStorages.Items.Count - 1))
            {
                MapViewer.UseDiskCache = false;

                SASCacheMapPath = ClearLastSlash(SASCacheMapPath);
                MapViewer.UserDefinedMapName = iS.CacheDir = @"LOCAL\" + SASCacheMapPath.Substring(SASCacheMapPath.LastIndexOf(@"\") + 1);                
                MapViewer.ImageSourceUrl = SASCacheMapPath;

                if (iCache.Items.Count > 0)
                {
                    string pp = Path.GetFullPath(MapViewer.ImageSourceUrl.ToLower());
                    int seli = -1;
                    for (int i = 0; i < iCache.Items.Count; i++)
                        if (iCache.Items[i] is SASItem)
                            if (Path.GetFullPath(((SASItem)iCache.Items[i]).Path.ToLower()) == pp)
                            {
                                seli = i;
                                break;
                            };
                    iCache.SelectedIndex = seli;
                };
                
            };

            iStorages.Refresh();
            MapViewer.ReloadMap();
        }

        private void MapUpdate()
        {
            string lreq = MapViewer.LastRequestedFile;
            if (lreq.Length > 60) lreq = "... " + lreq.Substring(lreq.Length - 60);
            toolStripStatusLabel1.Text = "LRF: " + lreq;
            toolStripStatusLabel2.Text = MapViewer.CenterDegreesLat.ToString("00.000000", System.Globalization.CultureInfo.InvariantCulture);
            toolStripStatusLabel3.Text = MapViewer.CenterDegreesLon.ToString("000.000000", System.Globalization.CultureInfo.InvariantCulture);

            string regNm = "...";
            if (MapViewer.ZoomID > 7)
            {
                int regNo = PIRU.PointInRegion(MapViewer.CenterDegreesY, MapViewer.CenterDegreesX);
                regNm = regNo > 0 ? PIRU.GetRegionName(regNo) : "...";
            };
            regName.Text = regNm;
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
            toolStripStatusLabel4.Text = m.Y.ToString("00.000000",System.Globalization.CultureInfo.InvariantCulture);
            toolStripStatusLabel5.Text = m.X.ToString("000.000000", System.Globalization.CultureInfo.InvariantCulture);
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

            NaviMapNet.MapObject mo = mapContent[int.Parse(objects.SelectedItems[0].SubItems[7].Text)];
            if ((mo is NaviMapNet.MapPolyLine) || (mo is NaviMapNet.MapPolygon))
            {
                if (mo is NaviMapNet.MapPolyLine)
                {
                    if ((mo.Bounds.Width > MapViewer.MapBoundsRectDegrees.Width) || (mo.Bounds.Height > MapViewer.MapBoundsRectDegrees.Height))
                        MapViewer.CenterDegrees = mo.Points[0];
                    else
                        MapViewer.ZoomByArea((mo as NaviMapNet.MapPolyLine).Bounds, MapViewer.ZoomID);
                }
                else
                {
                    byte nextZoom = MapViewer.ZoomID;
                    if ((mo.Bounds.Width > MapViewer.MapBoundsRectDegrees.Width) || (mo.Bounds.Height > MapViewer.MapBoundsRectDegrees.Height))
                    {
                        int pow = (int)Math.Round(Math.Max(mo.Bounds.Width / MapViewer.MapBoundsRectDegrees.Width, mo.Bounds.Height / MapViewer.MapBoundsRectDegrees.Height));
                        nextZoom = (byte)(nextZoom - pow);
                        if (nextZoom < 2) nextZoom = 2;
                        if (nextZoom > 20) nextZoom = 2;
                    };
                    MapViewer.ZoomByArea((mo as NaviMapNet.MapPolygon).Bounds, nextZoom);
                };
            }
            else
            {
                double[] b = MapViewer.MapBoundsMinMaxDegrees;
                if ((mo.Points[0].X < b[0]) || (mo.Points[0].Y < b[1]) || (mo.Points[0].X > b[2]) || (mo.Points[0].Y > b[3]))
                    MapViewer.CenterDegrees = mo.Points[0];
            };
            SelectOnMap(int.Parse(objects.SelectedItems[0].SubItems[7].Text));
        }

        private void MapViewer_MouseClick(object sender, MouseEventArgs e)
        {
            if (!locate) return;
            toolStripStatusLabel6.Text = "[NO]";
            toolStripStatusLabel6.Image = null;
            toolStripStatusLabel7.Text = "0";
            toolStripStatusLabel8.Text = "0";
                        
            Point clicked = MapViewer.MousePositionPixels;
            PointF sCenter = MapViewer.PixelsToDegrees(clicked);

            if ((mapContent.ObjectsCount == 0) && (mapCrafts.ObjectsCount == 0) && (mapAPRS.ObjectsCount == 0))
            {
                SubClick(sCenter, null);
                return;
            };

            if (mapContent.ObjectsCount > 0)
            {
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

                    if ((objects.SelectedIndices.Count == 0) || (int.Parse(objects.SelectedItems[0].SubItems[7].Text) != objs[ind].Index))
                    {
                        for (int i = 0; i < objects.Items.Count; i++)
                            if (int.Parse(objects.Items[i].SubItems[7].Text) == objs[ind].Index)
                            {
                                objects.Items[i].Selected = true;
                                objects.Items[i].Focused = true;
                                toolStripStatusLabel6.Text = objects.Items[i].Text;
                                if (objects.Items[i].ImageIndex >= 0)
                                    toolStripStatusLabel6.Image = objects.Items[i].ImageList.Images[objects.Items[i].ImageIndex];
                            };
                    };

                    SelectOnMap(objs[ind].Index);
                    if (objs[ind].PointsCount == 1)
                        SubClick(new PointF(objs[ind].Center.X, objs[ind].Center.Y), objs[ind].Name);
                    else
                        SubClick(sCenter, null);
                }
                else
                {
                    SubClick(sCenter, null);
                };
            }
            else
            {
                SubClick(sCenter, null);
            };

            if (mapCrafts.ObjectsCount > 0)
            {
                PointF sFrom = MapViewer.PixelsToDegrees(new Point(clicked.X - 10, clicked.Y + 10));
                PointF sTo = MapViewer.PixelsToDegrees(new Point(clicked.X + 10, clicked.Y - 10));
                NaviMapNet.MapObject[] objs = mapCrafts.Select(new RectangleF(sFrom, new SizeF(sTo.X - sFrom.X, sTo.Y - sFrom.Y)));
                if ((objs != null) && (objs.Length > 0))
                {
                    uint len = uint.MaxValue;
                    int ind = 0;
                    for (int i = 0; i < objs.Length; i++)
                    {
                        uint tl = GetLengthMetersC(sCenter.Y, sCenter.X, objs[i].Center.Y, objs[i].Center.X, false);
                        if (tl < len) { len = tl; ind = i; };
                    };

                    NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)objs[ind];
                    AirCraft ac = (AirCraft)mp.UserData;

                    toolStripStatusLabel6.Text = ac.CallSign;
                    toolStripStatusLabel7.Text = mp.Center.Y.ToString("00.000000",System.Globalization.CultureInfo.InvariantCulture);
                    toolStripStatusLabel8.Text = mp.Center.X.ToString("000.000000", System.Globalization.CultureInfo.InvariantCulture);
                    
                    SelectAirCraft(mp, ac);                    
                };
            };

            if (mapAPRS.ObjectsCount > 0)
            {
                PointF sFrom = MapViewer.PixelsToDegrees(new Point(clicked.X - 10, clicked.Y + 10));
                PointF sTo = MapViewer.PixelsToDegrees(new Point(clicked.X + 10, clicked.Y - 10));
                NaviMapNet.MapObject[] objs = mapAPRS.Select(new RectangleF(sFrom, new SizeF(sTo.X - sFrom.X, sTo.Y - sFrom.Y)));
                if ((objs != null) && (objs.Length > 0))
                {
                    uint len = uint.MaxValue;
                    int ind = 0;
                    for (int i = 0; i < objs.Length; i++)
                    {
                        uint tl = GetLengthMetersC(sCenter.Y, sCenter.X, objs[i].Center.Y, objs[i].Center.X, false);
                        if (tl < len) { len = tl; ind = i; };
                    };

                    NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)objs[ind];
                    Buddie b = (Buddie)mp.UserData;                    

                    toolStripStatusLabel6.Text = b.name;
                    toolStripStatusLabel7.Text = mp.Center.Y.ToString("00.000000", System.Globalization.CultureInfo.InvariantCulture);
                    toolStripStatusLabel8.Text = mp.Center.X.ToString("000.000000", System.Globalization.CultureInfo.InvariantCulture);

                    SelectAPRS(mp, b);
                };
            };
        }

        public void SelectAirCraft(NaviMapNet.MapPoint mp, AirCraft ac)
        {
            if (FR24BOX.Items[0].SubItems.Count == 1)
                for (int i = 0; i < FR24BOX.Items.Count; i++)
                    FR24BOX.Items[i].SubItems.Add("");

            if ((FR24_Selected == null) || (FR24_Selected.craft.CallSign != ac.CallSign))
            {
                if (FR24_Selected != null)
                {
                    FR24_Selected.point.TextFont = FRGFont;
                    if (FR24_Selected.trace != null)
                    {
                        FR24_Selected.trace.Width = 2;
                        FR24_Selected.trace.Color = RandomColor();
                    };
                };

                NaviMapNet.MapPolyLine pl = null;
                mtxTrace.WaitOne();
                if (mapTrace.ObjectsCount > 0)
                    for (int i = 0; i < mapTrace.ObjectsCount; i++)
                        if (mapTrace[i].Name == ac.CallSign)
                        {
                            pl = (NaviMapNet.MapPolyLine)mapTrace[i];
                            pl.Width = 4;
                            pl.Color = Color.Red;
                        };
                mtxTrace.ReleaseMutex();
                   
                
                FR24_Selected = new AirCraftOnMap(ac, mp, pl);
                UpdateCraft(mp, ac, true);
                MapViewer.DrawOnMapData();
            };

            if (FR24_Follow == null)
            {
                FR24FBTN.Enabled = true;
                FR24FBTN.Text = "Следить за самолетом " + ac.CallSign;
            };
            ShowAirCraftText(ac);
            FR24SelMnu.Enabled = true;
        }

        public void SelectAPRS(NaviMapNet.MapPoint mp, Buddie b)
        {
            aprs_follow.Enabled = true;
            if (!aprs_follow.Checked)
                aprs_follow.Text = "Следить за " + b.name;

            if(APRS_Selected == null)
                APRS_Draw(new string[] {APRS_Selected = b.name});
            else 
                APRS_Draw(new string[] { APRS_Selected, APRS_Selected = b.name });

            for (int i = 0; i < aprs_objs.Items.Count; i++)
                if (aprs_objs.Items[i].SubItems[0].Text == b.name)
                {
                    aprs_objs.Items[i].Selected = true;
                    aprs_objs.EnsureVisible(i);
                };
        }



        private void ShowAirCraftText(AirCraft ac)
        {
            FR24BOX.Items[0].SubItems[1].Text = ac.ID1;
            FR24BOX.Items[1].SubItems[1].Text = ac.ID2;
            FR24BOX.Items[2].SubItems[1].Text = ac.RegNo;
            FR24BOX.Items[3].SubItems[1].Text = ac.AirCraftType;
            FR24BOX.Items[4].SubItems[1].Text = ac.Flight;
            FR24BOX.Items[5].SubItems[1].Text = ac.GoingFrom;
            FR24BOX.Items[6].SubItems[1].Text = ac.GoingTo;
            FR24BOX.Items[7].SubItems[1].Text = ac.AirLine;
            FR24BOX.Items[8].SubItems[1].Text = ac.Hdg.ToString("000") + "°";
            FR24BOX.Items[9].SubItems[1].Text = ac.Alt.ToString("00000");
            FR24BOX.Items[10].SubItems[1].Text = "FL" + (ac.Alt / 100).ToString("000");
            FR24BOX.Items[11].SubItems[1].Text = ac.Spd.ToString("000") + " knots";
            FR24BOX.Items[12].SubItems[1].Text = (ac.Spd * 1.825).ToString("000") + " km/h";
            FR24BOX.Items[13].SubItems[1].Text = ac.Lat.ToString("00.000000", System.Globalization.CultureInfo.InvariantCulture);
            FR24BOX.Items[14].SubItems[1].Text = ac.Lon.ToString("000.000000", System.Globalization.CultureInfo.InvariantCulture);
            FR24BOX.Items[15].SubItems[1].Text = ac.Time.ToString("HH:mm:ss dd.MM.yyyy");
            FR24BOX.Items[16].SubItems[1].Text = ac.Age.ToString() + " s";
            FR24BOX.Items[17].SubItems[1].Text = ac.IsIdle ? "yes" : "no";

            FR24ST.Text = ac.ToString();
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

        public static string DegreesToCardinalDetailed(double degrees)
        {
            string[] caridnals = { "С", "ССВ", "СВ", "ВВС", "В", "ВВЮ", "ВЮ", "ЮЮВ", "Ю", "ЮЮЗ", "ЮЗ", "ЮЗЗ", "З", "СЗЗ", "СЗ", "ССЗ", "С" };
            return caridnals[(int)Math.Round(((double)degrees * 10 % 3600) / 225)];
        }

        public static double DegreeBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double dLon = ToRad(lon2 - lon1);
            double dPhi = Math.Log(
                Math.Tan(ToRad(lat2) / 2 + Math.PI / 4) / Math.Tan(ToRad(lat1) / 2 + Math.PI / 4));
            if (Math.Abs(dLon) > Math.PI)
                dLon = dLon > 0 ? -(2 * Math.PI - dLon) : (2 * Math.PI + dLon);
            return ToBearing(Math.Atan2(dLon, dPhi));
        }

        public static double ToRad(double degrees)
        {
            return degrees * (Math.PI / 180);
        }

        public static double ToDegrees(double radians)
        {
            return radians * 180 / Math.PI;
        }

        public static double ToBearing(double radians)
        {
            // convert radians to degrees (as bearing: 0...360)
            return (ToDegrees(radians) + 360) % 360;
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
            if (objects.SelectedItems.Count == 0)
            {
                pictureBox1.Image = pictureBox2.Image = null;
                persquare.Visible = false;
                return;
            };

            int si = int.Parse(objects.SelectedItems[0].SubItems[7].Text);

            objects.EnsureVisible(objects.SelectedIndices[0]);
            prevSII = objects.SelectedItems[0];
            prevSIC = objects.SelectedItems[0].BackColor;
            objects.SelectedItems[0].BackColor = Color.Red;

            NaviMapNet.MapObject mo = mapContent[si];

            tName.Text = objects.SelectedItems[0].SubItems[0].Text;
            tLat.Text = objects.SelectedItems[0].SubItems[4].Text;
            tLon.Text = objects.SelectedItems[0].SubItems[5].Text;
            tDesc.Text = objects.SelectedItems[0].SubItems[6].Text;
            
            if (mo is NaviMapNet.MapPolyLine)
            {
                uint len = GetDistInMeters(mo.Points, false);
                persquare.Text = "Length: " + (len < 1000 ? len.ToString() + " m" : ((double)len / 1000.0).ToString("0.00" + " km"));
                persquare.Visible = true;
            };
            if (mo is NaviMapNet.MapPolygon)
            {
                uint len = GetDistInMeters(mo.Points, true);
                double square = GetSquareInMeters(mo.Points);
                persquare.Text = "Perimeter: " + (len < 1000 ? len.ToString() + " m" : ((double)len / 1000.0).ToString("0.00" + " km"))
                    +
                    " / Square: " + square.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " km2";
                persquare.Visible = true;
            };

            // MAKE QRCODE
            pictureBox1.Image = pictureBox2.Image = GenerateGeoQRCode(mo.Center.Y, mo.Center.X, mo.Img);
            iminfo = new object[] { tName.Text, mo.Center.Y, mo.Center.X };
        }
        private object[] iminfo = new object[3];

        private static double GetDeterminant(double x1, double y1, double x2, double y2)
        {
            return x1 * y2 - x2 * y1;
        }

        /// <summary>
        ///     Calculate Square of Geographic Polygon By Simplify Method
        ///     (faster)
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public static double GetSquareInMetersA(PointF[] poly)
        {
            if (poly == null) return 0;
            if (poly.Length < 3) return 0;
            PointF st = new PointF(float.MaxValue, float.MaxValue);
            for (int i = 0; i < poly.Length; i++)
            {
                if (poly[i].X < st.X) st.X = poly[i].X;
                if (poly[i].Y < st.Y) st.Y = poly[i].Y;
            };
            PointF[] polygon = new PointF[poly.Length];
            for (int i = 0; i < polygon.Length; i++)
                polygon[i] = new PointF(GetGeoLengthInMetersC(st.Y, st.X, st.Y, poly[i].X, false), GetGeoLengthInMetersC(st.Y, st.X, poly[i].Y, st.X, false));

            double area = GetDeterminant(polygon[polygon.Length - 1].X, polygon[polygon.Length - 1].Y, polygon[0].X, polygon[0].Y);
            for (int i = 1; i < polygon.Length; i++)
                area += GetDeterminant(polygon[i - 1].X, polygon[i - 1].Y, polygon[i].X, polygon[i].Y);

            return Math.Abs(area / 2.0 / 1000000.0);
        }

        public static double GetSquareInMeters(PointF[] poly)
        {
            return GetSquareInMetersA(poly);
        }

        public static uint GetDistInMeters(PointF[] polyline, bool polygon)
        {
            if (polyline == null) return 0;
            if (polyline.Length < 2) return 0;
            uint res = 0;
            for (int i = 1; i < polyline.Length; i++)
                res += GetGeoLengthInMetersC(polyline[i - 1].Y, polyline[i - 1].X, polyline[i].Y, polyline[i].X, false);
            if (polygon)
                res += GetGeoLengthInMetersC(polyline[polyline.Length - 1].Y, polyline[polyline.Length - 1].X, polyline[0].Y, polyline[0].X, false);
            return res;
        }

        public static uint GetGeoLengthInMetersC(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
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

        public static Image GenerateGeoQRCode(double Lat, double Lon, Image img)
        {
            // https://en.wikipedia.org/wiki/Geo_URI_scheme
            // geo:37.786971,-122.399677
            string query = String.Format(System.Globalization.CultureInfo.InvariantCulture, "geo:{0},{1}", Lat, Lon );
            ThoughtWorks.QRCode.Codec.QRCodeEncoder qrCodeEncoder = new ThoughtWorks.QRCode.Codec.QRCodeEncoder();
            qrCodeEncoder.QRCodeEncodeMode = ThoughtWorks.QRCode.Codec.QRCodeEncoder.ENCODE_MODE.BYTE;
            qrCodeEncoder.QRCodeScale = 5;
            qrCodeEncoder.QRCodeVersion = 7;
            qrCodeEncoder.QRCodeErrorCorrect = ThoughtWorks.QRCode.Codec.QRCodeEncoder.ERROR_CORRECTION.M;
            Bitmap bmp = qrCodeEncoder.Encode(query);
            Bitmap bmpE = new Bitmap(bmp.Width + 32, bmp.Height + 32 + 20);
            Graphics g = Graphics.FromImage(bmpE);
            g.FillRectangle(Brushes.White, new Rectangle(0, 0, bmpE.Width, bmpE.Height));
            g.DrawImage(bmp, 16, 16);
            string text = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.000000} {1:0.000000}", Lat, Lon);
            Font ff = new Font("MS Sans Serif", 14, FontStyle.Bold);
            SizeF ms = g.MeasureString(text, ff);
            g.DrawString(text, ff, Brushes.Black, bmpE.Width / 2 - ms.Width / 2, bmpE.Height - 34);
            if (img != null)
            {
                g.FillRectangle(Brushes.White, new Rectangle(bmpE.Width / 2 - img.Width / 2 - 2, bmp.Height / 2 + 16 - img.Height / 2 - 2, img.Width + 4, img.Height + 4));
                g.DrawImage(img, bmpE.Width / 2 - img.Width / 2, bmp.Height / 2 + 16 - img.Height / 2);
            };
            g.Dispose();
            bmp.Dispose();
            return bmpE;
        }

        private void SelectOnMap(int id)
        {
            if (id < 0) return;

            mapSelect.Clear();
            if (mapContent[id].ObjectType == NaviMapNet.MapObjectType.mPolyline)
            {
                NaviMapNet.MapPolyLine mp = new NaviMapNet.MapPolyLine(mapContent[id].Points);
                mp.Name = "Selected";
                mp.Color = Color.FromArgb(100, Color.Blue);
                mp.Width = (mapContent[id] as NaviMapNet.MapPolyLine).Width + 4;
                mapSelect.Add(mp);
                MapViewer.DrawOnMapData();

                toolStripStatusLabel7.Text = "0";
                toolStripStatusLabel8.Text = "0";
            };
            if (mapContent[id].ObjectType == NaviMapNet.MapObjectType.mPolygon)
            {
                NaviMapNet.MapPolygon mp = new NaviMapNet.MapPolygon(mapContent[id].Points);
                mp.Name = "Selected";
                mp.Color = Color.FromArgb(100, Color.Blue);
                mp.Width = (mapContent[id] as NaviMapNet.MapPolygon).Width + 4;
                mapSelect.Add(mp);
                MapViewer.DrawOnMapData();

                toolStripStatusLabel7.Text = "0";
                toolStripStatusLabel8.Text = "0";
            };
            if (mapContent[id].ObjectType == NaviMapNet.MapObjectType.mPoint)
            {
                NaviMapNet.MapPoint mp = new NaviMapNet.MapPoint(mapContent[id].Center);
                mp.Name = "Selected";
                mp.SizePixels = new Size(22, 22);
                mp.Squared = false;
                mp.Color = Color.Fuchsia;
                mapSelect.Add(mp);
                MapViewer.DrawOnMapData();

                toolStripStatusLabel7.Text = mp.Center.Y.ToString("00.000000", System.Globalization.CultureInfo.InvariantCulture);
                toolStripStatusLabel8.Text = mp.Center.X.ToString("000.000000", System.Globalization.CultureInfo.InvariantCulture);
            };
        }

        private void objects_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != Convert.ToChar(Keys.Enter)) return;
            if (objects.SelectedIndices.Count == 0) return;            
            SelectOnMap(int.Parse(objects.SelectedItems[0].SubItems[7].Text));
        }

        private void открытьФайлToolStripMenuItem_Click(object sender, EventArgs e)
        {            
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select file";
            ofd.Filter = "Main supported files|*.kml;*.kmz;*.gpx;*.dat;*.wpt;*.gdb;*.fit;*.gpi|KML Format (*.kml)|*.kml|KMZ Format (*.kmz)|*.kmz|GPX Exchange Format (*.gpx)|*.gpx|ProGorod Favorites.dat (*.dat)|*.dat|OziExplorer Waypoint File (*.wpt)|*.wpt|Navitel Waypoints (*.gdb)|*.gdb|Garmin Ant Fit (*.fit)|*.fit|Garmin POI (*.gpi)|*.gpi";
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
            else if (this.fileExt == ".dat")
            {
                string ff = GetTempPath() + "doc.kml";
                File.Copy(filename, ff);
                ProGorodDat2KML(ff);
            }
            else if (this.fileExt == ".gdb")
            {
                string ff = GetTempPath() + "doc.kml";
                File.Copy(filename, ff);
                GarminGDB2KML(ff);
            }
            else if (this.fileExt == ".wpt")
            {
                string ff = GetTempPath() + "doc.kml";
                File.Copy(filename, ff);
                WPT2KML(ff);
            }
            else if (this.fileExt == ".gpx")
            {
                GPX2KML(filename);
            }
            else if (this.fileExt == ".fit")
            {
                string ff = GetTempPath() + "doc.kml";
                try
                {
                    FitParser.FitConverter.Fit2KML(filename, ff);
                }
                catch { };
            }
            else if (this.fileExt == ".gpi")
            {
                GPIReader.LOCALE_LANGUAGE = Properties["gpi_localization"].ToUpper();
                GPIReader.SAVE_MEDIA = Properties.GetBoolValue("gpireader_save_media");
                GPIReader.SAVE_MULTINAMES = Properties.GetBoolValue("gpireader_multinames_in_desc");
                GPIReader.CREATE_CATEGORY_IMAGES_IFNO = Properties.GetBoolValue("gpireader_create_cat_noimage");
                GPIReader.POI_IMAGE_FROM_JPEG = Properties.GetBoolValue("gpireader_poi_image_from_jpeg");
                try
                {
                    KMZ_Viewer.GPIReader gpir = new KMZ_Viewer.GPIReader(filename);
                    gpir.SaveToKML(GetTempPath() + "doc.kml");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Couldn't open gpi file!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                };
            }
            else
                UnZipKMZ(filename, GetTempPath());

            if (sPVToolStripMenuItem.CheckState == CheckState.Indeterminate)
                splitContainer1.Panel2Collapsed = false;

            mruF.AddFile(filename);

            OpenKML();
            LoadLayers();
            DrawCheckedLayers();
        }

        public void GPX2KML(string origin_name)
        {
            FileStream fs = new FileStream(origin_name, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            string xml = sr.ReadToEnd();
            sr.Close();
            fs.Close();

            xml = RemoveXMLNamespaces(xml);
            XmlDocument gpx = new XmlDocument();
            gpx.LoadXml(xml);

            fs = new FileStream(GetTempPath() + "doc.kml", FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
            sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sw.WriteLine("<kml>");
            sw.WriteLine("\t<Document>");
            sw.WriteLine("\t<name>" + Path.GetFileNameWithoutExtension(origin_name) + "</name>");
            sw.WriteLine("\t\t<Folder>");
            sw.WriteLine("\t\t<name>" + Path.GetFileNameWithoutExtension(origin_name) + "</name>");
            foreach (XmlNode wpt in gpx.SelectNodes("gpx/wpt"))
            {
                string nam = "";
                string desc = "";
                string xyz = wpt.Attributes["lon"].Value + "," + wpt.Attributes["lat"].Value + ",0";
                try { nam = wpt.SelectSingleNode("name").ChildNodes[0].Value; }
                catch { };
                try { desc = wpt.SelectSingleNode("desc").ChildNodes[0].Value; }
                catch { };
                sw.WriteLine("\t\t\t<Placemark>");
                sw.WriteLine("\t\t\t\t<name><![CDATA[" + nam + "]]></name>");
                sw.WriteLine("\t\t\t\t<description><![CDATA[" + desc + "]]></description>");
                sw.WriteLine("\t\t\t\t<styleUrl>#noicon</styleUrl>");
                sw.WriteLine("\t\t\t\t<Point><coordinates>" + xyz + "</coordinates></Point>");
                sw.WriteLine("\t\t\t</Placemark>");
            };
            foreach (XmlNode rte in gpx.SelectNodes("gpx/rte"))
            {
                string nam = "";
                string desc = "";
                try { nam = rte.SelectSingleNode("name").ChildNodes[0].Value; }
                catch { };
                try { desc = rte.SelectSingleNode("desc").ChildNodes[0].Value; }
                catch { };
                string xyz = "";
                foreach (XmlNode rtept in rte.SelectNodes("rtept"))
                    xyz += rtept.Attributes["lon"].Value + "," + rtept.Attributes["lat"].Value + ",0 ";
                sw.WriteLine("\t\t\t<Placemark>");
                sw.WriteLine("\t\t\t\t<name>" + nam + "</name>");
                sw.WriteLine("\t\t\t\t<description><![CDATA[" + desc + "]]></description>");
                sw.WriteLine("\t\t\t\t<LineString><coordinates>" + xyz + "</coordinates></LineString>");
                sw.WriteLine("\t\t\t</Placemark>");
            };
            foreach (XmlNode trk in gpx.SelectNodes("gpx/trk"))
            {
                string nam = "";
                string desc = "";
                try { nam = trk.SelectSingleNode("name").ChildNodes[0].Value; }
                catch { };
                try { desc = trk.SelectSingleNode("desc").ChildNodes[0].Value; }
                catch { };

                foreach (XmlNode trkseg in trk.SelectNodes("trkseg"))
                {
                    string xyz = "";
                    foreach (XmlNode trkpt in trkseg.SelectNodes("trkpt"))
                        xyz += trkpt.Attributes["lon"].Value + "," + trkpt.Attributes["lat"].Value + ",0 ";
                    sw.WriteLine("\t\t\t<Placemark>");
                    sw.WriteLine("\t\t\t\t<name>" + nam + "</name>");
                    sw.WriteLine("\t\t\t\t<description><![CDATA[" + desc + "]]></description>");
                    sw.WriteLine("\t\t\t\t<LineString><coordinates>" + xyz + "</coordinates></LineString>");
                    sw.WriteLine("\t\t\t</Placemark>");
                };
            };
            sw.WriteLine("\t\t</Folder>");
            sw.WriteLine("\t<Style id=\"noicon\"><IconStyle><Icon><href>images/noicon.png</href></Icon></IconStyle></Style>");
            sw.WriteLine("\t</Document>");
            sw.WriteLine("</kml>");
            sw.Close();
            fs.Close();
        }

        public void WPT2KML(string ff)
        {
            WPTPOI[] recs = WPTPOI.ReadFile(ff);

            FileStream fs = new FileStream(ff, FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
            sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sw.WriteLine("<kml>");
            sw.WriteLine("\t<Document>");
            sw.WriteLine("\t\t<Folder>");
            sw.WriteLine("\t\t<name>OziExplorer Waypoint File</name>");
            foreach (WPTPOI rec in recs)
            {
                string nam = rec.Name;
                string desc = rec.Description;
                string xyz = rec.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + rec.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",0";
                sw.WriteLine("\t\t\t<Placemark>");
                sw.WriteLine("\t\t\t\t<name><![CDATA[" + nam + "]]></name>");
                sw.WriteLine("\t\t\t\t<description><![CDATA[" + desc + "]]></description>");
                sw.WriteLine("\t\t\t\t<styleUrl>#noicon</styleUrl>");
                sw.WriteLine("\t\t\t\t<Point><coordinates>" + xyz + "</coordinates></Point>");
                sw.WriteLine("\t\t\t</Placemark>");
            };
            sw.WriteLine("\t\t</Folder>");
            sw.WriteLine("\t<Style id=\"noicon\"><IconStyle><Icon><href>images/noicon.png</href></Icon></IconStyle></Style>");
            sw.WriteLine("\t</Document>");
            sw.WriteLine("</kml>");
            sw.Close();
            fs.Close();
        }

        public void ProGorodDat2KML(string ff)
        {
            ProGorodPOI.FavRecord[] recs = ProGorodPOI.ReadFile(ff);

            FileStream fs = new FileStream(ff, FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
            sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sw.WriteLine("<kml>");
            sw.WriteLine("\t<Document>");
            sw.WriteLine("\t\t<Folder>");
            sw.WriteLine("\t\t<name>ProGorod Favorites</name>");
            foreach (ProGorodPOI.FavRecord rec in recs)
            {
                string nam = rec.Name;
                string desc = rec.Desc;
                string xyz = rec.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + rec.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",0";
                sw.WriteLine("\t\t\t<Placemark>");
                sw.WriteLine("\t\t\t\t<name><![CDATA[" + nam + "]]></name>");
                sw.WriteLine("\t\t\t\t<description><![CDATA[" + desc.Replace("&","&amp;") + "]]></description>");
                sw.WriteLine("\t\t\t\t<styleUrl>#progorod" + ((int)rec.Icon).ToString("00") + "</styleUrl>");
                sw.WriteLine("\t\t\t\t<Point><coordinates>" + xyz + "</coordinates></Point>");
                sw.WriteLine("\t\t\t</Placemark>");
            };
            sw.WriteLine("\t\t</Folder>");
            for (int i = 0; i < 20; i++)
            {
                System.Resources.ResourceManager rm = new global::System.Resources.ResourceManager("KMZViewer.Properties.Resources", typeof(KMZViewer.Properties.Resources).Assembly);
                object obj = rm.GetObject("progorod" + (i).ToString("00"), System.Globalization.CultureInfo.InvariantCulture);
                Bitmap bmp = ((System.Drawing.Bitmap)(obj));
                bmp.Save(ff.Replace("doc.kml",@"\images\progorod" + (i).ToString("00") + ".png"), ImageFormat.Png);
                sw.WriteLine("\t<Style id=\"progorod" + (i).ToString("00") + "\"><IconStyle><Icon><href>images/progorod" + (i).ToString("00") + ".png</href></Icon></IconStyle></Style>");
            };
            sw.WriteLine("\t</Document>");
            sw.WriteLine("</kml>");
            sw.Close();
            fs.Close();
        }

        public void GarminGDB2KML(string ff)
        {
            NavitelRecord[] recs = NavitelGDB.ReadFile(ff);

            //List<string> types = new List<string>();
            //for (int i = 0; i < 20; i++) types.Add(((ProGorodPOI.TType)i).ToString());

            FileStream fs = new FileStream(ff, FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
            sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sw.WriteLine("<kml>");
            sw.WriteLine("\t<Document>");
            sw.WriteLine("\t<name>Navitel GDB</name>");
            sw.WriteLine("\t\t<Folder>");
            sw.WriteLine("\t\t<name>Navitel GDB</name>");
            List<uint> iconlist = new List<uint>();
            foreach (NavitelRecord rec in recs)
            {
                string nam = rec.name;
                string desc = rec.desc;
                string xyz = rec.lon.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + rec.lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",0";
                sw.WriteLine("\t\t\t<Placemark>");
                sw.WriteLine("\t\t\t\t<name><![CDATA[" + nam + "]]></name>");
                sw.WriteLine("\t\t\t\t<description><![CDATA[" + desc + "]]></description>");
                sw.WriteLine("\t\t\t\t<styleUrl>#gdb" + (rec.iconIndex).ToString("000") + "</styleUrl>");
                sw.WriteLine("\t\t\t\t<Point><coordinates>" + xyz + "</coordinates></Point>");
                sw.WriteLine("\t\t\t</Placemark>");
                if (iconlist.IndexOf(rec.iconIndex) < 0) iconlist.Add(rec.iconIndex);
            };
            sw.WriteLine("\t\t</Folder>");
            string zipFile = KMZViewerForm.CurrentDirectory() + @"\gdbicons\gdb_icons.zip";
            for (int i = 0; i < iconlist.Count; i++)
            {
                if (File.Exists(zipFile))
                {
                    try
                    {
                        Image mi = ((Bitmap)GetImageFromZip(zipFile, (iconlist[i]).ToString("000") + ".png"));
                        mi.Save(ff.Replace("doc.kml", @"\images\gdb" + (iconlist[i]).ToString("000") + ".png"));
                    }
                    catch { };
                };
                sw.WriteLine("\t<Style id=\"gdb" + (iconlist[i]).ToString("000") + "\"><IconStyle><Icon><href>images/gdb" + (iconlist[i]).ToString("000") + ".png</href></Icon></IconStyle></Style>");
            };
            sw.WriteLine("\t</Document>");
            sw.WriteLine("</kml>");
            sw.Close();
            fs.Close();
        }
        public static Image GetImageFromZip(string zipfile, string imagefile)
        {
            try
            {
                FileStream fs = File.OpenRead(zipfile);
                ZipFile zf = new ZipFile(fs);
                int index = 0;
                foreach (ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile) continue; // Ignore directories
                    if (zipEntry.Name.ToLower() != imagefile.ToLower()) continue;

                    byte[] buffer = new byte[4096];     // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    try
                    {
                        Stream ms = new MemoryStream();
                        StreamUtils.Copy(zipStream, ms, buffer);
                        ms.Position = 0;
                        Image im = new Bitmap(ms);
                        ms.Dispose();
                        zf.Close();
                        fs.Close();
                        return im;
                    }
                    catch
                    {
                    };
                };
                zf.Close();
                fs.Close();
            }
            catch { };
            return null;
        }


        private string kmldocName = "";
        private string kmldocDesc = "";
        private void LoadLayers()
        {            
            // get doc name
            
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
            Sort(0);
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

            // Вытягиваем все вложеные папки (Folder) в документ (kml/Document)
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
                    doc = kmlDoc.SelectSingleNode("kml/Document"); // No Folders
                    if (doc == null)
                    {
                        doc = kmlDoc.SelectSingleNode("kml"); // No Document
                        if (doc != null)
                        {
                            doc.InnerXml = "<Document>" + doc.InnerXml + "</Document>";
                            doc = kmlDoc.SelectSingleNode("kml/Document/Folder");
                        };
                    }
                    else
                    {
                        string txt = doc.InnerXml;
                        XmlNode ns = kmlDoc.CreateElement("Folder");
                        ns.InnerXml = txt;
                        doc.AppendChild(ns);
                    };
                };
            };

            //вытягиваем все объекты без папки в папку
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
            {
                string xmlnsPattern = "<kml[^>]*?>";
                MatchCollection matchCol = Regex.Matches(outerXml, xmlnsPattern);
                foreach (Match match in matchCol)
                    outerXml = outerXml.Replace(match.ToString(), "<kml>");
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
            Sort(0);
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

                for(int bi = 0;bi<3;bi++)
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
                                string name = "NoName";
                                try { name = xns[i].SelectSingleNode("name").ChildNodes[0].Value; }
                                catch { };
                                string description = "";
                                try { description = xns[i].SelectSingleNode("description").ChildNodes[0].Value; }
                                catch { };

                                //LINE
                                if (bi == 1)
                                {                                    
                                    XmlNode cn = xns[i].SelectSingleNode("LineString/coordinates");
                                    if (cn != null)
                                    {
                                        toolStripStatusLabel1.Text = String.Format("Wait, creating {0} of {1} placemark", ++ttld, ttlc);
                                        if ((i % 35) == 0) statusStrip2.Update();

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
                                                        Convert.ToInt32(colval.Substring(6, 2), 16),
                                                        Convert.ToInt32(colval.Substring(4, 2), 16),
                                                        Convert.ToInt32(colval.Substring(2, 2), 16)
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
                                        foreach (string llzix in llza)
                                        {
                                            string llzi = llzix.Trim('\r').Trim('\n');
                                            if (String.IsNullOrEmpty(llzi)) continue;
                                            string[] llz = llzi.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                                            xy.Add(new PointF(float.Parse(llz[0], ni), float.Parse(llz[1], ni)));
                                        };

                                        NaviMapNet.MapPolyLine ml = new NaviMapNet.MapPolyLine(xy.ToArray());
                                        ml.Name = name;
                                        ml.UserData = new object[] { ci, i, name, description };
                                        ml.Color = lineColor;
                                        ml.Width = lineWidth;

                                        Image im = new Bitmap(16, 16);
                                        Graphics g = Graphics.FromImage(im);
                                        g.FillRectangle(new SolidBrush(lineColor), 0, 0, 16, 16);
                                        g.DrawString("L", new Font("Terminal", 11, FontStyle.Bold), new SolidBrush(Color.FromArgb(255 - lineColor.R, 255 - lineColor.G, 255 - lineColor.B)), 1, -1);
                                        g.Dispose();
                                        images.Images.Add(im);

                                        mapContent.Add(ml);
                                        ListViewItem lvi = objects.Items.Add(ml.Name, images.Images.Count - 1);
                                        lvi.SubItems.Add(String.Format("{0:00}", ci));
                                        lvi.SubItems.Add(String.Format("{0:00}", i));
                                        lvi.SubItems.Add("Line");
                                        lvi.SubItems.Add("-");
                                        lvi.SubItems.Add("-");
                                        lvi.SubItems.Add(description);
                                        lvi.SubItems.Add((objects.Items.Count - 1).ToString());
                                        lvi.SubItems.Add("");
                                    };
                                };

                                //POLYGON
                                if (bi == 0)
                                {                                    
                                    XmlNode cn = xns[i].SelectSingleNode("Polygon/outerBoundaryIs/LinearRing/coordinates");
                                    if (cn != null)
                                    {
                                        toolStripStatusLabel1.Text = String.Format("Wait, creating {0} of {1} placemark", ++ttld, ttlc);
                                        if ((i % 35) == 0) statusStrip2.Update();

                                        string[] llza = cn.ChildNodes[0].Value.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                                        string styleUrl = "";
                                        if (xns[i].SelectSingleNode("styleUrl") != null) styleUrl = xns[i].SelectSingleNode("styleUrl").ChildNodes[0].Value;
                                        if (styleUrl.IndexOf("#") == 0) styleUrl = styleUrl.Remove(0, 1);

                                        Color lineColor = Color.FromArgb(255, Color.Blue);
                                        int lineWidth = 3;
                                        Color fillColor = Color.FromArgb(255, Color.Blue);
                                        int fill = 1;

                                        XmlNode sl = null;
                                        XmlNode sf = null;
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
                                                sl = xd.SelectSingleNode("kml/Document/Style[@id='" + firstsid + "']/LineStyle");
                                            }
                                            catch { };
                                            try
                                            {
                                                sf = xd.SelectSingleNode("kml/Document/Style[@id='" + firstsid + "']/PolyStyle");
                                            }
                                            catch { };
                                        }
                                        else
                                        {
                                            sl = xns[i].SelectSingleNode("Style/LineStyle");
                                            sf = xns[i].SelectSingleNode("Style/PolyStyle");
                                        };
                                        if (sl != null)
                                        {
                                            string colval = sl.SelectSingleNode("color").ChildNodes[0].Value;
                                            try
                                            {
                                                lineColor = Color.FromName(colval);
                                                if (colval.Length == 8)
                                                {
                                                    lineColor = Color.FromArgb(
                                                        Convert.ToInt32(colval.Substring(0, 2), 16),
                                                        Convert.ToInt32(colval.Substring(6, 2), 16),
                                                        Convert.ToInt32(colval.Substring(4, 2), 16),
                                                        Convert.ToInt32(colval.Substring(2, 2), 16)
                                                        );
                                                };
                                            }
                                            catch { };
                                            string widval = sl.SelectSingleNode("width").ChildNodes[0].Value;
                                            try
                                            {
                                                lineWidth = (int)double.Parse(widval, ni);
                                                if (lineWidth < 2) lineWidth = 2;
                                            }
                                            catch { };
                                        };
                                        if (sf != null)
                                        {
                                            string colval = sf.SelectSingleNode("color").ChildNodes[0].Value;
                                            try
                                            {
                                                fillColor = Color.FromName(colval);
                                                if (colval.Length == 8)
                                                {
                                                    fillColor = Color.FromArgb(
                                                        Convert.ToInt32(colval.Substring(0, 2), 16),
                                                        Convert.ToInt32(colval.Substring(6, 2), 16),
                                                        Convert.ToInt32(colval.Substring(4, 2), 16),
                                                        Convert.ToInt32(colval.Substring(2, 2), 16)
                                                        );
                                                };
                                            }
                                            catch { };
                                            string fillval = sf.SelectSingleNode("fill").ChildNodes[0].Value;
                                            try
                                            {
                                                fill = int.Parse(fillval, ni);
                                            }
                                            catch { };
                                        };

                                        List<PointF> xy = new List<PointF>();
                                        foreach (string llzix in llza)
                                        {
                                            string llzi = llzix.Trim('\r').Trim('\n');
                                            if (String.IsNullOrEmpty(llzi)) continue;
                                            string[] llz = llzi.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                                            xy.Add(new PointF(float.Parse(llz[0], ni), float.Parse(llz[1], ni)));
                                        };

                                        NaviMapNet.MapPolygon mp = new NaviMapNet.MapPolygon(xy.ToArray());
                                        mp.Name = name;
                                        mp.UserData = description;
                                        mp.BorderColor = lineColor;
                                        mp.Width = lineWidth;
                                        mp.BodyColor = Color.FromArgb(0, fillColor);
                                        if (fill != 0)
                                            mp.BodyColor = fillColor;

                                        Image im = new Bitmap(16, 16);
                                        Graphics g = Graphics.FromImage(im);
                                        g.FillRectangle(new SolidBrush(fillColor), 0, 0, 16, 16);
                                        g.DrawRectangle(new Pen(new SolidBrush(lineColor), 2), 0, 0, 16, 16);
                                        g.DrawString("A", new Font("Terminal", 11, FontStyle.Bold), new SolidBrush(Color.FromArgb(255 - fillColor.R, 255 - fillColor.G, 255 - fillColor.B)), 1, -1);
                                        g.Dispose();
                                        images.Images.Add(im);

                                        mapContent.Add(mp);
                                        ListViewItem lvi = objects.Items.Add(mp.Name, images.Images.Count - 1);
                                        lvi.SubItems.Add(String.Format("{0:00}", ci));
                                        lvi.SubItems.Add(String.Format("{0:00}", i));
                                        lvi.SubItems.Add("Polygon");
                                        lvi.SubItems.Add("-");
                                        lvi.SubItems.Add("-");
                                        lvi.SubItems.Add(description);
                                        lvi.SubItems.Add((objects.Items.Count - 1).ToString());
                                        lvi.SubItems.Add("");
                                    };
                                };


                                // POINT
                                if (bi == 2)
                                {                                    
                                    XmlNode cn = xns[i].SelectSingleNode("Point/coordinates");
                                    if (cn != null)
                                    {
                                        toolStripStatusLabel1.Text = String.Format("Wait, creating {0} of {1} placemark", ++ttld, ttlc);
                                        if ((i % 35) == 0) statusStrip2.Update();

                                        string[] llz = cn.ChildNodes[0].Value.Replace("\r", "").Replace("\n", "").Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

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
                                        lvi.SubItems.Add((objects.Items.Count - 1).ToString());
                                        lvi.SubItems.Add("");
                                    };
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
            saveToRTFToolStripMenuItem.Enabled =
                copyToClipboardToolStripMenuItem.Enabled = 
                    sQRToolStripMenuItem.Enabled =
                        hideOnMapToolStripMenuItem.Enabled =
                            editPlacemarkXMLToolStripMenuItem.Enabled =
                                editPlacemarkXMLToolStripMenuItem1.Enabled =
                                    deletePlacemarkToolStripMenuItem.Enabled =
                                        objects.SelectedIndices.Count > 0;

            kmzfilterToolStripMenuItem.Enabled = 
            exp2repToolStripMenuItem.Enabled =
            exDATToolStripMenuItem.Enabled =
                exWPTToolStripMenuItem.Enabled =
                    sortToolStripMenuItem.Enabled =
                        exportToCSVToolStripMenuItem.Enabled =
                            sRTFLToolStripMenuItem.Enabled =
                                objects.Items.Count > 0;


            toolStripMenuItem20.Visible = true;
            kissSendToolStripMenuItem.Visible = true;
            xkissnmsToolStripMenuItem.Visible = true;
            xkissnmsToolStripMenuItem.Enabled = (objects.Items.Count > 0);
            kissSendToolStripMenuItem.Enabled = (objects.Items.Count > 0) && (aprsmode.SelectedIndex > 0) && (aprs_ison.Checked) && (kiss != null) && (kiss.Connected);            
            kissitToolStripMenuItem.Enabled = (objects.SelectedIndices.Count == 1) && (aprsmode.SelectedIndex > 0) && (aprs_ison.Checked) && (kiss != null) && (kiss.Connected);            

            hideOnMapToolStripMenuItem.Checked = false;
            if (objects.SelectedIndices.Count == 0) return;

            hideOnMapToolStripMenuItem.Checked = !mapContent[int.Parse(objects.SelectedItems[0].SubItems[7].Text)].Visible;            
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
                    NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)mapContent[int.Parse(objects.SelectedItems[0].SubItems[7].Text)];
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

            form.Text = "Изменить объект";
            nameText.Text = "Имя:";
            nameBox.Text = value;
            xText.Text = "Долгота:";
            xBox.Text = lon;
            yText.Text = "Широта:";
            yBox.Text = lat;
            dText.Text = "Описание:";
            dBox.Text = desc;

            if (!changeXY) xBox.Enabled = yBox.Enabled = false;

            xBox.KeyPress += new KeyPressEventHandler(xy_KeyPress);
            yBox.KeyPress += new KeyPressEventHandler(xy_KeyPress);

            buttonOk.Text = "OK";
            buttonCancel.Text = "Отмена";
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

            form.Text = "Изменить XML объекта";
            dBox.Text = value;
            dBox.BorderStyle = BorderStyle.FixedSingle;
            dBox.WordWrap = true;
            
            buttonOk.Text = "OK";
            buttonCancel.Text = "Отмена";
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
            buttonCancel.Text = "Отмена";
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
                NaviMapNet.MapObject mo = (NaviMapNet.MapObject)mapContent[int.Parse(objects.SelectedItems[0].SubItems[7].Text)];

                string name = "";
                n = xp.SelectSingleNode("name");
                if ((n != null) && (n.ChildNodes.Count > 0)) name = n.ChildNodes[0].Value;
                objects.SelectedItems[0].SubItems[0].Text = name;
                mo.Name = name;
                string desc = "";
                n = xp.SelectSingleNode("description");
                if ((n != null) && (n.ChildNodes.Count > 0)) desc = n.ChildNodes[0].Value;
                objects.SelectedItems[0].SubItems[6].Text = desc;

                if (point)
                {
                    n = xp.SelectSingleNode("Point/coordinates");
                    string[] llz = n.ChildNodes[0].Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    objects.SelectedItems[0].SubItems[4].Text = llz[1];
                    objects.SelectedItems[0].SubItems[5].Text = llz[0];
                    NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)mapContent[int.Parse(objects.SelectedItems[0].SubItems[7].Text)];
                    mp.Points[0].X = float.Parse(llz[0], ni);
                    mp.Points[0].Y = float.Parse(llz[1], ni);
                    MapViewer.DrawOnMapData();
                };                
            };
        }

        private void deletePlacemarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.SelectedIndices.Count == 0) return; 

            if (MessageBox.Show("Вы действительно хотите удалить текущий объект?", "Удаление объекта", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No) return;

            int d = int.Parse(objects.SelectedItems[0].SubItems[7].Text);
            int l = int.Parse(objects.SelectedItems[0].SubItems[1].Text);
            int p = int.Parse(objects.SelectedItems[0].SubItems[2].Text);
            bool point = objects.SelectedItems[0].SubItems[3].Text == "Point";
            XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
            XmlNode xp = xf.SelectNodes("Placemark")[p];

            NaviMapNet.MapObject mo = (NaviMapNet.MapObject)mapContent[d];            
            objects.Items.RemoveAt(objects.SelectedIndices[0]);
            xp.ParentNode.RemoveChild(xp);
            mapContent.Remove(mo.Index);
            MapViewer.DrawOnMapData();

            if(objects.Items.Count == 0) return;

            for (int i = 0; i < objects.Items.Count; i++)
            {
                int td = int.Parse(objects.Items[i].SubItems[7].Text);
                int tl = int.Parse(objects.Items[i].SubItems[1].Text);
                int tp = int.Parse(objects.Items[i].SubItems[2].Text);
                if (td > d) objects.Items[i].SubItems[7].Text = (td - 1).ToString();
                if ((tl == l) && (tp > p)) objects.Items[i].SubItems[2].Text = String.Format("{0:00}", tp - 1);
            };            
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
            if (MessageBox.Show("Вы действительно хотите удалить текущий слой?", "Удаление слоя", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No) return;
            
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

            DialogResult dr = InputBox("Слой", "Изменить имя слоя:", ref lname);
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

        private void сохранитьСлоиВФайлToolStripMenuItem_Click(object sender, EventArgs e)
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
            string comment_add = "";
            zipStream.SetComment("Google KMZ file For OruxMaps\r\n\r\n" + "Created at " + DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy") + "\r\nby " + this.Text + "\r\n\r\nUse OruxMaps for Android or KMZViewer for Windows to Explore file POI" + comment_add);
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

            bool vis = mapContent[int.Parse(objects.SelectedItems[0].SubItems[7].Text)].Visible;
            vis = !vis;
            mapContent[int.Parse(objects.SelectedItems[0].SubItems[7].Text)].Visible = vis;
            objects.Items[objects.SelectedIndices[0]].Font =
                new Font(objects.Items[objects.SelectedIndices[0]].Font, vis ? FontStyle.Regular : FontStyle.Strikeout);

            MapViewer.DrawOnMapData();
        }

        private string UserDefinedGetTileUrl(int x, int y, int z)
        {
            if (iStorages.SelectedIndex == (iStorages.Items.Count - 1)) 
                return SASPlanetCache(x, y, z + 1);
            return "";
        }

        private string SASPlanetCache(int x, int y, int z)
        {
            string basedir = String.Format(@"{1}\z{0}", z, SASCacheMapPath);
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
                        yy = ydir + @"\y" + y.ToString() + ".jpg";
                        if (File.Exists(yy))
                            return yy;
                        yy = ydir + @"\y" + y.ToString() + ".gif";
                        if (File.Exists(yy))
                            return yy;
                    };
                };
            };

            return "none";
        }        

        private void KMZViewerForm_Load(object sender, EventArgs e)
        {
            aprsmode.SelectedIndex = 0;

            // Map_Cache_Dirs.txt
            iCache.Items.Add("----- Map_Cache_Dirs.txt -----");
            string mcd = CurrentDirectory() + @"\Map_Cache_Dirs.txt";
            if (File.Exists(mcd))
            {
                try
                {
                    FileStream fs = new FileStream(mcd, FileMode.Open, FileAccess.Read);
                    StreamReader sr = new StreamReader(fs, System.Text.Encoding.GetEncoding(1251));
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (String.IsNullOrEmpty(line)) continue;
                        if (line.StartsWith("#")) continue;
                        if (line.StartsWith("@")) continue;
                        line = line.Replace("%CD%", CurrentDirectory());
                        string[] LP = line.Split(new char[] { '=' }, 2);
                        string prefix = "";
                        if (LP.Length > 1)
                        {
                            line = ClearLastSlash(LP[1]);
                            prefix = LP[0].Trim();
                        }
                        else
                            line = ClearLastSlash(LP[0]);
                        if (Directory.Exists(line))
                        {
                            SASItem si = new SASItem(line, prefix, "USER");
                            iCache.Items.Add(si);
                        };
                    };
                    sr.Close();
                    fs.Close();
                }
                catch
                { };
            };

            // SubDirectory MAPS
            iCache.Items.Add(@"----- %CD%\MAPS -----");
            string dMaps = CurrentDirectory() + @"\MAPS";
            if (Directory.Exists(dMaps))
            {
                string[] subdirs = Directory.GetDirectories(dMaps);
                if ((subdirs != null) && (subdirs.Length > 0))
                    foreach (string sub in subdirs)
                    {
                        SASItem si = new SASItem(sub, "", "SUB");
                        iCache.Items.Add(si);
                    };
            };

            // SASPlanet Cache Dir
            iCache.Items.Add(@"----- SASPlanet Cache Dir -----");
            if (Directory.Exists(state.SASCacheDir))
            {
                string[] subdirs = Directory.GetDirectories(state.SASCacheDir);
                if ((subdirs != null) && (subdirs.Length > 0))
                    foreach (string sub in subdirs)
                    {
                        SASItem si = new SASItem(sub, "", "SAS");
                        iCache.Items.Add(si);
                    };
            };

            // KMZ MRU List
            mruF = new MruPathList(CurrentDirectory() + @"\KMZViewer.fls", pofiles, 10, false);
            mruF.FileSelected += new MruPathList.FileSelectedEventHandler(mru3_FileSelected);

            // Cache Dirs MRU List
            mruD = new MruPathList(CurrentDirectory() + @"\KMZViewer.drs", pSI, 30, true);
            mruD.FileSelected += new MruPathList.FileSelectedEventHandler(mruD_DirSelected);
            mruD.FormatMenuItem += new MruPathList.FormatMenuItemTextEvent(mruD_GetFileNamePrefix);
            mruD.UpdateNames();

            // MBTiles Files MRUL List
            mruT = new MruPathList(CurrentDirectory() + @"\KMZViewer.mbt", prebMBTToolStripMenuItem, 30, false);
            mruT.FileSelected += new MruPathList.FileSelectedEventHandler(mruT_FileSelected);


            bool opFile = false;
            if ((args != null) && (args.Length > 0))
            {
                if (File.Exists(args[0]))
                {
                    OpenFile(args[0]);
                    opFile = true;
                };
            };

            if (state != null)
            {
                MapViewer.CenterDegreesX = state.X;
                MapViewer.CenterDegreesY = state.Y;
                MapViewer.ZoomID = (byte)state.Z;
                SASCacheMapPath = state.SASDir;
                UserDefinedURL = state.URL;
                UserDefindedFile = state.FILE;
                if (state.MapID < iStorages.Items.Count)
                    iStorages.SelectedIndex = state.MapID;
                //iStorages_SelectedIndexChanged(this, e);                
            };

            sPVToolStripMenuItem.CheckState = CheckState.Indeterminate;
            if (!opFile)
                splitContainer1.Panel2Collapsed = true;

            MapViewer.DrawMap = true;
            //MapViewer.ReloadMap();

            LoadXUN();

            //APRS CFG
            {
                aprs_cfg = APRSCFG.Load();          
                foreach (string hp in aprs_cfg.hipp)
                    aprs_h.Items.Add(hp);
                if(aprs_cfg.selected < aprs_h.Items.Count)
                    aprs_h.SelectedIndex = aprs_cfg.selected;
                aprs_u.Text = aprs_cfg.callsign;
                aprs_p.Text = aprs_cfg.password;
                aprs_filter.Text = aprs_cfg.filter;
                aprsmode.SelectedIndex = aprs_cfg.mode;
                aprs_h.Text = aprs_cfg.last;
            };

            PreLoadPlugins(openPlugInsToolStripMenuItem);
        }

        private void mruT_FileSelected(string file_name)
        {
            SelectMBTiles(file_name);
        }

        private Dictionary<string, string> plugins = new Dictionary<string, string>();
        private void PreLoadPlugins(ToolStripMenuItem topItem)
        {
            string pdir = CurrentDirectory() + @"\Plugins";
            if (!Directory.Exists(pdir)) return;
            string[] sdirs = Directory.GetDirectories(pdir);
            if ((sdirs == null) || (sdirs.Length == 0)) return;            
            foreach (string dir in sdirs)
            {
                string name = Path.GetFileName(dir);
                string ddir = name;
                string nmf = dir + @"\name.txt";
                if (File.Exists(nmf))
                {
                    try
                    {
                        FileStream fs = new FileStream(nmf, FileMode.Open, FileAccess.Read);
                        StreamReader sr = new StreamReader(fs, Encoding.UTF8);
                        name = sr.ReadToEnd();
                        sr.Close();
                        fs.Close();
                    }
                    catch { };
                };
                string[] fls = Directory.GetFiles(dir, "*.exe");
                if ((fls != null) && (fls.Length != 0))
                {
                    ToolStripMenuItem tsmi = new ToolStripMenuItem();
                    tsmi.Text = name;
                    topItem.DropDownItems.Add(tsmi);
                    tsmi.Click += new EventHandler(tsmi_Click);
                    plugins.Add(name, fls[0]);
                };
            };
            topItem.Visible = plugins.Count > 0;
        }

        private void tsmi_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem)
            {
                ToolStripMenuItem mi = (ToolStripMenuItem)sender;
                if (String.IsNullOrEmpty(mi.Text)) return;                
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(plugins[mi.Text]);
                double[] mm = MapViewer.MapBoundsMinMaxDegrees;
                psi.EnvironmentVariables.Add("MAPLEFT", mm[0].ToString(System.Globalization.CultureInfo.InvariantCulture));
                psi.EnvironmentVariables.Add("MAPBOTTOM", mm[1].ToString(System.Globalization.CultureInfo.InvariantCulture));
                psi.EnvironmentVariables.Add("MAPRIGHT", mm[2].ToString(System.Globalization.CultureInfo.InvariantCulture));
                psi.EnvironmentVariables.Add("MAPTOP", mm[3].ToString(System.Globalization.CultureInfo.InvariantCulture));                
                psi.UseShellExecute = false;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                string output = "";
                try
                {
                    KMZViewer.RunProcStdOutForm pf = new KMZViewer.RunProcStdOutForm(mi.Text);
                    pf.WriteLine("Starting plugin ...");
                    if (pf.StartProcAndShowWhileRunning(psi) != DialogResult.OK)
                        return;
                    output = pf.StdText;
                    pf.Dispose();                                                                               
                    if (!String.IsNullOrEmpty(output))
                    {
                        string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        string fn = lines[lines.Length - 1];
                        if (File.Exists(fn))
                        {
                            OpenFile(fn);
                            return;
                        };
                    };
                    if(output.IndexOf("Error") > 0)
                        MessageBox.Show("Plugin " + mi.Name + " return ERROR\r\n" + output, mi.Name, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    else
                        MessageBox.Show("Plugin " + mi.Name + " return nothing\r\n" + output, mi.Name, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Coldn't run plugin "+mi.Name+"\r\nError: "+ex.Message.ToString(), mi.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
            };
        }            

        private string mruD_GetFileNamePrefix(int index, FileInfo fi, ref Color color)
        {
            if (iCache.Items.Count == 0) return null;

            string pp = Path.GetFullPath(fi.FullName).ToLower();
            for (int i = 0; i < iCache.Items.Count; i++)
            {
                if (iCache.Items[i] is SASItem)
                {
                    SASItem si = (SASItem)iCache.Items[i];
                    if (Path.GetFullPath(si.Path.ToLower()) == pp)
                    {
                        if (si.Typ == "USER") color = Color.Green;
                        if (si.Typ == "SUB") color = Color.Blue;
                        if (si.Typ == "SAS") color = Color.Red;
                        return string.Format("&{0}: {1}", index, si.ToString());
                    };
                };
            };
            return null;
        }

        private void LoadXUN()
        {
            string fn = CurrentDirectory() + @"\Map_Places.txt";
            if (!File.Exists(fn)) return;
            FileStream fs = new FileStream(fn, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.GetEncoding(1251));
            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();
                if (line.StartsWith("#")) continue;
                if (line.StartsWith("@")) continue;
                if (line.Length < 5) continue;
                string[] xyn = line.Split(new char[] { ' ' }, 3);
                try
                {
                    double la = double.Parse(xyn[0], System.Globalization.CultureInfo.InvariantCulture);
                    double lo = double.Parse(xyn[1], System.Globalization.CultureInfo.InvariantCulture);
                    ListViewItem lvi = new ListViewItem(new string[]{xyn[0],xyn[1],xyn[2]});
                    listView1.Items.Add(lvi);
                }
                catch { };
            };
            sr.Close();
            fs.Close();
        }

        private void mru3_FileSelected(string file_name)
        {
            OpenFile(file_name);
        }

        // Compares two ListView items based on a selected column.
        public class ListViewComparer : System.Collections.IComparer
        {
            private int ColumnNumber;
            private SortOrder SortOrder;

            public ListViewComparer(int column_number,
                SortOrder sort_order)
            {
                ColumnNumber = column_number;
                SortOrder = sort_order;
            }

            // Compare two ListViewItems.
            public int Compare(object object_x, object object_y)
            {
                // Get the objects as ListViewItems.
                ListViewItem item_x = object_x as ListViewItem;
                ListViewItem item_y = object_y as ListViewItem;

                // Get the corresponding sub-item values.
                string string_x;
                if (item_x.SubItems.Count <= ColumnNumber)
                {
                    string_x = "";
                }
                else
                {
                    string_x = item_x.SubItems[ColumnNumber].Text;
                }

                string string_y;
                if (item_y.SubItems.Count <= ColumnNumber)
                {
                    string_y = "";
                }
                else
                {
                    string_y = item_y.SubItems[ColumnNumber].Text;
                }

                // Compare them.
                int result;
                double double_x, double_y;
                if (double.TryParse(string_x, out double_x) &&
                    double.TryParse(string_y, out double_y))
                {
                    // Treat as a number.
                    result = double_x.CompareTo(double_y);
                }
                else
                {
                    DateTime date_x, date_y;
                    if (DateTime.TryParse(string_x, out date_x) &&
                        DateTime.TryParse(string_y, out date_y))
                    {
                        // Treat as a date.
                        result = date_x.CompareTo(date_y);
                    }
                    else
                    {
                        // Treat as a string.
                        result = string_x.CompareTo(string_y);
                    }
                }

                // Return the correct result depending on whether
                // we're sorting ascending or descending.
                if (SortOrder == SortOrder.Ascending)
                {
                    return result;
                }
                else
                {
                    return -result;
                }
            }
        }

        public class SASItem
        {
            public string Path;
            public string Name;
            public string Prefix;
            public string Typ;

            public override string ToString()
            {
                return (String.IsNullOrEmpty(Prefix) ? "" : Prefix + ": ") + Name;
            }

            public SASItem(string Path, string Prefix, string Typ)
            {
                this.Path = Path;
                this.Name = Path.Substring(Path.LastIndexOf(@"\")+1);
                this.Prefix = Prefix;
                this.Typ = Typ;
            }
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
 
        private void toolStripDropDownButton1_DropDownOpening(object sender, EventArgs e)
        {
            pofiles.Enabled = mruF.Count > 0;
        }

        private void выбратьПапкуToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string spcd = SASCacheMapPath;
            if (System.Windows.Forms.InputBox.QueryDirectoryBox("SAS Planet Cache", "Enter Cache Path Here:", ref spcd) == DialogResult.OK)
            {
                SASCacheMapPath = ClearLastSlash(spcd);
                mruD.AddFile(SASCacheMapPath);
                if (iStorages.SelectedIndex == (iStorages.Items.Count - 1))
                    iStorages_SelectedIndexChanged(this, null);
                else
                    iStorages.SelectedIndex = iStorages.Items.Count - 1;
            };
        }

        private void contextMenuStrip3_Opening(object sender, CancelEventArgs e)
        {
            pSI.Enabled = (mruD != null) && (mruD.Count > 0);
            prebMBTToolStripMenuItem.Enabled = (mruT != null) && (mruT.Count > 0);
        }

        private void sPVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if ((sPVToolStripMenuItem.CheckState == CheckState.Indeterminate) && (!splitContainer1.Panel2Collapsed)) sPVToolStripMenuItem.Checked = true;
            if ((sPVToolStripMenuItem.CheckState == CheckState.Indeterminate) && (splitContainer1.Panel2Collapsed)) sPVToolStripMenuItem.Checked = false;
            sPVToolStripMenuItem.Checked = !(splitContainer1.Panel2Collapsed = sPVToolStripMenuItem.Checked);
        }

        private void sasCache_SelectPath(string file_name)
        {
            SASCacheMapPath = file_name;
            mruD.AddFile(file_name);
            if (iStorages.SelectedIndex == (iStorages.Items.Count - 1))
                iStorages_SelectedIndexChanged(this, null);
            else
                iStorages.SelectedIndex = iStorages.Items.Count - 1;
        }             

        private void KMZViewerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            callTCPTHreadStop();

            if (groute != null) groute.Save();

            string scd = state.SASCacheDir;
            state = new State();
            state.X = MapViewer.CenterDegreesX;
            state.Y = MapViewer.CenterDegreesY;
            state.Z = MapViewer.ZoomID;
            state.MapID = iStorages.SelectedIndex;
            state.SASDir = SASCacheMapPath;
            state.URL = UserDefinedURL;
            state.FILE = UserDefindedFile;
            state.SASCacheDir = scd;
            string fn = CurrentDirectory()+@"\KMZViewer.stt";
            State.Save(fn,state);

            Properties.Save();

            Save2APRSLast();
        }

        private void closeFToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RecreateTemp();
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != (Char)13) return;
            if (objects.Items.Count == 0) return;
            string st = textBox2.Text.ToLower();
            List<int> foundAll = new List<int>();
            for (int i = 0; i < objects.Items.Count; i++)
                if (objects.Items[i].SubItems[0].Text.ToLower().Contains(st))
                    foundAll.Add(i);

            if (foundAll.Count == 0) return;

            int si = -1;
            if(objects.SelectedIndices.Count > 0)
                si = foundAll.IndexOf(int.Parse(objects.SelectedItems[0].SubItems[7].Text));
            if ((si >= 0) && (si < (foundAll.Count - 1)))
            {
                objects.Items[foundAll[si + 1]].Selected = true;
                objects.Items[foundAll[si + 1]].EnsureVisible();
            }
            else
            {
                objects.Items[foundAll[0]].Selected = true;
                objects.Items[foundAll[0]].EnsureVisible();
            };
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != (Char)13) return;
            if (layers.Items.Count == 0) return;
            string st = textBox1.Text.ToLower();
            List<int> foundAll = new List<int>();
            for (int i = 0; i < layers.Items.Count; i++)
                if (layers.Items[i].ToString().ToLower().Contains(st))
                    foundAll.Add(i);

            if (foundAll.Count == 0) return;

            int si = foundAll.IndexOf(layers.SelectedIndex);
            if ((si >= 0) && (si<(foundAll.Count-1)))
                layers.SelectedIndex = foundAll[si+1];
            else
                layers.SelectedIndex = foundAll[0];
        }

        private void fullScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fullScreenToolStripMenuItem.Checked = !fullScreenToolStripMenuItem.Checked;
            GoFullscreen(fullScreenToolStripMenuItem.Checked);
        }

        private FormWindowState prevState;
        private Rectangle prevBounds;
        private void GoFullscreen(bool fullscreen)
        {
            if (fullscreen)
            {
                this.prevState = this.WindowState;
                this.prevBounds = this.Bounds;
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                this.Bounds = Screen.PrimaryScreen.Bounds;
            }
            else
            {
                this.WindowState = this.prevState;
                this.Bounds = this.prevBounds;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            }
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 0) return;

            try
            {
                double la = double.Parse(listView1.SelectedItems[0].SubItems[0].Text, System.Globalization.CultureInfo.InvariantCulture);
                double lo = double.Parse(listView1.SelectedItems[0].SubItems[1].Text, System.Globalization.CultureInfo.InvariantCulture);
                MapViewer.CenterDegrees = new PointF((float)lo, (float)la);
            }
            catch { };
        }

        private void iCache_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (iCache.SelectedIndex < 0) return;            
            if (!(iCache.SelectedItem is SASItem)) { iCache.SelectedIndex = -1; return; };

            SASItem itm = (SASItem)iCache.SelectedItem;
            string mp = "-1";
            string cp = "-2";
            
            try
            {
                mp = Path.GetFullPath(MapViewer.ImageSourceUrl.ToLower());
                cp = Path.GetFullPath(itm.Path.ToLower());
            }
            catch {};

            if(mp != cp)
                sasCache_SelectPath(itm.Path);
        }

        private void iStorages_DropDown(object sender, EventArgs e)
        {
            iStorages.BackColor = Color.White;
        }

        private void iStorages_DropDownClosed(object sender, EventArgs e)
        {
            if (iStorages.SelectedIndex < (iStorages.Items.Count - 1))
                iStorages.BackColor = Color.White;
            else
                iStorages.BackColor = Color.FromArgb(224, 224, 224);
        }

        private void iCache_DropDown(object sender, EventArgs e)
        {
            iCache.BackColor = Color.White;
        }

        private void iCache_DropDownClosed(object sender, EventArgs e)
        {
            if (iStorages.SelectedIndex < (iStorages.Items.Count - 1))
                iCache.BackColor = Color.FromArgb(224, 224, 224);
            else
                iCache.BackColor = Color.White;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            contextMenuStrip3.Show(button6, 2, 2);
        }
        
        private void KMZViewerForm_Resize(object sender, EventArgs e)
        {
            iStorages.Refresh();
            iCache.Refresh();
        }

        private void iCache_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1) return;

            string text = ((ComboBox)sender).Items[e.Index].ToString();

            bool selected = e.BackColor != ((ComboBox)sender).BackColor;

            Color itemForegroundColor = new Color();
            itemForegroundColor = ((ComboBox)sender).ForeColor;
            Color itemBackgroundColor = new Color();
            itemBackgroundColor = ((ComboBox)sender).BackColor;

            StringFormat format = new StringFormat();
            format.Alignment = StringAlignment.Near;            
            if (text.StartsWith("-"))
            {
                itemForegroundColor = Color.Silver;
                itemBackgroundColor = SystemColors.Window;
                if (selected)
                {
                    itemForegroundColor = SystemColors.Window;
                    itemBackgroundColor = Color.Silver;
                };
                format.Alignment = StringAlignment.Center;
            };
            if (((ComboBox)sender).Items[e.Index] is SASItem)
            {
                SASItem si = (SASItem)((ComboBox)sender).Items[e.Index];
                if (si.Typ == "USER") itemForegroundColor = Color.Green;
                if (si.Typ == "SUB") itemForegroundColor = Color.Blue;
                if (si.Typ == "SAS") itemForegroundColor = Color.Red;
                if (selected && ((ComboBox)sender).DroppedDown)
                {                    
                    text = si.Path;
                    SizeF ss = e.Graphics.MeasureString((string.IsNullOrEmpty(si.Prefix) ? "" : si.Prefix + ": ") + text, e.Font);
                    while ((ss.Width > e.Bounds.Width) && (text.Length > 0))
                    {
                        text = text.Remove(0, 1);
                        ss = e.Graphics.MeasureString((string.IsNullOrEmpty(si.Prefix) ? "" : si.Prefix + ": ") + text, e.Font);
                    };
                    text = (string.IsNullOrEmpty(si.Prefix) ? "" : si.Prefix + ": ") + text;
                };
            };                   
     
            e.DrawBackground();
            e.Graphics.FillRectangle(new SolidBrush(selected ? itemForegroundColor : itemBackgroundColor), e.Bounds);
            e.Graphics.DrawString(text, e.Font, new SolidBrush(selected ? itemBackgroundColor : itemForegroundColor), e.Bounds, format);
            e.DrawFocusRectangle();
        }

        private void iStorages_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1) return;

            string text = ((ComboBox)sender).Items[e.Index].ToString();
            int lastIndex = ((ComboBox)sender).Items.Count - 1;

            Color itemForegroundColor = new Color();
            itemForegroundColor = Color.Black;
            bool selected = e.BackColor != ((ComboBox)sender).BackColor;

            if (e.Index == 0)
                itemForegroundColor = Color.Silver;
            else if (e.Index < (lastIndex - 2))
                itemForegroundColor = Color.Black;
            else if (e.Index == (lastIndex - 2))
            {
                itemForegroundColor = Color.Crimson;
                if (selected && ((ComboBox)sender).DroppedDown)
                    text = UserDefindedFile;
                else
                {
                    string txt = UserDefindedFile;
                    SizeF sf = e.Graphics.MeasureString("FILE: .. " + txt, e.Font);
                    while (sf.Width > e.Bounds.Width)
                    {
                        txt = txt.Remove(0, 1);
                        sf = e.Graphics.MeasureString("FILE: .. " + txt, e.Font);
                    };
                    text = "FILE: .. " + txt;
                };
            }
            else if (e.Index == lastIndex)
            {
                itemForegroundColor = Color.DarkViolet;
                if (selected && ((ComboBox)sender).DroppedDown)
                    text = SASCacheMapPath;
                else
                {
                    string txt = SASCacheMapPath;
                    SizeF sf = e.Graphics.MeasureString("PATH: .. " + txt, e.Font);
                    while (sf.Width > e.Bounds.Width)
                    {
                        txt = txt.Remove(0, 1);
                        sf = e.Graphics.MeasureString("PATH: .. " + txt, e.Font);
                    };
                    text = "PATH: .. " + txt;
                };
            }
            else
            {
                itemForegroundColor = Color.Green;
                if (selected && ((ComboBox)sender).DroppedDown)
                    text = UserDefinedURL;
                else
                {
                    string txt = UserDefinedURL;
                    SizeF sf = e.Graphics.MeasureString("URL: .. " + txt, e.Font);
                    while (sf.Width > e.Bounds.Width)
                    {
                        txt = txt.Remove(0, 1);
                        sf = e.Graphics.MeasureString("URL: .. " + txt, e.Font);
                    };
                    text = "URL: .. " + txt;
                };
            };

            e.DrawBackground();
            e.Graphics.FillRectangle(new SolidBrush(selected ? itemForegroundColor : ((ComboBox)sender).BackColor), e.Bounds);
            e.Graphics.DrawString(text, e.Font, new SolidBrush(selected ? ((ComboBox)sender).BackColor : itemForegroundColor), e.Bounds);
            e.DrawFocusRectangle();
        }

        private void uRLChangeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string udut = UserDefinedURL;
            if (InputBox("Карта по User-Defined Url", "Введите Url карты  (http://my.map/{z}/{x}/{y}.png) :", ref udut) == DialogResult.OK)
            {
                UserDefinedURL = udut;
                if (iStorages.SelectedIndex == (iStorages.Items.Count - 2))
                    iStorages_SelectedIndexChanged(sender, e);
                else
                    iStorages.SelectedIndex = iStorages.Items.Count - 2;
            };
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            pictureBox2.Visible = checkBox1.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            pictureBox1.Visible = checkBox2.Checked;
        }

        private byte sortOrder = 0;

        private void поУмолчаниюToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Sort(0);
        }

        private void поАлфавитуToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Sort(sortOrder == 1 ? (byte)2 : (byte)1);
        }

        private void поУдалениюОтЦентраКартыToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Sort(3);
        }

        private void Sort(byte order)
        {
            sortOrder = order;
            so_0.Checked = sortOrder == 0;
            so_1.Checked = (sortOrder == 1) || (sortOrder == 2);
            so_3.Checked = sortOrder == 3;
            so_4.Checked = sortOrder == 4;
            so_5.Checked = sortOrder == 5;
            so_6.Checked = sortOrder == 6;
            so_7.Checked = sortOrder == 7;

            objects.Sorting = SortOrder.None;
            if (objects.Items.Count == 0) return;

            for (int i = 0; i < objects.Items.Count; i++)
                objects.Items[i].SubItems[8].Text = "";

            if (sortOrder == 0)
            {
                objects.ListViewItemSorter = new Objs0Sorter();
                objects.Sorting = SortOrder.Ascending;                
            }
            else if (sortOrder == 1)
            {
                objects.ListViewItemSorter = new Objs1Sorter();
                objects.Sorting = SortOrder.Ascending;
            }
            else if (sortOrder == 2)
            {
                objects.ListViewItemSorter = new Objs2Sorter();
                objects.Sorting = SortOrder.Ascending;
            }
            else if (sortOrder == 3)
            {
                objects.ListViewItemSorter = new Objs34Sorter(MapViewer.CenterDegreesX, MapViewer.CenterDegreesY, mapContent);
                objects.Sorting = SortOrder.Ascending;
                Sort34(MapViewer.CenterDegreesX, MapViewer.CenterDegreesY);
            }
            else if (sortOrder == 4)
            {
                NaviMapNet.MapObject mo = mapContent[int.Parse(objects.SelectedItems[0].SubItems[7].Text)];
                objects.ListViewItemSorter = new Objs34Sorter(mo.Center.X, mo.Center.Y, mapContent);
                objects.Sorting = SortOrder.Ascending;
                Sort34(mo.Center.X, mo.Center.Y);
            }
            else if (sortOrder == 5)
            {
                objects.ListViewItemSorter = new ObjsNSorter();
                objects.Sorting = SortOrder.Ascending;
            }
            else if (sortOrder == 6)
            {
                objects.ListViewItemSorter = new ObjsNASorter();
                objects.Sorting = SortOrder.Ascending;
            }
            else if (sortOrder == 7)
            {
                objects.ListViewItemSorter = new ObjsNDSorter();
                objects.Sorting = SortOrder.Ascending;
            };

        }

        private void Sort34(double X, double Y)
        {
            if (objects.Items.Count == 0) return;
            for (int i = 0; i < objects.Items.Count; i++)
            {
                int ind = int.Parse(objects.Items[i].SubItems[7].Text);
                NaviMapNet.MapObject mo = mapContent[ind];
                double l = GetLengthMetersC(Y, X, mo.Center.Y, mo.X, false) / 1000.0;
                double db = DegreeBearing(Y, X, mo.Center.Y, mo.Center.X);
                string dw = DegreesToCardinalDetailed(db);
                objects.Items[i].SubItems[8].Text = String.Format("{0:0.00} km - {1:0}° {2}", l, db, dw);
            };
        }

        public class Objs0Sorter : IComparer
        {
            public Objs0Sorter() { }
            public int Compare(object A, object B)
            {
                ListViewItem a = (ListViewItem)A;
                ListViewItem b = (ListViewItem)B;
                int pa = int.Parse(a.SubItems[7].Text);
                int pb = int.Parse(b.SubItems[7].Text);
                return pa.CompareTo(pb);
            }
        }

        public class ObjsLSorter : IComparer
        {
            private PointF[] route = null;
            private XmlDocument xd = null;
            private bool fromStart = false;
            private ObjsLSorter() { }
            public static ObjsLSorter Nearest(PointF[] route, XmlDocument xd)
            {
                ObjsLSorter res = new ObjsLSorter();
                res.route = route;
                res.fromStart = false;
                res.xd = xd;
                return res;
            }
            public static ObjsLSorter FromStart(PointF[] route, XmlDocument xd)
            {
                ObjsLSorter res = new ObjsLSorter();
                res.route = route;
                res.fromStart = true;
                res.xd = xd;
                return res;
            }
            public int Compare(object A, object B)
            {
                float dfsa = float.MaxValue;
                float dfsb = float.MaxValue;

                ListViewItem a = (ListViewItem)A;
                ListViewItem b = (ListViewItem)B;

                {
                    int l = int.Parse(a.SubItems[1].Text, System.Globalization.CultureInfo.InvariantCulture);
                    int p = int.Parse(a.SubItems[2].Text, System.Globalization.CultureInfo.InvariantCulture);
                    XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
                    XmlNode xp = xf.SelectNodes("Placemark")[p];

                    XmlNode xn = xp.SelectNodes("*/coordinates")[0];
                    string[] xy = xn.ChildNodes[0].Value.Trim('\n').Trim('\r').Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        string[] xyz = xy[0].Split(new char[] { ',' }, StringSplitOptions.None);
                        PointF cp = new PointF((float)double.Parse(xyz[0], System.Globalization.CultureInfo.InvariantCulture), (float)double.Parse(xyz[1], System.Globalization.CultureInfo.InvariantCulture));
                        float d2l = PolyLineBufferSimple.PolyLineBufferCrsimp.DistanceFromPointToRoute(cp, route, PolyLineBufferSimple.PolyLineBufferCrsimp.GeographicDistFunc, out dfsa);
                        if (!fromStart) dfsa = d2l;
                    }
                    catch { };
                };

                {
                    int l = int.Parse(b.SubItems[1].Text, System.Globalization.CultureInfo.InvariantCulture);
                    int p = int.Parse(b.SubItems[2].Text, System.Globalization.CultureInfo.InvariantCulture);
                    XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
                    XmlNode xp = xf.SelectNodes("Placemark")[p];

                    XmlNode xn = xp.SelectNodes("*/coordinates")[0];
                    string[] xy = xn.ChildNodes[0].Value.Trim('\n').Trim('\r').Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        string[] xyz = xy[0].Split(new char[] { ',' }, StringSplitOptions.None);
                        PointF cp = new PointF((float)double.Parse(xyz[0], System.Globalization.CultureInfo.InvariantCulture), (float)double.Parse(xyz[1], System.Globalization.CultureInfo.InvariantCulture));
                        float d2l = PolyLineBufferSimple.PolyLineBufferCrsimp.DistanceFromPointToRoute(cp, route, PolyLineBufferSimple.PolyLineBufferCrsimp.GeographicDistFunc, out dfsb);
                        if (!fromStart) dfsb = d2l;
                    }
                    catch { };
                };

                return dfsa.CompareTo(dfsb);
            }
        }

        public class Objs1Sorter : IComparer
        {
            public Objs1Sorter() { }
            public int Compare(object A, object B)
            {
                ListViewItem a = (ListViewItem)A;
                ListViewItem b = (ListViewItem)B;
                return string.Compare(a.Text, b.Text);
            }
        }

        public class ObjsNDSorter : IComparer
        {
            public ObjsNDSorter() { }
            public int Compare(object A, object B)
            {
                ListViewItem a = (ListViewItem)A;
                ListViewItem b = (ListViewItem)B;
                string ap = "2"; if (a.SubItems[3].Text == "Polygon") ap = "0"; if (a.SubItems[3].Text == "Line") ap = "1";
                string bp = "2"; if (b.SubItems[3].Text == "Polygon") bp = "0"; if (b.SubItems[3].Text == "Line") bp = "1";
                return string.Compare(ap + a.SubItems[1].Text + a.SubItems[2].Text, bp + b.SubItems[1].Text + b.SubItems[2].Text);
            }
        }

        public class ObjsNSorter : IComparer
        {
            public ObjsNSorter() { }
            public int Compare(object A, object B)
            {
                ListViewItem a = (ListViewItem)A;
                ListViewItem b = (ListViewItem)B;
                string ap = "2"; if (a.SubItems[3].Text == "Polygon") ap = "0"; if (a.SubItems[3].Text == "Line") ap = "1";
                string bp = "2"; if (b.SubItems[3].Text == "Polygon") bp = "0"; if (b.SubItems[3].Text == "Line") bp = "1";
                return string.Compare(ap + a.SubItems[1].Text + a.Text, bp + b.SubItems[1].Text + b.Text);
            }
        }

        public class ObjsNASorter : IComparer
        {
            public ObjsNASorter() { }
            public int Compare(object A, object B)
            {
                ListViewItem a = (ListViewItem)A;
                ListViewItem b = (ListViewItem)B;
                string ap = "2"; if (a.SubItems[3].Text == "Polygon") ap = "0"; if (a.SubItems[3].Text == "Line") ap = "1";
                string bp = "2"; if (b.SubItems[3].Text == "Polygon") bp = "0"; if (b.SubItems[3].Text == "Line") bp = "1";
                return string.Compare(ap + a.Text, bp + b.Text);
            }
        }

        public class Objs2Sorter : IComparer
        {
            public Objs2Sorter() { }
            public int Compare(object A, object B)
            {
                ListViewItem a = (ListViewItem)A;
                ListViewItem b = (ListViewItem)B;
                return string.Compare(b.Text, a.Text);
            }
        }

        public class Objs34Sorter : IComparer
        {
            private double x;
            private double y;
            private NaviMapNet.MapLayer layer;

            public Objs34Sorter(double x, double y, NaviMapNet.MapLayer objectsLayer) { this.x = x; this.y = y; this.layer = objectsLayer; }
            public int Compare(object A, object B)
            {
                int a = int.Parse(((ListViewItem)A).SubItems[7].Text);
                int b = int.Parse(((ListViewItem)B).SubItems[7].Text);
                double da = GetLengthMetersC(y, x, layer[a].Center.Y, layer[a].Center.X, false);
                double db = GetLengthMetersC(y, x, layer[b].Center.Y, layer[b].Center.X, false);
                return da.CompareTo(db);
            }
        }

        public class ObjsOSMSorter : IComparer
        {
            public ObjsOSMSorter() { }
            public int Compare(object A, object B)
            {
                double a = double.Parse(((ListViewItem)A).SubItems[6].Text, System.Globalization.CultureInfo.InvariantCulture);
                double b = double.Parse(((ListViewItem)B).SubItems[6].Text, System.Globalization.CultureInfo.InvariantCulture);
                if (a > b)
                    return 1;
                else if (a < b)
                    return -1;
                else
                    return 0;
            }
        }

        private void so_4_Click(object sender, EventArgs e)
        {
            Sort(4);
        }

        private void objects_KeyPress_1(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != '\r') return;

            if (objects.SelectedItems.Count == 0) return;

            SelectOnMap(int.Parse(objects.SelectedItems[0].SubItems[7].Text));
        }

        private bool FR24_GRAB_IS_ON = false;
        private void FR24On_CheckedChanged(object sender, EventArgs e)
        {
            FR24_GRAB_IS_ON = FR24On.Checked;
            if (FR24On.Checked)
                FR24Counter = -1;
        }

        private DateTime lastConnCheck = DateTime.UtcNow;
        private bool lastConnState = false;
        private void FR24Tick_Tick(object sender, EventArgs e)
        {
            FR24Tick.Enabled = false;

            // APRS
            {                
                if ((APRSMode > 0) && (kiss != null))
                {
                    if ((!lastConnState) || (DateTime.UtcNow.Subtract(lastConnCheck).TotalSeconds > 30)) 
                    {
                        lastConnState = kiss.Connected;
                        lastConnCheck = DateTime.UtcNow;
                    };
                    if ((tcpSttOk != 1) && (!lastConnState))
                    {
                        tcpState = "Cбой подключения к " + aprs_h.Text;
                        tcpSttOk = 1;
                    };
                    if ((tcpSttOk == 1) && lastConnState)
                    {
                        tcpSttOk = 2;
                        tcpState = "Подключен к " + aprs_h.Text;
                    };                    
                };
                aprs_state.Text = "APRS: " + tcpState;
                
                Buddies.mtx.WaitOne();
                if (Buddies.updates.Count > 0)
                    APRS_Draw(Buddies.updates.ToArray());                    
                if ((APRS_Selected != null) && (Buddies.updates.IndexOf(APRS_Selected) < 0) && (Buddies.list.ContainsKey(APRS_Selected)))
                {
                    TimeSpan ts = DateTime.UtcNow.Subtract(Buddies.list[APRS_Selected].last);
                    string lapse = ts.TotalHours.ToString("00") + " ч " + ts.Minutes.ToString("00") + " м " + ts.Seconds.ToString("00") + " с";
                    aprs_info.Items[4].SubItems[1].Text = lapse;
                };
                Buddies.updates.Clear();
                Buddies.mtx.ReleaseMutex();

                if (tcpSttOk == 2)
                {
                    tcpSttOk = 0;                    
                    string a = aprs_h.Text.Trim();
                    int ind = aprs_h.Items.IndexOf(a);
                    if (ind >= 0)
                        aprs_h.Items.RemoveAt(ind);
                    aprs_h.Items.Add(a);
                    aprs_h.SelectedIndex = aprs_h.Items.Count - 1;

                    Save2APRSLast();
                };
            };

            if (dispon.Checked)
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED); // no hibernate, display on
            else
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED); // no hibernate

            // FR24
            {                
                if ((FR24_GRAB_IS_ON) && (FRGI == null))
                {
                    FRGI = new Image[4];
                    FRGI[cAirCraftNormal] = Image.FromFile(CurrentDirectory() + @"\aircraft.png");
                    FRGI[cAirCraftBad] = Image.FromFile(CurrentDirectory() + @"\aircraftbad.png");
                    FRGI[cAirCraftOldBad] = Image.FromFile(CurrentDirectory() + @"\aircraftdbl.png");
                    FRGI[cAirCraftOld] = Image.FromFile(CurrentDirectory() + @"\aircraftold.png");
                };

                FR24Counter++;
                if (FR24Counter >= FR24Interval.Value) FR24Counter = 0;

                FR24_Clear();

                if (FR24_GRAB_IS_ON)
                    if (FR24Counter == 0)
                        FR24_Grab();
            };

            FR24Tick.Enabled = true;
        }

        private void Save2APRSLast()
        {
            aprs_cfg.hipp.Clear();
            for (int i = 0; i < aprs_h.Items.Count; i++)
                aprs_cfg.hipp.Add(aprs_h.Items[i].ToString());
            aprs_cfg.selected = aprs_h.SelectedIndex;

            aprs_cfg.callsign = aprs_u.Text.Trim();
            aprs_cfg.password = aprs_p.Text.Trim();
            aprs_cfg.filter = aprs_filter.Text.Trim();

            aprs_cfg.mode = (byte)aprsmode.SelectedIndex;
            aprs_cfg.last = aprs_h.Text;

            aprs_cfg.Save();
        }

        private void APRS_Draw(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if ((APRS_Follow != null) && (APRS_Follow == names[i]))
                {
                    Point[] area = MapViewer.MapBoundsAreaPixels;
                    area[0].X += 70; area[0].Y -= 70; area[1].X -= 70; area[1].Y += 70;
                    PointF BL = MapViewer.PixelsToDegrees(area[0]);
                    PointF TR = MapViewer.PixelsToDegrees(area[1]);
                    if ((Buddies.list[names[i]].lat >= TR.Y) || (Buddies.list[names[i]].lat <= BL.Y) || (Buddies.list[names[i]].lon <= BL.X) || (Buddies.list[names[i]].lon >= TR.X))
                        MapViewer.CenterDegrees = new PointF((float)Buddies.list[names[i]].lon, (float)Buddies.list[names[i]].lat);
                };

                bool selected = (APRS_Selected != null) && (APRS_Selected == names[i]) ? true : false;
                
                NaviMapNet.MapPoint mp = null;

                if (mapAPRS.ObjectsCount > 0)
                    for (int n = 0; n < mapAPRS.ObjectsCount; n++)
                        if (mapAPRS[n].Name == names[i])
                        {
                            mp = (NaviMapNet.MapPoint)mapAPRS[n];
                            n = int.MaxValue - 1;
                        };

                if (mp == null)
                {
                    mp = UpdateAPRS(new NaviMapNet.MapPoint(), Buddies.list[names[i]], selected);
                    mp.Color = Color.Transparent;
                    mapAPRS.Add(mp);
                }
                else
                {                    
                    UpdateAPRS(mp, Buddies.list[names[i]], selected);                                    
                };
                if (aprs_tracc.Checked)
                    UpdateAPRSTrace(mp, Buddies.list[names[i]], selected);    
                //APRS_Follow
                //if ((FR24_Follow != null) && (FR24_Follow.craft.CallSign == crafts[i].CallSign))
                //    UpdateFollowCraft(mp, crafts[i], true, true);
            };

            MapViewer.DrawOnMapData();
        }

        private void FR24_Clear()
        {
            bool hasChanges = false;

            if(FR24Online.Items.Count > 0)
                for (int i = FR24Online.Items.Count - 1; i >= 0; i--)
                {
                    DateTime DT = DateTime.Parse(FR24Online.Items[i].SubItems[5].Text);
                    decimal Age = (decimal)DateTime.Now.Subtract(DT).TotalSeconds;
                    if (FR24_GRAB_IS_ON)
                    {
                        bool todel = false;
                        if (Age >= FR24DelAge.Value)
                            todel = true;

                        if (!FR24ShowAll.Checked)
                        {
                            int alt = int.Parse(FR24Online.Items[i].SubItems[1].Text.Substring(2)) * 100;
                            int speed = int.Parse(FR24Online.Items[i].SubItems[2].Text);
                            if ((alt <= 50) || (speed < 20))
                                todel = true;
                        };

                        if (todel)
                            FR24Online.Items.RemoveAt(i);
                        else
                            UpdateOnlineTableCraft(FR24Online.Items[i]);
                    }
                    else
                    {
                        bool todel = false;

                        if (!FR24ShowAll.Checked)
                        {
                            int alt = int.Parse(FR24Online.Items[i].SubItems[1].Text.Substring(2)) * 100;
                            int speed = int.Parse(FR24Online.Items[i].SubItems[2].Text);
                            if ((alt <= 50) || (speed < 20))
                                todel = true;
                        };

                        if (todel)
                            FR24Online.Items.RemoveAt(i);
                        else
                            UpdateOnlineTableCraft(FR24Online.Items[i]);
                    };
                };

            mtxTrace.WaitOne();
            if (mapTrace.ObjectsCount > 0)
                if (!FR24ShowTrace.Checked)
                {
                    mapTrace.Clear();
                    hasChanges = true;
                }
                else
                    for (int i = mapTrace.ObjectsCount - 1; i >= 0; i--)
                        if ((((AirCraft)mapTrace[i].UserData).Age >= FR24DelAge.Value) || (((AirCraft)mapTrace[i].UserData).IsIdle && (!FR24ShowAll.Checked)))
                        {
                            mapTrace.Remove(i);
                            hasChanges = true;
                        };
            mtxTrace.ReleaseMutex();

            mtxCrafts.WaitOne();
            if (mapCrafts.ObjectsCount > 0)
                    for (int i = mapCrafts.ObjectsCount - 1; i >= 0; i--)
                        if ((((AirCraft)mapCrafts[i].UserData).Age >= FR24DelAge.Value) || (((AirCraft)mapCrafts[i].UserData).IsIdle && (!FR24ShowAll.Checked)))
                        {
                            if (((AirCraft)mapCrafts[i].UserData).IsIdle && (!FR24ShowAll.Checked))
                            {
                                mapCrafts.Remove(i);
                                hasChanges = true;
                            }
                            else if (FR24_GRAB_IS_ON)
                            {
                                mapCrafts.Remove(i);
                                hasChanges = true;
                            };
                        }
                        else if (((AirCraft)mapCrafts[i].UserData).Age >= FR24BlueAge.Value)
                        {
                            NaviMapNet.MapPoint mp = (NaviMapNet.MapPoint)mapCrafts[i];
                            bool selected = (FR24_Selected != null) && (FR24_Selected.craft.CallSign == ((AirCraft)mp.UserData).CallSign) ? true : false;
                            UpdateCraft(mp, (AirCraft)mp.UserData, selected);
                            hasChanges = true;
                        };
            mtxCrafts.ReleaseMutex();

            if (hasChanges)
                MapViewer.DrawOnMapData();
        }

        private void UpdateOnlineTable(AirCraft craft)
        {
            if (FR24Online.Items.Count > 0)
                for (int i = FR24Online.Items.Count - 1; i >= 0; i--)
                {
                    if (FR24Online.Items[i].Text == craft.CallSign)
                    {
                        FR24Online.Items[i].SubItems[1].Text = "FL"+(craft.Alt/100).ToString("000");
                        FR24Online.Items[i].SubItems[2].Text = craft.Spd.ToString("000");
                        FR24Online.Items[i].SubItems[3].Text = craft.Hdg.ToString("000") + "°";
                        FR24Online.Items[i].SubItems[4].Text = craft.AirCraftType;
                        FR24Online.Items[i].SubItems[5].Text = craft.Time.ToString();
                        FR24Online.Items[i].SubItems[6].Text = craft.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        FR24Online.Items[i].SubItems[7].Text = craft.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        FR24Online.Items[i].SubItems[8].Text = craft.AirLine;
                        UpdateOnlineTableCraft(FR24Online.Items[i]);
                        return;
                    };
                };

            if ((!FR24ShowAll.Checked) && (craft.IsIdle)) return;

            ListViewItem lvi = new ListViewItem(new string[] { craft.CallSign, "FL" + (craft.Alt / 100).ToString("000"), craft.Spd.ToString("000"), craft.Hdg.ToString("000") + "°", craft.AirCraftType, craft.Time.ToString(), craft.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture), craft.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture), craft.AirLine });
            UpdateOnlineTableCraft(lvi);
            if((FR24_AL_TOPUP == "-1") || (FR24_AL_TOPUP != lvi.SubItems[8].Text) || (FR24Online.Items.Count == 0))
                FR24Online.Items.Add(lvi);
            else
                for(int i=0;i<FR24Online.Items.Count;i++)
                    if (FR24Online.Items[i].SubItems[8].Text != FR24_AL_TOPUP)
                    {
                        FR24Online.Items.Insert(i, lvi);
                        i = int.MaxValue - 1;
                    };

        }

        private void UpdateOnlineTableCraft(ListViewItem lvi)
        {
            if ((FR24_Follow != null) && (FR24_Follow.craft.CallSign == lvi.Text))
                lvi.BackColor = Color.LightPink;
            else if ((FR24_Selected != null) && (FR24_Selected.craft.CallSign == lvi.Text))
                lvi.BackColor = Color.LightBlue;
            else if (lvi.Selected)
                lvi.BackColor = Color.LightGreen;
            else if (FR24_AL_TOPUP == lvi.SubItems[8].Text)
                lvi.BackColor = Color.Yellow;
            else
                lvi.BackColor = Color.White;
        }

        private void FR24_Grab()
        {
            int beep = 0;

            RectangleF mb = MapViewer.MapBoundsRectOversizeDegrees;
            AirCraft[] crafts = FRG.Grabb(mb.Bottom, mb.Top, mb.Left, mb.Right);
            if (crafts.Length > 0)
            {
                for (int i = 0; i < crafts.Length; i++)
                {
                    if (crafts[i].Age > FR24DelAge.Value) continue; // No add old //
                    UpdateOnlineTable(crafts[i]);

                    if ((FR24_Follow != null) && (FR24_Follow.craft.CallSign == crafts[i].CallSign))
                    {
                        Point[] area = MapViewer.MapBoundsAreaPixels;
                        area[0].X += 70; area[0].Y -= 70; area[1].X -= 70; area[1].Y += 70;
                        PointF BL = MapViewer.PixelsToDegrees(area[0]);
                        PointF TR = MapViewer.PixelsToDegrees(area[1]);
                        if((crafts[i].Lat >= TR.Y) || (crafts[i].Lat <= BL.Y) || (crafts[i].Lon <= BL.X) || (crafts[i].Lon >= TR.X)) 
                            MapViewer.CenterDegrees = new PointF(crafts[i].Lon, crafts[i].Lat);
                    };

                    bool selected = (FR24_Selected != null) && (FR24_Selected.craft.CallSign == crafts[i].CallSign) ? true : false;

                    NaviMapNet.MapPoint mp = null;

                    mtxCrafts.WaitOne();
                    if(mapCrafts.ObjectsCount > 0)
                        for(int n=0;n<mapCrafts.ObjectsCount;n++)
                            if (mapCrafts[n].Name == crafts[i].CallSign)
                            {
                                mp = (NaviMapNet.MapPoint)mapCrafts[n];
                                n = int.MaxValue - 1;
                            };

                    if (mp == null)
                    {
                        if (crafts[i].IsIdle && (!FR24ShowAll.Checked)) continue; // only update data

                        mp = UpdateCraft(new NaviMapNet.MapPoint(), crafts[i], selected);
                        mp.Color = Color.Transparent;
                        mapCrafts.Add(mp);

                        if ((FR24_AL_DOPIP != "-1") && (FR24_AL_DOPIP == crafts[i].AirLine))
                            beep++;
                    }
                    else
                    {
                        if (FR24ShowTrace.Checked)
                            UpdateTrace(mp, crafts[i], selected);
                        UpdateCraft(mp, crafts[i], selected);
                    };
                    if (selected)
                    {
                        FR24_Selected.point = mp;
                        ShowAirCraftText(crafts[i]);
                    };
                    if ((FR24_Follow != null) && (FR24_Follow.craft.CallSign == crafts[i].CallSign))
                            UpdateFollowCraft(mp, crafts[i], true, true);

                    mtxCrafts.ReleaseMutex();
                };
                
                MapViewer.DrawOnMapData();
            };
            if (beep > 0)
                FR24_BEEP_SND.Play();
        }

        private void UpdateTrace(NaviMapNet.MapPoint mp, AirCraft craft, bool selected)
        {            
            NaviMapNet.MapPolyLine pln = null;

            mtxTrace.WaitOne();
            if (mapTrace.ObjectsCount > 0)
                for (int n = 0; n < mapTrace.ObjectsCount; n++)
                    if (mapTrace[n].Name == craft.CallSign)
                    {
                        pln = (NaviMapNet.MapPolyLine)mapTrace[n];
                        n = int.MaxValue - 1;
                    };

            List<PointF> pts = new List<PointF>();
            if (pln == null)
            {
                pln = new NaviMapNet.MapPolyLine();
                pts.Add(mp.Points[0]);
                mapTrace.Add(pln);
            }
            else
                pts.AddRange(pln.Points);
            pts.Add(new PointF(craft.Lon, craft.Lat));
            pln.Points = pts.ToArray();
            pln.Width = selected ? 4 : 2;
            pln.Color = selected ? Color.Red : RandomColor();
            pln.Name = craft.CallSign;
            pln.Text = craft.CallSign;
            pln.UserData = craft;
            pln.Visible = true;

            if (selected) FR24_Selected.trace = pln;

            mtxTrace.ReleaseMutex();
        }

        private NaviMapNet.MapPoint UpdateAPRS(NaviMapNet.MapPoint mp, Buddie buddie, bool selected)
        {
            mp.Points = new PointF[] { new PointF((float)buddie.lon, (float)buddie.lat) };
            mp.Name = buddie.name;
            mp.Text = buddie.name;
            mp.SizePixels = new Size(24, 24);
            mp.Squared = true;
            mp.Visible = true;
            mp.UserData = buddie;
            mp.Img = buddie.course == 0 ? symbol2image(buddie.IconSymbol, false, selected) : RotateImage(symbol2image(buddie.IconSymbol, true, selected), new PointF(12, 12), buddie.course);
            mp.TextFont = selected ? new Font(FRGFont, FontStyle.Bold) : FRGFont;
            mp.TextBrush = selected ? Brushes.Red : Brushes.Black;
            mp.TextOffset = new Point(-16, 14);
            mp.DrawText = aprs_dt.Checked;            

            if (selected)
            {
                mp.DrawText = true;   
                TimeSpan ts = DateTime.UtcNow.Subtract(buddie.last);
                string lapse = ts.TotalHours.ToString("00") + " ч " + ts.Minutes.ToString("00") + " м " + ts.Seconds.ToString("00") + " с";
                aprs_info.Items[0].SubItems[1].Text = buddie.name;
                aprs_info.Items[1].SubItems[1].Text = buddie.lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + "°";
                aprs_info.Items[2].SubItems[1].Text = buddie.lon.ToString(System.Globalization.CultureInfo.InvariantCulture) + "°";
                aprs_info.Items[3].SubItems[1].Text = buddie.last.ToString("HH:mm.ss ddd dd.MM.yyyy");
                aprs_info.Items[4].SubItems[1].Text = lapse;
                aprs_info.Items[5].SubItems[1].Text = buddie.speed.ToString() + " км/ч";
                aprs_info.Items[6].SubItems[1].Text = buddie.course.ToString() + "°";
                aprs_info.Items[7].SubItems[1].Text = buddie.IconSymbol;
                aprs_info.Items[8].SubItems[1].Text = buddie.Comment;
                aprs_info.Items[9].SubItems[1].Text = buddie.Status;
            };

            ListViewItem lvi = null;
            for (int i = 0; i < aprs_objs.Items.Count; i++)
            {
                if (aprs_objs.Items[i].SubItems[0].Text == buddie.name)
                    lvi = aprs_objs.Items[i];
                else
                {
                    aprs_objs.Items[i].BackColor = (aprs_objs.Items[i].SubItems[0].Text == APRS_Selected) ? Color.LightGreen : Color.White;
                    if (aprs_objs.Items[i].SubItems[0].Text == APRS_Follow)
                        aprs_objs.Items[i].BackColor = (aprs_objs.Items[i].SubItems[0].Text == APRS_Selected) ? Color.RosyBrown : Color.Orange;
                };
            };
            if (lvi == null)
            {                
                aprs_objs.Items.Add(lvi = new ListViewItem(new string[] { buddie.name, "", "", "", "", "", "" }));                
            };
            lvi.SubItems[1].Text = buddie.lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            lvi.SubItems[2].Text = buddie.lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
            lvi.SubItems[3].Text = buddie.last.ToString("HH:mm:ss ddd dd.MM.yyyy");
            lvi.SubItems[4].Text = buddie.speed.ToString();
            lvi.SubItems[5].Text = buddie.course.ToString();
            lvi.SubItems[6].Text = buddie.IconSymbol;
            lvi.BackColor = selected ? Color.LightGreen : Color.White;
            if (buddie.name == APRS_Follow)
                lvi.BackColor = selected ? Color.RosyBrown : Color.Orange;
            aprs_ttl_c.Text = String.Format("Всего {0} объектов:", aprs_objs.Items.Count);
            lvi.ImageKey = buddie.IconSymbol;
            if (aprs_imlist.Images[buddie.IconSymbol] == null)
                    aprs_imlist.Images.Add(buddie.IconSymbol, symbol2image(buddie.IconSymbol, false, false));

            return mp;
        }

        private void UpdateAPRSTrace(NaviMapNet.MapPoint mp, Buddie b, bool selected)
        {
            NaviMapNet.MapPolyLine pln = null;

            if (mapAPRSTrace.ObjectsCount > 0)
                for (int n = 0; n < mapAPRSTrace.ObjectsCount; n++)
                    if (mapAPRSTrace[n].Name == b.name)
                    {
                        pln = (NaviMapNet.MapPolyLine)mapAPRSTrace[n];
                        n = int.MaxValue - 1;
                    };
            
            if (pln == null)
            {
                pln = new NaviMapNet.MapPolyLine();
                pln.Color = selected ? Color.Red : RandomColor();
                mapAPRSTrace.Add(pln);
            }
            else
            {
                if (selected)
                    pln.Color = Color.Red;
                else if(pln.Color == Color.Red)
                    pln.Color = RandomColor();
            };
            pln.Points = new PointF[b.tail.Count];
            for(int i=0;i<b.tail.Count;i++)
                pln.Points[i] = new PointF((float)b.tail[i][1], (float)b.tail[i][0]);
            pln.Width = selected ? 4 : 3;            
            pln.Name = b.name;
            pln.Text = b.Status;
            pln.UserData = b;
            pln.Visible = true;
        }

        private Image symbol2image(string symbol, bool drawDir, bool selected)
        {
            int imsz = 24;
            string symb = symbol;
            string prose = "primary";
            string label = "";
            bool revcol = false;
            if (symb.Length == 2)
            {
                if (symb[0] == '\\')
                    prose = "secondary";
                else if ((symb[0] != '/') && (("#&0>AW^_acnsuvz").IndexOf(symb[1]) >= 0))
                {
                    if (("#0A^cv").IndexOf(symb[1]) >= 0) revcol = true;
                    prose = "secondary";
                    label = symb[0].ToString();
                };
                symb = symb.Substring(1);
            };
            string symbtable = "!\"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            int idd = symbtable.IndexOf(symb);
            if (idd < 0) idd = 14;
            int itop = (int)Math.Truncate(idd / 16.0) * imsz;
            int ileft = (idd % 16) * imsz;
            try
            {
                System.Drawing.Image im = prose == "primary" ? global::KMZViewer.Properties.Resources.aprs1st : global::KMZViewer.Properties.Resources.aprs2nd; 
                System.Drawing.Image sm = new System.Drawing.Bitmap(imsz, imsz);
                System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(sm);
                g.Clear(System.Drawing.Color.Transparent);
                if (selected)
                {
                    g.FillEllipse(new SolidBrush(Color.Fuchsia), new Rectangle(0, 0, 24, 24));
                    g.DrawEllipse(new Pen(Brushes.Black, 1), new Rectangle(0, 0, 24, 24));
                };
                g.DrawImage(im, new System.Drawing.Point(-1 * ileft, -1 * itop));
                if (drawDir)
                {
                    PointF[] pts = new PointF[] { new PointF(12,1), new PointF(8,5), new PointF(16,5) };                                        
                    g.FillPolygon(new SolidBrush(Color.Red), pts);
                    g.DrawPolygon(new Pen(Brushes.Black, 1), pts);
                };
                if (label != "")
                {
                    int fs = 9, top = 0, left = 2;
                    System.Drawing.Font f = new System.Drawing.Font("Arial", fs, System.Drawing.FontStyle.Bold);
                    System.Drawing.SizeF w = g.MeasureString(label, f);
                    System.Drawing.SolidBrush br1 = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
                    System.Drawing.SolidBrush br2 = new System.Drawing.SolidBrush(System.Drawing.Color.White);

                    if (revcol) { br1 = br2; br2 = new System.Drawing.SolidBrush(System.Drawing.Color.Black); };
                    g.DrawString(label, f, br1, new System.Drawing.PointF(imsz / 2 + left - 1 - w.Width / 2, imsz / 2 + top + 1 - w.Height / 2));
                    g.DrawString(label, f, br1, new System.Drawing.PointF(imsz / 2 + left + 1 - w.Width / 2, imsz / 2 + top - 1 - w.Height / 2));
                    g.DrawString(label, f, br1, new System.Drawing.PointF(imsz / 2 + left - 1 - w.Width / 2, imsz / 2 + top - 1 - w.Height / 2));
                    g.DrawString(label, f, br1, new System.Drawing.PointF(imsz / 2 + left + 1 - w.Width / 2, imsz / 2 + top + 1 - w.Height / 2));
                    g.DrawString(label, f, br2, new System.Drawing.PointF(imsz / 2 + left - w.Width / 2, imsz / 2 + top - w.Height / 2));
                };
                g.Dispose();
                im.Dispose();

                return sm;
            }
            catch (Exception exception)
            {
            };
            return null;
        }

        private NaviMapNet.MapPoint UpdateCraft(NaviMapNet.MapPoint mp, AirCraft craft, bool selected)
        {
            mp.Points = new PointF[] { new PointF(craft.Lon, craft.Lat) };            
            mp.Name = craft.CallSign;
            mp.Text = craft.CallSign;
            mp.SizePixels = new Size(24, 24);
            mp.Squared = true;
            mp.Visible = true;
            mp.UserData = craft;
            if ((craft.IsIdle) && (craft.Age > FR24BlueAge.Value))
                mp.Img = craft.Hdg == 0 ? FRGI[cAirCraftOldBad] : RotateImage(FRGI[cAirCraftOldBad],new PointF(12,12), craft.Hdg);
            else if(craft.IsIdle)
                mp.Img = craft.Hdg == 0 ? FRGI[cAirCraftBad] : RotateImage(FRGI[cAirCraftBad], new PointF(12, 12), craft.Hdg);
            else if(craft.Age > FR24BlueAge.Value)
                mp.Img = craft.Hdg == 0 ? FRGI[cAirCraftOld] : RotateImage(FRGI[cAirCraftOld], new PointF(12, 12), craft.Hdg);
            else
                mp.Img = craft.Hdg == 0 ? FRGI[cAirCraftNormal] : RotateImage(FRGI[cAirCraftNormal], new PointF(12, 12), craft.Hdg);
            mp.TextFont = selected ? new Font(FRGFont, FontStyle.Bold) : FRGFont;
            mp.TextOffset = new Point(-12, 16);
            mp.DrawText = true;
            return mp;
        }

        private void UpdateFollowCraft(NaviMapNet.MapPoint mp, AirCraft craft, bool follow, bool nodraw)
        {
            if (follow)
                mp.TextBrush = new SolidBrush(Color.Red);
            else
                mp.TextBrush = new SolidBrush(Color.Black);
            if(!nodraw)
                MapViewer.DrawOnMapData();
        }

        private int FR24Counter = -1;
        private AirCraftOnMap FR24_Selected = null;
        private AirCraftOnMap FR24_Follow = null;
        public class AirCraftOnMap
        {
            public AirCraft craft;
            public NaviMapNet.MapPoint point;
            public NaviMapNet.MapPolyLine trace;
            public AirCraftOnMap(AirCraft ac, NaviMapNet.MapPoint mp)
            {
                craft = ac;
                point = mp;
            }
            public AirCraftOnMap(AirCraft ac, NaviMapNet.MapPoint mp, NaviMapNet.MapPolyLine path)
            {
                craft = ac;
                point = mp;
                trace = path;
            }
        }

        private Color RandomColor()
        {
            return FRGC[FRGCR.Next(0, FRGC.Length - 1)];
        }

        public static Bitmap RotateImage(Image image, PointF offset, float angle)
        {
            Bitmap rotatedBmp = new Bitmap(image.Width, image.Height);
            rotatedBmp.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            Graphics g = Graphics.FromImage(rotatedBmp);
            g.TranslateTransform(offset.X, offset.Y);
            g.RotateTransform(angle);
            g.TranslateTransform(-offset.X, -offset.Y);
            g.DrawImage(image, new PointF(0, 0));

            return rotatedBmp;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            FR24Online.Items.Clear();
            mtxCrafts.WaitOne();
            mapCrafts.Clear();
            mtxCrafts.ReleaseMutex();
            mtxTrace.WaitOne();
            mapTrace.Clear();
            mtxTrace.ReleaseMutex();
            MapViewer.DrawOnMapData();
        }

        private void выбранныйСамолетВЦентрКартыToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FR24_Selected == null) return;
            MapViewer.CenterDegrees = FR24_Selected.point.Center;
        }

        private void FR24SelMnu_Opening(object sender, CancelEventArgs e)
        {
            if(FR24_Selected != null)
            {
                ff1.Text = FR24_Selected.craft.CallSign + " в центр карты";
                ff2.Text = "Обновить информацию о " + FR24_Selected.craft.CallSign;
                ff3.Text = "Следить за " + FR24_Selected.craft.CallSign;
            };
            ff3.Enabled = ff2.Enabled = ff1.Enabled = FR24_Selected != null;
        }

        private void ff2_Click(object sender, EventArgs e)
        {
            if (FR24_Selected == null) return;
            ShowAirCraftText(FR24_Selected.craft);
        }

        private void ff3_Click(object sender, EventArgs e)
        {
            if (FR24_Selected == null) return;

            if (FR24_Follow != null)
                UpdateFollowCraft(FR24_Follow.point, FR24_Follow.craft, false, true);
            FR24_Follow = new AirCraftOnMap(FR24_Selected.craft, FR24_Selected.point);
            FR24FStat.Text = FR24_Follow.craft.CallSign;
            UpdateFollowCraft(FR24_Follow.point, FR24_Follow.craft, true, false);
            
            FR24FBTN.Enabled = true;
            FR24FBTN.Checked = true;
            FR24FBTN.Text = "Следить за самолетом " + FR24_Follow.craft.CallSign;
        }

        private void FR24FBTN_CheckedChanged(object sender, EventArgs e)
        {
            if (!FR24FBTN.Checked)
            {
                if (FR24_Follow != null)
                    UpdateFollowCraft(FR24_Follow.point, FR24_Follow.craft, false, false);

                FR24_Follow = null;
                FR24FStat.Text = "NoFollow";
                FR24FBTN.Text = "Следить за самолетом " + FR24_Selected.craft.CallSign;
            }
            else
            {
                FR24_Follow = new AirCraftOnMap(FR24_Selected.craft, FR24_Selected.point);
                FR24FStat.Text = FR24_Follow.craft.CallSign;
                UpdateFollowCraft(FR24_Follow.point, FR24_Follow.craft, true, false);
            };
        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {

        }

        private void topUpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FR24Online.Items.Count > 0)
            {
                List<ListViewItem> itms = new List<ListViewItem>();
                for (int i = FR24Online.Items.Count - 1; i >= 0; i--)
                    if (FR24Online.Items[i].BackColor != Color.White)
                        itms.Add(FR24Online.Items[i]);
                if(itms.Count > 0)
                    foreach (ListViewItem itm in itms)
                    {
                        FR24Online.Items.Remove(itm);
                        FR24Online.Items.Insert(0, itm);
                    };
            };
        }

        private void sortByNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FR24_AL_TOPUP = "-1";
            topALToolStripMenuItem.Checked = false;
            topALToolStripMenuItem.Text = "Все рейсы авиакомпании вверх списка";
            FR24Online.ListViewItemSorter = new ListViewComparer(0, SortOrder.Ascending);
            FR24Online.Sort();
            FR24Online.Sorting = SortOrder.None;
        }

        private void FR24Online_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (FR24Online.SelectedItems == null) return;
            if (FR24Online.SelectedItems.Count == 0) return;
            float lat = float.Parse(FR24Online.SelectedItems[0].SubItems[6].Text, System.Globalization.CultureInfo.InvariantCulture);
            float lon = float.Parse(FR24Online.SelectedItems[0].SubItems[7].Text, System.Globalization.CultureInfo.InvariantCulture);
            MapViewer.CenterDegrees = new PointF(lon, lat);
        }

        private void toSelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FR24Online.SelectedItems == null) return;
            if (FR24Online.SelectedItems.Count == 0) return;
            if (mapCrafts.ObjectsCount == 0) return;
            mtxCrafts.WaitOne();
            for(int i=0;i<mapCrafts.ObjectsCount;i++)
                if (((AirCraft)mapCrafts[i].UserData).CallSign == FR24Online.SelectedItems[0].Text)
                {
                    mtxCrafts.ReleaseMutex();
                    SelectAirCraft((NaviMapNet.MapPoint)mapCrafts[i], (AirCraft)mapCrafts[i].UserData);
                    return;
                };
            mtxCrafts.ReleaseMutex();
            
        }

        private void toFolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FR24Online.SelectedItems == null) return;
            if (FR24Online.SelectedItems.Count == 0) return;
            if (mapCrafts.ObjectsCount == 0) return;
            mtxCrafts.WaitOne();
            for (int i = 0; i < mapCrafts.ObjectsCount; i++)
                if (((AirCraft)mapCrafts[i].UserData).CallSign == FR24Online.SelectedItems[0].Text)
                {
                    mtxCrafts.ReleaseMutex();

                    if (FR24_Follow != null)
                        UpdateFollowCraft(FR24_Follow.point, FR24_Follow.craft, false, true);

                    FR24_Follow = new AirCraftOnMap((AirCraft)mapCrafts[i].UserData, (NaviMapNet.MapPoint)mapCrafts[i]);
                    FR24FStat.Text = ((AirCraft)mapCrafts[i].UserData).CallSign;
                    UpdateFollowCraft(FR24_Follow.point, FR24_Follow.craft, true, false);

                    FR24FBTN.Enabled = true;
                    FR24FBTN.Checked = true;
                    FR24FBTN.Text = "Следить за самолетом " + FR24_Follow.craft.CallSign;

                    return;
                };
            mtxCrafts.ReleaseMutex();
        }

        private void contextMenuStrip4_Opening(object sender, CancelEventArgs e)
        {
            doPipToolStripMenuItem.Enabled = topALToolStripMenuItem.Enabled = topALToolStripMenuItem.Enabled = toFolToolStripMenuItem.Enabled = toSelToolStripMenuItem.Enabled = (FR24Online.SelectedItems != null) && (FR24Online.SelectedItems.Count == 1);
            sortByNameToolStripMenuItem.Enabled = topUpToolStripMenuItem.Enabled = FR24Online.Items.Count > 0;
            if (FR24_AL_TOPUP != "-1") topALToolStripMenuItem.Enabled = true;
            if (FR24_AL_DOPIP != "-1") doPipToolStripMenuItem.Enabled = true;

            if ((Control.ModifierKeys == Keys.Shift) || (FR24Online.SelectedItems.Count == 0))
            {                                
                if (!topALToolStripMenuItem.Checked)
                {
                    topALToolStripMenuItem.Enabled = true;
                    topALToolStripMenuItem.Text = "Все рейсы `...` вверху списка";
                };
                if (!doPipToolStripMenuItem.Checked)
                {
                    doPipToolStripMenuItem.Enabled = true;
                    doPipToolStripMenuItem.Text = "Пищать на новые рейсы `...`";
                };
            };

            if((FR24Online.SelectedItems != null) && (FR24Online.SelectedItems.Count > 0))
            {
                string AL = FR24Online.SelectedItems[0].SubItems[8].Text;
                if(!topALToolStripMenuItem.Checked)
                    topALToolStripMenuItem.Text = "Все рейсы `" + AL + "` вверху списка";
                if(!doPipToolStripMenuItem.Checked)
                    doPipToolStripMenuItem.Text = "Пищать на новые рейсы `" + AL + "`";
            };
        }

        private string FR24_AL_TOPUP = "-1";
        private string FR24_AL_DOPIP = "-1";
        private void topALToolStripMenuItem_Click(object sender, EventArgs e)
        {            
            if (topALToolStripMenuItem.Checked)
            {
                topALToolStripMenuItem.Checked = false;
                FR24_AL_TOPUP = "-1";
                topALToolStripMenuItem.Text = "Все рейсы авиакомпании вверх списка";
                if (FR24Online.Items.Count == 0) topALToolStripMenuItem.Enabled = false;
                return;
            };

            string AL = "SVR";
            if (topALToolStripMenuItem.Text.Contains("..."))
            {
                if (InputBox("Остлеживание новых рейсов", "Наименование авиакомпании:", ref AL) != DialogResult.OK) 
                    return;
            }
            else
            {
                if (FR24Online.Items.Count == 0) return;
                if (FR24Online.SelectedItems == null) return;
                if (FR24Online.SelectedItems.Count == 0) return;
                if (mapCrafts.ObjectsCount == 0) return;
                AL = FR24Online.SelectedItems[0].SubItems[8].Text;
            };
            topALToolStripMenuItem.Checked = true;
            topALToolStripMenuItem.Text = "Все рейсы `" + AL + "` вверху списка";
            FR24_AL_TOPUP = AL;
            
            List<ListViewItem> itms = new List<ListViewItem>();
            for (int i = FR24Online.Items.Count - 1; i >= 0; i--)
                if (FR24Online.Items[i].SubItems[8].Text == AL)
                    itms.Add(FR24Online.Items[i]);
            if (itms.Count > 0)
            {
                FR24Online.ListViewItemSorter = null;
                FR24Online.Sorting = SortOrder.None;                
                foreach (ListViewItem itm in itms)
                {
                    FR24Online.Items.Remove(itm);
                    FR24Online.Items.Insert(0, itm);
                };
            };
        }

        private void sbAltToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FR24_AL_TOPUP = "-1";
            topALToolStripMenuItem.Checked = false;
            topALToolStripMenuItem.Text = "Все рейсы авиакомпании вверх списка";
            FR24Online.ListViewItemSorter = new ListViewComparer(1, SortOrder.Ascending);
            FR24Online.Sort();
            FR24Online.Sorting = SortOrder.None;
        }

        private void sSpdToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FR24_AL_TOPUP = "-1";
            topALToolStripMenuItem.Checked = false;
            topALToolStripMenuItem.Text = "Все рейсы авиакомпании вверх списка";
            FR24Online.ListViewItemSorter = new ListViewComparer(2, SortOrder.Ascending);
            FR24Online.Sort();
            FR24Online.Sorting = SortOrder.None;
        }

        private void sbtToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FR24_AL_TOPUP = "-1";
            topALToolStripMenuItem.Checked = false;
            topALToolStripMenuItem.Text = "Все рейсы авиакомпании вверх списка";
            FR24Online.ListViewItemSorter = new ListViewComparer(4, SortOrder.Ascending);
            FR24Online.Sort();
            FR24Online.Sorting = SortOrder.None;
        }

        private void sbalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FR24_AL_TOPUP = "-1";
            topALToolStripMenuItem.Checked = false;
            topALToolStripMenuItem.Text = "Все рейсы авиакомпании вверх списка";
            FR24Online.ListViewItemSorter = new ListViewComparer(8, SortOrder.Ascending);
            FR24Online.Sort();
            FR24Online.Sorting = SortOrder.None;
        }

        private void doPipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (doPipToolStripMenuItem.Checked)
            {
                doPipToolStripMenuItem.Checked = false;
                FR24_AL_DOPIP = "-1";
                doPipToolStripMenuItem.Text = "Пищать на новые рейсы авиакомпании";
                if (FR24Online.Items.Count == 0) doPipToolStripMenuItem.Enabled = false;
                return;
            };

            string AL = "SVR";
            if (doPipToolStripMenuItem.Text.Contains("..."))
            {
                if (InputBox("Пищать на новые рейсы", "Наименование авиакомпании:", ref AL) != DialogResult.OK)
                    return;
            }
            else
            {
                if (FR24Online.Items.Count == 0) return;
                if (FR24Online.SelectedItems == null) return;
                if (FR24Online.SelectedItems.Count == 0) return;
                if (mapCrafts.ObjectsCount == 0) return;
                AL = FR24Online.SelectedItems[0].SubItems[8].Text;
            };
            doPipToolStripMenuItem.Checked = true;
            doPipToolStripMenuItem.Text = "Пищать на новые рейсы `" + AL + "`";
            FR24_AL_DOPIP = AL;            
        }

        private void pSI_Click(object sender, EventArgs e)
        {

        }

        private void saveQRCode2FileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pictureBox2.Image == null) return;
            PictureBox pb = pictureBox2;
            
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save to";
            sfd.DefaultExt = ".jpg";
            sfd.Filter = "JPEG (*.jpg)|*.jpg";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                pb.Image.Save(sfd.FileName,ImageFormat.Jpeg);
                {
                    string tmpfn = sfd.FileName+".tmp";
                    Dictionary<string, object> dic = new Dictionary<string, object>();                    
                    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);                    
                    string comment = "GEO LOCATION";
                    string descr = "GEO LOCATION";
                    if ((iminfo != null) && (iminfo.Length == 3))
                    {
                        comment = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.000000} {1:0.000000}", iminfo[1], iminfo[2], iminfo[0]);
                        descr = iminfo[0].ToString();
                    };
                    dic.Add("Make", "KMZViewer");
                    dic.Add("Model", fvi.FileVersion);
                    dic.Add("Software", this.Text);
                    dic.Add("UserComment", comment);
                    dic.Add("ImageDescription", descr);
                    ExifInfo.ExifRewrite(sfd.FileName, tmpfn, dic);
                    File.Delete(sfd.FileName);
                    File.Move(tmpfn, sfd.FileName);
                };
            };
            sfd.Dispose();
        }

        private void sQRToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveQRCode2FileToolStripMenuItem_Click(sender, e);
        }

        private void cHSCDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string strf = state.SASCacheDir;
            if (System.Windows.Forms.InputBox.QueryDirectoryBox("SAS Planet Cache", "Путь к папке с кэшем:", ref strf) == DialogResult.OK)
            {
                state.SASCacheDir = strf;
                MessageBox.Show("Папка кэша изменена!\r\nДля загрузки карт из кэша необходимо перезапустить приложение!", "SAS Planet Cache", MessageBoxButtons.OK,MessageBoxIcon.Information);
            };
        }

        private void label9_Click(object sender, EventArgs e)
        {
            string mpf = CurrentDirectory() + @"\Map_Places.txt";
            try
            {
                System.Diagnostics.Process.Start(mpf);
            }
            catch { };
        }

        private void eMCDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string mpf = CurrentDirectory() + @"\Map_Cache_Dirs.txt";
            try
            {
                System.Diagnostics.Process.Start(mpf);
            }
            catch { };
        }

        private void сортироватьToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void exportToCSVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = Path.GetDirectoryName(this.fileName) + @"\" + Path.GetFileNameWithoutExtension(this.fileName) + ".csv";
            sfd.Title = "Save to";
            sfd.DefaultExt = ".csv";
            sfd.Filter = "CSV Files (*.csv)|*.csv";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
                for (int i = 0; i < objects.Columns.Count; i++)
                    sw.Write(String.Format("{0};", objects.Columns[i].Text));
                sw.WriteLine();
                for(int i=0;i<objects.Items.Count;i++)
                {
                    bool point = objects.Items[i].SubItems[3].Text == "Point";
                    if (!point) continue;

                    for (int c = 0; c < objects.Columns.Count; c++)
                        sw.Write(String.Format("{0};", objects.Items[i].SubItems[c].Text.Replace("\r", "").Replace("\n", "").Replace(";", ",")));
                    sw.WriteLine();
                };
                sw.Close();
                fs.Close();
            };
            sfd.Dispose();
        }

        private void copyToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;
            if (objects.SelectedItems.Count != 1) return;

            string text = "";
            int c = -1;
            c = 4;
            text += (String.Format("{0}\t", objects.SelectedItems[0].SubItems[c].Text.Replace("\r", "").Replace("\n", "").Replace("\t", ",")));
            c = 5;
            text += (String.Format("{0}\t", objects.SelectedItems[0].SubItems[c].Text.Replace("\r", "").Replace("\n", "").Replace("\t", ",")));
            c = 0;
            text += (String.Format("{0}\t", objects.SelectedItems[0].SubItems[c].Text.Replace("\r", "").Replace("\n", "").Replace("\t", ",")));
            Clipboard.SetText(text);          
        }

        private void saveToRTFToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;
            if (objects.SelectedItems.Count != 1) return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save to";
            sfd.DefaultExt = ".rtf";
            sfd.Filter = "Rich Text Format (*.rtf)|*.rtf";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
                sw.Write(@"{\rtf1\ansi\ansicpg1251\deff0\deflang1049\deflangfe1049\deftab708{\fonttbl{\f0\fswiss\fprq2\fcharset204{\*\fname Arial;}Arial CYR;}}");
                sw.Write(@"{\*\generator "+this.Text+@";}\viewkind4\uc1\pard\qc\b\f0\fs40 ");
                sw.Write(objects.SelectedItems[0].SubItems[0].Text.Replace(@"\", @"\\"));
                sw.Write(@"\line\b0\fs21 ");
                sw.Write(ImToRtf(pictureBox1.Image));
                sw.Write(@"\line\b0\fs28 ");
                sw.Write(objects.SelectedItems[0].SubItems[4].Text.Replace(@"\", @"\\"));
                sw.Write(@" ");
                sw.Write(objects.SelectedItems[0].SubItems[5].Text.Replace(@"\", @"\\"));
                sw.Write(@"\line\par\fs24 ");
                sw.Write(objects.SelectedItems[0].SubItems[6].Text.Replace(@"\", @"\\"));
                sw.Write(@"\par}");                
                sw.Close();
                fs.Close();
            };
            sfd.Dispose();
        }

        private static string ImToRtf(Image im)
        {
            MemoryStream ms = new MemoryStream();
            im.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] bytes = ms.ToArray();
            ms.Close();


            string str = BitConverter.ToString(bytes, 0).Replace("-", string.Empty);
            string mpic = @"{\pict\pngblip\picw" +
                im.Width.ToString() + @"\pich" + im.Height.ToString() +
                //@"\picwgoa" + width.ToString() + @"\pichgoa" + height.ToString() +
                @"\hex " + str + "}";

            //string imp = custom_RichTextBox.GetImagePrefix(im);
            //return imp + str + "}";

            return mpic;
        }

        private void sRTFLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = Path.GetDirectoryName(this.fileName) + @"\" + Path.GetFileNameWithoutExtension(this.fileName) + ".csv";
            sfd.Title = "Save to";
            sfd.DefaultExt = ".rtf";
            sfd.Filter = "Rich Text Format (*.rtf)|*.rtf";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.GetEncoding(1251));
                sw.WriteLine(@"{\rtf1\ansi\ansicpg1251\deff0\deflang1049\deflangfe1049\deftab708{\fonttbl{\f0\fnil Arial;}{\f2\fswiss\fcharset204{\*\fname Arial;}Arial CYR;}}");
                sw.WriteLine(@"{\colortbl ;\red0\green0\blue0;}");
                sw.WriteLine(@"{\*\generator " + this.Text + @";}\viewkind4\uc1");
                sw.WriteLine(@"\trowd\trgaph30\trleft-30\trrh290\trpaddl30\trpaddr30\trpaddfl3\trpaddfr3");
                sw.WriteLine(@"\cellx1002\cellx2034\cellx3066\cellx4098\cellx5130\cellx6162\cellx7194\cellx8226\cellx9258\pard\intbl\f0");
                for (int c = 0; c < 9; c++)
                    sw.WriteLine(objects.Columns[c].Text + @"\lang1049\f0\cell\f0");
                sw.WriteLine(@"\row");                
                for (int i = 0; i < objects.Items.Count; i++)
                {
                    bool point = objects.Items[i].SubItems[3].Text == "Point";
                    if (!point) continue;

                    sw.WriteLine(@"\trowd\trgaph30\trleft-30\trrh290\trpaddl30\trpaddr30\trpaddfl3\trpaddfr3");
                    sw.WriteLine(@"\cellx1002\cellx2034\cellx3066\cellx4098\cellx5130\cellx6162\cellx7194\cellx8226\cellx9258\pard\intbl\f0");
                    for (int c = 0; c < 9; c++)
                        sw.WriteLine(objects.Items[i].SubItems[c].Text.Replace("\r", "").Replace("\n", "").Replace(";", ",").Replace(@"\", @"\\") + @"\lang1049\f0\cell\f0");
                    sw.WriteLine();
                    sw.WriteLine(@"\row");
                };
                sw.WriteLine(@"\pard\par}");
                sw.Close();
                fs.Close();
            };
            sfd.Dispose();
        }

        private void toolStripStatusLabel2_Click(object sender, EventArgs e)
        {
            Clipboard.SetText((sender as ToolStripStatusLabel).Text);
        }

        private void toolStripStatusLabel2_DoubleClick(object sender, EventArgs e)
        {
            Clipboard.SetText((sender as ToolStripStatusLabel).Text);
        }

        public class GEOOSMJSON
        {
            public GEOMATCH[] matches;
            public string search;
            public string ver;
            public bool find;

            public class GEOMATCH
            {
                public string display_name;
                public string addr_type;
                public string name;
                public string weight;
                public double lon;
                public double lat;
            }
        }

        private void listView2_DoubleClick(object sender, EventArgs e)
        {
            if (listView2.SelectedIndices.Count == 0) return;

            try
            {
                double la = double.Parse(listView2.SelectedItems[0].SubItems[2].Text, System.Globalization.CultureInfo.InvariantCulture);
                double lo = double.Parse(listView2.SelectedItems[0].SubItems[3].Text, System.Globalization.CultureInfo.InvariantCulture);
                MapViewer.CenterDegrees = new PointF((float)lo, (float)la);
            }
            catch { };
        }

        private void textBox3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != '\r') return;
            string text = textBox3.Text.Trim();
            if (String.IsNullOrEmpty(text)) return;
            {
                GEOOSMJSON result = null;
                try
                {
                    System.Net.HttpWebRequest wq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(@"http://openstreetmap.ru/api/search?q=" + System.Security.SecurityElement.Escape(text));
                    System.Net.HttpWebResponse wr = (System.Net.HttpWebResponse)wq.GetResponse();
                    StreamReader sr = new StreamReader(wr.GetResponseStream(), System.Text.Encoding.ASCII);
                    string response = sr.ReadToEnd();
                    result = (GEOOSMJSON)Newtonsoft.Json.JsonConvert.DeserializeObject(response, typeof(GEOOSMJSON));
                    sr.Close();
                    wr.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Геопоиск OSM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                if ((result == null) || (result.matches == null) || (result.matches.Length == 0))
                {
                    MessageBox.Show("Ничего не найдено", "Геопоиск OSM", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                };

                listView2.Items.Clear();
                for (int i = 0; i < result.matches.Length; i++)
                {
                    ListViewItem lvi = new ListViewItem();
                    lvi.Text = result.matches[i].name;
                    lvi.SubItems.Add(result.matches[i].display_name);
                    lvi.SubItems.Add(result.matches[i].lat.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    lvi.SubItems.Add(result.matches[i].lon.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    lvi.SubItems.Add(result.matches[i].addr_type);
                    double d = GetLengthMetersC(MapViewer.CenterDegreesY, MapViewer.CenterDegreesX, result.matches[i].lat, result.matches[i].lon, false);
                    lvi.SubItems.Add(String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00} км", d / 1024.0));
                    lvi.SubItems.Add(String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", d));
                    listView2.Items.Add(lvi);                    
                };

                listView2.ListViewItemSorter = new ObjsOSMSorter();
                listView2.Sorting = SortOrder.Ascending;
            };
        }

        private void exWPTToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = Path.GetDirectoryName(this.fileName) + @"\" + Path.GetFileNameWithoutExtension(this.fileName) + ".wpt";
            sfd.Title = "Save to";
            sfd.DefaultExt = ".wpt";
            sfd.Filter = "OziExplorer Waypoint File (*.wpt)|*.wpt";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                List<WPTPOI> poi = new List<WPTPOI>();
                for (int i = 0; i < objects.Items.Count; i++)
                {
                    bool point = objects.Items[i].SubItems[3].Text == "Point";
                    if (!point) continue;
                    
                    WPTPOI p = new WPTPOI();
                    p.Name = objects.Items[i].SubItems[0].Text;
                    p.FIELDS[(byte)WPTPOI.FIELD.Latitude] = objects.Items[i].SubItems[4].Text;
                    p.FIELDS[(byte)WPTPOI.FIELD.Longitude] = objects.Items[i].SubItems[5].Text;
                    p.Description = objects.Items[i].SubItems[6].Text;
                    poi.Add(p);
                };

                WPTPOI.WriteFile(sfd.FileName, poi.ToArray());
            };
            sfd.Dispose();
        }

        private void exDATToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = Path.GetDirectoryName(this.fileName) + @"\" + Path.GetFileNameWithoutExtension(this.fileName) + ".wpt";
            sfd.Title = "Save to";
            sfd.FileName = "favorites.dat";
            sfd.DefaultExt = ".dat";
            sfd.Filter = "Favorites.dat (*.dat)|*.dat";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                List<string> styles = new List<string>();
                List<int> new_styles = new List<int>();
                ImageList imlF = new ImageList();
                for (int i = 0; i < objects.Items.Count; i++)
                {
                    string name = objects.Items[i].SubItems[0].Text;
                    int l = int.Parse(objects.Items[i].SubItems[1].Text);
                    int p = int.Parse(objects.Items[i].SubItems[2].Text);
                    bool point = objects.Items[i].SubItems[3].Text == "Point";
                    if (!point) continue;

                    string lat = objects.Items[i].SubItems[4].Text;
                    string lon = objects.Items[i].SubItems[5].Text;
                    string desc = objects.Items[i].SubItems[6].Text;
                    XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
                    XmlNode xp = xf.SelectNodes("Placemark")[p];
                    XmlNode su = xp.SelectNodes("styleUrl")[0];
                    if (styles.IndexOf(su.InnerText) < 0)
                    {
                        styles.Add(su.InnerText);
                        new_styles.Add(0);
                        string im = su.InnerText.Substring(1);
                        XmlNode him = xd.SelectSingleNode("kml/Document/Style[@id='" + im + "']/IconStyle/Icon/href");
                        if (him != null)
                        {
                            im = GetTempPath() + him.InnerText.Replace("/", @"\");
                            imlF.Images.Add(Image.FromFile(im));
                        }
                        else
                            imlF.Images.Add(new Bitmap(16,16));
                    };
                };                
                // LIST STYLES //
                if (styles.Count > 0)
                {
                    imlF.ImageSize = new Size(16, 16);
                    KMZViewer.RenameDat rd = new KMZViewer.RenameDat();
                    rd.listView2.SmallImageList = imlF;
                    for (int i = 0; i < styles.Count; i++)
                    {
                        ListViewItem lvi = new ListViewItem(styles[i]);
                        lvi.SubItems.Add(((ProGorodPOI.TType)new_styles[i]).ToString());
                        rd.listView2.Items.Add(lvi);
                        lvi.ImageIndex = i;
                    };
                    if (rd.ShowDialog() == DialogResult.OK)
                    {
                        for (int i = 0; i < styles.Count; i++)
                            new_styles[i] = rd.ns.IndexOf(rd.listView2.Items[i].SubItems[1].Text);
                    }
                    else
                    {
                        rd.Dispose();
                        sfd.Dispose();
                        return;
                    };
                    rd.Dispose();

                    //// PROCESS //
                    List<ProGorodPOI.FavRecord> recs = new List<ProGorodPOI.FavRecord>();
                    for (int i = 0; i < objects.Items.Count; i++)
                    {
                        string name = objects.Items[i].SubItems[0].Text;
                        int l = int.Parse(objects.Items[i].SubItems[1].Text);
                        int p = int.Parse(objects.Items[i].SubItems[2].Text);
                        bool point = objects.Items[i].SubItems[3].Text == "Point";
                        if (!point) continue;

                        string lat = objects.Items[i].SubItems[4].Text;
                        string lon = objects.Items[i].SubItems[5].Text;
                        string desc = objects.Items[i].SubItems[6].Text;
                        XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
                        XmlNode xp = xf.SelectNodes("Placemark")[p];
                        XmlNode su = xp.SelectNodes("styleUrl")[0];
                        int icon = styles.IndexOf(su.InnerText) < 0 ? 0 : new_styles[styles.IndexOf(su.InnerText)];

                        ProGorodPOI.FavRecord rec = new ProGorodPOI.FavRecord();
                        rec.Name = name;
                        rec.Desc = desc;
                        rec.Lat = double.Parse(lat, System.Globalization.CultureInfo.InvariantCulture);
                        rec.Lon = double.Parse(lon, System.Globalization.CultureInfo.InvariantCulture);
                        rec.HomeOffice = ProGorodPOI.THomeOffice.None;
                        rec.Icon = (ProGorodPOI.TType)icon;
                        recs.Add(rec);
                    };
                    if (recs.Count > 0)
                        ProGorodPOI.WriteFile(sfd.FileName, recs.ToArray());
                };
            };
            sfd.Dispose();
        }

        private void s4etNull_Click(object sender, EventArgs e)
        {
            objects.SelectedItems.Clear();
            mapSelect.Clear();
            MapViewer.DrawOnMapData();
        }

        public const uint ES_CONTINUOUS = 0x80000000;
        public const uint ES_SYSTEM_REQUIRED = 0x00000001;
        public const uint ES_DISPLAY_REQUIRED = 0x00000002;
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SetThreadExecutionState([In] uint esFlags);
        private void dispon_Click(object sender, EventArgs e)
        {
            aprsmdisponToolStripMenuItem.Checked = disp2.Checked = dispon.Checked = !dispon.Checked;            
        }

        private void sBXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Sort(5);
        }

        private void sort6ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Sort(6);
        }

        private void sort7ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Sort(7);
        }

        private void sHMAreasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            for (int i = 0; i < objects.Items.Count; i++)
            {
                if (objects.Items[i].SubItems[3].Text != "Polygon") continue;
                bool vis = mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible;
                vis = true;
                mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible = vis;
                objects.Items[i].Font =
                    new Font(objects.Items[i].Font, vis ? FontStyle.Regular : FontStyle.Strikeout);
            };

            MapViewer.DrawOnMapData();
        }

        private void sHMAreas2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            for (int i = 0; i < objects.Items.Count; i++)
            {
                if (objects.Items[i].SubItems[3].Text != "Polygon") continue;
                bool vis = mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible;
                vis = false;
                mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible = vis;
                objects.Items[i].Font =
                    new Font(objects.Items[i].Font, vis ? FontStyle.Regular : FontStyle.Strikeout);
            };

            MapViewer.DrawOnMapData();
        }

        private void sHMPolysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            for (int i = 0; i < objects.Items.Count; i++)
            {
                if (objects.Items[i].SubItems[3].Text != "Line") continue;
                bool vis = mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible;
                vis = true;
                mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible = vis;
                objects.Items[i].Font =
                    new Font(objects.Items[i].Font, vis ? FontStyle.Regular : FontStyle.Strikeout);
            };

            MapViewer.DrawOnMapData();
        }

        private void sHMPolys2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            for (int i = 0; i < objects.Items.Count; i++)
            {
                if (objects.Items[i].SubItems[3].Text != "Line") continue;
                bool vis = mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible;
                vis = false;
                mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible = vis;
                objects.Items[i].Font =
                    new Font(objects.Items[i].Font, vis ? FontStyle.Regular : FontStyle.Strikeout);
            };

            MapViewer.DrawOnMapData();
        }

        private void sHMPointsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            for (int i = 0; i < objects.Items.Count; i++)
            {
                if (objects.Items[i].SubItems[3].Text != "Point") continue;
                bool vis = mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible;
                vis = true;
                mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible = vis;
                objects.Items[i].Font =
                    new Font(objects.Items[i].Font, vis ? FontStyle.Regular : FontStyle.Strikeout);
            };

            MapViewer.DrawOnMapData();
        }

        private void sHMPoints2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            for (int i = 0; i < objects.Items.Count; i++)
            {
                if (objects.Items[i].SubItems[3].Text != "Point") continue;
                bool vis = mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible;
                vis = false;
                mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible = vis;
                objects.Items[i].Font =
                    new Font(objects.Items[i].Font, vis ? FontStyle.Regular : FontStyle.Strikeout);
            };

            MapViewer.DrawOnMapData();
        }

        private void sHowAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            for (int i = 0; i < objects.Items.Count; i++)
            {
                bool vis = mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible;
                vis = true;
                mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible = vis;
                objects.Items[i].Font =
                    new Font(objects.Items[i].Font, vis ? FontStyle.Regular : FontStyle.Strikeout);
            };

            MapViewer.DrawOnMapData();
        }

        private void hideAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            for (int i = 0; i < objects.Items.Count; i++)
            {
                bool vis = mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible;
                vis = false;
                mapContent[int.Parse(objects.Items[i].SubItems[7].Text)].Visible = vis;
                objects.Items[i].Font =
                    new Font(objects.Items[i].Font, vis ? FontStyle.Regular : FontStyle.Strikeout);
            };

            MapViewer.DrawOnMapData();
        }

        private void exp2repToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;

            KMLReport.ReportForm rf = new KMLReport.ReportForm();
            string rd = "";
            if (rf.ShowDialog() == DialogResult.OK)
                rd = rf.Repord.Text;
            rf.Dispose();
            if (String.IsNullOrEmpty(rd)) return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Select file name";
            sfd.Filter = "CSV files (*.csv)|*.csv|HTML files (*.htm)|*.htm";
            sfd.DefaultExt = ".csv";
            sfd.FileName = "report.csv";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                Dictionary<string, string[]> rfd = null;
                try
                {
                    rfd = GetReportFields(rd);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error in Report Template: \r\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    sfd.Dispose();
                    return;
                };
                if ((rfd != null) && (rfd.Count > 0))
                {
                    if (sfd.FilterIndex == 1)
                        Save2ReportCSV(sfd.FileName, rfd);
                    if (sfd.FilterIndex == 2)
                        Save2ReportHTML(sfd.FileName, rfd);
                };
            };
            sfd.Dispose();    
        }

        public Dictionary<string, string[]> GetReportFields(string text)
        {
            Dictionary<string, string[]> res = new Dictionary<string, string[]>();
            if (!String.IsNullOrEmpty(text))
            {
                string[] lns = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lns.Length; i++)
                {
                    lns[i] = lns[i].Trim();
                    if (lns[i].Length == 0) continue;
                    if (lns[i][0] != '[') continue;
                    if (lns[i][lns[i].Length - 1] != ']') continue;
                    string fName = lns[i].Substring(1, lns[i].Length - 2);
                    string fText = lns[++i];
                    string fRegex = lns[++i].Substring(6);
                    res.Add(fName, new string[] { fText, fRegex });
                };
            };
            return res;
        }

        private void Save2ReportCSV(string filename, IDictionary<string, string[]> fields)
        {
            System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            System.IO.StreamWriter sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);
            if(!String.IsNullOrEmpty(kmldocName))
                sw.WriteLine(kmldocName);
            sw.WriteLine("Created by " + this.Text);
            foreach (KeyValuePair<string, string[]> h in fields)
                sw.Write(h.Key.Replace("\t", " ") + "\t");
            if((sortOrder == 3) || (sortOrder == 4))
                sw.Write("-DIST-");
            sw.WriteLine();

            List<string> layers = new List<string>();
            int ttlpm = 0;
            for (int i = 0; i < objects.Items.Count; i++)
            {
                bool point = objects.Items[i].SubItems[3].Text == "Point";
                if(point)
                {
                    XmlNode xn = xd.SelectNodes("kml/Document/Folder")[int.Parse(objects.Items[i].SubItems[1].Text)];
                    XmlNode xns = xn.SelectNodes("Placemark")[int.Parse(objects.Items[i].SubItems[2].Text)];
                    xns = xns.SelectNodes("Point/coordinates")[0];

                    string layer = "";
                    try { layer = xn.SelectSingleNode("name").ChildNodes[0].Value; }
                    catch { };
                    if (layers.IndexOf(layer) < 0) layers.Add(layer);

                    ttlpm++;
                    string[] llz = xns.ChildNodes[0].Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    string name = xns.ParentNode.ParentNode.SelectSingleNode("name").ChildNodes[0].Value.Replace(",", ";");
                    string desc = "";
                    XmlNode std = xns.ParentNode.ParentNode.SelectSingleNode("description");
                    if ((std != null) && (std.ChildNodes.Count > 0))
                        desc = std.ChildNodes[0].Value;
                    string lat = llz[1].Replace("\r", "").Replace("\n", "").Replace(" ", "");
                    string lon = llz[0].Replace("\r", "").Replace("\n", "").Replace(" ", "");

                    foreach (KeyValuePair<string, string[]> h in fields)
                    {
                        string value = h.Value[0].Replace("{layer}", layer).Replace("{name}", name).Replace("{latitude}", lat).Replace("{longitude}", lon).Replace("{description}", desc);
                        if (!String.IsNullOrEmpty(h.Value[1]))
                        {
                            Regex rx = new Regex(h.Value[1]);
                            Match mc = rx.Match(value);
                            if (mc.Success)
                                value = mc.Groups[1].Value;
                            else
                                value = "";
                        };
                        sw.Write(value.Replace("\t", " ") + "\t");
                    };
                    if ((sortOrder == 3) || (sortOrder == 4))
                        sw.Write(this.objects.Items[i].SubItems[8].Text);
                    sw.WriteLine();
                };
            };
            sw.WriteLine(String.Format("Report {1} placemarks in {0} layer(s)", layers.Count, ttlpm));
            sw.Close();
            fs.Close();

            MessageBox.Show(String.Format("Report {1} placemarks in {0} layer(s)", layers.Count, ttlpm), " CSV Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Save2ReportHTML(string filename, IDictionary<string, string[]> fields)
        {
            System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            System.IO.StreamWriter sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);
            sw.WriteLine("<html>");
            sw.WriteLine("<head>");
            if (!String.IsNullOrEmpty(kmldocName))
                sw.WriteLine("<title>" + kmldocName + "</title>");
            sw.WriteLine("</head><body>");
            if (!String.IsNullOrEmpty(kmldocName))
                sw.WriteLine("<h1>" + kmldocName + "</h1>");
            sw.WriteLine("<table border=\"1\" cellpadding=\"2\" cellspacing=\"1\">");
            sw.WriteLine("<tr>");
            foreach (KeyValuePair<string, string[]> h in fields)
                sw.WriteLine("<td><b>" + h.Key + "</b></td>");
            if ((sortOrder == 3) || (sortOrder == 4))
                sw.Write("<td><b>-DIST-</b></td>");
            sw.WriteLine("</tr>");

            List<string> layers = new List<string>();
            int ttlpm = 0;
            for (int i = 0; i < objects.Items.Count; i++)
            {
                bool point = objects.Items[i].SubItems[3].Text == "Point";
                if(point)
                {
                    XmlNode xn = xd.SelectNodes("kml/Document/Folder")[int.Parse(objects.Items[i].SubItems[1].Text)];
                    XmlNode xns = xn.SelectNodes("Placemark")[int.Parse(objects.Items[i].SubItems[2].Text)];
                    xns = xns.SelectNodes("Point/coordinates")[0];

                    string layer = "";
                    try { layer = xn.SelectSingleNode("name").ChildNodes[0].Value; }
                    catch { };
                    if (layers.IndexOf(layer) < 0) layers.Add(layer);

                    ttlpm++;
                    string[] llz = xns.ChildNodes[0].Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    string name = xns.ParentNode.ParentNode.SelectSingleNode("name").ChildNodes[0].Value.Replace(",", ";");
                    string desc = "";
                    XmlNode std = xns.ParentNode.ParentNode.SelectSingleNode("description");
                    if ((std != null) && (std.ChildNodes.Count > 0))
                        desc = std.ChildNodes[0].Value;
                    string lat = llz[1].Replace("\r", "").Replace("\n", "").Replace(" ", "");
                    string lon = llz[0].Replace("\r", "").Replace("\n", "").Replace(" ", "");

                    sw.WriteLine("<tr>");
                    foreach (KeyValuePair<string, string[]> h in fields)
                    {
                        sw.Write("<td>");
                        string value = h.Value[0].Replace("{layer}", layer).Replace("{name}", name).Replace("{latitude}", lat).Replace("{longitude}", lon).Replace("{description}", desc);
                        if (!String.IsNullOrEmpty(h.Value[1]))
                        {
                            Regex rx = new Regex(h.Value[1]);
                            Match mc = rx.Match(value);
                            if (mc.Success)
                                value = mc.Groups[1].Value;
                            else
                                value = "";
                        };
                        sw.Write(value);
                        sw.Write("</td>");
                    };
                    if ((sortOrder == 3) || (sortOrder == 4))
                        sw.Write("<td>" + this.objects.Items[i].SubItems[8].Text + "</td>");
                    sw.WriteLine("</tr>");
                };
            };
            sw.WriteLine("</table>");
            sw.WriteLine("<div>" + String.Format("Report {1} placemarks in {0} layer(s)", layers.Count, ttlpm) + "</div>");
            sw.WriteLine("<div>Created by " + this.Text + "</div>");
            sw.WriteLine("</body></html>");
            sw.Close();
            fs.Close();

            MessageBox.Show(String.Format("Report {1} placemarks in {0} layer(s)", layers.Count, ttlpm), "HTML Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void kmzfilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.objects.Items.Count == 0) return;
            KMZRebuilder.SelectionViewer_Filter sf = new KMZRebuilder.SelectionViewer_Filter(this, xd);
            sf.ShowDialog();
            sf.Dispose();
        }

        private byte RadioPort = 0;
        private int APRSMode = 0; // 0 - APRS-IS, 1 - AGW, 2 - Kiss-TCP, 3 - Kiss-Serial
        private void aprs_ison_CheckedChanged(object sender, EventArgs e)
        {
            if (!aprs_ison.Checked)
            {
                callTCPTHreadStop();
                aprsmode.Enabled = aprs_h.Enabled = aprs_u.Enabled = aprs_p.Enabled = aprs_filter.Enabled = true;
                if (!aprs_follow.Checked)
                    aprs_follow.Enabled = false;
            }
            else
            {
                APRSMode = aprsmode.SelectedIndex;
                string a = "localhost:14580";
                string[] d = aprs_h.Text.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                int port = 14580;
                if ((d.Length > 0) && (d[0].Length > 1)) a = a.Replace("localhost", d[0].Trim());
                if ((d.Length > 1) && (d[1].Length > 1) && int.TryParse(d[1].Trim(), out port) && (port > 0) && (port < 65536)) a = a.Replace(":14580", ":" + port.ToString());
                if ((d.Length > 2) && (d[2].Length > 0)) if (byte.TryParse(d[2], out RadioPort)) { a += ":" + RadioPort.ToString(); } else RadioPort = 0; 
                aprs_h.Text = a;
                aprs_u.Text = aprs_u.Text.Replace(" ", "").Trim();
                if(aprs_u.Text == "") aprs_u.Text = "viewonly";
                aprs_p.Text = aprs_p.Text.Replace(" ","").Trim();
                if(aprs_p.Text == "") aprs_p.Text = "-1";

                callTCPTHreadStart();
                aprsmode.Enabled = aprs_h.Enabled = aprs_u.Enabled = aprs_p.Enabled = aprs_filter.Enabled = false;
                if ((APRS_Selected != null) || (APRS_Follow != null))
                    aprs_follow.Enabled = true;
            };
            txbtn.Enabled = aprs_ison.Checked;
        }

        private string APRS_Selected = null;
        private string APRS_Follow = null;
        private System.Net.Sockets.TcpClient tcpClient = null;
        private System.IO.Stream tcpStream = null;
        private System.Threading.Thread tcpThread = null;        
        private bool tcpAlive = false;
        internal static ax25kiss.KISSTNC kiss = null;
        internal static string tcpState = "Не подключен";
        internal static short tcpSttOk = 0;
        private void callTCPTHreadStart()
        {
            tcpSttOk = 1;
            if (APRSMode == 0)
            {                
                tcpAlive = true;
                tcpThread = new System.Threading.Thread(tcpTHreadProc);
                string[] hp = aprs_h.Text.Trim().Split(new char[] { ':' });
                tcpThread.Start(new string[] { hp[0], hp[1], aprs_u.Text.Trim(), aprs_p.Text.Trim(), aprs_filter.Text.Trim() });
            }
            else
            {
                lastConnCheck = DateTime.UtcNow;
                lastConnState = false;
                string[] hp = aprs_h.Text.Trim().Split(new char[] { ':' });
                if (APRSMode == 1)
                {
                    kiss = new ax25kiss.KISSTNC(hp[0], int.Parse(hp[1]), ax25kiss.KISSTNC.ConnectionMode.AGW);
                    if (hp.Length > 2) kiss.AGWRadioPort = byte.Parse(hp[2]);
                };
                if (APRSMode == 2) kiss = new ax25kiss.KISSTNC(hp[0], int.Parse(hp[1]), ax25kiss.KISSTNC.ConnectionMode.TCPIP);
                if (APRSMode == 3) kiss = new ax25kiss.KISSTNC(hp[0], int.Parse(hp[1]), ax25kiss.KISSTNC.ConnectionMode.SERIAL);
                kiss.onPacket = new PacketXchange(this);
                tcpState = "Подключение " + kiss.Mode + " к " + aprs_h.Text;
                kisslog.Clear();
                kiss.Start();
            };
        }
        private void callTCPTHreadStop()
        {
            tcpSttOk = 0;
            tcpAlive = false;

            if (APRSMode == 0)
            {
                if (tcpStream != null) tcpStream.Close();
                if (tcpClient != null) tcpClient.Close();
                if (tcpThread != null) tcpThread.Abort();
                tcpStream = null;
                tcpClient = null;
                tcpThread = null;
            }
            else
            {
                if (kiss != null) kiss.Stop();
                lastConnState = false;
                kiss = null;
            };
            tcpState = "Не подключен";            
        }
        private void tcpTHreadProc(object flds)
        {
            ulong rcvd_b = 0;
            ulong rcvd_p = 0;
            DateTime last_packet = DateTime.UtcNow;

            string[] fields = (string[])flds;
            while (tcpAlive)
            {
                if (tcpClient == null)
                    tcpClient = new System.Net.Sockets.TcpClient();
                if (!tcpClient.Connected)
                {
                    tcpState = "APRS: Не подключен";
                    try
                    {
                        if(tcpAlive)
                            tcpState = "Попытка подключения ...";
                        tcpClient.Connect(fields[0], int.Parse(fields[1]));
                        tcpStream = tcpClient.GetStream();
                        string p = "user " + fields[2] + " pass " + fields[3] + " vers KMZViewer 20.6 " + (fields[4] == "" ? "" : "filter ") + fields[4];
                        byte[] pts = System.Text.Encoding.GetEncoding(1251).GetBytes(p + "\r\n");
                        tcpStream.Write(pts, 0, pts.Length);
                        tcpStream.Flush();
                        last_packet = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        if (tcpAlive)
                            tcpState = "Ошибка подключения " + ex.Message;
                        try { tcpStream.Close(); } catch { };
                        try { tcpClient.Close(); } catch { };
                        tcpClient = null;
                        tcpStream = null;
                    };
                }
                else if(DateTime.UtcNow.Subtract(last_packet).TotalSeconds > 15)
                {
                    try
                    {
                        string pt = "#ping";
                        byte[] pts = System.Text.Encoding.GetEncoding(1251).GetBytes(pt);
                        tcpStream.Write(pts, 0, pts.Length);
                        tcpStream.Flush();
                        last_packet = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        try { tcpStream.Close(); } catch { };
                        try { tcpClient.Close(); } catch { };                        
                        tcpClient = null;
                        tcpStream = null;
                        if(tcpAlive)
                            tcpState = "Сбой подключения " + ex.Message;                        
                    };
                };
                /////

                if ((tcpClient != null) && (tcpClient.Connected) && (tcpStream != null))
                {
                    if (tcpAlive)
                        tcpState = String.Format("Подключено, получено {0} пакетов; {1}", rcvd_p, FormatSize(rcvd_b));
                    try
                    {
                        int ava = tcpClient.Available;
                        if (ava > 0)
                        {
                            byte[] btr = new byte[ava];
                            tcpStream.Read(btr, 0, btr.Length);
                            rcvd_b += (ulong)ava;
                            string txt = System.Text.Encoding.GetEncoding(1251).GetString(btr);
                            while (!String.IsNullOrEmpty(txt))
                            {
                                string[] txtlns = txt.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                rcvd_p += (ulong)txtlns.Length;
                                foreach (string txtln in txtlns)
                                {
                                    if (txtln.StartsWith("#")) continue;
                                    if (txtln.IndexOf(">") > 0)
                                    {
                                        Buddie b = APRSData.ParseAPRSPacket(txtln);
                                        if ((b != null) && b.PositionIsValid && (!String.IsNullOrEmpty(b.name)))
                                            Buddies.Update(b);
                                        if (tcpSttOk == 1)
                                            tcpSttOk = 2;
                                    };
                                };
                                txt = "";
                            };
                            if (tcpAlive)
                                tcpState = String.Format("Подключено, получено {0} пакетов; {1}", rcvd_p, FormatSize(rcvd_b));
                        };
                    }
                    catch (Exception ex)
                    {
                        if (tcpAlive)
                            tcpState = "Ошибка при приеме данных " + ex.Message;  
                    };
                };
                System.Threading.Thread.Sleep(100);
            };
        }

        static readonly string[] suffixes = { "байт", "KB", "MB", "GB", "TB", "PB" };
        public static string FormatSize(ulong bytes)
        {
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:n2} {1}", number, suffixes[counter]);
        }  


        private void aprs_clear_Click(object sender, EventArgs e)
        {
            aprs_cmnuopener_is_btn = true;
            contextMenuStrip6.Show(aprs_clear, new Point(0, 0));
        }

        private void aprs_tracc_CheckedChanged(object sender, EventArgs e)
        {
            if (aprs_tracc.Checked)
            {
                Buddies.mtx.WaitOne();
                Buddies.updates.Clear();
                foreach (KeyValuePair<string, Buddie> b in Buddies.list)
                    if(b.Value.tail.Count > 1)
                        Buddies.updates.Add(b.Key);
                APRS_Draw(Buddies.updates.ToArray());
                Buddies.updates.Clear();
                Buddies.mtx.ReleaseMutex();
            }
            else
            {
                mapAPRSTrace.Clear();
                MapViewer.DrawOnMapData();
            };
        }

        private void aprs_follow_CheckedChanged(object sender, EventArgs e)
        {
            if (!aprs_follow.Checked)
            {
                APRS_Follow = null;
                aprs_follow.Text = "Следить за передвижением";
                if (APRS_Selected != null)
                    aprs_follow.Text = "Следить за " + APRS_Selected;
                else 
                    aprs_follow.Enabled = false;
            }
            else if (APRS_Selected != null)
                APRS_Follow = APRS_Selected;
        }

        private void button7_Click(object sender, EventArgs e)
        {

        }

        private void очиститьКартуToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Buddies.mtx.WaitOne();
            //Buddies.list.Clear();
            //Buddies.updates.Clear();
            //Buddies.mtx.ReleaseMutex();
            mapAPRS.Clear();
            mapAPRSTrace.Clear();
            MapViewer.DrawOnMapData();
            aprs_objs.Items.Clear();
            aprs_ttl_c.Text = String.Format("Всего {0} объектов:", aprs_objs.Items.Count);
        }

        private void очиститьКартуИОбъектыToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Buddies.mtx.WaitOne();
            Buddies.list.Clear();
            Buddies.updates.Clear();
            Buddies.mtx.ReleaseMutex();
            mapAPRS.Clear();
            mapAPRSTrace.Clear();
            MapViewer.DrawOnMapData();
            aprs_objs.Items.Clear();
            aprs_ttl_c.Text = String.Format("Всего {0} объектов:", aprs_objs.Items.Count);
        }

        private void очиститьВсеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Buddies.mtx.WaitOne();
            Buddies.list.Clear();
            Buddies.updates.Clear();
            Buddies.mtx.ReleaseMutex();
            mapAPRS.Clear();
            mapAPRSTrace.Clear();
            MapViewer.DrawOnMapData();
            APRS_Follow = null;
            APRS_Selected = null;
            aprs_follow.Checked = false;
            for (int i = 0; i < 10; i++)
                aprs_info.Items[i].SubItems[1].Text = "" ;
            aprs_objs.Items.Clear();
            aprs_ttl_c.Text = String.Format("Всего {0} объектов:", aprs_objs.Items.Count);
            aprs_follow.Text = "Следить за передвижением";
            aprs_follow.Enabled = false;
        }

        private void очиститьХвостыToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Buddies.mtx.WaitOne();
            foreach (KeyValuePair<string, Buddie> b in Buddies.list)
                b.Value.tail.Clear();
            Buddies.mtx.ReleaseMutex();
            mapAPRSTrace.Clear();
            MapViewer.DrawOnMapData();
        }

        private void aprs_objs_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (aprs_objs.SelectedItems.Count == 0) return;
            Buddies.mtx.WaitOne();
            if (Buddies.list.ContainsKey(aprs_objs.SelectedItems[0].SubItems[0].Text))
            {
                Buddie b = Buddies.list[aprs_objs.SelectedItems[0].SubItems[0].Text];
                MapViewer.CenterDegrees = new PointF((float)b.lon, (float)b.lat);
            };
            Buddies.mtx.ReleaseMutex();
        }

        private bool aprs_cmnuopener_is_btn = false;
        private void contextMenuStrip6_Opening(object sender, CancelEventArgs e)
        {
            sendheardToolStripMenuItem.Enabled =
                (aprsmode.SelectedIndex > 0) && (aprs_ison.Checked) && (kiss != null) && (kiss.Connected) && (aprs_objs.Items.Count > 0);

            toolStripMenuItem21.Visible = false;

            aprsmnavToolStripMenuItem.Visible =
                aprsmfolToolStripMenuItem.Visible =
                     aprsmselToolStripMenuItem.Visible =
                        aprsmqrToolStripMenuItem.Visible =
                            aprsmdel0.Visible = aprsmdel1.Visible = 
                                aprsmddToolStripMenuItem.Visible = aprsmdbToolStripMenuItem.Visible = 
                                    aprsmdisponToolStripMenuItem.Visible = 
                                        aprs_cmnuopener_is_btn == false;

            очиститьВсеToolStripMenuItem.Visible =
                очиститьКартуToolStripMenuItem.Visible =
                    очиститьКартуИОбъектыToolStripMenuItem.Visible =
                        очиститьХвостыToolStripMenuItem.Visible =
                            aprs_cmnuopener_is_btn == true;

            aprsmnavToolStripMenuItem.Enabled =
                aprsmfolToolStripMenuItem.Enabled =
                     aprsmselToolStripMenuItem.Enabled =
                        aprsmqrToolStripMenuItem.Enabled =
                            aprsmddToolStripMenuItem.Enabled = aprsmdbToolStripMenuItem.Enabled = 
                                aprs_objs.SelectedItems.Count > 0;

            if (aprs_objs.SelectedItems.Count > 0)
            {
                aprsmnavToolStripMenuItem.Text = "Центрировать карту на "+aprs_objs.SelectedItems[0].SubItems[0].Text;
                aprsmfolToolStripMenuItem.Text = "Следить за " + aprs_objs.SelectedItems[0].SubItems[0].Text;
                aprsmselToolStripMenuItem.Text = "Выбрать " + aprs_objs.SelectedItems[0].SubItems[0].Text + " и показать на карте";
                aprsmqrToolStripMenuItem.Text = "Показать QR код с координатами " + aprs_objs.SelectedItems[0].SubItems[0].Text;
                aprsmddToolStripMenuItem.Text = "Удалить " + aprs_objs.SelectedItems[0].SubItems[0].Text;
                aprsmdbToolStripMenuItem.Text = "Удалить все кроме " + aprs_objs.SelectedItems[0].SubItems[0].Text;
            };

            aprs_cmnuopener_is_btn = false;
        }

        private void aprsmnavToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (aprs_objs.SelectedItems.Count == 0) return;
            Buddies.mtx.WaitOne();
            if (Buddies.list.ContainsKey(aprs_objs.SelectedItems[0].SubItems[0].Text))
            {
                Buddie b = Buddies.list[aprs_objs.SelectedItems[0].SubItems[0].Text];
                MapViewer.CenterDegrees = new PointF((float)b.lon, (float)b.lat);
            };
            Buddies.mtx.ReleaseMutex();
        }

        private void aprsmselToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (aprs_objs.SelectedItems.Count == 0) return;            
            Buddies.mtx.WaitOne();
            if (Buddies.list.ContainsKey(aprs_objs.SelectedItems[0].SubItems[0].Text))
            {
                Buddie b = Buddies.list[aprs_objs.SelectedItems[0].SubItems[0].Text];
                MapViewer.CenterDegrees = new PointF((float)b.lon, (float)b.lat);
                if (!aprs_follow.Checked)
                    aprs_follow.Text = "Следить за " + b.name;
                aprs_follow.Enabled = true;
                if(APRS_Selected == null)
                    APRS_Draw(new string[] { APRS_Selected = b.name });
                else
                    APRS_Draw(new string[] { APRS_Selected, APRS_Selected = b.name });
            };
            Buddies.mtx.ReleaseMutex();
        }

        private void aprsmfolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (aprs_objs.SelectedItems.Count == 0) return;
            Buddies.mtx.WaitOne();
            if (Buddies.list.ContainsKey(aprs_objs.SelectedItems[0].SubItems[0].Text))
            {
                Buddie b = Buddies.list[aprs_objs.SelectedItems[0].SubItems[0].Text];
                aprs_follow.Enabled = true;
                aprs_follow.Checked = true;
                APRS_Follow = b.name;
                aprs_follow.Text = "Следить за " + APRS_Follow;
                APRS_Draw(new string[] { b.name });
            };
            Buddies.mtx.ReleaseMutex();
        }

        private void contextMenuStrip6_Opened(object sender, EventArgs e)
        {

        }

        public void ShowXYQRForm(double lat, double lon, string name, string IconSymbol)
        {
            Image im = GenerateGeoQRCode(lat, lon, symbol2image(IconSymbol, false, false));
            Form f = new Form();
            f.BackColor = Color.White;
            f.Text = "Координаты " + name;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.Height = 320;
            f.Width = 300;
            PictureBox pb = new PictureBox();
            pb.SizeMode = PictureBoxSizeMode.CenterImage;
            pb.Dock = DockStyle.Fill;
            f.Controls.Add(pb);
            pb.Image = im;
            f.ShowDialog();
            f.Dispose();
        }

        private void aprsmqrToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (aprs_objs.SelectedItems.Count == 0) return;
            Buddie b = null;
            Buddies.mtx.WaitOne();
            if (Buddies.list.ContainsKey(aprs_objs.SelectedItems[0].SubItems[0].Text))
                b = Buddies.list[aprs_objs.SelectedItems[0].SubItems[0].Text];
            Buddies.mtx.ReleaseMutex();
            if(b != null)
                ShowXYQRForm(b.lat, b.lon, b.name, b.IconSymbol);
        }

        private void aprs_info_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (aprs_info.Items[0].SubItems[1].Text == "") return;
            string name = aprs_info.Items[0].SubItems[1].Text;
            string lat = aprs_info.Items[2].SubItems[1].Text;
            string lon = aprs_info.Items[2].SubItems[1].Text;
            string desc = aprs_info.Items[8].SubItems[1].Text;
            DialogResult dr = InputXY(true, ref name, ref lat, ref lon, ref desc);           
        }

        private void aprs_info_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void aprs_info_KeyUp(object sender, KeyEventArgs e)
        {
            if (((int)e.KeyCode) != 113) return;
            if (aprs_info.Items[0].SubItems[1].Text == "") return;
            string name = aprs_info.Items[0].SubItems[1].Text;
            string lat = aprs_info.Items[2].SubItems[1].Text;
            string lon = aprs_info.Items[2].SubItems[1].Text;
            string desc = "";
            DialogResult dr = InputXY(true, ref name, ref lat, ref lon, ref desc);   
        }

        private void DeleteAllBut(string name)
        {
            Buddies.mtx.WaitOne();
            if (Buddies.list.ContainsKey(name))
            {
                Buddie b = Buddies.list[name];
                Buddies.list.Clear();
                Buddies.list.Add(name, b);
            };
            for (int i = mapAPRS.ObjectsCount - 1; i >= 0; i--)
                if (mapAPRS[i].Name != name)
                    mapAPRS.Remove(i);
            for (int i = mapAPRSTrace.ObjectsCount - 1; i >= 0; i--)
                if (mapAPRSTrace[i].Name != name)
                    mapAPRSTrace.Remove(i);
            for (int i = aprs_objs.Items.Count - 1; i >= 0; i--)
                if (aprs_objs.Items[i].SubItems[0].Text != name)
                    aprs_objs.Items.RemoveAt(i);
            Buddies.mtx.ReleaseMutex();
            MapViewer.DrawOnMapData();            
        }

        private void aprs_objs_KeyUp(object sender, KeyEventArgs e)
        {
            if (aprs_objs.SelectedItems.Count == 0) return;
            if (((int)e.KeyCode) == 113)
            {
                string name = aprs_objs.SelectedItems[0].SubItems[0].Text;
                string lat = aprs_objs.SelectedItems[0].SubItems[1].Text;
                string lon = aprs_objs.SelectedItems[0].SubItems[2].Text;
                string desc = aprs_objs.SelectedItems[0].SubItems[0].Text;
                DialogResult dr = InputXY(true, ref name, ref lat, ref lon, ref desc);
            };
            if (((int)e.KeyCode) == 46)
            {
                aprsmddToolStripMenuItem_Click(sender, null);
            };
        }

        private void aprsmddToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (aprs_objs.SelectedItems.Count == 0) return;
            string name = aprs_objs.SelectedItems[0].SubItems[0].Text;

            if (APRS_Follow == name)
                if (MessageBox.Show("Вы следите за объектом " + name + "\r\nУдалить объект " + name + "?", "Удаление оъбекта", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                    ==
                    DialogResult.No)
                    return;

            if (APRS_Selected == name)
            {
                APRS_Selected = null;
                for (int i = 0; i < 10; i++) aprs_info.Items[i].SubItems[1].Text = "";
                if (!aprs_follow.Checked) aprs_follow.Enabled = false;
            };
            Buddies.mtx.WaitOne();
            if (Buddies.list.ContainsKey(name))
                Buddies.list.Remove(name);
            for (int i = mapAPRS.ObjectsCount - 1; i >= 0; i--)
                if (mapAPRS[i].Name == name)
                    mapAPRS.Remove(i);
            for (int i = mapAPRSTrace.ObjectsCount - 1; i >= 0; i--)
                if (mapAPRSTrace[i].Name == name)
                    mapAPRSTrace.Remove(i);
            aprs_objs.Items.RemoveAt(aprs_objs.SelectedIndices[0]);
            Buddies.mtx.ReleaseMutex();
            MapViewer.DrawOnMapData();
        }

        private void aprsmdbToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (aprs_objs.SelectedItems.Count == 0) return;
            string name = aprs_objs.SelectedItems[0].SubItems[0].Text;
            DeleteAllBut(name);
        }

        private void aprs_dt_CheckedChanged(object sender, EventArgs e)
        {
            if (mapAPRS.ObjectsCount > 0)
                for (int i = mapAPRS.ObjectsCount - 1; i >= 0; i--)
                    if(APRS_Selected != mapAPRS[i].Name)
                        mapAPRS[i].DrawText = aprs_dt.Checked;
            MapViewer.DrawOnMapData();
        }

        private void n2l_Click(object sender, EventArgs e)
        {
            PointF[] r = loadroute();
            if (r == null) return;
            objects.ListViewItemSorter = ObjsLSorter.Nearest(r, xd);
            objects.Sorting = SortOrder.Ascending;
        }

        private void n2s_Click(object sender, EventArgs e)
        {
            PointF[] r = loadroute();
            if (r == null) return;
            objects.ListViewItemSorter = ObjsLSorter.FromStart(r, xd);
            objects.Sorting = SortOrder.Ascending;
        }

        private PointF[] loadroute()
        {
            string filename = null;
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "KML, GPX & Shape files (*.kml;*.gpx;*.shp)|*.kml;*.gpx;*.shp";
            ofd.DefaultExt = "*.kml,*.gpx";
            if (ofd.ShowDialog() == DialogResult.OK)
                filename = ofd.FileName;
            ofd.Dispose();

            if (String.IsNullOrEmpty(filename)) return null;
            if (!File.Exists(filename)) return null;

            System.IO.FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            System.IO.StreamReader sr = new StreamReader(fs);
            List<PointF> res = new List<PointF>();

            if (System.IO.Path.GetExtension(filename).ToLower() == ".shp")
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
                                    res.Add(ap);
                                };
                            };
                        };
                    };
                };
            };
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
                        res.Add(new PointF(float.Parse(xyz[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(xyz[1], System.Globalization.CultureInfo.InvariantCulture)));
                    };
            };
            if (System.IO.Path.GetExtension(filename).ToLower() == ".gpx")
            {
                string file = sr.ReadToEnd();
                int si = 0;
                int ei = 0;
                // rtept
                {
                    si = file.IndexOf("<rtept", ei);
                    if (si > 0)
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
                        res.Add(new PointF(float.Parse(lon, System.Globalization.CultureInfo.InvariantCulture), float.Parse(lat, System.Globalization.CultureInfo.InvariantCulture)));

                        si = file.IndexOf("<rtept", ei);
                        if (si > 0)
                            ei = file.IndexOf(">", si);
                    };
                };
                // trkpt
                {
                    si = file.IndexOf("<trkpt", ei);
                    if (si > 0)
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
                        res.Add(new PointF(float.Parse(lon, System.Globalization.CultureInfo.InvariantCulture), float.Parse(lat, System.Globalization.CultureInfo.InvariantCulture)));

                        si = file.IndexOf("<trkpt", ei);
                        if (si > 0)
                            ei = file.IndexOf(">", si);
                    };
                };
            };
            sr.Close();
            fs.Close();

            return res.ToArray();
        }

        private PointF[] routeFromSelected(ListViewItem lvi)
        {
            List<PointF> route = new List<PointF>();
            int l = int.Parse(lvi.SubItems[1].Text, System.Globalization.CultureInfo.InvariantCulture);
            int p = int.Parse(lvi.SubItems[2].Text, System.Globalization.CultureInfo.InvariantCulture);
            XmlNode xf = xd.SelectNodes("kml/Document/Folder")[l];
            XmlNode xp = xf.SelectNodes("Placemark")[p];

            XmlNode xn = xp.SelectNodes("*/coordinates")[0];
            string[] xya = xn.ChildNodes[0].Value.Trim('\n').Trim('\r').Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string xy in xya)
            {
                try
                {
                    string[] xyz = xy.Split(new char[] { ',' }, StringSplitOptions.None);
                    PointF cp = new PointF((float)double.Parse(xyz[0], System.Globalization.CultureInfo.InvariantCulture), (float)double.Parse(xyz[1], System.Globalization.CultureInfo.InvariantCulture));
                    route.Add(cp);
                }
                catch { };
            };
            return route.ToArray();
        }

        public void UpdateKissLog(string line)
        {
            kisslog.Text += line;
            if (kisslog.Text.Length > 65535)
                kisslog.Text = kisslog.Text.Substring(kisslog.Text.Length - 65535);
            kisslog.SelectionStart = kisslog.Text.Length - 1;
            kisslog.SelectionLength = 0;
            kisslog.ScrollToCaret();
        }

        private void n2en_Click(object sender, EventArgs e)
        {
            if (objects.SelectedItems.Count < 1) return;
            if (!objects.SelectedItems[0].SubItems[3].Text.StartsWith("Line")) return;

            PointF[] r = routeFromSelected(objects.SelectedItems[0]);
            if (r == null) return;
            objects.ListViewItemSorter = ObjsLSorter.Nearest(r, xd);
            objects.Sorting = SortOrder.Ascending;
        }

        private void n2es_Click(object sender, EventArgs e)
        {
            if (objects.SelectedItems.Count < 1) return;
            if (!objects.SelectedItems[0].SubItems[3].Text.StartsWith("Line")) return;

            PointF[] r = routeFromSelected(objects.SelectedItems[0]);
            if (r == null) return;
            objects.ListViewItemSorter = ObjsLSorter.FromStart(r, xd);
            objects.Sorting = SortOrder.Ascending;
        }

        private void kissSendToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Send2Kiss(-1);
        }

        private void Send2Kiss(int objId)
        {
            if (objects.Items.Count == 0) return;
            if (kiss == null) return;
            if (!kiss.Connected) return;
            string callsign = tncstate.SOURCE_CALLSIGN;
            if (callsign == "") callsign = "NOCALL";
            callsign = Transliteration.Front(callsign);
            Regex rx = new Regex(@"(?:(?:symbol|aprs|ssid)?icon:\s?(?<icon>[\\\/][A-Za-z\\/!""#$%&'\(\)\*\+,.0-9\:\;\<\=\>\?\@\[\]\^_\'\{\|\|\~]))", RegexOptions.IgnoreCase);
            toolStripStatusLabel1.Text = "Отправка APRS...";
            statusStrip2.Update();
            for (int i = 0; i < objects.Items.Count; i++)
            {                
                if ((objId >= 0) && (objId != i)) continue;                
                ListViewItem lvi = objects.Items[i];
                if (lvi.SubItems[3].Text != "Point") continue;
                toolStripStatusLabel1.Text = String.Format("Отправка APRS {0}/{1}", i + 1, objects.Items.Count);
                statusStrip2.Update();
                double lat = double.Parse(lvi.SubItems[4].Text, System.Globalization.CultureInfo.InvariantCulture);
                double lon = double.Parse(lvi.SubItems[5].Text, System.Globalization.CultureInfo.InvariantCulture);
                string name = Transliteration.Front(lvi.SubItems[0].Text.Replace("*", "").Replace("_", "").Replace("*", "").Replace("$", "").Replace("*", "").Replace(":", "").Replace("*", "").Replace(";", ""));
                string comm = name;
                if (comm.Length > 36) comm = comm.Substring(0, 36);
                if (name.Length > 9) name = name.Substring(0, 9);
                if (ao1.Checked)
                {
                    while (name.Length < 3)
                        name += " ";
                }
                else
                {
                    while (name.Length < 9)
                        name += " ";
                };

                string icon = @"\<";
                string comment = lvi.SubItems[6].Text;
                if (!String.IsNullOrEmpty(comment))
                {
                    Match mc = rx.Match(comment);
                    if (mc.Success && (!String.IsNullOrEmpty(mc.Groups["icon"].Value))) icon = mc.Groups["icon"].Value;
                };

                string cmd = @";{0}" + tncstate.SUFFIX_OBJECT + "{1:ddHHmm}z{2}" + icon[0] + "{3}" + icon[1] + "000/000#{4}";  // Object
                if (ao1.Checked) cmd = @"){0}" + tncstate.SUFFIX_ITEM + "{2}" + icon[0] + "{3}" + icon[1] + "000/000#{4}";     // Item            
                if (ao2.Checked) cmd = tncstate.PREFIX_BEACON + @"{2}" + icon[0] + "{3}" + icon[1] + "000/000#{4}";            // Beacon                
                cmd = String.Format(cmd,
                    name, // 0
                    DateTime.UtcNow, // 1                                        
                    Math.Truncate(lat).ToString("00") + ((lat - Math.Truncate(lat)) * 60).ToString("00.00").Replace(",", ".") + (lat > 0 ? "N" : "S"), // 2
                    Math.Truncate(lon).ToString("000") + ((lon - Math.Truncate(lon)) * 60).ToString("00.00").Replace(",", ".") + (lon > 0 ? "E" : "W"), // 3
                    comm); // 4
                try
                {
                    toolStripStatusLabel1.Text = String.Format("Отправка APRS {0}/{1}/{2}", name, i + 1, objects.Items.Count);
                    statusStrip2.Update();
                    if(ao2.Checked)
                        kiss.Send(tncstate.DESTINATION_CALLSIGN, name, tncstate.P_WAY, cmd);
                    else
                        kiss.Send(tncstate.DESTINATION_CALLSIGN, callsign, tncstate.P_WAY, cmd);
                    if ((objId == -1) && (kiss.Mode == ax25kiss.KISSTNC.ConnectionMode.SERIAL))
                        System.Threading.Thread.Sleep(2500);
                }
                catch { };
            };
            toolStripStatusLabel1.Text = "Отправка APRS завершена";
            statusStrip2.Update();
        }

        private void Send2Kiss(string line)
        {
            if(kiss == null) return;
            if(!kiss.Connected) return;
            if (String.IsNullOrEmpty(line)) return;
            int a = line.IndexOf(">");
            int b = line.IndexOf(":");
            if (a < 0) return;
            if (b <= a) return;

            string callsign = line.Substring(0, a);
            string[] path = line.Substring(a + 1, b - a - 1).Split(new char[]{','},StringSplitOptions.None);
            string callto = path[0];
            string[] pto = null;
            if (path.Length > 1)
            {
                pto = new string[path.Length - 1];
                for (int i = 1; i < path.Length; i++) pto[i - 1] = path[i];
            };
            string msg = line.Substring(b + 1);

            try
            {
               kiss.Send(callto, callsign, pto, msg);
            }
            catch { };
        }

        private void Send2KissBeacon(string txdata)
        {
            if (kiss == null) return;
            if (!kiss.Connected) return;
            if (String.IsNullOrEmpty(txdata)) return;

            try
            {
                kiss.Send(tncstate.DESTINATION_CALLSIGN, tncstate.SOURCE_CALLSIGN, tncstate.P_WAY, txdata);
            }
            catch { };
        }

        private void Send2KissStatus(string status)
        {
            if (kiss == null) return;
            if (!kiss.Connected) return;
            if (String.IsNullOrEmpty(status)) return;

            string msg = String.Format(">{0}", status.Trim());

            try
            {
                kiss.Send("APRS", tncstate.SOURCE_CALLSIGN, tncstate.P_WAY, msg);
            }
            catch { };
        }

        private void Send2KissMessage(string dest, string mess)
        {
            if (kiss == null) return;
            if (!kiss.Connected) return;
            if (String.IsNullOrEmpty(dest)) return;
            if (String.IsNullOrEmpty(mess)) return;

            string callto = dest.ToUpper().Trim(); ;
            string msg = String.Format(":{0,-9}:{1}", dest, mess.Trim());

            try
            {
                kiss.Send(dest, tncstate.SOURCE_CALLSIGN, tncstate.P_WAY, msg);
            }
            catch { };
        }

        private void xkissnmsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;
            for (int i = 0; i < objects.Items.Count; i++)
            {
                ListViewItem lvi = objects.Items[i];
                if (lvi.SubItems[3].Text != "Point") continue;
                string name = Transliteration.Front(lvi.SubItems[0].Text.Replace("*", "").Replace("_", "").Replace("*", "").Replace("$", "").Replace("*", "").Replace(":", "").Replace("*", "").Replace(";", ""));
                string comm = name;
                if (comm.Length > 36) comm = comm.Substring(0, 36);
                if (name.Length > 9) name = name.Substring(0, 9);
                lvi.SubItems[0].Text = name;
            };
        }

        private void kissitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objects.Items.Count == 0) return;
            if (objects.SelectedItems.Count != 1) return;
            Send2Kiss(objects.SelectedIndices[0]);
        }

        private void ao0_Click(object sender, EventArgs e)
        {
            ao0.Checked = true;
            ao1.Checked = !ao0.Checked;
            ao2.Checked = !ao0.Checked;
        }

        private void ao1_Click(object sender, EventArgs e)
        {
            ao1.Checked = true;
            ao0.Checked = !ao1.Checked;
            ao2.Checked = !ao1.Checked;
        }

        private void ao2_Click(object sender, EventArgs e)
        {
            ao2.Checked = true;
            ao0.Checked = !ao2.Checked;
            ao1.Checked = !ao2.Checked;
        }

        private void sendCustom_Click(object sender, EventArgs e)
        {
            
        }

        private void sendMes_Click(object sender, EventArgs e)
        {
            string dest = tncstate.DESTINATION_CALLSIGN;
            string mess = tncstate.CURRENT_MES;
            if (InputBox("APRS Message", "Позывной получателя:", ref dest, null) == DialogResult.OK)
                if (InputBox("APRS Message", "Сообщение получателю:", ref mess, null) == DialogResult.OK)
                    Send2KissMessage(dest, mess);
        }

        private void label23_Click(object sender, EventArgs e)
        {

        }

        private void txprops_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
                SetProperty();
            if (e.KeyChar == ' ')
                SetProperty();
        }
       
        private void txprops_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            SetProperty();
        }

        public TNCState tncstate = new TNCState();
        private void LoadTNC(bool defaults)
        {
            tncstate = new TNCState();
            try
            {
                if(!defaults)
                    tncstate = TNCState.Load(KMZViewerForm.CurrentDirectory() + @"\TNCConfig.xml");                
            }
            catch {   };
            txprops.Items[0].SubItems[1].Text = tncstate.SOURCE_CALLSIGN;
            txprops.Items[1].SubItems[1].Text = tncstate.DESTINATION_CALLSIGN;
            txprops.Items[2].SubItems[1].Text = tncstate.DIGIPATH;
            txbox.Text = tncstate.PAYLOAD;
            for (int i = 3; i <= 10; i++)
                txprops.Items[i].SubItems[1].Text = tncstate.CURRENT_DATA[i - 3];
            txbox.Items.Clear();
            txbox.Items.AddRange(tncstate.LIST_OF_PAYLOAD);
            UpdateTXData();
        }
        private void LoadTNC(string fileName)
        {
            try
            {
                tncstate = TNCState.Load(fileName);
            }
            catch { return; };
            txprops.Items[0].SubItems[1].Text = tncstate.SOURCE_CALLSIGN;
            txprops.Items[1].SubItems[1].Text = tncstate.DESTINATION_CALLSIGN;
            txprops.Items[2].SubItems[1].Text = tncstate.DIGIPATH;
            txbox.Text = tncstate.PAYLOAD;
            for (int i = 3; i <= 10; i++)
                txprops.Items[i].SubItems[1].Text = tncstate.CURRENT_DATA[i - 3];
            txbox.Items.Clear();
            txbox.Items.AddRange(tncstate.LIST_OF_PAYLOAD);
            UpdateTXData();
        }
        private void SaveTNC()
        {
            try
            {
                TNCState.Save(KMZViewerForm.CurrentDirectory() + @"\TNCConfig.xml",tncstate);
            }
            catch { };
        }
        private void SaveTNC(string fileName)
        {
            try
            {
                TNCState.Save(fileName, tncstate);
            }
            catch { };
        }

        private void SetProperty() { SetProperty(0); }
        private void SetProperty(int dir)
        {
            if (txprops.SelectedItems.Count == 0) return;
            int index = txprops.SelectedIndices[0];
            if (index < 0) return;

            if (dir == 0)
            {
                string reftext = txprops.Items[index].SubItems[1].Text;
                List<string> his = null;

                //from 0
                //to 1
                //path 2
                //lat 3
                //lon 4
                //alt 5
                //symbol 6
                //comment 7   
                //status 8
                //message 9 
                //type 10 : BEACON OBJECT ITEM MESSAGE STATUS 

                switch (index)
                {
                    case 0:
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "Callsign отправителя:", ref reftext, @"R^[a-zA-Z0-9\-]{1,9}$") == DialogResult.OK)
                            txprops.Items[index].SubItems[1].Text = reftext.ToUpper();
                        break;
                    case 1:
                        his = new List<string>(tncstate.LIST_OF_DESTINATION); if (!his.Contains(reftext)) his.Add(reftext);
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "Callsign получателя/Имя объекта:", his.ToArray(), ref reftext, @"R^[a-zA-Z0-9\-]{1,9}$", null) == DialogResult.OK)
                            txprops.Items[index].SubItems[1].Text = reftext.ToUpper();
                        break;
                    case 2:
                        his = new List<string>(tncstate.LIST_OF_DIGIPATH); if (!his.Contains(reftext)) his.Add(reftext);
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "APRS DIGI Path (Путь):", his.ToArray(), ref reftext, @"R^[A-Za-z0-9\-]{1,9}((,[A-Za-z0-9\-]{1,9})|,)*$", null) == DialogResult.OK)
                            txprops.Items[index].SubItems[1].Text = reftext.ToUpper();
                        break;
                    case 3:
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "GPS Широта:", ref reftext, @"R^((?:\d{1,2}\.\d*°?\s*[NnSs])|(?:\d{1,2}°?\s+\d{1,2}\.\d*\'?\s*[NnSs])|(?:\d{1,2}°?\s+\d{1,2}\'?\s+\d{1,2}\""?\s*[NnSs])|([\s\d\.]*))$") == DialogResult.OK)
                            txprops.Items[index].SubItems[1].Text = reftext.ToUpper();
                        break;
                    case 4:
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "GPS Долгота:", ref reftext, @"R^((?:\d{1,3}\.\d*°?\s*[EeWw])|(?:\d{1,3}°?\s+\d{1,2}\.\d*\'?\s*[EeWw])|(?:\d{1,3}°?\s+\d{1,2}\'?\s+\d{1,2}\""?\s*[EeWw])|([\s\d\.]*))$") == DialogResult.OK)
                            txprops.Items[index].SubItems[1].Text = reftext.ToUpper();
                        break;
                    case 5:
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "GPS Высота (...m/f):", ref reftext, @"R^\d{1,5}\s*[мМфФmMfF]?$") == DialogResult.OK)
                        {
                            string nalt = reftext.Trim();
                            if (nalt.Length == 0) nalt = "0";
                            char ls = nalt[nalt.Length - 1];
                            if (!char.IsNumber(ls))
                            {
                                nalt = nalt.Substring(0, nalt.Length - 1).Trim();
                                if ((ls == 'f') || (ls == 'F') || (ls == 'ф') || (ls == 'Ф'))
                                {
                                    double d = 0;
                                    double.TryParse(nalt, out d);
                                    nalt = ((int)Math.Round(d * 0.3048)).ToString();
                                };

                            };
                            txprops.Items[index].SubItems[1].Text = nalt;
                        };
                        break;
                    case 6:
                        his = new List<string>(tncstate.LIST_OF_SYMBOL); if (!his.Contains(reftext)) his.Add(reftext);
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "Иконка (символ):", his.ToArray(), ref reftext, @"R^.{1,2}$", null) == DialogResult.OK)
                            txprops.Items[index].SubItems[1].Text = reftext;
                        break;
                    case 7:
                        his = new List<string>(tncstate.LIST_OF_COMMENT); if (!his.Contains(reftext)) his.Add(reftext);
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "Комментарий:", his.ToArray(), ref reftext, true) == DialogResult.OK)
                            txprops.Items[index].SubItems[1].Text = reftext;
                        break;
                    case 8:
                        his = new List<string>(tncstate.LIST_OF_STATE); if (!his.Contains(reftext)) his.Add(reftext);
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "Статус:", his.ToArray(), ref reftext, true) == DialogResult.OK)
                            txprops.Items[index].SubItems[1].Text = reftext;
                        break;
                    case 9:
                        his = new List<string>(tncstate.LIST_OF_MESSAGE); if (!his.Contains(reftext)) his.Add(reftext);
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "Сообщение:", his.ToArray(), ref reftext, true) == DialogResult.OK)
                            txprops.Items[index].SubItems[1].Text = reftext;
                        break;
                    case 10:
                        his = new List<string>(new string[] { "BEACON", "OBJECT", "ITEM", "MESSAGE", "STATUS" });
                        if (System.Windows.Forms.InputBox.Show("Изменение параметров:", "Тип пакета:", his.ToArray(), ref reftext, false) == DialogResult.OK)
                            txprops.Items[index].SubItems[1].Text = reftext;
                        break;
                };

                txprops.Items[0].SubItems[1].Text = tncstate.SOURCE_CALLSIGN = String.IsNullOrEmpty(txprops.Items[0].SubItems[1].Text) ? "NOCALL-10" : txprops.Items[0].SubItems[1].Text;
                txprops.Items[1].SubItems[1].Text = tncstate.DESTINATION_CALLSIGN = String.IsNullOrEmpty(txprops.Items[1].SubItems[1].Text) ? "APRS" : txprops.Items[1].SubItems[1].Text;
                txprops.Items[2].SubItems[1].Text = tncstate.DIGIPATH = txprops.Items[2].SubItems[1].Text;
                if (txprops.Items[6].SubItems[1].Text.Length < 2) txprops.Items[6].SubItems[1].Text = "/[";
                for (int i = 3; i <= 10; i++) tncstate.CURRENT_DATA[i - 3] = txprops.Items[i].SubItems[1].Text;

                UpdateTXData();
                SavePresets(false);            
            }
            else
            {
                string reftext = txprops.Items[index].SubItems[1].Text;
                List<string> his = null;
                int ex = -1;
                switch (index)
                {
                    case 1:
                        his = new List<string>(tncstate.LIST_OF_DESTINATION); if (!his.Contains(reftext)) his.Add(reftext);
                        ex = his.IndexOf(reftext) + dir;
                        if (ex < 0) ex = his.Count - 1; else if (ex >= his.Count) ex = 0;
                        reftext = his[ex];
                        txprops.Items[index].SubItems[1].Text = reftext.ToUpper();
                        break;
                    case 2:
                        his = new List<string>(tncstate.LIST_OF_DIGIPATH); if (!his.Contains(reftext)) his.Add(reftext);
                        ex = his.IndexOf(reftext) + dir;
                        if (ex < 0) ex = his.Count - 1; else if (ex >= his.Count) ex = 0;
                        reftext = his[ex];
                        txprops.Items[index].SubItems[1].Text = reftext.ToUpper();
                        break;
                    case 5:
                        int alt = 0;
                        int.TryParse(reftext, out alt);
                        alt += dir * 5;
                        if (alt < 0) alt = 0; else if (alt > 300000) alt = 0;
                        txprops.Items[index].SubItems[1].Text = alt.ToString();
                        break;
                    case 6:
                        his = new List<string>(tncstate.LIST_OF_SYMBOL); if (!his.Contains(reftext)) his.Add(reftext);
                        ex = his.IndexOf(reftext) + dir;
                        if (ex < 0) ex = his.Count - 1; else if (ex >= his.Count) ex = 0;
                        reftext = his[ex];
                        txprops.Items[index].SubItems[1].Text = reftext;
                        break;
                    case 7:
                        his = new List<string>(tncstate.LIST_OF_COMMENT); if (!his.Contains(reftext)) his.Add(reftext);
                        ex = his.IndexOf(reftext) + dir;
                        if (ex < 0) ex = his.Count - 1; else if (ex >= his.Count) ex = 0;
                        reftext = his[ex];
                        txprops.Items[index].SubItems[1].Text = reftext;
                        break;
                    case 8:
                        his = new List<string>(tncstate.LIST_OF_STATE); if (!his.Contains(reftext)) his.Add(reftext);
                        ex = his.IndexOf(reftext) + dir;
                        if (ex < 0) ex = his.Count - 1; else if (ex >= his.Count) ex = 0;
                        reftext = his[ex];
                        txprops.Items[index].SubItems[1].Text = reftext;
                        break;
                    case 9:
                        his = new List<string>(tncstate.LIST_OF_MESSAGE); if (!his.Contains(reftext)) his.Add(reftext);
                        ex = his.IndexOf(reftext) + dir;
                        if (ex < 0) ex = his.Count - 1; else if (ex >= his.Count) ex = 0;
                        reftext = his[ex];
                        txprops.Items[index].SubItems[1].Text = reftext;
                        break;
                    case 10:
                        his = new List<string>(new string[] { "BEACON", "OBJECT", "ITEM", "MESSAGE", "STATUS" });
                        ex = his.IndexOf(reftext) + dir;
                        if (ex < 0) ex = his.Count - 1; else if (ex >= his.Count) ex = 0;
                        reftext = his[ex];
                        txprops.Items[index].SubItems[1].Text = reftext;
                        break;
                };

                txprops.Items[0].SubItems[1].Text = tncstate.SOURCE_CALLSIGN = String.IsNullOrEmpty(txprops.Items[0].SubItems[1].Text) ? "NOCALL-10" : txprops.Items[0].SubItems[1].Text;
                txprops.Items[1].SubItems[1].Text = tncstate.DESTINATION_CALLSIGN = String.IsNullOrEmpty(txprops.Items[1].SubItems[1].Text) ? "APRS" : txprops.Items[1].SubItems[1].Text;
                txprops.Items[2].SubItems[1].Text = tncstate.DIGIPATH = txprops.Items[2].SubItems[1].Text;
                if (txprops.Items[6].SubItems[1].Text.Length < 2) txprops.Items[6].SubItems[1].Text = "/[";
                for (int i = 3; i <= 10; i++) tncstate.CURRENT_DATA[i - 3] = txprops.Items[i].SubItems[1].Text;

                UpdateTXData();
            };
        }

        private void UpdateTXData()
        {
            string pdata = "";
            switch (txprops.Items[10].SubItems[1].Text) //type 10 
            {
                case "BEACON":
                    txprops.Items[1].SubItems[0].Text = "Callsign получателя:";
                    if(tncstate.P_ALT == "000000")
                        pdata = String.Format(tncstate.PREFIX_BEACON + "{0}{1}{2}{3}000/000#{5}", new object[] { tncstate.P_LAT, tncstate.CURRENT_ICO[0], tncstate.P_LON, tncstate.CURRENT_ICO[1], tncstate.P_ALT, tncstate.CURRENT_COM });                        
                    else
                        pdata = String.Format(tncstate.PREFIX_BEACON + "{0}{1}{2}{3}000/000/A={4}{5}", new object[] { tncstate.P_LAT, tncstate.CURRENT_ICO[0], tncstate.P_LON, tncstate.CURRENT_ICO[1], tncstate.P_ALT, tncstate.CURRENT_COM });
                    break;
                case "OBJECT":
                    txprops.Items[1].SubItems[0].Text = "Имя объекта:";
                    pdata = String.Format(";{0,-9}"+tncstate.SUFFIX_OBJECT+"{1:ddHHmm}z{2}{3}{4}{5}000/000{6}", new object[] { txprops.Items[1].SubItems[1].Text, DateTime.UtcNow, tncstate.P_LAT, tncstate.CURRENT_ICO[0], tncstate.P_LON, tncstate.CURRENT_ICO[1], tncstate.CURRENT_COM });
                    break;
                case "ITEM":
                    txprops.Items[1].SubItems[0].Text = "Имя объекта:";
                    pdata = String.Format("){0,-3}"+tncstate.SUFFIX_ITEM+"{1}{2}{3}{4}000/000{5}", new object[] { txprops.Items[1].SubItems[1].Text, tncstate.P_LAT, tncstate.CURRENT_ICO[0], tncstate.P_LON, tncstate.CURRENT_ICO[1], tncstate.CURRENT_COM });
                    break;
                case "MESSAGE":
                    txprops.Items[1].SubItems[0].Text = "Callsign получателя:";
                    pdata = String.Format(":{0,-9}:{1}", txprops.Items[1].SubItems[1].Text, txprops.Items[9].SubItems[1].Text); //message 9;
                    break;
                case "STATUS":
                    txprops.Items[1].SubItems[0].Text = "Callsign получателя:";
                    pdata = String.Format(">{0}", txprops.Items[8].SubItems[1].Text); //status 8
                    break;
                default:
                    txprops.Items[1].SubItems[0].Text = "Callsign получателя:";
                    pdata = String.Format(">{0}", txprops.Items[8].SubItems[1].Text); //status 8
                    break;
            };
            txbox.Text = pdata;
        }

        private void SavePresets(bool savePayload)
        {
            // SAVE PRESETS //
            List<string> list = null;
            
            list = new List<string>(tncstate.LIST_OF_DESTINATION); // TO
            if(!list.Contains(txprops.Items[1].SubItems[1].Text)) {
                list.Add(txprops.Items[1].SubItems[1].Text);
                if (list.Count > 25) list.RemoveAt(4);
                tncstate.LIST_OF_DESTINATION = list.ToArray(); 
            };
            
            list = new List<string>(tncstate.LIST_OF_DIGIPATH); // PATH
            if(!list.Contains(txprops.Items[2].SubItems[1].Text)) {
                list.Add(txprops.Items[2].SubItems[1].Text);
                if (list.Count > 25) list.RemoveAt(6);
                tncstate.LIST_OF_DIGIPATH = list.ToArray(); 
            };
            
            list = new List<string>(tncstate.LIST_OF_SYMBOL); // Symbol
            if(!list.Contains(txprops.Items[6].SubItems[1].Text)) {
                list.Add(txprops.Items[6].SubItems[1].Text);
                if (list.Count > 25) list.RemoveAt(1);
                tncstate.LIST_OF_SYMBOL = list.ToArray(); 
            };
            
            list = new List<string>(tncstate.LIST_OF_COMMENT); // Comment
            if(!list.Contains(txprops.Items[7].SubItems[1].Text)) {
                list.Add(txprops.Items[7].SubItems[1].Text);
                if (list.Count > 25) list.RemoveAt(0);
                tncstate.LIST_OF_COMMENT = list.ToArray(); 
            };
            
            list = new List<string>(tncstate.LIST_OF_STATE); // Status
            if(!list.Contains(txprops.Items[8].SubItems[1].Text)) {
                list.Add(txprops.Items[8].SubItems[1].Text);
                if (list.Count > 25) list.RemoveAt(0); 
                tncstate.LIST_OF_STATE = list.ToArray();
            };
            
            list = new List<string>(tncstate.LIST_OF_MESSAGE); // Message
            if(!list.Contains(txprops.Items[9].SubItems[1].Text)) {
                list.Add(txprops.Items[9].SubItems[1].Text);
                if (list.Count > 25) list.RemoveAt(0); 
                tncstate.LIST_OF_MESSAGE = list.ToArray();
            };

            tncstate.PAYLOAD = txbox.Text.Trim();
            if (savePayload)
            {
                list = new List<string>(tncstate.LIST_OF_PAYLOAD); // Data
                if (!list.Contains(txbox.Text))
                {
                    list.Add(txbox.Text);
                    if (list.Count > 40) list.RemoveAt(0);
                    tncstate.LIST_OF_PAYLOAD = list.ToArray();
                    txbox.Items.Clear();
                    txbox.Items.AddRange(tncstate.LIST_OF_PAYLOAD);
                };
            };            
            SaveTNC();
        }

        private void txbtn_Click_1(object sender, EventArgs e)
        {
            if (kiss == null) return;
            if (!kiss.Connected) return;

            SavePresets(true);

            txbtn.Enabled = false;
            toolStripStatusLabel1.Text = "Отправка пакета...";
            statusStrip2.Update();
            try { 
                kiss.Send(tncstate.DESTINATION_CALLSIGN, tncstate.SOURCE_CALLSIGN, tncstate.P_WAY, tncstate.PAYLOAD);
                System.Threading.Thread.Sleep(1500);
                toolStripStatusLabel1.Text = "Пакет отправлен";
            }
            catch { toolStripStatusLabel1.Text = "Ошибка отправки"; };
            statusStrip2.Update();
            txbtn.Enabled = aprs_ison.Checked;
        }

        private void sendOwnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string packet = String.Format("{0}>{1}{2}::{1,-9}:Hello World!", tncstate.SOURCE_CALLSIGN, tncstate.DESTINATION_CALLSIGN, tncstate.P_WAY == null ? "" : "," + tncstate.DIGIPATH);
            if (InputBox("Custom APRS Packet", "CALLSING>TO,VIA:DATA", ref packet, null) == DialogResult.OK)
                Send2Kiss(packet);
        }

        private void refToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateTXData();
        }

        private void cl1ToolStripMenuItem_Click(object sender, EventArgs e)
        {            
            txbox.Items.Clear();
            txbox.Text = "";
            tncstate.LIST_OF_PAYLOAD = new string[0];
        }

        private void cllogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            kisslog.Clear();
        }

        private void saca2fToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.DefaultExt = ".txt";
            sfd.Filter = "Text Files (*.txt)|*.txt";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                FileStream fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write);
                byte[] b = System.Text.Encoding.GetEncoding(1251).GetBytes(kisslog.Text);
                fs.Write(b, 0, b.Length);
                fs.Close();
            };
            sfd.Dispose();
        }

        private void contextMenuStrip7_Opening(object sender, CancelEventArgs e)
        {
            rrstToolStripMenuItem.Enabled =
                sendOwnToolStripMenuItem.Enabled = 
                    sbcToolStripMenuItem.Enabled = 
                        ssToolStripMenuItem.Enabled = 
                            sendMes.Enabled = 
                                (aprsmode.SelectedIndex > 0) && (aprs_ison.Checked) && (kiss != null) && (kiss.Connected);
        }

        private void defToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Сбросить все настройки?", "Настройки", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No) return;
            LoadTNC(true);
            MessageBox.Show("Настройки сброшены!", "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ssToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string mess = tncstate.CURRENT_STT;
            if (InputBox("APRS Статус", "Статус:", ref mess, null) == DialogResult.OK)
                Send2KissStatus(mess);
        }

        private void sbcToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string txdata = "";
            if (tncstate.P_ALT == "000000")
                txdata = String.Format(tncstate.PREFIX_BEACON + "{0}{1}{2}{3}000/000#{5}", new object[] { tncstate.P_LAT, tncstate.CURRENT_ICO[0], tncstate.P_LON, tncstate.CURRENT_ICO[1], tncstate.P_ALT, tncstate.CURRENT_COM });
            else
                txdata = String.Format(tncstate.PREFIX_BEACON + "{0}{1}{2}{3}000/000/A={4}{5}", new object[] { tncstate.P_LAT, tncstate.CURRENT_ICO[0], tncstate.P_LON, tncstate.CURRENT_ICO[1], tncstate.P_ALT, tncstate.CURRENT_COM });                
            if (InputBox("APRS Beacon", "TX Text Data:", ref txdata, null) == DialogResult.OK)
                Send2KissBeacon(txdata);
        }

        private void txprops_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == 39) SetProperty(1);
            if (e.KeyValue == 37) SetProperty(-1);
        }

        private void rrstToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UpdateTXData();
            txbox.Refresh();
            txbtn_Click_1(sender, e);
        }

        private void aprsmode_SelectedIndexChanged(object sender, EventArgs e)
        {
            txprops.Enabled = aprsmode.SelectedIndex > 0;
            txbox.Enabled = aprsmode.SelectedIndex > 0;
        }

        private void chicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string cic = tncstate.CURRENT_ICO;
            string nic = Image2Symbol(tncstate.CURRENT_ICO);
            if(cic == nic) return;
            tncstate.CURRENT_ICO = nic;
            txprops.Items[6].SubItems[1].Text = nic;
            UpdateTXData();
        }

        private string Image2Symbol(string symbol)
        {            
            KMZViewer.SelectIcon si = new KMZViewer.SelectIcon();
            string res = symbol;
            si.SetIcon(res);
            if (si.ShowDialog() == DialogResult.OK)
                res = si.GetIcon();
            si.Dispose();
            return res;
        }

        private void sendheardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (aprs_objs.Items.Count == 0) return;
            string hrd = "";
            for (int i = 0; i < aprs_objs.Items.Count; i++)
                hrd += (hrd.Length > 0 ? "," : "") + aprs_objs.Items[i].SubItems[0].Text;
            Send2KissMessage("Heard", hrd);
        }

        private void loadPresetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = ".tncx";
            ofd.Filter = "KMZViewer TNC XML Presets (*.tncx)|*.tncx";
            if (ofd.ShowDialog() == DialogResult.OK)
                LoadTNC(ofd.FileName);
            ofd.Dispose();
        }

        private void savePresetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.DefaultExt = ".tncx";
            sfd.Filter = "KMZViewer TNC XML Presets (*.tncx)|*.tncx";
            if (sfd.ShowDialog() == DialogResult.OK)
                SaveTNC(sfd.FileName);
            sfd.Dispose();
        }

        private void savecurrToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string text = String.Format("Настройки от {0:HH:mm:ss ddd dd.MM.yyyy}", DateTime.Now);
            if (System.Windows.Forms.InputBox.Show("Сохранение настроек", "Название:", ref text) == DialogResult.OK)
            {
                TNCSub.SetPresets(tncstate, text);
                MessageBox.Show("Настройки сохранены!", "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        private void presetsToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            int c = presetsToolStripMenuItem.DropDownItems.Count;
            if (c > 5) for (int i = c - 1; i >= 5; i--) presetsToolStripMenuItem.DropDownItems.RemoveAt(i);
            string[][] presets =  TNCSub.GetPresets();
            if (presets == null) return;

            foreach (string[] preset in presets)
            {
                ToolStripItem tsi = presetsToolStripMenuItem.DropDownItems.Add(preset[2] + " ...");
                tsi.Tag = preset;
                tsi.Click += new EventHandler(tsi_Click);
            };
        }

        private void tsi_Click(object sender, EventArgs e)
        {
            if (sender == null) return;
            if (!(sender is ToolStripItem)) return;
            ToolStripItem tsi = (ToolStripItem)sender;
            if (tsi.Tag == null) return;
            string[] preset = (string[])tsi.Tag;
            int si = 0;
            if(System.Windows.Forms.InputBox.Show("Настройки", preset[2] + ":", new string[] { "Загрузить найстройки", "Обновить настройки", "Удалить настройки" }, ref si) != DialogResult.OK) return;
            if (si == 0)
            {
                LoadTNC(preset[0]);
                MessageBox.Show("Настройки загружены!", "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (si == 1)
            {
                TNCSub.UpdatePresets(tncstate, preset[2], preset[0]);
                MessageBox.Show("Настройки обновлены!", "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                try
                {
                    File.Delete(preset[0]);
                    MessageBox.Show("Настройки удалены!", "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch { };
            };
        }

        private void SelectMBTiles(string fName)
        {
            if (String.IsNullOrEmpty(fName)) return;
            if (!File.Exists(fName)) return;
            mruT.AddFile(fName);

            UserDefindedFile = fName;
            if (iStorages.SelectedIndex == (iStorages.Items.Count - 3))
                iStorages_SelectedIndexChanged(this, null);
            else
                iStorages.SelectedIndex = iStorages.Items.Count - 3;
        }

        private void selmbtToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fName = null;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select MBTiles File";
            ofd.DefaultExt = ".mbtiles";
            ofd.Filter = "All supported files|*.mbtiles;*.sqlite;*.db;*.db3|All Types (*.*)|*.*";
            try { ofd.FileName = UserDefindedFile; }
            catch { };
            if (ofd.ShowDialog() == DialogResult.OK) fName = ofd.FileName;
            ofd.Dispose();
            SelectMBTiles(fName);
        }

        private void showrouteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showrouteToolStripMenuItem.Checked = !showrouteToolStripMenuItem.Checked;
            routeBar.Visible = showrouteToolStripMenuItem.Checked;
            if (routeBar.Visible && (groute == null))
            {
                System.Windows.Forms.InputBox.defWidth = 600;
                groute = GetRouter.Load();
                rbSet.Text = String.Format("Set ({0})", groute.ServiceIndex);
                rbGet.Text = String.Format("Get ({0})", groute.ServiceIndex);
                if (groute.mode == 0) rbDN_Click(sender, e);
                if (groute.mode == 1) rbStFi_Click(sender, e);
                if (groute.mode == 2) rbSt_Click(sender, e);
                if (groute.mode == 3) rbFi_Click(sender, e);
                if (groute.mode == 4) rbMi_Click(sender, e);
                if (groute.getroute) rbGR_Click(sender, e);
            };
            if (routeBar.Visible && (groute.mode == 0)) rbStFi_Click(sender, e);
        }

        private void rbDN_Click(object sender, EventArgs e)
        {
            groute.mode = 0;
            rbDN.Checked = true;
            rbStFi.Checked = false;
            rbSt.Checked = false;
            rbFi.Checked = false;
            rbMi.Checked = false;
            oncb.Text = rbDN.Text;
        }

        private void rbStFi_Click(object sender, EventArgs e)
        {
            groute.mode = 1;
            rbDN.Checked = false;
            rbStFi.Checked = true;
            rbSt.Checked = false;
            rbFi.Checked = false;
            rbMi.Checked = false;
            oncb.Text = rbStFi.Text;
        }

        private void rbSt_Click(object sender, EventArgs e)
        {
            groute.mode = 2;
            rbDN.Checked = false;
            rbStFi.Checked = false;
            rbSt.Checked = true;
            rbFi.Checked = false;
            rbMi.Checked = false;
            oncb.Text = rbSt.Text;
        }

        private void rbFi_Click(object sender, EventArgs e)
        {
            groute.mode = 3;
            rbDN.Checked = false;
            rbStFi.Checked = false;
            rbSt.Checked = false;
            rbFi.Checked = true;
            rbMi.Checked = false;
            oncb.Text = rbFi.Text;
        }

        private void rbGR_Click(object sender, EventArgs e)
        {
            rbGR.Checked = !rbGR.Checked;
            groute.getroute = rbGR.Checked;
        }

        private void rbSet_ButtonClick(object sender, EventArgs e)
        {
            setURLToolStripMenuItem_Click(sender, e);
        }

        private void setURLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (groute == null) return;
            if (groute.service < 0)
            {
                string url = groute.ServiceURL;
                List<string> urls = new List<string>();
                urls.Add("http://localhost:8080/nms/");
                if (!urls.Contains(url)) urls.Insert(0, url);
                if (System.Windows.Forms.InputBox.Show("Web Route Engine", "Enter HTTP Url to " + groute.ServiceName + ":", urls.ToArray(), ref url, true) != DialogResult.OK) return;
                url = url.Trim();
                int si = -1;
                for (int i = 0; i < groute.url_dkxce.Count; i++)
                    if (groute.url_dkxce[i].url == url)
                        si = i;
                if (si >= 0)
                {
                    groute.service = (si + 1) * -1;
                    rbSet.Text = String.Format("Set ({0})", groute.ServiceIndex);
                    rbGet.Text = String.Format("Get ({0})", groute.ServiceIndex);
                }
                else
                {
                    GetRouter.DRSParams p = new GetRouter.DRSParams();
                    p.url = url;
                    Uri uri = new Uri(url);
                    p.name = uri.Host + ":" + uri.Port + " # " + DateTime.Now.ToString("HHmmssMMddyy");
                    groute.url_dkxce.Add(p);
                    groute.service = (groute.url_dkxce.Count) * -1;
                    rbSet.Text = String.Format("Set ({0})", groute.ServiceIndex);
                    rbGet.Text = String.Format("Get ({0})", groute.ServiceIndex);
                };
            }
            else
            {
                string url = groute.ServiceURL;
                if (System.Windows.Forms.InputBox.Show("Web Route Engine", "Enter HTTP Url to " + groute.ServiceName + ":", ref url) != DialogResult.OK) return;
                url = url.Trim();
                int si = -1;
                for (int i = 0; i < groute.url_osrm.Count; i++)
                    if (groute.url_osrm[i].url == url)
                        si = i;
                if (si >= 0)
                {
                    groute.service = (si + 1);
                    rbSet.Text = String.Format("Set ({0})", groute.ServiceIndex);
                    rbGet.Text = String.Format("Get ({0})", groute.ServiceIndex);
                }
                else
                {
                    GetRouter.OSRMParams p = new GetRouter.OSRMParams();
                    p.url = url;
                    Uri uri = new Uri(url);
                    p.name = uri.Host + ":" + uri.Port + " # " + DateTime.Now.ToString("HHmmssMMddyy");
                    groute.url_osrm.Add(p);
                    groute.service = (groute.url_osrm.Count);
                    rbSet.Text = String.Format("Set ({0})", groute.ServiceIndex);
                    rbGet.Text = String.Format("Get ({0})", groute.ServiceIndex);
                };
            };
        }

        private void setKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (groute.service >= 0) return;
            int ki = groute.service * -1 - 1;

            string key = groute.url_dkxce[ki].key;
            List<string> keys = new List<string>();
            keys.Add("TEST");
            if (!keys.Contains(key)) keys.Insert(0, key);
            if (System.Windows.Forms.InputBox.Show("Web Route Engine", "Enter Key to " + groute.ServiceName + ":", keys.ToArray(), ref key, true) != DialogResult.OK) return;
            groute.url_dkxce[ki].key = key;
        }

        private void setToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (groute.service >= 0) return;
            int ki = groute.service * -1 - 1;

            string ra = groute.url_dkxce[ki].ra;
            List<string> ras = new List<string>();
            ras.Add("00000000000000000000000000000000");
            if (!ras.Contains(ra)) ras.Insert(0, ra);
            if (System.Windows.Forms.InputBox.Show("Web Route Engine", "Enter RA (Route Attributes) to " + groute.ServiceName + ":", ras.ToArray(), ref ra, true) != DialogResult.OK) return;
            groute.url_dkxce[ki].ra = ra;
        }

        private void setColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Color c = groute.color;
            if (System.Windows.Forms.InputBox.QueryColorBox("Web Route Engine", "Select color for Route:", ref c) != DialogResult.OK) return;
            groute.color = c;
        }

        private void setWidthToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int w = groute.width;
            if (System.Windows.Forms.InputBox.Show("Web Route Engine", "Select width for Route:", ref w, 2, 20) != DialogResult.OK) return;
            groute.width = w;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            string atext = "This is a tool for dkxce Route Engine and OSRM Route Engine\r\n\r\nMore info:\r\n   https://github.com/dkxce/\r\nBy:\r\n   " + fvi.CompanyName;
            MessageBox.Show(atext, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SubClick(PointF click, string name)
        {
            if (!showrouteToolStripMenuItem.Checked) return;
            if (groute == null) return;
            if (groute.mode == 0) return;

            groute.counter++;
            if (groute.mode == 1)
            {
                if ((groute.counter % 2) > 0)
                {
                    if (mapRStart == null)
                    {
                        mapRStart = new NaviMapNet.MapPoint();
                        mapRStart.Squared = true;
                        mapRStart.SizePixels = new Size(12, 12);
                        mapRStart.Img = global::KMZViewer.Properties.Resources.rStart;
                        mapRoute.Add(mapRStart);
                    };
                    mapRStart.Points = new PointF[] { click };
                    groute.start = new object[] { name, click.X, click.Y };
                    rtStart.Text = String.IsNullOrEmpty(name) ? String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", click.Y, click.X) : name;
                }
                else
                {
                    if (mapRFinish == null)
                    {
                        mapRFinish = new NaviMapNet.MapPoint();
                        mapRFinish.Squared = true;
                        mapRFinish.SizePixels = new Size(12, 12);
                        mapRFinish.Img = global::KMZViewer.Properties.Resources.rFinish;
                        mapRoute.Add(mapRFinish);
                    };
                    mapRFinish.Points = new PointF[] { click };
                    groute.finish = new object[] { name, click.X, click.Y };
                    rtFinish.Text = String.IsNullOrEmpty(name) ? String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", click.Y, click.X) : name;
                };
            }
            else if (groute.mode == 2)
            {
                if (mapRStart == null)
                {
                    mapRStart = new NaviMapNet.MapPoint();
                    mapRStart.Squared = true;
                    mapRStart.SizePixels = new Size(12, 12);
                    mapRStart.Img = global::KMZViewer.Properties.Resources.rStart;
                    mapRoute.Add(mapRStart);
                };
                mapRStart.Points = new PointF[] { click };
                groute.start = new object[] { name, click.X, click.Y };
                rtStart.Text = String.IsNullOrEmpty(name) ? String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", click.Y, click.X) : name;
            }
            else if (groute.mode == 3)
            {
                if (mapRFinish == null)
                {
                    mapRFinish = new NaviMapNet.MapPoint();
                    mapRFinish.Squared = true;
                    mapRFinish.SizePixels = new Size(12, 12);
                    mapRFinish.Img = global::KMZViewer.Properties.Resources.rFinish;
                    mapRoute.Add(mapRFinish);
                };
                mapRFinish.Points = new PointF[] { click };
                groute.finish = new object[] { name, click.X, click.Y };
                rtFinish.Text = String.IsNullOrEmpty(name) ? String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", click.Y, click.X) : name;
            }
            else if (groute.mode == 4)
            {
                MultiPointFormShow(null);
                if (String.IsNullOrEmpty(name)) name = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", click.Y, click.X);
                NaviMapNet.MapPoint mapRPoint = new NaviMapNet.MapPoint();
                mapRPoint.Squared = true;
                mapRPoint.SizePixels = new Size(16, 16);
                mapRPoint.Img = GetRouter.ImageFromNumber(mapRMulti.Count + 1);
                mapRPoint.Points = new PointF[] { click };
                mapRPoint.Text = name;
                mapRoute.Add(mapRPoint);
                mapRMulti.AddPoint(new KeyValuePair<string, PointF>(name, click), mapRPoint);
                MapViewer.DrawOnMapData();
                return;
            };
            AfterClick();
        }

        private void MultiPointFormShow(List<KeyValuePair<string, PointF>> pArr)
        {
            if (mapRMulti == null)
            {
                mapRMulti = new KMZRebuilder.MultiPointRouteForm();
                mapRMulti.StartPosition = FormStartPosition.Manual;
                mapRMulti.Left = this.Left + this.Width - objects.Width;
                mapRMulti.Top = this.Top + panel1.Height;
                mapRMulti.buttonOk.Click += new EventHandler(buttonOk_Click);
                mapRMulti.buttonCancel.Click += new EventHandler(buttonCancel_Click);
                mapRMulti.FormClosed += new FormClosedEventHandler(mapRMulti_FormClosed);
                mapRMulti.TopMost = true;
                mapRMulti.Show();
            };            
            if (pArr != null) mapRMulti.Points = pArr;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            mapRMulti.Clear();
            mapRMulti.Close();
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            mapRMulti.Close();
        }

        private void mapRMulti_FormClosed(object sender, FormClosedEventArgs e)
        {
            bool hasMarkers = false;
            for (int i = 0; i < mapRMulti.OnMapPoints.Count; i++)
                try { hasMarkers = true; mapRoute.Remove(mapRMulti.OnMapPoints[i]); }
                catch { };

            List<KeyValuePair<string, PointF>> pArr = mapRMulti.Points;
            mapRMulti.Dispose();
            mapRMulti = null;

            if (pArr.Count < 2)
            {
                if (hasMarkers) MapViewer.DrawOnMapData();
                try
                {
                    MapViewer.Focus();
                    MapViewer.Select();
                }
                catch { };
                return;
            };
            List<PointF> pVector = new List<PointF>();
            for (int i = 0; i < pArr.Count; i++) pVector.Add(pArr[i].Value);
            WaitingBoxForm wbf = new WaitingBoxForm(this);
            wbf.Show("Get Route: Multipoints", "Wait, requesting route of " + pVector.Count.ToString() + " points");
            PointF[] vector = null;
            nmsRouteClient.Route route = null;
            rtStatus.Text = "Request route...";
            Application.DoEvents();
            double rLength = groute.GetRoute(pVector.ToArray(), wbf, out vector, out route);

            wbf.Hide();
            if (mapRVector != null) mapRoute.Remove(mapRVector);
            Application.DoEvents();
            if ((rLength == double.MaxValue) || (vector == null))
            {
                rtStatus.Text = "No route found";
                MapViewer.DrawOnMapData();
                try
                {
                    MapViewer.Focus();
                    MapViewer.Select();
                }
                catch { };
                return;
            };

            rtStatus.Text = String.Format(System.Globalization.CultureInfo.InvariantCulture, "Route length: {0:0.00} km", rLength / 1000.0);

            mapRVector = new NaviMapNet.MapPolyLine(vector);
            mapRVector.Color = groute.color;
            mapRVector.Width = groute.width;
            mapRVector.UserData = route;
            mapRoute.Add(mapRVector);

            MapViewer.DrawOnMapData();
            try
            {
                MapViewer.Focus();
                MapViewer.Select();
            }
            catch { };
        }

        private void AfterClick()
        {
            MapViewer.DrawOnMapData();
            if (rbGR.Checked)
            {
                GetRoute();                
            };
        }

        private bool GetRoute()
        {
            if (groute == null) return false;
            if ((groute.start == null) || (groute.finish == null)) return false;
            if (mapRVector != null) mapRoute.Remove(mapRVector);
            //
            PointF a = new PointF((float)groute.start[1], (float)groute.start[2]);
            PointF b = new PointF((float)groute.finish[1], (float)groute.finish[2]);
            PointF[] vector = null;
            nmsRouteClient.Route route = null;
            rtStatus.Text = "Запрос маршрута...";
            Application.DoEvents();
            WaitingBoxForm wbf = new WaitingBoxForm(this);
            double rLength = groute.GetRoute(a, b, wbf, out vector, out route);
            wbf.Hide();
            wbf = null;
            Application.DoEvents();
            if ((rLength == double.MaxValue) || (vector == null))
            {
                rtStatus.Text = "Маршрут не найден";
                MapViewer.DrawOnMapData();
                try
                {
                    MapViewer.Focus();
                    MapViewer.Select();
                }
                catch { };
                return false;
            };

            rtStatus.Text = String.Format(System.Globalization.CultureInfo.InvariantCulture, "Длина пути: {0:0.00} km", rLength / 1000.0);

            mapRVector = new NaviMapNet.MapPolyLine(vector);
            mapRVector.Color = groute.color;
            mapRVector.Width = groute.width;
            mapRVector.UserData = route;
            mapRoute.Add(mapRVector);

            MapViewer.DrawOnMapData();
            try
            {
                MapViewer.Focus();
                MapViewer.Select();
            }
            catch { };
            return true;
        }

        private void rbGet_ButtonClick(object sender, EventArgs e)
        {
            GetRoute();
        }

        private void rbClear_ButtonClick(object sender, EventArgs e)
        {
            if (groute != null)
            {
                groute.start = null;
                groute.finish = null;
                groute.counter = 0;
            };
            rtStart.Text = "START";
            rtFinish.Text = "FINISH";
            mapRoute.Clear();
            mapRStart = null;
            mapRFinish = null;
            mapRVector = null;
            MapViewer.DrawOnMapData();
        }

        private void clearAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rbClear_ButtonClick(sender, e);
        }

        private void clearWayOnlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (mapRVector != null)
                mapRoute.Remove(mapRVector);
            mapRVector = null;
            MapViewer.DrawOnMapData();
        }

        private void timeoutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int w = groute.timeout;
            if (System.Windows.Forms.InputBox.Show("Web Route Engine", "Select timeout for Route request:", ref w, 10, 180) != DialogResult.OK) return;
            groute.timeout = w;
        }

        private void rbSet_DropDownOpening(object sender, EventArgs e)
        {
            setKeyToolStripMenuItem.Enabled = (groute != null) && (groute.service < 0);
            setToolStripMenuItem.Enabled = (groute != null) && (groute.service < 0);
            if (groute == null) return;
            if (groute.service > 0)
                rOUTEENFINEToolStripMenuItem.Text = String.Format("{0} - OSRM Engine [{1}] ...", groute.ServiceIndex, groute.ServiceName);
            else
                rOUTEENFINEToolStripMenuItem.Text = String.Format("{0} - dkxce Engine [{1}] ...", groute.ServiceIndex, groute.ServiceName);
        }

        private void rOUTEENFINEToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void selectRouteServiceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (groute == null) return;

            int si = 0;
            List<string> svcs = new List<string>();
            List<int> svci = new List<int>();
            if (groute.url_dkxce.Count > 0)
            {
                for (int i = 0; i < groute.url_dkxce.Count; i++)
                {
                    svcs.Add(String.Format("D{0}: {1}", i + 1, groute.url_dkxce[i].name));
                    int sid = -1 * i - 1;
                    svci.Add(sid);
                    if (sid == groute.service) si = svci.Count - 1;
                };
            };
            if (groute.url_osrm.Count > 0)
            {
                for (int i = 0; i < groute.url_osrm.Count; i++)
                {
                    svcs.Add(String.Format("O{0}: {1}", i + 1, groute.url_osrm[i].name));
                    int sid = i + 1;
                    svci.Add(sid);
                    if (sid == groute.service) si = svci.Count - 1;
                };
            };

            if (System.Windows.Forms.InputBox.Show("Select Route Engine", "Select Web Route Service (D - dkxce Engine, O - OSRM Engine):", svcs.ToArray(), ref si) != DialogResult.OK) return;
            groute.service = svci[si];
            rbSet.Text = String.Format("Set ({0})", groute.ServiceIndex);
            rbGet.Text = String.Format("Get ({0})", groute.ServiceIndex);
        }

        private void makerout_Click(object sender, EventArgs e)
        {
            GetRoute();
        }

        private void rbMi_Click(object sender, EventArgs e)
        {
            groute.mode = 4;
            rbDN.Checked = false;
            rbStFi.Checked = false;
            rbSt.Checked = false;
            rbFi.Checked = false;
            rbMi.Checked = true;
            oncb.Text = rbMi.Text;
        }

        private void propsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.ShowChangeDialog();
            Properties.Save();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XProcessData
    {
        public int dataSize;
        public IntPtr dataPtr;
        public int dataType; // 1 - string
    }

    [Serializable]
    public class State : XMLSaved<State>
    {
        public string SASCacheDir = @"C:\Program Files\SASPlanet\cache";
        public int MapID = -1;
        public string SASDir = null;
        public string URL = null;
        public string FILE = null;
        public double X = 39.549407958984375;
        public double Y = 52.590535060652833;
        public int Z = 10;
    }

    [Serializable]
    public class XMLSaved<T>
    {
        public static string ToUpper(string str)
        {
            if (String.IsNullOrEmpty(str)) return "";
            return str.ToUpper();
        }

        /// <summary>
        ///     Сохранение структуры в файл
        /// </summary>
        /// <param name="file">Полный путь к файлу</param>
        /// <param name="obj">Структура</param>
        public static void Save(string file, T obj)
        {
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.IO.StreamWriter writer = System.IO.File.CreateText(file);
            xs.Serialize(writer, obj);
            writer.Flush();
            writer.Close();
        }

        public static string Save(T obj)
        {
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.IO.MemoryStream ms = new MemoryStream();
            System.IO.StreamWriter writer = new StreamWriter(ms);
            xs.Serialize(writer, obj);
            writer.Flush();
            ms.Position = 0;
            byte[] bb = new byte[ms.Length];
            ms.Read(bb, 0, bb.Length);
            writer.Close();
            return System.Text.Encoding.UTF8.GetString(bb); ;
        }

        /// <summary>
        ///     Подключение структуры из файла
        /// </summary>
        /// <param name="file">Полный путь к файлу</param>
        /// <returns>Структура</returns>
        public static T Load(string file)
        {
            // if couldn't create file in temp - add credintals
            // http://support.microsoft.com/kb/908158/ru
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(T));
            System.IO.StreamReader reader = System.IO.File.OpenText(file);
            T c = (T)xs.Deserialize(reader);
            reader.Close();
            return c;
        }
    }

    public class APRSCFG
    {
        public List<string> hipp = new List<string>();
        public int selected = 0;
        public string callsign = "viewonly";
        public string password = "-1";
        public string filter = "r/55.55/37.55/350 m/350 -fn/AIR";
        public byte mode = 0;
        public string last = "aprs.cqham.ru:14580";

        public APRSCFG()
        {
            
        }

        public static APRSCFG CreateNew()
        {
            APRSCFG res = new APRSCFG();
            res.hipp.Add("aprs.cqham.ru:14580");
            res.hipp.Add("russia.aprs2.net:14580");
            return res;
        }

        public void Save()
        {
            try
            {
                string gn = KMZViewerForm.CurrentDirectory() + @"\KMZViewer.aprs";
                XMLSaved<APRSCFG>.Save(gn, this);
            }
            catch { };
        }

        public static APRSCFG Load()
        {
            string gn = KMZViewerForm.CurrentDirectory() + @"\KMZViewer.aprs";
            if (File.Exists(gn))
            {
                try
                {
                    return XMLSaved<APRSCFG>.Load(gn);                    
                }
                catch { };
            };
            return APRSCFG.CreateNew();
        }
    }

    internal class PacketXchange: ax25kiss.AX25Handler
    {
        private KMZViewerForm parent;
        private ulong pCounter = 0;

        public PacketXchange(KMZViewerForm parent)
        {
            this.parent = parent;
        }

        public void handlePacket(ax25kiss.Packet packet)
        {
            string spacket = packet.ToString();
            if (spacket.IndexOf(":}") > 0) spacket = spacket.Substring(spacket.IndexOf(":}") + 2);
            Buddie b = APRSData.ParseAPRSPacket(spacket);
            if ((b != null) && b.PositionIsValid && (!String.IsNullOrEmpty(b.name)))
                Buddies.Update(b);            
            string frm = "???";
            if(KMZViewerForm.kiss != null) frm = KMZViewerForm.kiss.Mode == ax25kiss.KISSTNC.ConnectionMode.AGW ? KMZViewerForm.kiss.Mode.ToString() : "KISS " + KMZViewerForm.kiss.Mode.ToString();
            KMZViewerForm.tcpState = String.Format("Получено {0} пакетов ({1})", ++pCounter, frm);
            string txt = DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy") + ": \r\n" + spacket + "\r\n\r\n";
            parent.Invoke(new KMZViewer.RunProcStdOutForm.DoText(parent.UpdateKissLog), new object[] { txt });
        }
    }

    [Serializable]
    public class GetRouter
    {
        public int mode = 0;
        public int service = -1; // < 0 - dkxce; > 0 - OSRM      
        public bool getroute = false;
        public bool saveroute = false;
        public int routecolor { get { return color.ToArgb(); } set { color = Color.FromArgb(value); } }
        public int width = 5;
        public int timeout = 30;

        [XmlIgnore]
        public object[] start = null;
        [XmlIgnore]
        public object[] finish = null;
        [XmlIgnore]
        public ulong counter = 0;
        [XmlIgnore]
        public Color color = Color.OrangeRed;

        [XmlArray(ElementName = "dkxce.Route.Service")]
        [XmlArrayItem(ElementName = "url")]
        public List<DRSParams> url_dkxce = new List<DRSParams>();

        [XmlArray(ElementName = "OSRMaps.Route")]
        [XmlArrayItem(ElementName = "url")] // http://project-osrm.org/docs/v5.24.0/api/#
        public List<OSRMParams> url_osrm = new List<OSRMParams>();

        [XmlIgnore]
        public string ServiceURL
        {
            get { if (service >= 0) return url_osrm[service - 1].url; else return url_dkxce[-1 * service - 1].url; }
            set { if (service >= 0) url_osrm[service - 1].url = value; else url_dkxce[-1 * service - 1].url = value; }
        }
        [XmlIgnore]
        public string ServiceName
        {
            get { if (service >= 0) return url_osrm[service - 1].name; else return url_dkxce[-1 * service - 1].name; }
            set { if (service >= 0) url_osrm[service - 1].name = value; else url_dkxce[-1 * service - 1].name = value; }
        }
        [XmlIgnore]
        public string ServiceIndex
        {
            get { if (service >= 0) return String.Format("O{0}", service); else return String.Format("D{0}", -1 * service); }
        }
        [XmlIgnore]
        public string ServiceEngine { get { if (service > 0) return "OSRM"; else return "dkxce"; } }

        public double GetRoute(PointF[] points, WaitingBoxForm wbf, out PointF[] vector, out nmsRouteClient.Route route)
        {
            vector = null;
            route = null;
            if (service >= 0) return GetRouteOSRM(points, wbf, out vector, out route, url_osrm[service - 1]);
            return GetRouteDKXCE(points, wbf, out vector, out route, url_dkxce[-1 * service - 1]);
            return 0;
        }

        public nmsRouteClient.Route GetRoute(PointF a, PointF b)
        {
            if (service >= 0) return GetRouteOSRM(a, b, url_osrm[service - 1]);
            return GetRouteDKXCE(a, b, url_dkxce[-1 * service - 1]);
        }

        public nmsRouteClient.Route GetRouteDKXCE(PointF a, PointF b, DRSParams param)
        {
            nmsRouteClient.Route res = new nmsRouteClient.Route();
            res.LastError = "Couldn't request route";

            string furl = param.url;
            {
                int iu = furl.ToUpper().IndexOf("/NMS");
                if (iu > 0) furl = furl.Substring(0, iu + 4) + "/";
                string xx = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", a.X, b.X);
                string yy = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", a.Y, b.Y);
                furl += String.Format("route?k={0}&f=2&p=1&i=0&minby=time&x={1}&y={2}&ra={3}&n=start,dest", param.key.Trim(), xx, yy, param.ra.Trim());
            };

            try
            {
                System.Net.HttpWebRequest wReq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(furl);
                wReq.Timeout = this.timeout * 1000;
                System.Net.HttpWebResponse wRes = (System.Net.HttpWebResponse)wReq.GetResponse();
                StreamReader sr = new StreamReader(wRes.GetResponseStream());
                string xml = sr.ReadToEnd();
                sr.Close();
                wRes.Close();

                if (String.IsNullOrEmpty(xml)) { res.LastError = "No valid route XML"; return res; };

                return nmsRouteClient.RouteClient.XMLToObject(xml);
            }
            catch (Exception ex)
            {
                res.LastError = ex.Message;
                return res;
            };
        }

        public nmsRouteClient.Route GetRouteOSRM(PointF a, PointF b, OSRMParams param)
        {
            nmsRouteClient.Route route = new nmsRouteClient.Route();
            route.LastError = "Couldn't request route";

            string furl = param.url;
            {
                int iu = furl.LastIndexOf("/");
                if (iu > 0) furl = furl.Substring(0, iu + 1);
                string xyxy = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1};{2},{3}", a.X, a.Y, b.X, b.Y);
                furl += String.Format("{0}?overview=full&geometries=polyline", xyxy);
            };

            try
            {
                System.Net.HttpWebRequest wReq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(furl);
                wReq.Timeout = this.timeout * 1000;
                System.Net.HttpWebResponse wRes = (System.Net.HttpWebResponse)wReq.GetResponse();
                StreamReader sr = new StreamReader(wRes.GetResponseStream());
                string json = sr.ReadToEnd();
                sr.Close();
                wRes.Close();

                if (String.IsNullOrEmpty(json)) throw new Exception("No valid route JSON");

                OSMRResponse osmr = OSMRResponse.FromText(json);
                if ((!String.IsNullOrEmpty(osmr.code)) && (osmr.code.ToLower() != "ok")) throw new Exception(osmr.code);
                if ((osmr.routes == null) || (osmr.routes.Length == 0)) { route.LastError = osmr.code; return route; };

                PointF[] vector = osmr.routes[0].points;
                route = new nmsRouteClient.Route();
                route.driveLength = osmr.routes[0].distance;
                route.driveTime = osmr.routes[0].duration / 60.0;
                route.polyline = new nmsRouteClient.XYPoint[vector.Length];
                for (int i = 0; i < vector.Length; i++)
                    route.polyline[i] = new nmsRouteClient.XYPoint(vector[i].X, vector[i].Y);
            }
            catch (Exception ex)
            {
                route.LastError = ex.Message;
                return route;
            };
            return route;
        }

        public double GetRoute(PointF a, PointF b, WaitingBoxForm wbf, out PointF[] vector, out nmsRouteClient.Route route)
        {
            if (service >= 0) return GetRouteOSRM(a, b, wbf, out vector, out route, url_osrm[service - 1]);
            return GetRouteDKXCE(a, b, wbf, out vector, out route, url_dkxce[-1 * service - 1]);
        }

        public double GetRouteDKXCE(PointF a, PointF b, WaitingBoxForm wbf, out PointF[] vector, out nmsRouteClient.Route route, DRSParams param)
        {
            vector = null;
            route = null;
            double res = double.MaxValue;

            string furl = param.url;
            {
                int iu = furl.ToUpper().IndexOf("/NMS");
                if (iu > 0) furl = furl.Substring(0, iu + 4) + "/";
                string xx = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", a.X, b.X);
                string yy = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", a.Y, b.Y);
                furl += String.Format("route?k={0}&f=2&p=1&i=0&minby=time&x={1}&y={2}&ra={3}&n=start,dest", param.key.Trim(), xx, yy, param.ra.Trim());
            };
            wbf.Show("Request route", param.url);
            try
            {
                System.Net.HttpWebRequest wReq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(furl);
                wReq.Timeout = this.timeout * 1000;
                System.Net.HttpWebResponse wRes = (System.Net.HttpWebResponse)wReq.GetResponse();
                StreamReader sr = new StreamReader(wRes.GetResponseStream());
                string xml = sr.ReadToEnd();
                sr.Close();
                wRes.Close();

                if (String.IsNullOrEmpty(xml)) throw new Exception("No valid route XML");

                route = nmsRouteClient.RouteClient.XMLToObject(xml);
                if (!String.IsNullOrEmpty(route.LastError)) throw new Exception(route.LastError);
                res = route.driveLength;
                vector = new PointF[route.polyline.Length];
                for (int i = 0; i < vector.Length; i++)
                    vector[i] = new PointF((float)route.polyline[i].x, (float)route.polyline[i].y);
            }
            catch (Exception ex)
            {
                wbf.Hide();
                MessageBox.Show("Get route failed\r\nError: " + ex.Message, "Get Route", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return double.MaxValue;
            };
            wbf.Hide();
            return res;
        }

        public double GetRouteDKXCE(PointF[] points, WaitingBoxForm wbf, out PointF[] vector, out nmsRouteClient.Route route, DRSParams param)
        {
            vector = null;
            route = null;
            double res = double.MaxValue;
            if ((points == null) || (points.Length < 2)) return res;

            string furl = param.url;
            {
                int iu = furl.ToUpper().IndexOf("/NMS");
                if (iu > 0) furl = furl.Substring(0, iu + 4) + "/";
                string xx = "";
                string yy = "";
                for (int i = 0; i < points.Length; i++)
                {
                    if (xx.Length > 0) xx += ",";
                    if (yy.Length > 0) yy += ",";
                    xx += String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", points[i].X);
                    yy += String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", points[i].Y);
                };
                furl += String.Format("route?k={0}&f=2&p=1&i=0&minby=time&x={1}&y={2}&ra={3}&n=start,dest", param.key.Trim(), xx, yy, param.ra.Trim());
            };
            wbf.Show("Request route", param.url);
            try
            {
                System.Net.HttpWebRequest wReq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(furl);
                wReq.Timeout = this.timeout * 1000;
                System.Net.HttpWebResponse wRes = (System.Net.HttpWebResponse)wReq.GetResponse();
                StreamReader sr = new StreamReader(wRes.GetResponseStream());
                string xml = sr.ReadToEnd();
                sr.Close();
                wRes.Close();

                if (String.IsNullOrEmpty(xml)) throw new Exception("No valid route XML");

                route = nmsRouteClient.RouteClient.XMLToObject(xml);
                if (!String.IsNullOrEmpty(route.LastError)) throw new Exception(route.LastError);
                res = route.driveLength;
                vector = new PointF[route.polyline.Length];
                for (int i = 0; i < vector.Length; i++)
                    vector[i] = new PointF((float)route.polyline[i].x, (float)route.polyline[i].y);
            }
            catch (Exception ex)
            {
                wbf.Hide();
                MessageBox.Show("Get route failed\r\nError: " + ex.Message, "Get Route", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return double.MaxValue;
            };
            wbf.Hide();
            return res;
        }

        public double GetRouteOSRM(PointF a, PointF b, WaitingBoxForm wbf, out PointF[] vector, out nmsRouteClient.Route route, OSRMParams param)
        {
            vector = null;
            route = null;
            double res = double.MaxValue;

            string furl = param.url;
            {
                int iu = furl.LastIndexOf("/");
                if (iu > 0) furl = furl.Substring(0, iu + 1);
                string xyxy = String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1};{2},{3}", a.X, a.Y, b.X, b.Y);
                furl += String.Format("{0}?overview=full&geometries=polyline", xyxy);
            };
            wbf.Show("Request route", param.url);
            try
            {
                System.Net.HttpWebRequest wReq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(furl);
                wReq.Timeout = this.timeout * 1000;
                System.Net.HttpWebResponse wRes = (System.Net.HttpWebResponse)wReq.GetResponse();
                StreamReader sr = new StreamReader(wRes.GetResponseStream());
                string json = sr.ReadToEnd();
                sr.Close();
                wRes.Close();

                if (String.IsNullOrEmpty(json)) throw new Exception("No valid route JSON");

                OSMRResponse osmr = OSMRResponse.FromText(json);
                if ((!String.IsNullOrEmpty(osmr.code)) && (osmr.code.ToLower() != "ok")) throw new Exception(osmr.code);
                if ((osmr.routes == null) || (osmr.routes.Length == 0)) return double.MaxValue;
                res = osmr.routes[0].distance;
                vector = osmr.routes[0].points;

                route = new nmsRouteClient.Route();
                route.driveLength = osmr.routes[0].distance;
                route.driveTime = osmr.routes[0].duration / 60.0;
                route.polyline = new nmsRouteClient.XYPoint[vector.Length];
                for (int i = 0; i < vector.Length; i++)
                    route.polyline[i] = new nmsRouteClient.XYPoint(vector[i].X, vector[i].Y);
            }
            catch (Exception ex)
            {
                wbf.Hide();
                MessageBox.Show("Get route failed\r\nError: " + ex.Message, "Get Route", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return double.MaxValue;
            };
            wbf.Hide();
            return res;
        }

        public double GetRouteOSRM(PointF[] points, WaitingBoxForm wbf, out PointF[] vector, out nmsRouteClient.Route route, OSRMParams param)
        {
            vector = null;
            route = null;
            double res = double.MaxValue;

            if ((points == null) || (points.Length < 2)) return res;

            string furl = param.url;
            {
                int iu = furl.LastIndexOf("/");
                if (iu > 0) furl = furl.Substring(0, iu + 1);
                string xyxy = "";
                for (int i = 0; i < points.Length; i++)
                {
                    if (xyxy.Length > 0) xyxy += ";";
                    xyxy += String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", points[i].X, points[i].Y);
                };
                furl += String.Format("{0}?overview=full&geometries=polyline", xyxy);
            };
            wbf.Show("Request route", param.url);
            try
            {
                System.Net.HttpWebRequest wReq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(furl);
                wReq.Timeout = this.timeout * 1000;
                System.Net.HttpWebResponse wRes = (System.Net.HttpWebResponse)wReq.GetResponse();
                StreamReader sr = new StreamReader(wRes.GetResponseStream());
                string json = sr.ReadToEnd();
                sr.Close();
                wRes.Close();

                if (String.IsNullOrEmpty(json)) throw new Exception("No valid route JSON");

                OSMRResponse osmr = OSMRResponse.FromText(json);
                if ((!String.IsNullOrEmpty(osmr.code)) && (osmr.code.ToLower() != "ok")) throw new Exception(osmr.code);
                if ((osmr.routes == null) || (osmr.routes.Length == 0)) return double.MaxValue;
                res = osmr.routes[0].distance;
                vector = osmr.routes[0].points;

                route = new nmsRouteClient.Route();
                route.driveLength = osmr.routes[0].distance;
                route.driveTime = osmr.routes[0].duration / 60.0;
                route.polyline = new nmsRouteClient.XYPoint[vector.Length];
                for (int i = 0; i < vector.Length; i++)
                    route.polyline[i] = new nmsRouteClient.XYPoint(vector[i].X, vector[i].Y);
            }
            catch (Exception ex)
            {
                wbf.Hide();
                MessageBox.Show("Get route failed\r\nError: " + ex.Message, "Get Route", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return double.MaxValue;
            };
            wbf.Hide();
            return res;
        }

        public static GetRouter Load()
        {
            string fName = KMZViewerForm.CurrentDirectory() + @"\KMZViewer.rtc";
            if (File.Exists(fName))
            {
                try
                {
                    GetRouter res = XMLSaved<GetRouter>.Load(fName);
                    if (res.service == 0) res.service = -1;
                    if (res.service > 0)
                    {
                        int ind = res.service - 1;
                        if (ind >= res.url_osrm.Count) res.service = -1;
                    };
                    if (res.service < 0)
                    {
                        int ind = res.service * -1 - 1;
                        if (ind >= res.url_dkxce.Count) res.service = -1;
                    };
                    if ((res.url_dkxce == null) || (res.url_dkxce.Count == 0)) res.url_dkxce = new List<DRSParams>(new DRSParams[] { new DRSParams() });
                    if ((res.url_osrm == null) || (res.url_osrm.Count == 0)) res.url_osrm = new List<OSRMParams>(new OSRMParams[] { new OSRMParams("map.project-osrm.org", "http://router.project-osrm.org/route/v1/driving/"), new OSRMParams("maps.openrouteservice.org", "http://routing.openstreetmap.de/routed-car/route/v1/driving/") });
                    return res;
                }
                catch { };
            };
            {
                GetRouter res = new GetRouter();
                res.url_dkxce = new List<DRSParams>(new DRSParams[] { new DRSParams() });
                res.url_osrm = new List<OSRMParams>(new OSRMParams[] { new OSRMParams("map.project-osrm.org", "http://router.project-osrm.org/route/v1/driving/"), new OSRMParams("maps.openrouteservice.org", "http://routing.openstreetmap.de/routed-car/route/v1/driving/") });
                return res;
            };
        }

        public void Save()
        {
            try
            {
                string fName = KMZViewerForm.CurrentDirectory() + @"\KMZViewer.rtc";
                XMLSaved<GetRouter>.Save(fName, this);
            }
            catch { };
        }

        public class DRSParams
        {
            [XmlText]
            public string url = "http://localhost:8080/nms/";
            [XmlAttribute]
            public string key = "TEST";
            [XmlAttribute]
            public string ra = "00000000000000000000000000000000";
            [XmlAttribute]
            public string name = "localhost:8080";
        }

        public class OSRMParams
        {
            [XmlText]
            public string url;
            [XmlAttribute]
            public string name;
            public OSRMParams() { }
            public OSRMParams(string name, string url) { this.name = name; this.url = url; }
        }

        public class OSMRResponse
        {
            public class OSMRRoute
            {
                public string geometry;
                public string weight_name;
                public double weight;
                public double distance;
                public double duration;
                public PointF[] points { get { return DecodeA(this.geometry); } }

                private static IEnumerable<PointF> Decode(string polylineString)
                {
                    char[] polylineChars = polylineString.ToCharArray();
                    int index = 0;

                    double currentLat = 0;
                    double currentLng = 0;

                    while (index < polylineChars.Length)
                    {
                        // Next lat
                        int sum = 0;
                        int shifter = 0;
                        int nextFiveBits;
                        do
                        {
                            nextFiveBits = polylineChars[index++] - 63;
                            sum |= (nextFiveBits & 31) << shifter;
                            shifter += 5;
                        } while (nextFiveBits >= 32 && index < polylineChars.Length);

                        if (index >= polylineChars.Length)
                            break;

                        currentLat += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                        // Next lng
                        sum = 0;
                        shifter = 0;
                        do
                        {
                            nextFiveBits = polylineChars[index++] - 63;
                            sum |= (nextFiveBits & 31) << shifter;
                            shifter += 5;
                        } while (nextFiveBits >= 32 && index < polylineChars.Length);

                        if (index >= polylineChars.Length && nextFiveBits >= 32)
                            break;

                        currentLng += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                        yield return new PointF((float)(Convert.ToDouble(currentLng) / 1.0E+5), (float)(Convert.ToDouble(currentLat) / 1.0E+5));
                    };
                }

                private static PointF[] DecodeA(string polylineString)
                {
                    List<PointF> res = new List<PointF>();
                    foreach (PointF pnt in Decode(polylineString)) res.Add(pnt);
                    return res.ToArray();
                }
            }

            public string code;
            public OSMRRoute[] routes;

            public static OSMRResponse FromText(string text)
            {
                OSMRResponse result = new OSMRResponse();
                List<OSMRRoute> resrts = new List<OSMRRoute>();

                Newtonsoft.Json.Linq.JToken osmd = (Newtonsoft.Json.Linq.JContainer)Newtonsoft.Json.JsonConvert.DeserializeObject(text);
                foreach (Newtonsoft.Json.Linq.JProperty suntoken in osmd)
                {
                    if (suntoken.Name == "code") result.code = suntoken.Value.ToString();
                    if (suntoken.Name == "routes")
                    {
                        foreach (Newtonsoft.Json.Linq.JObject rt in suntoken.Value)
                        {
                            OSMRRoute rres = new OSMRRoute();
                            foreach (Newtonsoft.Json.Linq.JProperty trp in (Newtonsoft.Json.Linq.JContainer)rt)
                            {
                                if (trp.Name == "geometry") rres.geometry = trp.Value.ToString();
                                if (trp.Name == "weight_name") rres.weight_name = trp.Value.ToString();
                                if (trp.Name == "weight") double.TryParse(trp.Value.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out rres.weight);
                                if (trp.Name == "distance") double.TryParse(trp.Value.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out rres.distance);
                                if (trp.Name == "duration") double.TryParse(trp.Value.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out rres.duration);
                            };
                            resrts.Add(rres);
                        };
                    };
                };
                result.routes = resrts.ToArray();
                return result;
            }
        }

        public static Image ImageFromNumber(int num)
        {
            Bitmap im = new Bitmap(16, 16);
            Graphics g = Graphics.FromImage(im);
            g.FillRectangle(new SolidBrush(Color.Black), new Rectangle(0, 0, 16, 16));
            g.DrawString(String.Format("{0:00}", num), new Font("Tahoma", 8, FontStyle.Regular), new SolidBrush(Color.White), new PointF(0, 1));
            g.Dispose();
            return im;
        }
    }

}