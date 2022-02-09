/***************************************************/
/*                                                 */
/*            C# Garmin POI File Reader            */
/*              (by milokz@gmail.com)              */
/*                                                 */
/*         GPIReader by milokz@gmail.com           */
/*     Part of KMZRebuilder & KMZViewer Project    */
/*                                                 */
/*             by reverse engineering              */
/*                                                 */
/***************************************************/

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.InteropServices;

namespace KMZ_Viewer
{
    #region RECTYPES
    /// <summary>
    ///     GPI File Record Types
    /// </summary>
    public enum RecType : ushort
    {
        Header0 = 0,
        Header1 = 1,
        Waypoint = 2,
        Alert = 3,
        BitmapReference = 4,
        Bitmap = 5,
        CategoryReference = 6,
        Category = 7,
        Area = 8,
        POIGroup = 9,
        Comment = 10,
        Address = 11,
        Contact = 12,
        Image = 13,
        Description = 14,
        ProductInfo = 15,
        AlertCircle = 16,
        Copyright = 17,
        Media = 18,
        SpeedCamera = 19,
        AlertTriggerOptions = 27,
        End = 0xFFFF
    }

    /// <summary>
    ///     Type Utils
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class RecEnum<T>
    {
        public static bool IsDefined(string name)
        {
            return Enum.IsDefined(typeof(T), name);
        }

        public static bool IsDefined(T value)
        {
            return Enum.IsDefined(typeof(T), value);
        }
    }

    /// <summary>
    ///     GPI File Record Block
    /// </summary>
    public class Record
    {
        /// <summary>
        ///     Parent Record
        /// </summary>
        public Record Parent = null;
        /// <summary>
        ///     Child Records
        /// </summary>
        public List<Record> Childs = new List<Record>();

        /// <summary>
        ///     Record is on top
        /// </summary>
        public bool RootIsTop { get { return Parent == null; } }
        /// <summary>
        ///     Record Nesting Index
        /// </summary>
        public int RootLevel { get { return Parent == null ? 0 : Parent.RootLevel + 1; } }
        /// <summary>
        ///     Record Nesting Ierarchy
        /// </summary>
        internal string RootIerarchy { get { return Parent == null ? @"\Root" : Parent.RootIerarchy + @"\" + RecordType.ToString(); } }

        /// <summary>
        ///     Has Extra Block Data
        /// </summary>
        public bool RecHasExtra { get { return (RecFlags & 0x08) == 0x08; } }
        /// <summary>
        ///     GPI Record Type
        /// </summary>
        public RecType RecordType { get { return (RecType)RecType; } }
        /// <summary>
        ///     GPI Record Type
        /// </summary>
        internal ushort RecType = 0;
        /// <summary>
        ///     GPI Record Flags
        /// </summary>
        internal ushort RecFlags = 0;

        /// <summary>
        ///     Offset of Record Block
        /// </summary>
        internal uint OffsetBlock = 0;
        /// <summary>
        ///     Offset of Record Main Data Block
        /// </summary>
        internal uint OffsetMain = 0;
        /// <summary>
        ///     Offset of Record Extra Data Block
        /// </summary>
        internal uint OffsetExtra = 0;

        /// <summary>
        ///     Record Block Length
        /// </summary>
        internal uint LengthBlock = 0;
        /// <summary>
        ///     Record Block Main Data Length
        /// </summary>
        internal uint LengthMain = 0;
        /// <summary>
        ///     Record Block Extra Data Length
        /// </summary>
        internal uint LengthExtra = 0;
        /// <summary>
        ///     Record Block Main & Extra Data Length
        /// </summary>
        internal uint LengthTotal = 0;

        /// <summary>
        ///     Source Block Without Any Offsets
        /// </summary>
        internal byte[] DataBlock;
        /// <summary>
        ///     Record Main Data
        /// </summary>
        public byte[] DataMain
        {
            get
            {
                if (DataBlock == null) return null;
                byte[] res = new byte[LengthMain];
                Array.Copy(DataBlock, OffsetMain, res, 0, LengthMain);
                return res;
            }
        }
        /// <summary>
        ///     Record Extra Data
        /// </summary>
        public byte[] DataExtra
        {
            get
            {
                if (DataBlock == null) return null;
                byte[] res = new byte[LengthExtra];
                Array.Copy(DataBlock, OffsetExtra, res, 0, LengthExtra);
                return res;
            }
        }
        /// <summary>
        ///     Record Main & Extra Data
        /// </summary>
        public byte[] DataTotal
        {
            get
            {
                if (DataBlock == null) return null;
                byte[] res = new byte[LengthTotal];
                Array.Copy(DataBlock, OffsetMain, res, 0, LengthTotal);
                return res;
            }
        }

        /// <summary>
        ///     Last Read Record Block Error
        /// </summary>
        internal Exception ReadError = null;

        /// <summary>
        ///     Create with Parent
        /// </summary>
        /// <param name="parent"></param>
        protected Record(Record parent)
        {
            this.Parent = parent;
            if (parent != null) parent.Childs.Add(this);
        }

        /// <summary>
        ///     Create No Parent (File Root)
        /// </summary>
        public static Record ROOT
        {
            get
            {
                return new Record(null);
            }
        }

        public static Record Create(Record parent, uint offset, ref byte[] sourceData, ushort RecordType)
        {
            Record res = null;
            if (RecordType == 0) res = new RecHeader0(parent);
            if (RecordType == 1) res = new RecHeader1(parent);
            if (RecordType == 2) res = new RecWaypoint(parent);
            if (RecordType == 3) res = new RecAlert(parent);
            if (RecordType == 4) res = new RecBitmapReference(parent);
            if (RecordType == 5) res = new RecBitmap(parent);
            if (RecordType == 6) res = new RecCategoryReference(parent);
            if (RecordType == 7) res = new RecCategory(parent);
            if (RecordType == 8) res = new RecArea(parent);
            if (RecordType == 9) res = new RecPOIGroup(parent);
            if (RecordType == 10) res = new RecComment(parent);
            if (RecordType == 11) res = new RecAddress(parent);
            if (RecordType == 12) res = new RecContact(parent);
            if (RecordType == 13) res = new RecImage(parent);
            if (RecordType == 14) res = new RecDescription(parent);
            if (RecordType == 15) res = new RecProductInfo(parent);
            if (RecordType == 16) res = new RecAlertCircle(parent);
            if (RecordType == 17) res = new RecCopyright(parent);
            if (RecordType == 18) res = new RecMedia(parent);
            if (RecordType == 19) res = new RecSpeedCamera(parent);
            if (RecordType == 27) res = new RecAlertTriggerOptions(parent);
            if (RecordType == 0xFFFF) res = new RecEnd(parent);
            if (res == null) res = new Record(parent);
            res.RecType = RecordType;
            res.OffsetBlock = offset;
            res.DataBlock = sourceData;
            return res;
        }

        public override string ToString()
        {
            return String.Format("{1}[{2}]{3}", RecordType, RecType, RootLevel, RootIerarchy);
        }
    }

    // 0
    public sealed class RecHeader0 : Record
    {
        internal RecHeader0(Record parent) : base(parent) { }
        public string Header = null;
        public string Version = null;
        public DateTime Created = DateTime.MinValue;
        public string Name = null;
    }

    // 1
    public sealed class RecHeader1 : Record
    {
        internal RecHeader1(Record parent) : base(parent) { }
        public string Content = null;
        public ushort CodePage = 0xFDE9;
        public Encoding Encoding
        {
            get
            {
                try { return Encoding.GetEncoding(CodePage); }
                catch { };
                return Encoding.Unicode;
            }
        }
    }

    // 2
    public sealed class RecWaypoint : Record
    {
        internal RecWaypoint(Record parent) : base(parent) { }
        internal int cLat;
        internal int cLon;
        public double Lat { get { return (double)cLat * 360.0 / Math.Pow(2, 32); } }
        public double Lon { get { return (double)cLon * 360.0 / Math.Pow(2, 32); } }
        public List<KeyValuePair<string, string>> ShortName = new List<KeyValuePair<string, string>>();

        public string Name
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in ShortName)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in ShortName)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in ShortName)
                    return kvp.Value;
                return null;
            }
        }

        public RecAlert Alert;
        public RecBitmap Bitmap;
        public RecImage Image;

        public RecDescription Description;
        public RecComment Comment;
        public RecContact Contact;
        public RecAddress Address;
    }

    // 3
    public sealed class RecAlert : Record
    {
        internal RecAlert(Record parent) : base(parent) { }
        public ushort Proximity;
        internal ushort cSpeed;
        public int Speed { get { return (int)Math.Round((double)cSpeed / 100.0 * 3.6); } }
        public byte Alert;
        public byte AlertType;
        public byte SoundNumber;
        public byte AudioAlert;
        public bool IsOn { get { return Alert == 1; } }
        public string IsType
        {
            get
            {
                if (AlertType == 0) return "proximity";
                if (AlertType == 1) return "along_road";
                if (AlertType == 2) return "toure_guide";
                return AlertType.ToString();
            }
        }
        public RecAlertCircle AlertCircles;
        public RecAlertTriggerOptions AlertTriggerOptions;
    }

    // 4
    public sealed class RecBitmapReference : Record
    {
        internal RecBitmapReference(Record parent) : base(parent) { }
        public ushort BitmapID;
    }

    // 5
    public sealed class RecBitmap : Record
    {
        internal RecBitmap(Record parent) : base(parent) { }
        public ushort BitmapID;
        public ushort Height;
        public ushort Width;
        public ushort LineSize;
        public ushort BitsPerPixel;
        public ushort Reserved9;
        public uint ImageSize; // LineSize * Height
        public uint Reserved10;
        public uint Palette;
        public uint TransparentColor;
        public uint Flags;
        public uint Reserved11;
        public byte[] Pixels;
        public uint[] Colors;
    }

    // 6
    public sealed class RecCategoryReference : Record
    {
        internal RecCategoryReference(Record parent) : base(parent) { }
        public ushort CategoryID;
    }

    // 7
    public sealed class RecCategory : Record
    {
        internal RecCategory(Record parent) : base(parent) { }
        public ushort CategoryID;
        public List<KeyValuePair<string, string>> Category = new List<KeyValuePair<string, string>>();
        public string Name
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in Category)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in Category)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in Category)
                    return kvp.Value;
                return null;
            }
        }

        public List<RecWaypoint> Waypoints = new List<RecWaypoint>();
        public RecBitmap Bitmap = null;

        public RecDescription Description;
        public RecComment Comment;
        public RecContact Contact;
    }

    // 8
    public sealed class RecArea : Record
    {
        internal RecArea(Record parent) : base(parent) { }
        internal int cMaxLat;
        internal int cMaxLon;
        internal int cMinLat;
        internal int cMinLon;
        public double MaxLat { get { return (double)cMaxLat * 360.0 / Math.Pow(2, 32); } }
        public double MaxLon { get { return (double)cMaxLon * 360.0 / Math.Pow(2, 32); } }
        public double MinLat { get { return (double)cMinLat * 360.0 / Math.Pow(2, 32); } }
        public double MinLon { get { return (double)cMinLon * 360.0 / Math.Pow(2, 32); } }
    }

    // 9
    public sealed class RecPOIGroup : Record
    {
        internal RecPOIGroup(Record parent) : base(parent) { }
        public List<KeyValuePair<string, string>> DataSource = new List<KeyValuePair<string, string>>();

        public string Name
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in DataSource)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in DataSource)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in DataSource)
                    return kvp.Value;
                return null;
            }
        }
    }

    // 10
    public sealed class RecComment : Record
    {
        internal RecComment(Record parent) : base(parent) { }
        public List<KeyValuePair<string, string>> Comment = new List<KeyValuePair<string, string>>();

        public string Text
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in Comment)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in Comment)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in Comment)
                    return kvp.Value;
                return null;
            }
        }
    }

    // 11
    public sealed class RecAddress : Record
    {
        internal RecAddress(Record parent) : base(parent) { }
        public ushort Flags;
        public List<KeyValuePair<string, string>> aCity = new List<KeyValuePair<string, string>>();
        public List<KeyValuePair<string, string>> aCountry = new List<KeyValuePair<string, string>>();
        public List<KeyValuePair<string, string>> aState = new List<KeyValuePair<string, string>>();
        public string Postal;
        public List<KeyValuePair<string, string>> aStreet = new List<KeyValuePair<string, string>>();
        public string House;

        public string City
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in aCity)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in aCity)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in aCity)
                    return kvp.Value;
                return null;
            }
        }

        public string Country
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in aCountry)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in aCountry)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in aCountry)
                    return kvp.Value;
                return null;
            }
        }

        public string State
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in aState)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in aState)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in aState)
                    return kvp.Value;
                return null;
            }
        }

        public string Street
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in aStreet)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in aStreet)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in aStreet)
                    return kvp.Value;
                return null;
            }
        }
    }

    // 12
    public sealed class RecContact : Record
    {
        internal RecContact(Record parent) : base(parent) { }
        public ushort Flags;
        public string Phone;
        public string Phone2;
        public string Fax;
        public string Email;
        public string Web;
    }

    // 13
    public sealed class RecImage : Record
    {
        internal RecImage(Record parent) : base(parent) { }
        public uint Length;
        public byte[] ImageData;
    }

    // 14
    public sealed class RecDescription : Record
    {
        internal RecDescription(Record parent) : base(parent) { }
        public List<KeyValuePair<string, string>> Description = new List<KeyValuePair<string, string>>();

        public string Text
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in Description)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in Description)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in Description)
                    return kvp.Value;
                return null;
            }
        }
    }

    // 15
    public sealed class RecProductInfo : Record
    {
        internal RecProductInfo(Record parent) : base(parent) { }
        public ushort FactoryID;
        public byte ProductID;
        public byte RegionID;
        public byte VendorID;
    }

    // 16
    public sealed class RecAlertCircle : Record
    {
        internal RecAlertCircle(Record parent) : base(parent) { }
        public ushort Count;
        public double[] lat;
        public double[] lon;
        public uint[] radius;
    }

    // 17
    public sealed class RecCopyright : Record
    {
        internal RecCopyright(Record parent) : base(parent) { }
        public ushort Flags1 = 0;
        public ushort Flags2 = 0;
        public List<KeyValuePair<string, string>> cDataSource = new List<KeyValuePair<string, string>>();
        public List<KeyValuePair<string, string>> cCopyrights = new List<KeyValuePair<string, string>>();
        public string DeviceModel = null;

        public string DataSource
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in cDataSource)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in cDataSource)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in cDataSource)
                    return kvp.Value;
                return null;
            }
        }

        public string Copyrights
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in cCopyrights)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in cCopyrights)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in cCopyrights)
                    return kvp.Value;
                return null;
            }
        }
    }

    // 18
    public sealed class RecMedia : Record
    {
        internal RecMedia(Record parent) : base(parent) { }
        public ushort MediaID;
        public byte Format;
        public bool IsWav { get { return Format == 0; } }
        public bool IsMP3 { get { return Format == 1; } }
        public List<KeyValuePair<string, byte[]>> Content = new List<KeyValuePair<string, byte[]>>();

        public byte[] Media
        {
            get
            {
                foreach (KeyValuePair<string, byte[]> kvp in Content)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, byte[]> kvp in Content)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                return null;
            }
        }
    }

    // 19
    public sealed class RecSpeedCamera : Record
    {
        internal RecSpeedCamera(Record parent) : base(parent) { }
        internal int cMaxLat;
        internal int cMaxLon;
        internal int cMinLat;
        internal int cMinLon;
        public double MaxLat { get { return (double)cMaxLat * 360.0 / Math.Pow(2, 24); } }
        public double MaxLon { get { return (double)cMaxLon * 360.0 / Math.Pow(2, 24); } }
        public double MinLat { get { return (double)cMinLat * 360.0 / Math.Pow(2, 24); } }
        public double MinLon { get { return (double)cMinLon * 360.0 / Math.Pow(2, 24); } }
        public byte Flags;
        internal int cLat;
        internal int cLon;
        public double Lat { get { return (double)cLat * 360.0 / Math.Pow(2, 24); } }
        public double Lon { get { return (double)cLon * 360.0 / Math.Pow(2, 24); } }
    }

    // 27 
    public sealed class RecAlertTriggerOptions : Record
    {
        internal RecAlertTriggerOptions(Record parent) : base(parent) { }
        public byte BearingCount = 0;
        public ushort[] BearingAngle;
        public ushort[] BearingWide;
        public bool[] BearingBiDir;
        public byte[] DateTimeBlock;
        public List<string> DateTimeList = new List<string>();
    }

    [Serializable]
    public class MarkerBlock
    {
        public MarkerBlock() { }

        public static MarkerBlock FromBytes(byte[] data)
        {
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(MarkerBlock));
            MemoryStream ms = new MemoryStream(data);
            System.IO.StreamReader reader = new System.IO.StreamReader(ms, System.Text.Encoding.UTF8);
            MarkerBlock c = (MarkerBlock)xs.Deserialize(reader);
            ms.Close();
            return c;
        }

        public byte[] ToBytes()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            this.Creator = "KMZRebuilder v" + fvi.FileVersion;
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = false;
            settings.OmitXmlDeclaration = true;
            settings.NewLineHandling = NewLineHandling.None;
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            System.Xml.Serialization.XmlSerializer xs = new System.Xml.Serialization.XmlSerializer(typeof(MarkerBlock));
            System.IO.MemoryStream ms = new MemoryStream();
            XmlWriter writer = XmlWriter.Create(ms, settings);
            xs.Serialize(writer, this, ns);
            writer.Flush();
            ms.Position = 0;
            byte[] bb = new byte[ms.Length];
            ms.Read(bb, 0, bb.Length);
            writer.Close();
            ms.Close();
            return bb;
        }

        [XmlArray("Bounds"), XmlArrayItem("B")]
        public double[] Bounds = null;
        public string Creator = "KMZRebuilder";
        [XmlElement("DT")]
        public DateTime Created = DateTime.MinValue;
        public uint AlertZones = 0;
        public string Description = null;
    }

    // 0xFFFF
    public sealed class RecEnd : Record
    {
        internal RecEnd(Record parent) : base(parent) { }
    }
    #endregion RECTYPES

    /// <summary>
    ///     GPI Reader
    /// </summary>
    public class GPIReader
    {
        public delegate void Add2LogProc(string text);

        /// <summary>
        ///     Current Locale Language ISO-639
        /// </summary>
        public static string LOCALE_LANGUAGE = "EN"; // 2-SYMBOLS
        /// <summary>
        ///     Default Language ISO-639
        /// </summary>
        public static string DEFAULT_LANGUAGE = "EN"; // 2-SYMBOLS
        /// <summary>
        ///     Save Media Content to disk
        /// </summary>
        public static bool SAVE_MEDIA = false;
        /// <summary>
        ///     Create Images for categories without images
        /// </summary>
        public static bool CREATE_CATEGORY_IMAGES_IFNO = false;
        /// <summary>
        ///     Set kmz poi image from jpeg (not bitmap); false - from bitmap; true - from image (if specified)
        /// </summary>
        public static bool POI_IMAGE_FROM_JPEG = false; // bitmap o
        /// <summary>
        ///     Save Multilanguage Names in Description
        /// </summary>
        public static bool SAVE_MULTINAMES = true;

        /// <summary>
        ///     Source File Name
        /// </summary>
        public string FileName { get { return fileName; } }
        private string fileName;

        /// <summary>
        ///     Public GPI Root Element
        /// </summary>
        public Record RootElement = Record.ROOT;

        /// <summary>
        ///     GPI File Document Name
        /// </summary>
        public string Content = null;
        /// <summary>
        ///     GPI Text CodePage
        /// </summary>
        public ushort CodePage = 0xFDE9;
        /// <summary>
        ///     GPI Text Encoding
        /// </summary>
        public Encoding Encoding = Encoding.Unicode;
        /// <summary>
        ///     GPI File Header Text
        /// </summary>
        public string Header = null;
        /// <summary>
        ///     GPI File Version
        /// </summary>
        public string Version = null;
        /// <summary>
        ///     GPI File DateTime Created
        /// </summary>
        public DateTime Created = DateTime.MinValue;
        /// <summary>
        ///     GPI File Name
        /// </summary>
        public string Name = null;
        /// <summary>
        ///     Multilang Content Data Sources
        /// </summary>
        public List<KeyValuePair<string, string>> cDataSource = new List<KeyValuePair<string, string>>();
        /// <summary>
        ///     Multilang Content Copyrights
        /// </summary>
        public List<KeyValuePair<string, string>> cCopyrights = new List<KeyValuePair<string, string>>();

        /// <summary>
        ///     List Of POI Categories in file
        /// </summary>
        public Dictionary<ushort, RecCategory> Categories = new Dictionary<ushort, RecCategory>();

        /// <summary>
        ///     Default Category
        /// </summary>
        private RecCategory DefaultCategory = new RecCategory(null);

        /// <summary>
        ///     List of Bitmaps in file
        /// </summary>
        public Dictionary<ushort, RecBitmap> Bitmaps = new Dictionary<ushort, RecBitmap>();

        /// <summary>
        ///     List of Media in file
        /// </summary>
        public Dictionary<ushort, RecMedia> Medias = new Dictionary<ushort, RecMedia>();

        private Add2LogProc Add2Log;
        private uint readNotifier;

        /// <summary>
        ///     File Content Data Source (local language)
        /// </summary>
        public string DataSource
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in cDataSource)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in cDataSource)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in cDataSource)
                    return kvp.Value;
                return null;
            }
        }

        /// <summary>
        ///     File Content Copyrights (local language)
        /// </summary>
        public string Copyrights
        {
            get
            {
                foreach (KeyValuePair<string, string> kvp in cCopyrights)
                    if (kvp.Key == GPIReader.LOCALE_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in cCopyrights)
                    if (kvp.Key == GPIReader.DEFAULT_LANGUAGE)
                        return kvp.Value;
                foreach (KeyValuePair<string, string> kvp in cCopyrights)
                    return kvp.Value;
                return null;
            }
        }

        /// <summary>
        ///     Marker Block
        /// </summary>
        public MarkerBlock MarkerData = null;

        /// <summary>
        ///     Constructor (GPI File Reader)
        /// </summary>
        /// <param name="fileName"></param>
        public GPIReader(string fileName)
        {
            this.fileName = fileName;
            this.Read();
            this.LoopRecords(this.RootElement.Childs);
            this.IfNoCats();
        }

        /// <summary>
        ///     Constructor (GPI File Reader)
        /// </summary>
        /// <param name="fileName"></param>
        public GPIReader(string fileName, Add2LogProc Add2Log)
        {
            this.Add2Log = Add2Log;
            this.fileName = fileName;
            this.Read();
            if (Add2Log != null) Add2Log(String.Format("POI File, version {0}", this.Version));
            if (Add2Log != null) Add2Log("Reading References...");
            this.LoopRecords(this.RootElement.Childs);
            this.IfNoCats();
            if (Add2Log != null) Add2Log("Reading Done");
        }

        private void IfNoCats()
        {
            if (this.Categories.Count > 0) return;
            this.DefaultCategory.Category.Add(new KeyValuePair<string, string>("EN", "No Category"));
            this.Categories.Add(0, this.DefaultCategory);
        }

        /// <summary>
        ///     Save File Content to KML file
        /// </summary>
        /// <param name="fileName"></param>
        public void SaveToKML(string fileName)
        {
            SaveToKML(fileName, null);
        }

        /// <summary>
        ///     Save File Content to KML file
        /// </summary>
        /// <param name="fileName"></param>
        public void SaveToKML(string fileName, Add2LogProc Add2Log)
        {
            if (Add2Log != null) this.Add2Log = Add2Log;
            string images_file_dir = Path.GetDirectoryName(fileName) + @"\images\";
            Directory.CreateDirectory(images_file_dir);

            if (this.Add2Log != null) this.Add2Log("Saving to kml...");
            FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
            sw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sw.WriteLine("<kml><Document>");
            string caption = (String.IsNullOrEmpty(this.Name) ? "GPI Has No Name" : this.Name);
            if (!String.IsNullOrEmpty(this.DataSource)) caption = this.DataSource;
            sw.WriteLine("<name><![CDATA[" + caption + "]]></name><createdby>KMZ Rebuilder GPI Reader</createdby>");
            string desc = "Created: " + this.Created.ToString() + "\r\n";
            foreach (KeyValuePair<string, string> langval in this.cDataSource)
                desc += String.Format("data_source:{0}={1}\r\n", langval.Key.ToLower(), langval.Value);
            foreach (KeyValuePair<string, string> langval in this.cCopyrights)
                desc += String.Format("copyrights:{0}={1}\r\n", langval.Key.ToLower(), langval.Value);
            sw.WriteLine("<description><![CDATA[" + desc + "]]></description>");
            List<string> simstyles = new List<string>();
            int ccount = 0;
            foreach (KeyValuePair<ushort, RecCategory> kCat in this.Categories)
            {
                ccount++;
                if (kCat.Value.Waypoints.Count == 0) continue;
                if (this.Add2Log != null) this.Add2Log(String.Format("Saving {2} POIs of {0}/{1} Category...", ccount, this.Categories.Count, kCat.Value.Waypoints.Count));

                string style = "catid" + kCat.Value.CategoryID.ToString();
                if (kCat.Value.Bitmap != null) style = "imgid" + kCat.Value.Bitmap.BitmapID.ToString();

                sw.WriteLine("<Folder><name><![CDATA[" + kCat.Value.Name + "]]></name>");
                desc = "CategoryID: " + kCat.Value.CategoryID.ToString() + "\r\n";
                desc += "Objects: " + kCat.Value.Waypoints.Count.ToString() + "\r\n";
                if (GPIReader.SAVE_MULTINAMES)
                    foreach (KeyValuePair<string, string> langval in kCat.Value.Category)
                        desc += String.Format("name:{0}={1}\r\n", langval.Key.ToLower(), langval.Value);
                if (kCat.Value.Comment != null)
                    foreach (KeyValuePair<string, string> langval in kCat.Value.Comment.Comment)
                        desc += String.Format("comm:{0}={1}\r\n", langval.Key.ToLower(), langval.Value);
                if (kCat.Value.Contact != null)
                {
                    if (!String.IsNullOrEmpty(kCat.Value.Contact.Phone))
                        desc += String.Format("contact_phone={0}\r\n", kCat.Value.Contact.Phone);
                    if (!String.IsNullOrEmpty(kCat.Value.Contact.Phone2))
                        desc += String.Format("contact_phone2={0}\r\n", kCat.Value.Contact.Phone2);
                    if (!String.IsNullOrEmpty(kCat.Value.Contact.Fax))
                        desc += String.Format("contact_fax={0}\r\n", kCat.Value.Contact.Fax);
                    if (!String.IsNullOrEmpty(kCat.Value.Contact.Email))
                        desc += String.Format("contact_email={0}\r\n", kCat.Value.Contact.Email);
                    if (!String.IsNullOrEmpty(kCat.Value.Contact.Web))
                        desc += String.Format("contact_web={0}\r\n", kCat.Value.Contact.Web);
                };
                if ((kCat.Value.Description != null) && (kCat.Value.Description.Description.Count > 0))
                {
                    if (desc.Length > 0) desc += "\r\n";
                    foreach (KeyValuePair<string, string> langval in kCat.Value.Description.Description)
                        desc += String.Format("desc:{0}={1}\r\n\r\n", langval.Key.ToLower(), TrimDesc(langval.Value));
                };
                sw.WriteLine("<description><![CDATA[" + desc + "]]></description>");
                foreach (RecWaypoint wp in kCat.Value.Waypoints)
                {
                    sw.WriteLine("<Placemark>");
                    sw.WriteLine("<name><![CDATA[" + wp.Name + "]]></name>");
                    string text = "";
                    if (GPIReader.SAVE_MULTINAMES)
                        foreach (KeyValuePair<string, string> langval in wp.ShortName)
                            text += String.Format("name:{0}={1}\r\n", langval.Key.ToLower(), langval.Value);
                    if (wp.Comment != null)
                        foreach (KeyValuePair<string, string> langval in wp.Comment.Comment)
                            text += String.Format("comm:{0}={1}\r\n", langval.Key.ToLower(), langval.Value);
                    if (wp.Contact != null)
                    {
                        if (!String.IsNullOrEmpty(wp.Contact.Phone))
                            text += String.Format("contact_phone={0}\r\n", wp.Contact.Phone);
                        if (!String.IsNullOrEmpty(wp.Contact.Phone2))
                            text += String.Format("contact_phone2={0}\r\n", wp.Contact.Phone2);
                        if (!String.IsNullOrEmpty(wp.Contact.Fax))
                            text += String.Format("contact_fax={0}\r\n", wp.Contact.Fax);
                        if (!String.IsNullOrEmpty(wp.Contact.Email))
                            text += String.Format("contact_email={0}\r\n", wp.Contact.Email);
                        if (!String.IsNullOrEmpty(wp.Contact.Web))
                            text += String.Format("contact_web={0}\r\n", wp.Contact.Web);
                    };
                    if (wp.Address != null)
                    {
                        foreach (KeyValuePair<string, string> langval in wp.Address.aCountry)
                            text += String.Format("addr_country:{0}={1}\r\n", langval.Key.ToLower(), langval.Value);
                        if (!String.IsNullOrEmpty(wp.Address.Postal))
                            text += String.Format("addr_postal={0}\r\n", wp.Address.Postal);
                        foreach (KeyValuePair<string, string> langval in wp.Address.aState)
                            text += String.Format("addr_state:{0}={1}\r\n", langval.Key.ToLower(), langval.Value);
                        foreach (KeyValuePair<string, string> langval in wp.Address.aCity)
                            text += String.Format("addr_city:{0}={1}\r\n", langval.Key.ToLower(), langval.Value);
                        foreach (KeyValuePair<string, string> langval in wp.Address.aStreet)
                            text += String.Format("addr_street:{0}={1}\r\n", langval.Key.ToLower(), langval.Value);
                        if (!String.IsNullOrEmpty(wp.Address.House))
                            text += String.Format("addr_house={0}\r\n", wp.Address.House);
                    };
                    if (wp.Alert != null)
                    {
                        text += String.Format("alert_proximity={0}\r\n", wp.Alert.Proximity);
                        text += String.Format("alert_speed={0}\r\n", wp.Alert.Speed);
                        text += String.Format("alert_ison={0}\r\n", wp.Alert.Alert);
                        text += String.Format("alert_type={0}\r\n", wp.Alert.IsType);
                        if (SAVE_MEDIA)
                        {
                            ushort sn = (ushort)(wp.Alert.SoundNumber + (wp.Alert.AudioAlert << 8));
                            if (Medias.ContainsKey(sn))
                            {
                                string ext = "bin";
                                if (Medias[sn].Format == 0) ext = "wav";
                                if (Medias[sn].Format == 1) ext = "mp3";
                                string fName = String.Format("{0}-{1}.{2}", Medias[sn].MediaID, Medias[sn].Content[0].Key, ext);
                                text += String.Format("alert_sound=media/{0}\r\n", fName);
                            };
                        };
                        if (wp.Alert.AlertCircles != null)
                        {
                            for (int z = 0; z < wp.Alert.AlertCircles.Count; z++)
                            {
                                double clat = wp.Alert.AlertCircles.lat[z];
                                double clon = wp.Alert.AlertCircles.lon[z];
                                uint crad = wp.Alert.AlertCircles.radius[z];
                                if ((clat == wp.Lat) && (clon == wp.Lon))
                                    text += String.Format("alert_circle={0}\r\n", crad);
                                else
                                    text += String.Format(System.Globalization.CultureInfo.InvariantCulture, "alert_circle={0},{1:0.000000},{2:0.000000}\r\n", crad, clat, clon);
                            };
                        };
                        if (wp.Alert.AlertTriggerOptions != null)
                        {
                            if (wp.Alert.AlertTriggerOptions.BearingCount > 0)
                                for (int z = 0; z < wp.Alert.AlertTriggerOptions.BearingCount; z++)
                                    text += String.Format(System.Globalization.CultureInfo.InvariantCulture, "alert_bearing={0},{1},{2}\r\n", wp.Alert.AlertTriggerOptions.BearingAngle[z], wp.Alert.AlertTriggerOptions.BearingWide[z], wp.Alert.AlertTriggerOptions.BearingBiDir[z] ? "bidir" : "onedir");
                            if ((wp.Alert.AlertTriggerOptions.DateTimeList != null) && (wp.Alert.AlertTriggerOptions.DateTimeList.Count > 0))
                                for (int z = 0; z < wp.Alert.AlertTriggerOptions.DateTimeList.Count; z++)
                                    text += String.Format("alert_datetime={0}\r\n", wp.Alert.AlertTriggerOptions.DateTimeList[z]);
                        };
                    };
                    if ((wp.Description != null) && (wp.Description.Description.Count > 0))
                    {
                        if (text.Length > 0) text += "\r\n";
                        foreach (KeyValuePair<string, string> langval in wp.Description.Description)
                            text += String.Format("desc:{0}={1}\r\n\r\n", langval.Key.ToLower(), TrimDesc(langval.Value));
                    };
                    if (wp.Bitmap != null) style = "imgid" + wp.Bitmap.BitmapID.ToString();
                    if ((wp.Image != null) && (wp.Image.Length > 0))
                    {
                        try
                        {
                            string simid = "simid" + simstyles.Count.ToString();
                            FileStream fsimid = new FileStream(images_file_dir + simid + ".jpg", FileMode.Create, FileAccess.Write);
                            fsimid.Write(wp.Image.ImageData, 0, wp.Image.ImageData.Length);
                            fsimid.Close();
                            simstyles.Add(simid);
                            if (POI_IMAGE_FROM_JPEG) style = simid;
                        }
                        catch (Exception ex) { };
                    };
                    sw.WriteLine("<description><![CDATA[" + text + "]]></description>");
                    sw.WriteLine("<styleUrl>#" + style + "</styleUrl>");
                    string xyz = wp.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + wp.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",0";
                    sw.WriteLine("<Point><coordinates>" + xyz + "</coordinates></Point>");
                    sw.WriteLine("</Placemark>");
                };
                sw.WriteLine("</Folder>");
            };
            foreach (string simid in simstyles)
            {
                sw.WriteLine("\t<Style id=\"" + simid + "\"><IconStyle><Icon><href>images/" + simid + ".jpg</href></Icon></IconStyle></Style>");
            };
            if (CREATE_CATEGORY_IMAGES_IFNO)
            {
                if (this.Add2Log != null)
                    this.Add2Log(String.Format("Saving Images for {0} Categories...", this.Categories.Count));
                int imsvd = 0;
                foreach (KeyValuePair<ushort, RecCategory> kCat in this.Categories)
                {
                    if (kCat.Value.Bitmap != null) continue;
                    string catID = "catid" + kCat.Value.CategoryID.ToString();
                    sw.WriteLine("\t<Style id=\"" + catID + "\"><IconStyle><Icon><href>images/" + catID + ".png</href></Icon></IconStyle></Style>");
                    try
                    {
                        Image im = new Bitmap(16, 16);
                        Graphics g = Graphics.FromImage(im);
                        g.FillEllipse(Brushes.Magenta, 0, 0, 16, 16);
                        string ttd = kCat.Value.CategoryID.ToString();
                        while (ttd.Length < 2) ttd = "0" + ttd;
                        g.DrawString(ttd, new Font("MS Sans Serif", 8), Brushes.Black, 0, 2);
                        g.Dispose();
                        im.Save(images_file_dir + catID + ".png");
                        imsvd++;
                    }
                    catch (Exception ex) { };
                };
                if (this.Add2Log != null) this.Add2Log(String.Format("Saved {0} Images", imsvd));
            };
            if (this.Add2Log != null) this.Add2Log(String.Format("Saving {0} Bitmaps...", this.Bitmaps.Count));
            foreach (KeyValuePair<ushort, RecBitmap> bitmaps in this.Bitmaps)
            {
                string imgID = "imgid" + bitmaps.Value.BitmapID.ToString();
                sw.WriteLine("\t<Style id=\"" + imgID + "\"><IconStyle><Icon><href>images/" + imgID + ".png</href></Icon></IconStyle></Style>");
                RecBitmap br = bitmaps.Value;
                if ((br.Pixels != null) && (br.Pixels.Length > 0))
                {
                    try
                    {
                        int wi = br.Width;
                        byte[] sub = new byte[4];
                        int pixelsize = 1;
                        if (br.Palette == 0) pixelsize = br.LineSize / br.Width;
                        if (br.Palette == 16) wi = br.Width / 2;

                        Bitmap bmp = new Bitmap(br.Width, br.Height);
                        Graphics g = Graphics.FromImage(bmp);
                        g.Clear(Color.Transparent);
                        g.Dispose();
                        for (int h = 0; h < br.Height; h++)
                        {
                            int voffset = br.LineSize * h;
                            for (int w = 0; w < wi; w++)
                            {
                                int hoffset = voffset + w * pixelsize;
                                Array.Copy(br.Pixels, hoffset, sub, 0, pixelsize);
                                uint color = BitConverter.ToUInt32(sub, 0);
                                Color c = Color.Transparent;
                                if (br.Palette == 0)
                                {
                                    bmp.SetPixel(w, h, Color.Transparent);
                                    if (color == br.TransparentColor) continue;
                                    c = ColorFromUint(color);
                                    bmp.SetPixel(w, h, c);
                                }
                                else if (br.Palette > 16)
                                {
                                    bmp.SetPixel(w, h, Color.Transparent);
                                    color = br.Colors[color];
                                    if (color == br.TransparentColor) continue;
                                    c = ColorFromUint(color);
                                    bmp.SetPixel(w, h, c);
                                }
                                else
                                {
                                    bmp.SetPixel(2 * w, h, Color.Transparent);
                                    bmp.SetPixel(2 * w + 1, h, Color.Transparent);
                                    int low = (int)br.Colors[(color) & 0x0F];
                                    int hi = (int)br.Colors[((color) & 0xF0) >> 4];
                                    if (low != br.TransparentColor) bmp.SetPixel(2 * w, h, Color.FromArgb(low));
                                    if (hi != br.TransparentColor) bmp.SetPixel(2 * w + 1, h, Color.FromArgb(hi));
                                };
                            };
                        };
                        bmp.Save(images_file_dir + imgID + ".png");
                        bmp.Dispose();
                    }
                    catch (Exception ex)
                    {

                    };
                };
            };
            if (SAVE_MEDIA && ((this.MarkerData == null ? this.Medias.Count : this.Medias.Count - 1) > 0))
            {
                if (this.Add2Log != null)
                    this.Add2Log(String.Format("Saving {0} Medias...", this.MarkerData == null ? this.Medias.Count : this.Medias.Count - 1));
                string medias_file_dir = Path.GetDirectoryName(fileName) + @"\media\";
                Directory.CreateDirectory(medias_file_dir);
                foreach (KeyValuePair<ushort, RecMedia> rm in Medias)
                {
                    for (int i = 0; i < rm.Value.Content.Count; i++)
                    {
                        if (rm.Value.Format != 0x77)
                        {
                            string ext = "bin";
                            if (rm.Value.Format == 0) ext = "wav";
                            if (rm.Value.Format == 1) ext = "mp3";
                            string fName = String.Format("{0}{1}-{2}.{3}", medias_file_dir, rm.Value.MediaID, rm.Value.Content[i].Key, ext);
                            try
                            {
                                FileStream fsw = new FileStream(fName, FileMode.Create, FileAccess.Write);
                                fsw.Write(rm.Value.Content[i].Value, 0, rm.Value.Content[i].Value.Length);
                                fsw.Close();
                            }
                            catch (Exception ex) { };
                        };
                    };
                };
            };
            sw.WriteLine("</Document></kml>");
            sw.Close();
            fs.Close();
            if (this.Add2Log != null) this.Add2Log("All data saved");
            if ((this.MarkerData != null) && (this.Add2Log != null))
            {
                this.Add2Log(String.Format(System.Globalization.CultureInfo.InvariantCulture, "Bounds: {0:0.000000},{2:0.000000} - {1:0.000000},{3:0.000000}", this.MarkerData.Bounds[0], this.MarkerData.Bounds[1], this.MarkerData.Bounds[2], this.MarkerData.Bounds[3]));
                this.Add2Log(String.Format("Creator: {0}", this.MarkerData.Creator));
                this.Add2Log(String.Format("Created: {0}", this.MarkerData.Created));
                this.Add2Log(String.Format("Alert Zones: {0}", this.MarkerData.AlertZones));
            };
        }

        /// <summary>
        ///     Trim Text
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private string TrimDesc(string text)
        {
            while (text.IndexOf("\r\n\r\n") >= 0) text = text.Replace("\r\n\r\n", "\r\n");
            text = text.Trim(new char[] { '\r', '\n' });
            return text;
        }

        /// <summary>
        ///     Loop Records References
        /// </summary>
        /// <param name="records"></param>
        private void LoopRecords(List<Record> records)
        {
            if ((records == null) || (records.Count == 0)) return;
            foreach (Record r in records)
            {
                GetReferences(r);
                if ((this.Categories.Count == 0) && (r is RecWaypoint))
                    DefaultCategory.Waypoints.Add((RecWaypoint)r);
                LoopRecords(r.Childs);
            };
        }

        /// <summary>
        ///     Get Record References
        /// </summary>
        /// <param name="r"></param>
        private void GetReferences(Record r)
        {
            if (r is RecBitmapReference)
            {
                RecBitmapReference rec = (RecBitmapReference)r;
                if ((rec.Parent != null) && (rec.Parent is RecCategory))
                    try { ((RecCategory)rec.Parent).Bitmap = this.Bitmaps[rec.BitmapID]; }
                    catch { /* No Bitmap */ };
                if ((rec.Parent != null) && (rec.Parent is RecWaypoint))
                    try { ((RecWaypoint)rec.Parent).Bitmap = this.Bitmaps[rec.BitmapID]; }
                    catch { /* No Bitmap */ };
            };
            if (r is RecCategoryReference)
            {
                RecCategoryReference rec = (RecCategoryReference)r;
                if ((rec.Parent != null) && (rec.Parent is RecWaypoint))
                {
                    RecWaypoint rw = (RecWaypoint)rec.Parent;
                    try { this.Categories[rec.CategoryID].Waypoints.Add(rw); }
                    catch { /* No Category */ };
                };
            };
        }

        /// <summary>
        ///     Read Source File Data
        /// </summary>
        private void Read()
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            byte[] fileData = new byte[fs.Length];
            fs.Read(fileData, 0, fileData.Length);
            fs.Close();

            if (fileData.Length != 0)
                ReadData(ref fileData, 0, (uint)fileData.Length, RootElement);
        }

        /// <summary>
        ///     Read Block Data
        /// </summary>
        /// <param name="fileData"></param>
        /// <param name="parent"></param>
        private void ReadData(ref byte[] blockData, uint blockOffset, uint blockLength, Record parent)
        {
            uint currOffset = blockOffset;
            while (currOffset < (blockOffset + blockLength))
            {
                if (this.Add2Log != null)
                {
                    if (currOffset >= readNotifier)
                    {
                        this.Add2Log(String.Format("Reading {0}/{1} Data...", currOffset, blockData.Length));
                        readNotifier += 256000; // 256kb
                    };
                };
                uint readedLength = ReadRecordBlock(ref blockData, parent, currOffset);
                currOffset += readedLength;
            };
        }

        /// <summary>
        ///     Read Block Record Data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="parent"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        private uint ReadRecordBlock(ref byte[] data, Record parent, uint offset)
        {
            Record rec = Record.Create(parent, offset, ref data, BitConverter.ToUInt16(data, (int)offset)); offset += 2;
            rec.RecFlags = BitConverter.ToUInt16(data, (int)offset); offset += 2;
            rec.LengthTotal = BitConverter.ToUInt32(data, (int)offset); offset += 4;
            rec.LengthMain = rec.LengthTotal;
            rec.LengthBlock = rec.LengthTotal + (uint)(rec.RecHasExtra ? 12 : 8);
            try
            {
                if (rec.RecHasExtra)
                {
                    rec.LengthMain = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                    rec.LengthExtra = rec.LengthTotal - rec.LengthMain;
                };
                rec.OffsetMain = offset;
                rec.OffsetExtra = rec.OffsetMain + rec.LengthMain; //
                if (RecEnum<RecType>.IsDefined((RecType)rec.RecType)) // only if specified
                {
                    bool processExtras = ReadMainBlock(ref data, rec);
                    if (processExtras && rec.RecHasExtra) ReadData(ref data, rec.OffsetExtra, rec.LengthExtra, rec);
                }
                else
                {

                };
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return rec.LengthBlock;
        }

        /// <summary>
        ///     Read Block Record Main Data
        /// </summary>
        /// <param name="rec"></param>
        private bool ReadMainBlock(ref byte[] data, Record rec)
        {
            if ((rec.RecType == 0) && (rec is RecHeader0)) return Read00Header1(ref data, (RecHeader0)rec);
            if ((rec.RecType == 1) && (rec is RecHeader1)) return Read01Header2(ref data, (RecHeader1)rec);
            if ((rec.RecType == 2) && (rec is RecWaypoint)) return Read02Waypoint(ref data, (RecWaypoint)rec);
            if ((rec.RecType == 3) && (rec is RecAlert)) return Read03Alert(ref data, (RecAlert)rec);
            if ((rec.RecType == 4) && (rec is RecBitmapReference)) return Read04BitmapReference(ref data, (RecBitmapReference)rec);
            if ((rec.RecType == 5) && (rec is RecBitmap)) return Read05Bitmap(ref data, (RecBitmap)rec);
            if ((rec.RecType == 6) && (rec is RecCategoryReference)) return Read06CategoryReference(ref data, (RecCategoryReference)rec);
            if ((rec.RecType == 7) && (rec is RecCategory)) return Read07Category(ref data, (RecCategory)rec);
            if ((rec.RecType == 8) && (rec is RecArea)) return Read08Area(ref data, (RecArea)rec);
            if ((rec.RecType == 9) && (rec is RecPOIGroup)) return Read09POIGroup(ref data, (RecPOIGroup)rec);
            if ((rec.RecType == 10) && (rec is RecComment)) return Read10Comment(ref data, (RecComment)rec);
            if ((rec.RecType == 11) && (rec is RecAddress)) return Read11Address(ref data, (RecAddress)rec);
            if ((rec.RecType == 12) && (rec is RecContact)) return Read12Contact(ref data, (RecContact)rec);
            if ((rec.RecType == 13) && (rec is RecImage)) return Read13Image(ref data, (RecImage)rec);
            if ((rec.RecType == 14) && (rec is RecDescription)) return Read14Decription(ref data, (RecDescription)rec);
            if ((rec.RecType == 15) && (rec is RecProductInfo)) return Read15ProductInfo(ref data, (RecProductInfo)rec);
            if ((rec.RecType == 16) && (rec is RecAlertCircle)) return Read16AlertCircle(ref data, (RecAlertCircle)rec);
            if ((rec.RecType == 17) && (rec is RecCopyright)) return Read17Copyright(ref data, (RecCopyright)rec);
            if ((rec.RecType == 18) && (rec is RecMedia)) return Read18Media(ref data, (RecMedia)rec);
            if ((rec.RecType == 19) && (rec is RecSpeedCamera)) return Read19SpeedCamera(ref data, (RecSpeedCamera)rec);
            if ((rec.RecType == 27) && (rec is RecAlertTriggerOptions)) return Read27AlertTriggerOptions(ref data, (RecAlertTriggerOptions)rec);
            return true;
        }

        private bool Read00Header1(ref byte[] data, RecHeader0 rec) // 0
        {
            uint offset = rec.OffsetMain;
            byte[] sub = new byte[6];
            Array.Copy(data, offset, sub, 0, 6);
            rec.Header = Header = Encoding.ASCII.GetString(sub);
            sub = new byte[2];
            Array.Copy(data, offset + 6, sub, 0, 2);
            rec.Version = Version = Encoding.ASCII.GetString(sub);
            uint time = BitConverter.ToUInt32(data, (int)offset + 8);
            if (time != 0xFFFFFFFF)
                rec.Created = Created = (new DateTime(1990, 1, 1)).AddSeconds(time);
            ushort slen = BitConverter.ToUInt16(data, (int)offset + 14);
            rec.Name = Name = Encoding.ASCII.GetString(data, (int)offset + 16, slen);
            return true;
        }

        private bool Read01Header2(ref byte[] data, RecHeader1 rec) // 1
        {
            uint offset = rec.OffsetMain;
            int bLen = 0;
            while (data[offset + bLen] != 0) bLen++;
            rec.Content = this.Content = Encoding.ASCII.GetString(data, (int)offset, bLen++);
            rec.CodePage = this.CodePage = BitConverter.ToUInt16(data, (int)offset + bLen + 4);
            this.Encoding = rec.Encoding;
            return true;
        }

        private bool Read02Waypoint(ref byte[] data, RecWaypoint rec) // 2
        {
            uint offset = rec.OffsetMain;
            rec.cLat = BitConverter.ToInt32(data, (int)offset); offset += 4;
            rec.cLon = BitConverter.ToInt32(data, (int)offset); offset += 4;
            offset += 3;
            uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
            int readed = 0;
            while (readed < len)
            {
                string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                {
                    string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                    rec.ShortName.Add(new KeyValuePair<string, string>(lang, text));
                }
                else
                    offset += tlen;
            };
            return true;
        }

        private bool Read03Alert(ref byte[] data, RecAlert rec) // 3
        {
            try
            {
                uint offset = rec.OffsetMain;
                rec.Proximity = BitConverter.ToUInt16(data, (int)offset);
                rec.cSpeed = BitConverter.ToUInt16(data, (int)offset + 2);
                ushort flags = BitConverter.ToUInt16(data, (int)offset + 4);
                rec.Alert = data[(int)offset + 8];
                rec.AlertType = data[(int)offset + 9];
                rec.SoundNumber = data[(int)offset + 10];
                rec.AudioAlert = data[(int)offset + 11];
                if ((rec.Parent != null) && (rec.Parent is RecWaypoint)) ((RecWaypoint)rec.Parent).Alert = rec;
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return true;
        }

        public bool Read04BitmapReference(ref byte[] data, RecBitmapReference rec) // 4
        {
            rec.BitmapID = BitConverter.ToUInt16(data, (int)rec.OffsetMain);
            return false;
        }

        public bool Read05Bitmap(ref byte[] data, RecBitmap rec) // 5
        {
            try
            {
                uint offset = rec.OffsetMain;
                rec.BitmapID = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                rec.Height = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                rec.Width = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                rec.LineSize = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                rec.BitsPerPixel = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                rec.Reserved9 = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                rec.ImageSize = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                rec.Reserved10 = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                rec.Palette = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                rec.TransparentColor = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                rec.Flags = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                rec.Reserved11 = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                rec.Pixels = new byte[rec.ImageSize];
                Array.Copy(data, offset, rec.Pixels, 0, rec.ImageSize); offset += rec.ImageSize;
                rec.Colors = new uint[rec.Palette];
                for (int i = 0; i < rec.Colors.Length; i++) { rec.Colors[i] = BitConverter.ToUInt32(data, (int)offset); offset += 4; };
                this.Bitmaps.Add(rec.BitmapID, rec);
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private bool Read06CategoryReference(ref byte[] data, RecCategoryReference rec) // 6
        {
            rec.CategoryID = BitConverter.ToUInt16(data, (int)rec.OffsetMain);
            return false;
        }

        private bool Read07Category(ref byte[] data, RecCategory rec) // 7
        {
            uint offset = rec.OffsetMain;
            rec.CategoryID = BitConverter.ToUInt16(data, (int)offset); offset += 2;
            uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
            int readed = 0;
            while (readed < len)
            {
                string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                {
                    string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                    rec.Category.Add(new KeyValuePair<string, string>(lang, text));
                }
                else
                    offset += tlen;
            };
            this.Categories.Add(rec.CategoryID, rec);
            return true;
        }

        private bool Read08Area(ref byte[] data, RecArea rec) // 8
        {
            uint offset = rec.OffsetMain;
            rec.cMaxLat = BitConverter.ToInt32(data, (int)offset);
            rec.cMaxLon = BitConverter.ToInt32(data, (int)offset + 4);
            rec.cMinLat = BitConverter.ToInt32(data, (int)offset + 8);
            rec.cMinLon = BitConverter.ToInt32(data, (int)offset + 12);
            return true;
        }

        private bool Read09POIGroup(ref byte[] data, RecPOIGroup rec) // 9
        {
            uint offset = rec.OffsetMain;
            uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
            int readed = 0;
            while (readed < len)
            {
                string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                {
                    string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                    rec.DataSource.Add(new KeyValuePair<string, string>(lang, text));
                }
                else
                    offset += tlen;
            };

            ReadData(ref data, (uint)offset, rec.LengthMain - (uint)readed, rec);
            return true;
        }

        private bool Read10Comment(ref byte[] data, RecComment rec) // 10
        {
            try
            {
                uint offset = rec.OffsetMain;
                uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                int readed = 0;
                while (readed < len)
                {
                    string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                    if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                    {
                        string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                        rec.Comment.Add(new KeyValuePair<string, string>(lang, text));
                    }
                    else
                        offset += tlen;
                };
                if ((rec.Parent != null) && (rec.Parent is RecWaypoint)) ((RecWaypoint)rec.Parent).Comment = rec;
                if ((rec.Parent != null) && (rec.Parent is RecCategory)) ((RecCategory)rec.Parent).Comment = rec;
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private bool Read11Address(ref byte[] data, RecAddress rec) // 11
        {
            uint offset = rec.OffsetMain;
            rec.Flags = BitConverter.ToUInt16(data, (int)offset); offset += 2;
            try
            {
                if ((rec.Flags & 0x0001) == 0x0001)
                {
                    uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                    int readed = 0;
                    while (readed < len)
                    {
                        string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                        ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                        if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                        {
                            string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                            rec.aCity.Add(new KeyValuePair<string, string>(lang, text));
                        }
                        else
                            offset += tlen;
                    };
                };
                if ((rec.Flags & 0x0002) == 0x0002)
                {
                    uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                    int readed = 0;
                    while (readed < len)
                    {
                        string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                        ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                        if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                        {
                            string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                            rec.aCountry.Add(new KeyValuePair<string, string>(lang, text));
                        }
                        else
                            offset += tlen;
                    };
                };
                if ((rec.Flags & 0x0004) == 0x0004)
                {
                    uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                    int readed = 0;
                    while (readed < len)
                    {
                        string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                        ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                        if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                        {
                            string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                            rec.aState.Add(new KeyValuePair<string, string>(lang, text));
                        }
                        else
                            offset += tlen;
                    };
                };
                if ((rec.Flags & 0x0008) == 0x0008)
                {
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                    string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen;
                    rec.Postal = text;
                };
                if ((rec.Flags & 0x0010) == 0x0010)
                {
                    uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                    int readed = 0;
                    while (readed < len)
                    {
                        string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                        ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                        if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                        {
                            string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                            rec.aStreet.Add(new KeyValuePair<string, string>(lang, text));
                        }
                        else
                            offset += tlen;
                    };
                };
                if ((rec.Flags & 0x0020) == 0x0020)
                {
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                    string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen;
                    rec.House = text;
                };
                if ((rec.Parent != null) && (rec.Parent is RecWaypoint)) ((RecWaypoint)rec.Parent).Address = rec;
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private bool Read12Contact(ref byte[] data, RecContact rec) // 12
        {
            uint offset = rec.OffsetMain;
            rec.Flags = BitConverter.ToUInt16(data, (int)offset); offset += 2;
            try
            {
                if ((rec.Flags & 0x0001) == 0x0001)
                {
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                    string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen;
                    rec.Phone = text;
                };
                if ((rec.Flags & 0x0002) == 0x0002)
                {
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                    string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen;
                    rec.Phone2 = text;
                };
                if ((rec.Flags & 0x0004) == 0x0004)
                {
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                    string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen;
                    rec.Fax = text;
                };
                if ((rec.Flags & 0x0008) == 0x0008)
                {
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                    string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen;
                    rec.Email = text;
                };
                if ((rec.Flags & 0x0010) == 0x0010)
                {
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                    string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen;
                    rec.Web = text;
                };
                if ((rec.Parent != null) && (rec.Parent is RecWaypoint)) ((RecWaypoint)rec.Parent).Contact = rec;
                if ((rec.Parent != null) && (rec.Parent is RecCategory)) ((RecCategory)rec.Parent).Contact = rec;
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private bool Read13Image(ref byte[] data, RecImage rec) // 13
        {
            try
            {
                uint offset = rec.OffsetMain;
                rec.Length = BitConverter.ToUInt32(data, (int)offset + 1);
                rec.ImageData = new byte[rec.Length];
                if (rec.Length > 0)
                {
                    Array.Copy(data, (int)offset + 5, rec.ImageData, 0, rec.Length);
                    if ((rec.Parent != null) && (rec.Parent is RecWaypoint))
                        ((RecWaypoint)rec.Parent).Image = rec;
                };
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private bool Read14Decription(ref byte[] data, RecDescription rec) // 14
        {
            try
            {
                uint offset = rec.OffsetMain + 1;
                uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                int readed = 0;
                while (readed < len)
                {
                    string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                    if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                    {
                        string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                        rec.Description.Add(new KeyValuePair<string, string>(lang, text));
                    }
                    else
                        offset += tlen;
                };
                if ((rec.Parent != null) && (rec.Parent is RecWaypoint)) ((RecWaypoint)rec.Parent).Description = rec;
                if ((rec.Parent != null) && (rec.Parent is RecCategory)) ((RecCategory)rec.Parent).Description = rec;
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private bool Read15ProductInfo(ref byte[] data, RecProductInfo rec) // 15
        {
            uint offset = rec.OffsetMain;
            rec.FactoryID = BitConverter.ToUInt16(data, (int)offset);
            rec.ProductID = data[(int)offset + 2];
            rec.RegionID = data[(int)offset + 3];
            rec.VendorID = data[(int)offset + 4];
            return false;
        }

        private bool Read16AlertCircle(ref byte[] data, RecAlertCircle rec) // 16
        {
            try
            {
                uint offset = rec.OffsetMain;
                rec.Count = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                rec.lat = new double[rec.Count];
                rec.lon = new double[rec.Count];
                rec.radius = new uint[rec.Count];
                for (int i = 0; i < rec.Count; i++)
                {
                    rec.lat[i] = (double)BitConverter.ToUInt32(data, (int)offset + i * 12) * 360.0 / Math.Pow(2, 32);
                    rec.lon[i] = (double)BitConverter.ToUInt32(data, (int)offset + i * 12 + 4) * 360.0 / Math.Pow(2, 32);
                    rec.radius[i] = BitConverter.ToUInt32(data, (int)offset + i * 12 + 8);
                };
                if ((rec.Parent != null) && (rec.Parent is RecAlert)) ((RecAlert)rec.Parent).AlertCircles = rec;
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private bool Read17Copyright(ref byte[] data, RecCopyright rec) // 17
        {
            try
            {
                uint offset = rec.OffsetMain;
                rec.Flags1 = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                rec.Flags2 = BitConverter.ToUInt16(data, (int)offset); offset += 2; offset += 4;
                uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                int readed = 0;
                while (readed < len)
                {
                    string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                    if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                    {
                        string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                        rec.cDataSource.Add(new KeyValuePair<string, string>(lang, text));
                    }
                    else
                        offset += tlen;
                };
                len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                readed = 0;
                while (readed < len)
                {
                    string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                    if ((tlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                    {
                        string text = this.Encoding.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                        rec.cCopyrights.Add(new KeyValuePair<string, string>(lang, text));
                    }
                    else
                        offset += tlen;
                };
                this.cDataSource = rec.cDataSource;
                this.cCopyrights = rec.cCopyrights;
                if ((rec.Flags1 & 0x0400) == 0x0400)
                {
                    ushort tlen = BitConverter.ToUInt16(data, (int)offset); offset += 2; readed += 2;
                    string text = Encoding.ASCII.GetString(data, (int)offset, tlen); offset += tlen; readed += tlen;
                    rec.DeviceModel = text;
                };
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private bool Read18Media(ref byte[] data, RecMedia rec) // 18
        {
            uint offset = rec.OffsetMain;
            rec.MediaID = BitConverter.ToUInt16(data, (int)offset); offset += 2;
            rec.Format = data[(int)offset];
            try
            {
                offset = rec.OffsetExtra;
                int readed = 0;
                uint len = BitConverter.ToUInt32(data, (int)offset); offset += 4;
                while (readed < len)
                {
                    string lang = Encoding.ASCII.GetString(data, (int)offset, 2); offset += 2; readed += 2;
                    uint mlen = BitConverter.ToUInt32(data, (int)offset); offset += 4; readed += 4;
                    if ((mlen > 0) && char.IsLetter(lang[0]) && char.IsLetter(lang[1]))
                    {
                        byte[] media = new byte[mlen];
                        Array.Copy(data, offset, media, 0, mlen); offset += mlen; readed += (int)mlen;
                        if ((rec.MediaID == 0x7777) && (rec.Format == 0x77))
                            try { this.MarkerData = MarkerBlock.FromBytes(media); }
                            catch { };
                        rec.Content.Add(new KeyValuePair<string, byte[]>(lang, media));
                    }
                    else
                        offset += mlen;
                };
                Medias.Add(rec.MediaID, rec);
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private bool Read19SpeedCamera(ref byte[] data, RecSpeedCamera rec) // 19
        {
            try
            {
                uint offset = rec.OffsetMain;
                byte[] buff = new byte[4];
                Array.Copy(data, (int)offset, buff, 0, 3); offset += 3;
                rec.cMaxLat = BitConverter.ToInt32(buff, 0);
                Array.Copy(data, (int)offset, buff, 0, 3); offset += 3;
                rec.cMaxLon = BitConverter.ToInt32(buff, 0);
                Array.Copy(data, (int)offset, buff, 0, 3); offset += 3;
                rec.cMinLat = BitConverter.ToInt32(buff, 0);
                Array.Copy(data, (int)offset, buff, 0, 3); offset += 3;
                rec.cMinLon = BitConverter.ToInt32(buff, 0);
                rec.Flags = data[(int)offset]; offset++;
                if (rec.Flags == 0x81) offset += 11;
                if ((rec.Flags == 0x80) || (rec.Flags > 0x81)) offset++;
                byte f10v = data[(int)offset]; offset++;
                if (rec.Flags == 0x81) offset++;
                offset += (uint)(1 + f10v);
                Array.Copy(data, (int)offset, buff, 0, 3); offset += 3;
                rec.cLat = BitConverter.ToInt32(buff, 0);
                Array.Copy(data, (int)offset, buff, 0, 3); offset += 3;
                rec.cLon = BitConverter.ToInt32(buff, 0);
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private bool Read27AlertTriggerOptions(ref byte[] data, RecAlertTriggerOptions rec) // 27
        {
            try
            {
                uint offset = rec.OffsetMain;
                ushort keyv = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                if (keyv < 16)
                {
                    rec.BearingCount = (byte)(keyv & 0x07);
                    bool hasDTL = (keyv & 0x08) == 0x08;
                    if (rec.BearingCount > 0)
                    {
                        rec.BearingAngle = new ushort[rec.BearingCount];
                        rec.BearingWide = new ushort[rec.BearingCount];
                        rec.BearingBiDir = new bool[rec.BearingCount];
                        for (int i = 0; i < rec.BearingCount; i++)
                        {
                            ushort br = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                            rec.BearingAngle[i] = (ushort)(br & 0x01FF);
                            rec.BearingWide[i] = (ushort)(((br >> 9) & 0x0F) * 5);
                            rec.BearingBiDir[i] = (br & 0x2000) == 0x2000;
                        };
                    };
                    if (hasDTL) // DateTime List Block
                    {
                        ushort len = BitConverter.ToUInt16(data, (int)offset); offset += 2;
                        if (len > 0)
                        {
                            rec.DateTimeBlock = new byte[len];
                            Array.Copy(data, offset, rec.DateTimeBlock, 0, len); offset += len;
                            Read27DateTimeListBlock(rec);
                        };
                    };
                    if ((rec.Parent != null) && (rec.Parent is RecAlert)) ((RecAlert)rec.Parent).AlertTriggerOptions = rec;
                };
            }
            catch (Exception ex)
            {
                rec.ReadError = ex;
            };
            return false;
        }

        private static void Read27DateTime00Block(RecAlertTriggerOptions rec, byte b8Flag, ref int offset) // By Month
        {
            bool has_month_from = (b8Flag & 0x01) == 0x01;
            bool has_month_till = (b8Flag & 0x02) == 0x02;
            //bool has_time_start = (b8Flag & 0x04) == 0x04;
            //bool has_time_end   = (b8Flag & 0x08) == 0x08;
            //bool has_dof_end    = (b8Flag & 0x10) == 0x10;

            byte tfromh = 0; byte tfromm = 0; byte ttillh = 24; byte ttillm = 0; byte bydw = 0x7F;
            string line = "";

            byte mfrom = 1; byte mtill = 12;
            if (has_month_from) mfrom = rec.DateTimeBlock[offset++];
            if (has_month_till) mtill = rec.DateTimeBlock[offset++];

            if ((mfrom != 1) || (mtill != 12))
                line += String.Format("on_month:{0:00}~{1:00},", mfrom, mtill);

            Read27DateTimeBlockTDOFPart(rec, ref offset, b8Flag, ref tfromh, ref tfromm, ref ttillh, ref ttillm, ref bydw);
            line += DateTimeDOFToLine(tfromh, tfromm, ttillh, ttillm, bydw);

            rec.DateTimeList.Add(line);
        }

        private static void Read27DateTime20Block(RecAlertTriggerOptions rec, byte b8Flag, ref int offset) // Dates
        {
            bool has_no_year = (b8Flag & 0x01) == 0x01;
            bool has_day_month = (b8Flag & 0x02) == 0x02;
            //bool has_time_start = (b8Flag & 0x04) == 0x04;
            //bool has_time_end   = (b8Flag & 0x08) == 0x08;
            //bool has_dof_end    = (b8Flag & 0x10) == 0x10;

            byte tfromh = 0; byte tfromm = 0; byte ttillh = 24; byte ttillm = 0; byte bydw = 0x7F;
            string line = "on_day:";

            if (has_no_year)
            {
                int mmddf = rec.DateTimeBlock[offset++] + (rec.DateTimeBlock[offset++] << 8);
                int mmddt = rec.DateTimeBlock[offset++] + (rec.DateTimeBlock[offset++] << 8);
                line += String.Format("{0:00}.{1:00}-", (mmddf >> 4) & 0x1F, mmddf & 0x0F);
                line += String.Format("{0:00}.{1:00},", (mmddt >> 4) & 0x1F, mmddt & 0x0F);
            }
            else
            {
                int ddf = rec.DateTimeBlock[offset++];
                int mmyyf = rec.DateTimeBlock[offset++] + (rec.DateTimeBlock[offset++] << 8);
                int ddt = rec.DateTimeBlock[offset++];
                int mmyyt = rec.DateTimeBlock[offset++] + (rec.DateTimeBlock[offset++] << 8);
                line += String.Format("{0:00}.{1:00}.{2:0000}-", ddf, mmyyf & 0x0F, (mmyyf >> 4) & 0x0FFF);
                line += String.Format("{0:00}.{1:00}.{2:0000},", ddt, mmyyt & 0x0F, (mmyyt >> 4) & 0x0FFF);
            };

            Read27DateTimeBlockTDOFPart(rec, ref offset, b8Flag, ref tfromh, ref tfromm, ref ttillh, ref ttillm, ref bydw);
            line += DateTimeDOFToLine(tfromh, tfromm, ttillh, ttillm, bydw);

            rec.DateTimeList.Add(line);
        }

        private static void Read27DateTime40Block(RecAlertTriggerOptions rec, byte b8Flag, ref int offset) // Day of year by week
        {
            bool has_dof_start = (b8Flag & 0x01) == 0x01;
            bool has_day_oyear = (b8Flag & 0x02) == 0x02;
            //bool has_time_start = (b8Flag & 0x04) == 0x04;
            //bool has_time_end   = (b8Flag & 0x08) == 0x08;
            //bool has_dof_end   = (b8Flag & 0x10) == 0x10;

            byte tfromh = 0; byte tfromm = 0; byte ttillh = 24; byte ttillm = 0; byte bydw = 0x7F;
            string line = "";

            if (has_day_oyear)
            {
                line = "on_day:";
                ushort ddf = (ushort)(rec.DateTimeBlock[offset++] + (rec.DateTimeBlock[offset++] << 8));
                ushort ddt = (ushort)(rec.DateTimeBlock[offset++] + (rec.DateTimeBlock[offset++] << 8));
                line += String.Format("{0:000}~{1:000},", ddf, ddt);
            };

            if (has_dof_start) bydw = rec.DateTimeBlock[offset++];

            Read27DateTimeBlockTDOFPart(rec, ref offset, b8Flag, ref tfromh, ref tfromm, ref ttillh, ref ttillm, ref bydw);
            line += DateTimeDOFToLine(tfromh, tfromm, ttillh, ttillm, bydw);

            rec.DateTimeList.Add(line);
        }

        private static void Read27DateTime60Block(RecAlertTriggerOptions rec, byte b8Flag, ref int offset) // day of month
        {
            bool unset_flag = (b8Flag & 0x01) == 0x01;
            bool has_day_omonth = (b8Flag & 0x02) == 0x02;
            //bool has_time_start = (b8Flag & 0x04) == 0x04;
            //bool has_time_end   = (b8Flag & 0x08) == 0x08;
            //bool has_dof_end    = (b8Flag & 0x10) == 0x10;

            byte tfromh = 0; byte tfromm = 0; byte ttillh = 24; byte ttillm = 0; byte bydw = 0x7F;
            string line = "on_day:";

            if (has_day_omonth)
                line += String.Format("{0:00}-{1:00},", rec.DateTimeBlock[offset++], rec.DateTimeBlock[offset++]);

            Read27DateTimeBlockTDOFPart(rec, ref offset, b8Flag, ref tfromh, ref tfromm, ref ttillh, ref ttillm, ref bydw);
            line += DateTimeDOFToLine(tfromh, tfromm, ttillh, ttillm, bydw);

            rec.DateTimeList.Add(line);
        }

        private static void Read27DateTimeC0Block(RecAlertTriggerOptions rec, byte b8Flag, ref int offset) // week of month
        {
            bool unset_flag = (b8Flag & 0x01) == 0x01;
            bool has_week_omonth = (b8Flag & 0x02) == 0x02;
            //bool has_time_start  = (b8Flag & 0x04) == 0x04;
            //bool has_time_end    = (b8Flag & 0x08) == 0x08;
            //bool has_dof_end     = (b8Flag & 0x10) == 0x10;

            byte tfromh = 0; byte tfromm = 0; byte ttillh = 24; byte ttillm = 0; byte bydw = 0x7F;
            string line = "on_week:";

            if (has_week_omonth)
                line += String.Format("{0:0}-{1:0},", rec.DateTimeBlock[offset++], rec.DateTimeBlock[offset++]);

            Read27DateTimeBlockTDOFPart(rec, ref offset, b8Flag, ref tfromh, ref tfromm, ref ttillh, ref ttillm, ref bydw);
            line += DateTimeDOFToLine(tfromh, tfromm, ttillh, ttillm, bydw);

            rec.DateTimeList.Add(line);
        }

        private static void Read27DateTimeE0Block(RecAlertTriggerOptions rec, byte b8Flag, ref int offset) // week of year
        {
            bool unset_flag = (b8Flag & 0x01) == 0x01;
            bool has_week_oyear = (b8Flag & 0x02) == 0x02;
            //bool has_time_start = (b8Flag & 0x04) == 0x04;
            //bool has_time_end   = (b8Flag & 0x08) == 0x08;
            //bool has_dof_end    = (b8Flag & 0x10) == 0x10;

            byte tfromh = 0; byte tfromm = 0; byte ttillh = 24; byte ttillm = 0; byte bydw = 0x7F;
            string line = "on_week:";

            if (has_week_oyear)
                line += String.Format("{0:00}~{1:00},", rec.DateTimeBlock[offset++], rec.DateTimeBlock[offset++]);

            Read27DateTimeBlockTDOFPart(rec, ref offset, b8Flag, ref tfromh, ref tfromm, ref ttillh, ref ttillm, ref bydw);
            line += DateTimeDOFToLine(tfromh, tfromm, ttillh, ttillm, bydw);

            rec.DateTimeList.Add(line);
        }

        /// <summary>
        ///     Read Time & Day of Week Part of DateTime Block
        /// </summary>
        /// <param name="rec">Record</param>
        /// <param name="offset">offset</param>
        /// <param name="b8Flag">block08 flags</param>
        /// <param name="tfromh">Hour from</param>
        /// <param name="tfromm">Minutes from</param>
        /// <param name="ttillh">Hour till</param>
        /// <param name="ttillm">Minutes till</param>
        /// <param name="bydw">Day of week masked</param>
        private static void Read27DateTimeBlockTDOFPart(RecAlertTriggerOptions rec, ref int offset, byte b8Flag, ref byte tfromh, ref byte tfromm, ref byte ttillh, ref byte ttillm, ref byte bydw)
        {
            bool has_time_start = (b8Flag & 0x04) == 0x04;
            bool has_time_end = (b8Flag & 0x08) == 0x08;
            bool has_dof_end = (b8Flag & 0x10) == 0x10;

            if (has_time_start)
            {
                tfromh = rec.DateTimeBlock[offset++];
                if ((tfromh & 0x80) == 0x80)
                {
                    tfromh = (byte)(tfromh & 0x7F);
                    tfromm = rec.DateTimeBlock[offset++];
                };
            };

            if (has_time_end)
            {
                ttillh = rec.DateTimeBlock[offset++];
                if ((ttillh & 0x80) == 0x80)
                {
                    ttillh = (byte)(ttillh & 0x7F);
                    ttillm = rec.DateTimeBlock[offset++];
                };
            };

            if (has_dof_end)
                bydw = rec.DateTimeBlock[offset++];
        }

        /// <summary>
        ///     Time & Day of Week to text
        /// </summary>
        /// <param name="tfromh">Hour from</param>
        /// <param name="tfromm">Minutes from</param>
        /// <param name="ttillh">Hour till</param>
        /// <param name="ttillm">Minutes till</param>
        /// <param name="bydw">Day of week masked</param>
        /// <returns></returns>
        private static string DateTimeDOFToLine(byte tfromh, byte tfromm, byte ttillh, byte ttillm, byte bydw)
        {
            string line = "";

            line += String.Format("{0:00}:{1:00}..", tfromh, tfromm);
            line += String.Format("{0:00}:{1:00}", ttillh, ttillm);

            if (bydw != 0x7F)
                for (int z = 0; z < 7; z++)
                {
                    int vz = (int)Math.Pow(2, z);
                    if ((bydw & vz) == vz)
                    {
                        if (LOCALE_LANGUAGE == "RU")
                            line += String.Format(",{0}", (new string[] { "��", "��", "��", "��", "��", "��", "��" })[z]);
                        else
                            line += String.Format(",{0}", (new string[] { "sa", "fr", "th", "we", "tu", "mo", "su" })[z]);
                    };
                };
            return line;
        }

        /// <summary>
        ///     Part of 27 Record Type (RecAlertTriggerOptions)
        /// </summary>
        /// <param name="rec"></param>
        private static void Read27DateTimeListBlock(RecAlertTriggerOptions rec)
        {
            int offset = 0;
            while (true)
            {
                try
                {
                    byte b8Type = rec.DateTimeBlock[offset++];
                    byte b8Flag = rec.DateTimeBlock[offset++];
                    bool is_last_entry = (b8Flag & 0x80) == 0x80;
                    if (b8Type == 0x00) Read27DateTime00Block(rec, b8Flag, ref offset); // By Month
                    if (b8Type == 0x20) Read27DateTime20Block(rec, b8Flag, ref offset); // Dates                    
                    if (b8Type == 0x40) Read27DateTime40Block(rec, b8Flag, ref offset); // Day of year by week
                    if (b8Type == 0x60) Read27DateTime60Block(rec, b8Flag, ref offset); // day of month
                    if (b8Type == 0xC0) Read27DateTimeC0Block(rec, b8Flag, ref offset); // week of month
                    if (b8Type == 0xE0) Read27DateTimeE0Block(rec, b8Flag, ref offset); // week of year
                    if (is_last_entry || (offset >= rec.DateTimeBlock.Length)) break;
                }
                catch (Exception ex)
                {
                    rec.ReadError = ex;
                    break;
                };
            };
        } // part of 27

        /// <summary>
        ///     Get Color from number
        /// </summary>
        /// <param name="value">number</param>
        /// <returns></returns>
        private static Color ColorFromUint(uint value)
        {
            return Color.FromArgb((int)((value >> 0) & 0xFF), (int)((value >> 8) & 0xFF), (int)((value >> 16) & 0xFF));
        }
    }

    /* See KMZRebuilder to GPIWriter */
}
