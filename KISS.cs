//////////////////////////////////////
//                                  //
//   KISS TNC by milokz@gmail.com   //
//                                  //
//////////////////////////////////////


using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace ax25kiss
{
    public class KISSTNC
    {
        public enum ConnectionMode
        {
            TCPIP = 0,
            SERIAL = 1,
            AGW = 2
        }

        private ConnectionMode mode = ConnectionMode.TCPIP;
        private string server_or_serial = "";
        private int port_or_baud = 0;
        private byte radio_port = 0;

        private bool isRunning = false;
        private System.Threading.Thread mainThread = null;
        private TcpClient TCP = null;
        private SerialPort SP = null;
        private AgwpePort.AgwpePort AGW = null;
        public bool Debug = false;
        public bool ExitKissModeOnClose = false;

        private int ttlBR = 0;
        public int TotalBytesRead { get { return ttlBR; } }
        private int ttlBW = 0;
        public int TotalBytesWrite { get { return ttlBW; } }
        private ax25kiss.AX25Handler handler = null;
        public ax25kiss.AX25Handler onPacket { set { handler = value; } }
        public string Server { get { if (mode == ConnectionMode.SERIAL) return null; else return server_or_serial; } }
        public int Port { get { if (mode == ConnectionMode.SERIAL) return 0; else return port_or_baud; } }
        public string Serial { get { if (mode == ConnectionMode.SERIAL) return server_or_serial; else return null; } }
        public int Baud { get { if (mode == ConnectionMode.SERIAL) return port_or_baud; else return 0; } }
        private string kissInit = null;
        public string KISSInit { get { return kissInit; } set { kissInit = value; if ((!String.IsNullOrEmpty(kissInit)) && (!kissInit.EndsWith("\n"))) kissInit += "\r\n"; ;} }
        private DateTime last = DateTime.MinValue;
        public DateTime LastStatusDT { get { return last; } }

        /// <summary>
        ///     server:port -- TCP;
        ///     serial:baud -- SERIAL;
        ///     server:port:radio -- AGW;
        /// </summary>
        /// <param name="initConnectionLine">server/serial:port/baud[:radio]</param>
        public KISSTNC(string initConnectionLine)
        {
            if (String.IsNullOrEmpty(initConnectionLine)) throw new InvalidCastException("Wrong Parameter");
            if (initConnectionLine.IndexOf(":") < 0) throw new InvalidCastException("Wrong Parameter"); 
            string[] p = initConnectionLine.Split(new char[] { ':'}, StringSplitOptions.None);
            this.server_or_serial = p[0];
            this.port_or_baud = int.Parse(p[1]);
            this.mode = server_or_serial.ToLower().StartsWith("com") && char.IsDigit(server_or_serial[server_or_serial.Length - 1]) ? ConnectionMode.SERIAL : ConnectionMode.TCPIP;
            if (p.Length == 3)
            {
                this.radio_port = byte.Parse(p[2]);
                this.mode = ConnectionMode.AGW;
            };
        }

        public KISSTNC(string server_or_serial, int ipport_or_baud)
        {
            this.server_or_serial = server_or_serial;
            this.port_or_baud = ipport_or_baud;
            this.mode = server_or_serial.ToLower().StartsWith("com") && char.IsDigit(server_or_serial[server_or_serial.Length-1]) ? ConnectionMode.SERIAL : ConnectionMode.TCPIP;
        }

        public KISSTNC(string server_or_serial, int ipport_or_baud, ConnectionMode mode)
        {
            this.mode = mode;
            this.server_or_serial = server_or_serial;
            this.port_or_baud = ipport_or_baud;
        }

        public static KISSTNC CreateTCP(string server, int port)
        {
            return new KISSTNC(server, port, ConnectionMode.TCPIP);
        }

        public static KISSTNC CreateCOM(string port, int baud)
        {
            return new KISSTNC(port, baud, ConnectionMode.SERIAL);
        }

        public static KISSTNC CreateAGWE(string port, int baud, byte radioPort)
        {
            KISSTNC res = new KISSTNC(port, baud, ConnectionMode.AGW);
            res.radio_port = radioPort;
            return res;
        }

        public byte AGWRadioPort { get { return radio_port; } set { if (IsRunning) throw new Exception("Couldn't change port if running"); radio_port = value; } }
        public ConnectionMode Mode { get { return mode; } }
        public bool IsRunning { get { return isRunning; } }
        public bool Connected { get 
        {
            if (!IsRunning) return false;
            try
            {                
                if ((mode == ConnectionMode.TCPIP) && (TCP != null)) return IsConnected(TCP);
                if ((mode == ConnectionMode.SERIAL) && (SP != null)) return SP.IsOpen;
                if ((mode == ConnectionMode.AGW) && (AGW != null)) return AGW.IsOpen();
            }
            catch {};
            return false;
        } }

        public void MainThread()
        {
            ttlBR = 0;
            ttlBW = 0;
            List<byte> readed = new List<byte>();
            last = DateTime.UtcNow;
            
            bool valid = false;

            while (isRunning)
            {
                if (mode == ConnectionMode.TCPIP)
                {
                    try
                    {
                        if (TCP == null) TCP = new TcpClient();
                        if (!TCP.Connected) { 
                            TCP.Connect(server_or_serial, port_or_baud);
                            last = DateTime.UtcNow;
                            if (!String.IsNullOrEmpty(kissInit))
                            {
                                byte[] b2w = System.Text.Encoding.ASCII.GetBytes(kissInit);
                                TCP.GetStream().Write(b2w, 0, b2w.Length);
                                TCP.GetStream().Flush();
                                ttlBW += b2w.Length;
                            };
                        };
                        NetworkStream tcps = TCP.GetStream();
                        while (TCP.Available > 0) 
                            if(OnIncomingByte(tcps.ReadByte(), ref valid, ref readed)) 
                                last = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                        try { TCP.Close(); }
                        catch { };
                        TCP = null;
                        readed.Clear();
                        valid = false;
                    };
                    if ((TCP != null) && (DateTime.UtcNow.Subtract(last).TotalSeconds > 90))
                    {
                        try
                        {
                            if (!IsConnected(TCP)) throw new Exception("Not Connected");
                            last = DateTime.UtcNow;
                        }
                        catch  (Exception ex)
                        {
                            Console.WriteLine("Error: " + ex.Message);
                            try { TCP.Close(); }
                            catch { };
                            TCP = null;
                            readed.Clear();
                            valid = false;
                        };
                    };
                };

                if (mode == ConnectionMode.SERIAL)
                {
                    try
                    {
                        if (SP == null) SP = new SerialPort(server_or_serial, port_or_baud, Parity.None, 8, StopBits.One);
                        if (!SP.IsOpen) 
                        { 
                            SP.Open(); 
                            last = DateTime.UtcNow;
                            if (!String.IsNullOrEmpty(kissInit))
                            {
                                byte[] b2w = System.Text.Encoding.ASCII.GetBytes(kissInit);
                                SP.Write(b2w, 0, b2w.Length);
                                ttlBW += b2w.Length;
                            };
                        };
                        while(SP.BytesToRead > 0)
                            if (OnIncomingByte(SP.ReadByte(), ref valid, ref readed)) 
                                last = DateTime.UtcNow;                       
                    }
                    catch (TimeoutException tex) { }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                        try { SP.Close(); }
                        catch { };
                        SP = null;
                        readed.Clear();
                        valid = false;
                    };
                };

                if (mode == ConnectionMode.AGW)
                {
                    try
                    {
                        if (AGW == null) AGW = new AgwpePort.AgwpePort(0, server_or_serial, port_or_baud);
                        if (!AGW.IsOpen())
                        {
                            AGW.Open(radio_port, server_or_serial, port_or_baud);
                            AGW.FrameReceived += new AgwpePort.AgwpePort.AgwpeFrameReceivedEventHandler(OnIncomingAGW);
                            AGW.StartMonitoring();
                        };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                        try { AGW.Close(); } catch { };
                        AGW = null;
                    };
                };

                System.Threading.Thread.Sleep(50);
            };

            try
            {
                if (TCP != null)
                {
                    try { if (ExitKissModeOnClose) TCP.GetStream().Write(new byte[3] { 0xC0, 0xFF, 0xC0 }, 0, 3); } catch { };
                    TCP.Close();
                }
            }
            catch { };
            TCP = null;

            try
            {
                if (SP != null)
                {
                    try { if (ExitKissModeOnClose) SP.Write(new byte[3] { 0xC0, 0xFF, 0xC0 }, 0, 3); } catch { };
                    SP.Close();
                }
            }
            catch { };
            SP = null;

            try { if (AGW != null) AGW.Close(); } catch { };
            AGW = null;
        }

        private void OnIncomingAGW(object sender, AgwpePort.AgwpeEventArgs e)
        {
            // AGWPE Config and Information Frames // AX25 Unproto (UI)
            if (e.FrameHeader.DataKind == ((byte)'U'))
            {
                AgwpePort.AgwpeMoniUnproto md = (AgwpePort.AgwpeMoniUnproto)e.FrameData;
                string callTo = md.AX25CallTo;
                string[] path = new string[0];
                int iofvia = callTo.IndexOf(" Via ");
                if (iofvia > 0)
                {
                    path = callTo.Substring(iofvia+5).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    callTo = callTo.Substring(0, iofvia);
                };
                ax25kiss.Packet p = new ax25kiss.Packet(callTo, md.AX25CallFrom, path, 0, 0, md.AX25Data);
                OnIncomingPacket(p);
            };
        }       

        private bool OnIncomingByte(int bRead, ref bool valid, ref List<byte> readed)
        {            
            if (bRead >= 0)
            {
                ttlBR++;
                if (!valid)
                {
                    if (bRead == ax25kiss.Packet.KISS_FEND)
                    {
                        valid = true;
                        readed.Add((byte)bRead);
                    }
                    else if (Debug)
                    {
                        Console.Write((char)bRead);
                        if (bRead == 13) Console.WriteLine();
                    };
                }
                else
                {
                    readed.Add((byte)bRead);
                    if (bRead == ax25kiss.Packet.KISS_FEND)
                    {
                        valid = false;
                        byte[] arr = readed.ToArray();
                        ax25kiss.Packet p = ax25kiss.Packet.FromKissFullFrame(arr);
                        readed.Clear();
                        if (p != null) 
                            OnIncomingPacket(p);
                        else 
                            OnIncomingNonPacket(arr);
                    };
                };
                return true;
            }
            else
                return false;
        }

        private void OnIncomingPacket(ax25kiss.Packet packet)
        {
            if (handler != null)
                handler.handlePacket(packet);
            else
                Console.WriteLine(packet.ToString());
        }

        private void OnIncomingNonPacket(byte[] packet)
        {
            if (!Debug) return;
            Console.WriteLine("Type: {0} Data: {1}", packet[1], BitConverter.ToString(packet, 2, packet.Length - 3).Replace("-", ""));
        }

        /// <summary>
        ///     The amount of time to wait between keying the transmitter and beginning to send data (in ms, max 2550)
        /// </summary>
        /// <param name="ms">ms</param>
        public void SetTXDelay(ushort ms)
        {
            if (!isRunning) return;
            if (mode == ConnectionMode.AGW) return;
            byte delay = ms > 2550 ? (byte)255 : (byte)(ms / 10);
            if ((mode == ConnectionMode.TCPIP) && (TCP != null) && (TCP.Connected))
            {                
                try {
                    TCP.GetStream().Write(new byte[4] { 0xC0, 0x01, delay == 0xC0 ? (byte)0xC1 : (byte)delay, 0xC0 }, 0, 4);
                    TCP.GetStream().Flush();
                }
                catch { };
            };
            if ((mode == ConnectionMode.SERIAL) && (SP != null) && (SP.IsOpen))
            {
                try {
                    SP.Write(new byte[4] { 0xC0, 0x01, delay == 0xC0 ? (byte)0xC1 : (byte)delay, 0xC0 }, 0, 4);
                }
                catch { };
            };
        }

        /// <summary>
        ///     The length of time to keep the transmitter keyed after sending the data (in ms, max 2550)
        /// </summary>
        /// <param name="ms"></param>
        public void SetTXTail(ushort ms)
        {
            if (!isRunning) return;
            if (mode == ConnectionMode.AGW) return;
            byte delay = ms > 2550 ? (byte)255 : (byte)(ms / 10);
            if ((mode == ConnectionMode.TCPIP) && (TCP != null) && (TCP.Connected))
            {
                try
                {
                    TCP.GetStream().Write(new byte[4] { 0xC0, 0x04, delay == 0xC0 ? (byte)0xC1 : (byte)delay, 0xC0 }, 0, 4);
                    TCP.GetStream().Flush();
                }
                catch { };
            };
            if ((mode == ConnectionMode.SERIAL) && (SP != null) && (SP.IsOpen))
            {
                try
                {
                    SP.Write(new byte[4] { 0xC0, 0x04, delay == 0xC0 ? (byte)0xC1 : (byte)delay, 0xC0 }, 0, 4);
                }
                catch { };
            };
        }

        public void Start()
        {
            if (IsRunning) return;
            isRunning = true;

            mainThread = new System.Threading.Thread(MainThread);
            mainThread.Start();
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;

            if (mainThread != null) mainThread.Join();
            mainThread = null;

            try { if (TCP != null) TCP.Close(); } catch { };
            TCP = null;

            try { if (SP != null) SP.Close(); } catch { };
            SP = null;

            try { if (AGW != null) AGW.Close(); } catch { };
            AGW = null;
        }

        public bool Write(byte[] buffer, int offset, int count)
        {
            if(!IsRunning) return false;

            if ((mode == ConnectionMode.TCPIP) && (TCP != null) && (TCP.Connected))
            {
                try
                {
                    TCP.GetStream().Write(buffer, offset, count);
                    TCP.GetStream().Flush();
                    ttlBW += count;
                    return true;
                }
                catch { return false; };
            };

            if ((mode == ConnectionMode.SERIAL) && (SP != null) && (SP.IsOpen))
            {
                try
                {
                    SP.Write(buffer, offset, count);
                    ttlBW += count;
                    return true;
                }
                catch { return false; };
            };            

            return false;
        }

        public bool Send(string destination, string source, string[] digipath, string data)
        {
            if (!IsRunning) return false;
            if ((mode == ConnectionMode.TCPIP) || (mode == ConnectionMode.SERIAL))
            {
                byte[] kissDataFrame = ax25kiss.Packet.KissDataFrame(destination, source, digipath, ax25kiss.Packet.AX25_CONTROL_APRS, ax25kiss.Packet.AX25_PROTOCOL_NO_LAYER_3, System.Text.Encoding.ASCII.GetBytes(data));
                byte[] kissFullFrame = ax25kiss.Packet.KissFullFrame(kissDataFrame);
                //ax25kiss.Packet pTest = ax25kiss.Packet.FromKissFullFrame(kissFullFrame);
                return Write(kissFullFrame, 0, kissFullFrame.Length);
            };
            if (mode == ConnectionMode.AGW)
            {
                if((AGW != null) && AGW.IsOpen())
                {
                    try
                    {
                        if((digipath == null) || (digipath.Length == 0))
                            AGW.SendUnproto(radio_port, source, destination, System.Text.Encoding.ASCII.GetBytes(data));
                        else
                            AGW.SendUnproto(radio_port, source, destination, System.Text.Encoding.ASCII.GetBytes(data), digipath);
                        return true;
                    }
                    catch {return false;};
                };
            };
            return false;
        }

        private static bool IsConnected(TcpClient Client)
        {
            if (!Client.Connected) return false;
            if (Client.Client.Poll(0, SelectMode.SelectRead))
            {
                byte[] buff = new byte[1];
                try
                {
                    if (Client.Client.Receive(buff, SocketFlags.Peek) == 0)
                        return false;
                }
                catch
                {
                    return false;
                };
            };
            return true;
        }

        /// <summary>
        ///     NMEA Checksum $...*
        /// </summary>
        /// <param name="sentence"></param>
        /// <returns></returns>
        public static string GetNMEAChecksum(string sentence)
        {
            int checksum = Convert.ToByte(sentence[sentence.IndexOf('$') + 1]);
            int tolen = sentence.IndexOf('*');
            if (tolen < 0) tolen = sentence.Length;
            for (int i = sentence.IndexOf('$') + 2; i < tolen; i++)
                checksum ^= Convert.ToByte(sentence[i]);
            return checksum.ToString("X2");
        }

        public static void Test()
        {
            string CallSign = "ZADIRA";
            string Destination = "APRS";
            string infoField = "!5533.00ND03733.00E&RNG0001/A=000010 440 Voice 439.73750MHz +0.0000MHz";

            byte[] kissDataFrame = ax25kiss.Packet.KissDataFrame(Destination, CallSign, new string[] { "WIDE2-1" }, ax25kiss.Packet.AX25_CONTROL_APRS, ax25kiss.Packet.AX25_PROTOCOL_NO_LAYER_3, System.Text.Encoding.ASCII.GetBytes(infoField));
            foreach (byte b in kissDataFrame) Console.Write("{0}", (char)b);
            Console.WriteLine(); Console.WriteLine();

            // Test
            ax25kiss.Packet p1 = new ax25kiss.Packet(kissDataFrame);

            byte[] kissFullFrame = ax25kiss.Packet.KissFullFrame(kissDataFrame);
            foreach (byte b in kissFullFrame) Console.Write("{0}", (char)b);
            Console.WriteLine(); Console.WriteLine();

            // Test
            ax25kiss.Packet p2 = ax25kiss.Packet.FromKissFullFrame(kissFullFrame);

            // TCP Test -- Sound Modem 0.97b -- AFSK AX.25 1200bd : 1700
            TcpClient tcp = new TcpClient();
            tcp.Connect("127.0.0.1", 8100);
            NetworkStream tcps = tcp.GetStream();
            tcps.Write(kissFullFrame, 0, kissFullFrame.Length);
            bool loop = true;
            bool valid = false;
            while (loop)
            {
                //
                // READ INCOMING PACKETS
                //
                List<byte> readed = new List<byte>();
                int bRead = -1;
                while (tcp.Available > 0)
                {
                    bRead = tcps.ReadByte();
                    if (bRead >= 0)
                    {
                        if (!valid)
                        {
                            if (bRead == ax25kiss.Packet.KISS_FEND)
                            {
                                valid = true;
                                readed.Add((byte)bRead);
                            };
                        }
                        else                        
                        {
                            readed.Add((byte)bRead);
                            if (bRead == ax25kiss.Packet.KISS_FEND)
                            {
                                valid = false;
                                ax25kiss.Packet p3 = ax25kiss.Packet.FromKissFullFrame(readed.ToArray());
                                readed.Clear();
                                if (p3 != null)
                                {
                                    // OK
                                    Console.WriteLine(p3.ToString());
                                };
                            };                 
                        };                               
                    };
                };
                System.Threading.Thread.Sleep(50);
            };
            tcp.Close();
            return;

            //Serial Test
            using (SerialPort sp = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One))
            {
                sp.Open();
                sp.Write(kissFullFrame, 0, kissFullFrame.Length);
                sp.Close();
            };

            Console.ReadLine();
        }
    }
}