using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace KMZ_Viewer
{
    public static class Buddies
    {
        public static System.Threading.Mutex mtx = new System.Threading.Mutex();
        public static List<string> updates = new List<string>();
        public static Dictionary<string,Buddie> list = new Dictionary<string,Buddie>();

        public static void Update(Buddie b)
        {
            mtx.WaitOne();
            
            if (list.ContainsKey(b.name))
            {
                if (!String.IsNullOrEmpty(b.Comment)) list[b.name].Comment = b.Comment;                
                list[b.name].course = b.course;
                list[b.name].IconSymbol = b.IconSymbol;
                list[b.name].last = b.last;
                list[b.name].lastPacket = b.lastPacket;
                list[b.name].lat = b.lat;
                list[b.name].lon = b.lon;
                list[b.name].speed = b.speed;
                if (!String.IsNullOrEmpty(b.Status)) list[b.name].Status = b.Status;                
            }
            else
                list.Add(b.name, b);
            
            if ((list[b.name].tail.Count == 0) || (list[b.name].tail[list[b.name].tail.Count - 1][0] != b.lat) || (list[b.name].tail[list[b.name].tail.Count - 1][1] != b.lon))
                list[b.name].tail.Add(new double[] { b.lat, b.lon });
            while (list[b.name].tail.Count > 100) list[b.name].tail.RemoveAt(0);

            if(updates.IndexOf(b.name) < 0)
                updates.Add(b.name);
            
            mtx.ReleaseMutex();
        }
    }

    public class Buddie
    {
        public static Regex BuddieNameRegex = new Regex("^([A-Z0-9]{3,9})$");
        public static Regex BuddieCallSignRegex = new Regex(@"^([A-Z0-9\-]{3,9})$");
        public static string symbolAny = "/*/</=/>/C/F/M/P/U/X/Y/Z/[/a/b/e/f/j/k/p/s/u/v\\O\\j\\k\\u\\v/0/1/2/3/4/5/6/7/8/9/'/O";
        public static int symbolAnyLength = 40;

        internal static ulong _id = 0;
        private ulong _ID = 0;
        internal ulong ID
        {
            get { return _ID; }
            set
            {
                _ID = value;
                if (_ID == 0)
                {
                    IconSymbol = "//";
                    return;
                }
                else if (Buddie.IsNullIcon(IconSymbol))
                    IconSymbol = Buddie.symbolAny.Substring((((int)_ID - 1) % Buddie.symbolAnyLength) * 2, 2);
            }
        }

        public static bool IsNullIcon(string symbol)
        {
            return (symbol == null) || (symbol == String.Empty) || (symbol == "//");
        }

        public byte source; // 0 - unknown; 1 - GPSGate Format; 2 - MapMyTracks Format; 3 - APRS; 4 - FRS; 5 - everytime; 6 - static
        public string name;
        public double lat;
        public double lon;
        public short speed;
        public short course;

        public DateTime last;
        public bool green;

        private string aAIS = "";
        private byte[] aAISNMEA = null;
        private string bAIS = "";
        private byte[] bAISNMEA = null;

        public string AIS
        {
            get
            {
                return green ? bAIS : aAIS;
            }
        }
        public byte[] AISNMEA
        {
            get
            {
                return green ? bAISNMEA : aAISNMEA;
            }
        }

        public string APRS = "";
        public byte[] APRSData = null;

        public string FRPOS = "";
        public byte[] FRPOSData = null;

        public string IconSymbol = "//";

        public string parsedComment = "";
        public string Comment
        {
            get
            {
                if ((parsedComment != null) && (parsedComment != String.Empty)) return parsedComment;
                return "";
            }
            set
            {
                parsedComment = value;
            }
        }
        public string Status = "";

        public string lastPacket = "";

        public bool PositionIsValid
        {
            get { return (lat != 0) && (lon != 0); }
        }

        public Buddie(byte source, string name, double lat, double lon, short speed, short course)
        {
            this.source = source;
            this.name = name;
            this.lat = lat;
            this.lon = lon;
            this.speed = speed;
            this.course = course;
            this.last = DateTime.UtcNow;
            this.green = false;
        }


        internal void SetAPRS()
        {
            if (this.source == 3)
            {
                if (((this.parsedComment == null) || (this.parsedComment == String.Empty)) && (this.Comment != null))
                {
                    this.APRS = this.APRS.Insert(this.APRS.Length - 2, " " + this.Comment);
                    this.APRSData = Encoding.ASCII.GetBytes(this.APRS);
                };
                return;
            };

            APRS =
                name + ">APRS,TCPIP*:=" + // Position without timestamp + APRS message
                Math.Truncate(lat).ToString("00") + ((lat - Math.Truncate(lat)) * 60).ToString("00.00").Replace(",", ".") +
                (lat > 0 ? "N" : "S") +
                IconSymbol[0] +
                Math.Truncate(lon).ToString("000") + ((lon - Math.Truncate(lon)) * 60).ToString("00.00").Replace(",", ".") +
                (lon > 0 ? "E" : "W") +
                IconSymbol[1] +
                course.ToString("000") + "/" + Math.Truncate(speed / 1.852).ToString("000") +
                ((this.Comment != null) && (this.Comment != String.Empty) ? " " + this.Comment : "") +
                "\r\n";
            APRSData = Encoding.ASCII.GetBytes(APRS);
        }

        public override string ToString()
        {
            return String.Format("{0} at {1}, {2} {3} {4}, {5}", new object[] { name, source, lat, lon, speed, course });
        }

        public static int Hash(string name)
        {
            string upname = name == null ? "" : name;
            int stophere = upname.IndexOf("-");
            if (stophere > 0) upname = upname.Substring(0, stophere);
            while (upname.Length < 9) upname += " ";

            int hash = 0x2017;
            int i = 0;
            while (i < 9)
            {
                hash ^= (int)(upname.Substring(i, 1))[0] << 16;
                hash ^= (int)(upname.Substring(i + 1, 1))[0] << 8;
                hash ^= (int)(upname.Substring(i + 2, 1))[0];
                i += 3;
            };
            return hash & 0x7FFFFF;
        }

        public static uint MMSI(string name)
        {
            string upname = name == null ? "" : name;
            while (upname.Length < 9) upname += " ";
            int hash = 2017;
            int i = 0;
            while (i < 9)
            {
                hash ^= (int)(upname.Substring(i, 1))[0] << 16;
                hash ^= (int)(upname.Substring(i + 1, 1))[0] << 8;
                hash ^= (int)(upname.Substring(i + 2, 1))[0];
                i += 3;
            };
            return (uint)(hash & 0xFFFFFF);
        }

        public static void CopyData(Buddie copyFrom, Buddie copyTo)
        {
            if ((copyTo.source != 3) && (!Buddie.IsNullIcon(copyFrom.IconSymbol)))
                copyTo.IconSymbol = copyFrom.IconSymbol;

            if (Buddie.IsNullIcon(copyTo.IconSymbol))
                copyTo.IconSymbol = copyFrom.IconSymbol;

            if ((copyTo.parsedComment == null) || (copyTo.parsedComment == String.Empty))
            {
                copyTo.parsedComment = copyFrom.parsedComment;
                if ((copyTo.source == 3) && (copyTo.parsedComment != null) && (copyTo.parsedComment != String.Empty))
                {
                    copyTo.APRS = copyTo.APRS.Insert(copyTo.APRS.Length - 2, " " + copyTo.Comment);
                    copyTo.APRSData = Encoding.ASCII.GetBytes(copyTo.APRS);
                };
            };

            copyTo.ID = copyFrom.ID;
            copyTo.Status = copyFrom.Status;
        }

        public List<double[]> tail = new List<double[]>();
        public System.Drawing.Color color = System.Drawing.Color.Plum;
    }

    // APRS
    public class APRSData
    {
        public static int CallsignChecksum(string callsign)
        {
            if (callsign == null) return 99999;
            if (callsign.Length == 0) return 99999;
            if (callsign.Length > 10) return 99999;

            int stophere = callsign.IndexOf("-");
            if (stophere > 0) callsign = callsign.Substring(0, stophere);
            string realcall = callsign.ToUpper();
            while (realcall.Length < 10) realcall += " ";

            // initialize hash 
            int hash = 0x73e2;
            int i = 0;
            int len = realcall.Length;

            // hash callsign two bytes at a time 
            while (i < len)
            {
                hash ^= (int)(realcall.Substring(i, 1))[0] << 8;
                hash ^= (int)(realcall.Substring(i + 1, 1))[0];
                i += 2;
            }
            // mask off the high bit so number is always positive 
            return hash & 0x7fff;
        }

        public static Buddie ParseAPRSPacket(string line)
        {
            if (line.IndexOf("#") == 0) return null; // comment packet

            // Valid APRS?
            int fChr = line.IndexOf(">");
            if (fChr <= 1) return null;  // invalid packet
            int sChr = line.IndexOf(":");
            if (sChr < fChr) return null;  // invalid packet

            string callsign = line.Substring(0, fChr);
            string pckroute = line.Substring(fChr + 1, sChr - fChr - 1);
            string packet = line.Substring(sChr);

            if (packet.Length < 2) return null; // invalid packet

            Buddie b = new Buddie(3, callsign, 0, 0, 0, 0);
            b.lastPacket = line;
            b.APRS = line + "\r\n";
            b.APRSData = Encoding.ASCII.GetBytes(b.APRS);

            switch (packet[1])
            {
                /* Object */
                case ';':
                    int sk0 = Math.Max(packet.IndexOf("*", 2, 10), packet.IndexOf("_", 2, 10));
                    if (sk0 < 0) return null;
                    string obj_name = packet.Substring(2, sk0 - 2).Trim();
                    if (packet.IndexOf("*") > 0)
                        return ParseAPRSPacket(obj_name + ">" + pckroute + ":@" + packet.Substring(sk0 + 1)); // set object name as callsign and packet as position
                    break;
                /* Item Report Format */
                case ')':
                    int sk1 = Math.Max(packet.IndexOf("!", 2, 10), packet.IndexOf("_", 2, 10));
                    if (sk1 < 0) return null;
                    string rep_name = packet.Substring(2, sk1 - 2).Trim();
                    if (packet.IndexOf("!") > 0)
                        return ParseAPRSPacket(rep_name + ">" + pckroute + ":@" + packet.Substring(sk1 + 1)); // set object name as callsign and packet as position
                    break;

                /* Positions Reports */
                case '!': // Positions with no time, no APRS
                case '=': // Position with no time, but APRS
                case '/': // Position with time, no APRS
                case '@': // Position with time and APRS
                    {
                        string pos = packet.Substring(2);
                        if (pos[0] == '!') break; // Raw Weather Data

                        DateTime received = DateTime.UtcNow;
                        if (pos[0] != '/') // not compressed data firsts
                        {
                            switch (packet[8])
                            {
                                case 'z': // zulu ddHHmm time
                                    received = new DateTime(DateTime.Now.Year, DateTime.Now.Month, int.Parse(packet.Substring(2, 2)),
                                    int.Parse(packet.Substring(4, 2)), int.Parse(packet.Substring(6, 2)), 0, DateTimeKind.Utc);
                                    pos = packet.Substring(9);
                                    break;
                                case '/': // local ddHHmm time
                                    received = new DateTime(DateTime.Now.Year, DateTime.Now.Month, int.Parse(packet.Substring(2, 2)),
                                    int.Parse(packet.Substring(4, 2)), int.Parse(packet.Substring(6, 2)), 0, DateTimeKind.Local);
                                    pos = packet.Substring(9);
                                    break;
                                case 'h': // HHmmss time
                                    received = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                                    int.Parse(packet.Substring(2, 2)), int.Parse(packet.Substring(4, 2)), int.Parse(packet.Substring(6, 2)), DateTimeKind.Local);
                                    pos = packet.Substring(9);
                                    break;
                            };
                        };

                        string aftertext = "";
                        char prim_or_sec = '/';
                        char symbol = '>';

                        if (pos[0] == '/') // compressed data YYYYXXXXcsT // 
                        {
                            string yyyy = pos.Substring(1, 4);
                            b.lat = 90 - (((byte)yyyy[0] - 33) * Math.Pow(91, 3) + ((byte)yyyy[1] - 33) * Math.Pow(91, 2) + ((byte)yyyy[2] - 33) * 91 + ((byte)yyyy[3] - 33)) / 380926;
                            string xxxx = pos.Substring(5, 4);
                            b.lon = -180 + (((byte)xxxx[0] - 33) * Math.Pow(91, 3) + ((byte)xxxx[1] - 33) * Math.Pow(91, 2) + ((byte)xxxx[2] - 33) * 91 + ((byte)xxxx[3] - 33)) / 190463;
                            symbol = pos[9];
                            string cmpv = pos.Substring(10, 2);
                            int addIfWeather = 0;
                            if (cmpv[0] == '_') // with weather report
                            {
                                symbol = '_';
                                cmpv = pos.Substring(11, 2);
                                addIfWeather = 1;
                            };
                            if (cmpv[0] != ' ') // ' ' - no data
                            {
                                int cmpt = ((byte)pos[12 + addIfWeather] - 33);
                                if (((cmpt & 0x18) == 0x18) && (cmpv[0] != '{') && (cmpv[0] != '|')) // RMC sentence with course & speed
                                {
                                    b.course = (short)(((byte)cmpv[0] - 33) * 4);
                                    b.speed = (short)(((int)Math.Pow(1.08, ((byte)cmpv[1] - 33)) - 1) * 1.852);
                                };
                            };
                            aftertext = pos.Substring(13 + addIfWeather);
                            b.IconSymbol = "/" + symbol.ToString();
                        }
                        else // not compressed //
                        {
                            if (pos.Substring(0, 18).Contains(" ")) return null; // nearest degree

                            b.lat = double.Parse(pos.Substring(2, 5), System.Globalization.CultureInfo.InvariantCulture);
                            b.lat = double.Parse(pos.Substring(0, 2), System.Globalization.CultureInfo.InvariantCulture) + b.lat / 60;
                            if (pos[7] == 'S') b.lat *= -1;

                            b.lon = double.Parse(pos.Substring(12, 5), System.Globalization.CultureInfo.InvariantCulture);
                            b.lon = double.Parse(pos.Substring(9, 3), System.Globalization.CultureInfo.InvariantCulture) + b.lon / 60;
                            if (pos[17] == 'W') b.lon *= -1;

                            prim_or_sec = pos[8];
                            symbol = pos[18];
                            aftertext = pos.Substring(19);

                            b.IconSymbol = prim_or_sec.ToString() + symbol.ToString();
                        };

                        // course/speed or course/speed/bearing/NRQ
                        if ((symbol != '_') && (aftertext.Length >= 7) && (aftertext[3] == '/')) // course/speed 000/000
                        {
                            short.TryParse(aftertext.Substring(0, 3), out b.course);
                            short.TryParse(aftertext.Substring(4, 3), out b.speed);
                            aftertext = aftertext.Remove(0, 7);
                        };

                        b.Comment = aftertext.Trim();

                    };
                    break;
                /* All Other */
                default:
                    //
                    break;
            };
            return b;
        }
    }
}
