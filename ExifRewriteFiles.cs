using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace KMZ_Viewer
{
    /// <summary>
    ///     ExifInfo READ/WRITE
    /// </summary>
    public class ExifInfo
    {
        public List<IFD_Entry> Entries = new List<IFD_Entry>();

        public int Count
        {
            get
            {
                if (Entries == null) return 0;
                return Entries.Count;
            }
        }

        public void TrimUnknown()
        {
            if (Count == 0) return;
            for (int i = Entries.Count - 1; i >= 0; i--)
                if (Entries[i].entry_tag_name == "unknown")
                    Entries.RemoveAt(i);
        }

        public static IFD_Entry[] ParseExifSrc(byte[] ff00_exif_data)
        {
            List<byte> normal = new List<byte>();
            normal.Add(ff00_exif_data[0]);
            for (int i = 1; i < ff00_exif_data.Length; i++)
            {
                if ((ff00_exif_data[i - 1] == 0xFF) && (ff00_exif_data[i] == 0x00))
                    continue;
                else
                    normal.Add(ff00_exif_data[i]);
            };
            return ParseExifNrm(normal.ToArray());
        }

        public static IFD_Entry[] ParseExifNrm(byte[] normallized_exif_data)
        {
            string exif_header = System.Text.Encoding.ASCII.GetString(normallized_exif_data, 0, 6);
            if (exif_header != "Exif\0\0") return null; // No Valid Data

            byte[] data = new byte[normallized_exif_data.Length - 6];
            Array.Copy(normallized_exif_data, 6, data, 0, data.Length);

            bool littleEndian = false;
            if ((data[0] == 0x49) && (data[1] == 0x49)) // Intel type byte align
                littleEndian = true;
            if ((data[0] == 0x4D) && (data[1] == 0x4D)) // Motorola type byte align
                littleEndian = false;
            MyBitConverter bc = new MyBitConverter(littleEndian);

            bool valid_tiff_header = false;
            if (littleEndian && (data[2] == 0x2A) && (data[3] == 0x00)) valid_tiff_header = true;
            if ((!littleEndian) && (data[3] == 0x2A) && (data[2] == 0x00)) valid_tiff_header = true;
            if (!valid_tiff_header) throw new IOException("Invalid Tiff Header");

            List<ushort> entry_no = new List<ushort>();
            List<uint> entry_offsets = new List<uint>();

            entry_no.Add(0); entry_offsets.Add(bc.ToUInt32(data, 4)); // offset to IFD record // IFD0
            List<IFD_Entry> Exif = new List<IFD_Entry>();
            ushort IFD_ID = 0;
            while (entry_offsets.Count > 0)
            {
                ushort number = entry_no[0]; entry_no.RemoveAt(0);
                uint offset = entry_offsets[0]; entry_offsets.RemoveAt(0);

                int maxEntNo = (data.Length - 2 - 4 - 8) / 12;
                    ushort entNo = bc.ToUInt16(data, (int)offset); // number of entries in IFD // 2 bytes
                    if (entNo > 890) continue; // wrong data
                    bool add = true;
                    for (int i = 0; i < entNo; i++)
                    {
                        try
                        {
                            int _eo = (int)offset + 2 + i * 12;
                            if(_eo >= data.Length) continue;
                            IFD_Entry ent = new IFD_Entry(number, bc, data, _eo);
                            if (ent.entry_tag_number == 0x0000 ) continue;
							if (ent.data_length_calculated == 0) continue;
                            if (ent.entry_tag_number == 0x8769) // ExifOffset(0x8769). Its value is an offset to Exif SubIFD
                        {
                            entry_no.Add(0x8769);
                            entry_offsets.Add((uint)ent.entry_value);
                            add = false;
                        };
                        if (ent.entry_tag_number == 0x8825) // ExifOffset(0x8825). Its value is an offset to GPS SubIFD
                        {
                            entry_no.Add(0x8825);
                            entry_offsets.Add((uint)ent.entry_value);
                            add = false;
                        }
                        if (ent.entry_tag_number == 0xA005) // ExifOffset(0xA005). Its value is an offset to Interoperability SubIFD
                        {
                            entry_no.Add(0xA005);
                            entry_offsets.Add((uint)ent.entry_value);
                            add = false;
                        }
						if ((ent.entry_tag_number == 0x927C) && (ent.entry_value is byte[])) // MakerNote
                            {
                                string bs = System.Text.Encoding.ASCII.GetString((byte[])ent.entry_value,0,5);
                                if (bs == "Nikon")
                                {
                                    byte[] NikonData = new byte[ent.data_length_calculated-10+6];
                                    Array.Copy(System.Text.Encoding.ASCII.GetBytes("Exif\0\0"), 0, NikonData, 0, 6);
                                    Array.Copy((byte[])ent.entry_value, 10, NikonData, 6, ent.data_length_calculated - 10);
                                    IFD_Entry[] NikonEntries = ParseExifNrm(NikonData);
                                    if ((NikonEntries != null) && (NikonEntries.Length > 0))
                                    {
                                        foreach (IFD_Entry en in NikonEntries)
                                            en.entry_IFD_number = 0x927C;
                                        Exif.AddRange(NikonEntries);
                                    };
                                };
                            };
                        if (add)
                            Exif.Add(ent);
                    }
                    catch { };
                };
                offset = offset + (uint)2 + (uint)entNo * (uint)12;
				if (offset >= data.Length) continue;
                offset = bc.ToUInt32(data, (int)offset);
                if ((offset > 10) && (offset < normallized_exif_data.Length))
                {
                    entry_no.Add(IFD_ID++);
                    entry_offsets.Add(offset);
                };
            };

            return Exif.ToArray();
        }

        public static ExifInfo ParseExifFile(string jpeg_file_name)
        {
            ExifInfo exi = new ExifInfo();

            FileStream fs = new FileStream(jpeg_file_name, FileMode.Open, FileAccess.Read);
            int prev_byte = fs.ReadByte();
            if ((prev_byte == 0xFF) && ((prev_byte = fs.ReadByte()) == 0xD8)) // JPEG TAG
            {
                while (prev_byte >= 0)
                {
                    if ((prev_byte == 0xFF) && ((prev_byte = fs.ReadByte()) >= 0xE0)) // JFIF
                    {
                        int len = (((byte)fs.ReadByte()) << 8) + fs.ReadByte() - 2;  // Exif Length                   
                        byte[] exif_data = new byte[len];
                        fs.Read(exif_data, 0, len);
                        if (prev_byte == 0xE1) // Exif
                        {
                            IFD_Entry[] entries = ExifInfo.ParseExifSrc(exif_data);
                            if ((entries != null) && (entries.Length != 0))
                                exi.Entries.AddRange(entries);
                        };
                    }
                    else
                        prev_byte = fs.ReadByte();
                };
            };
            fs.Close();
            return exi;
        }

        public IFD_Entry this[string name]
        {
            get
            {
                string nm = name.ToLower().Trim();
                if (Entries.Count == 0) return null;
                for (int i = 0; i < Entries.Count; i++)
                    if (Entries[i].entry_tag_name.ToLower() == nm)
                        return Entries[i];
                return null;
            }
        }

        public IFD_Entry this[int index]
        {
            get
            {
                if (Entries.Count == 0) return null;
                for (int i = 0; i < Entries.Count; i++)
                    if (Entries[i].entry_tag_number == index)
                        return Entries[i];
                return null;
            }
        }

        public string this[string key, string nullValue]
        {
            get
            {
                IFD_Entry result = this[key];
                if (result == null)
                    return nullValue;
                else
                    return result.entry_text;
            }
        }

        public string this[int index, string nullValue]
        {
            get
            {
                IFD_Entry result = this[index];
                if (result == null)
                    return nullValue;
                else
                    return result.entry_text;
            }
        }


        public class IFD_Entry
        {
            public int entry_IFD_number;
            public ushort entry_tag_number;
            public ushort entry_data_format;
            public Type entry_format_type;
            public object entry_value;

            public uint data_number_of_components;
            public uint data_value_or_offset;
            public uint data_length_calculated;
            public uint data_offset_in_exif;

            private int _offset;

            public override string ToString()
            {
                return String.Format("0x{0:X4} {1}: {2}", new object[] { entry_tag_number, entry_tag_name, entry_value });
            }
			
			public string entry_text
                {
                    get
                    {                        
                        if (entry_tag_number == 0xa460) //   CompositeImage
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Unknown";
                                case 1: return "Not a Composite Image";
                                case 2: return "General Composite Image";
                                case 3: return "Composite Image Captured While Shooting";
                            };
                        };
                        if (entry_tag_number == 0xa40C) //   SubjectDistanceRange
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Unknown";
                                case 1: return "Macro";
                                case 2: return "Close";
                                case 3: return "Distant";
                            };
                        };
                        if (entry_tag_number == 0xa40A) //   Sharpness
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Normal";
                                case 1: return "Soft";
                                case 2: return "Hard";
                            };
                        };
                        if (entry_tag_number == 0xa409) //   Saturation
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Normal";
                                case 1: return "Low";
                                case 2: return "High";
                            };
                        };
                        if (entry_tag_number == 0xa408) //   Contrast
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Normal";
                                case 1: return "Low";
                                case 2: return "High";
                            };
                        };
                        if (entry_tag_number == 0xa407) //   GainControl
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "None";
                                case 1: return "Low gain up";
                                case 2: return "High gain up";
                                case 3: return "Low gain down";
                                case 4: return "High gain down";
                            };
                        };
                        if (entry_tag_number == 0xa406) //   SceneCaptureType
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Standard";
                                case 1: return "Landscape";
                                case 2: return "Portrait";
                                case 3: return "Night";
                                case 4: return "Other";
                            };
                        };
                        if (entry_tag_number == 0xa403) //   WhiteBalance
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Auto";
                                case 1: return "Manual";
                            };
                        };
                        if (entry_tag_number == 0xa402) //   ExposureMode
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Auto";
                                case 1: return "Manual";
                                case 2: return "Auto bracket";
                            };
                        };
                        if (entry_tag_number == 0xa401) //   CustomRendered
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Normal";
                                case 1: return "Custom";
                                case 2: return "HDR (no original saved)";
                                case 3: return "HDR (original saved)";
                                case 4: return "Original (for HDR)";
                                case 6: return "Panorama";
                                case 7: return "Portrait HDR";
                                case 8: return "Portrait";
                            };
                        };
                        if (entry_tag_number == 0xa217) //   SensingMethod
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 1: return "Not defined";
                                case 2: return "One-chip color area";
                                case 3: return "Two-chip color area";
                                case 4: return "Three-chip color area";
                                case 5: return "Color sequential area";
                                case 7: return "Trilinear";
                                case 8: return "Color sequential linear";
                            };
                        };
                        if (entry_tag_number == 0xa210) //   FocalPlaneResolutionUnit
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 1: return "None";
                                case 2: return "inches";
                                case 3: return "cm";
                                case 4: return "mm";
                                case 5: return "um";
                            };
                        };
                        if (entry_tag_number == 0xa001) //   ColorSpace
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0x0001: return "sRGB";
                                case 0x0002: return "Adobe RGB";
                                case 0xfffd: return "Wide Gamut RGB";
                                case 0xfffe: return "ICC Profile";
                                case 0xffff: return "Uncalibrated";
                            };
                        };
                        if (entry_tag_number == 0x9209) // FLASH
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0x0: return "No Flash";
                                case 0x1: return "Fired";
                                case 0x5: return "Fired, Return not detected";
                                case 0x7: return "Fired, Return detected";
                                case 0x8: return "On, Did not fire";
                                case 0x9: return "On, Fired";
                                case 0xd: return "On, Return not detected";
                                case 0xf: return "On, Return detected";
                                case 0x10: return "Off, Did not fire";
                                case 0x14: return "Off, Did not fire, Return not detected";
                                case 0x18: return "Auto, Did not fire";
                                case 0x19: return "Auto, Fired";
                                case 0x1d: return "Auto, Fired, Return not detected";
                                case 0x1f: return "Auto, Fired, Return detected";
                                case 0x20: return "No flash function";
                                case 0x30: return "Off, No flash function";
                                case 0x41: return "Fired, Red-eye reduction";
                                case 0x45: return "Fired, Red-eye reduction, Return not detected";
                                case 0x47: return "Fired, Red-eye reduction, Return detected";
                                case 0x49: return "On, Red-eye reduction";
                                case 0x4d: return "On, Red-eye reduction, Return not detected";
                                case 0x4f: return "On, Red-eye reduction, Return detected";
                                case 0x50: return "Off, Red-eye reduction";
                                case 0x58: return "Auto, Did not fire, Red-eye reduction";
                                case 0x59: return "Auto, Fired, Red-eye reduction";
                                case 0x5d: return "Auto, Fired, Red-eye reduction, Return not detected";
                                case 0x5f: return "Auto, Fired, Red-eye reduction, Return detected";
                            };
                        };
                        if (entry_tag_number == 0x9208) //   LightSource
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Unknown";
                                case 1: return "Daylight";
                                case 2: return "Fluorescent";
                                case 3: return "Tungsten (Incandescent)";
                                case 4: return "Flash";
                                case 9: return "Fine Weather";
                                case 10: return "Cloudy";
                                case 11: return "Shade";
                                case 12: return "Daylight Fluorescent";
                                case 13: return "Day White Fluorescent";
                                case 14: return "Cool White Fluorescent";
                                case 15: return "White Fluorescent";
                                case 16: return "Warm White Fluorescent";
                                case 17: return "Standard Light A";
                                case 18: return "Standard Light B";
                                case 19: return "Standard Light C";
                                case 20: return "D55";
                                case 21: return "D65";
                                case 22: return "D75";
                                case 23: return "D50";
                                case 24: return "ISO Studio Tungsten";
                                case 255: return "Other";
                            };
                        };
                        if (entry_tag_number == 0x9207) //   MeteringMode
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Unknown";
                                case 1: return "Average";
                                case 2: return "Center-weighted average";
                                case 3: return "Spot";
                                case 4: return "Multi-spot";
                                case 5: return "Multi-segment";
                                case 6: return "Partial";
                                case 255: return "Other";
                            };
                        };
                        if (entry_tag_number == 0x9000) //   ExifVersion
                        {
                            byte[] val = new byte[0];
                            try { 
                                val = (byte[])entry_value;
                                return System.Text.Encoding.ASCII.GetString(val);
                            }
                            catch {  };                            
                        };
                        if (entry_tag_number == 0x8830) //   SensitivityType
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Unknown";
                                case 1: return "Standard Output Sensitivity";
                                case 2: return "Recommended Exposure Index";
                                case 3: return "ISO Speed";
                                case 4: return "Standard Output Sensitivity and Recommended Exposure Index";
                                case 5: return "Standard Output Sensitivity and ISO Speed";
                                case 6: return "Recommended Exposure Index and ISO Speed";
                                case 7: return "Standard Output Sensitivity, Recommended Exposure Index and ISO Speed";
                            };
                        };
                        if (entry_tag_number == 0x8822) //   ExposureProgram
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "Not Defined";
                                case 1: return "Manual";
                                case 2: return "Program AE";
                                case 3: return "Aperture-priority AE";
                                case 4: return "Shutter speed priority AE";
                                case 5: return "Creative (Slow speed)";
                                case 6: return "Action (High speed)";
                                case 7: return "Portrait";
                                case 8: return "Landscape";
                                case 9: return "Bulb";
                            };
                        };
                        if (entry_tag_number == 0x013D) //   Predictor
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 1: return "None";
                                case 2: return "Horizontal differencing";
                            };
                        };
                        if (entry_tag_number == 0x0128) //   ResolutionUnit
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 1: return "None ";
                                case 2: return "inches ";
                                case 3: return "cm";
                            };
                        };
                        if (entry_tag_number == 0x0122) //   GrayResponseUnit
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 1: return "0.1";
                                case 2: return "0.001";
                                case 3: return "0.0001";
                                case 4: return "1e-05";
                                case 5: return "1e-06";
                            };
                        };
                        if (entry_tag_number == 0x011C) //   PlanarConfiguration
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 1: return "Chunky";
                                case 2: return "Planar";
                            };
                        };
                        if (entry_tag_number == 0x0112) //   Orientation
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 1: return "Horizontal (normal)";
                                case 2: return "Mirror horizontal";
                                case 3: return "Rotate 180";
                                case 4: return "Mirror vertical";
                                case 5: return "Mirror horizontal and rotate 270 CW";
                                case 6: return "Rotate 90 CW";
                                case 7: return "Mirror horizontal and rotate 90 CW";
                                case 8: return "Rotate 270 CW";
                            };
                        };
                        if (entry_tag_number == 0x010A) //   FillOrder
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 1: return "Normal";
                                case 2: return "Reversed";
                            };
                        };
                        if (entry_tag_number == 0x0107) //   Thresholding
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 1: return "No dithering or halftoning";
                                case 2: return "Ordered dither or halftone";
                                case 3: return "Randomized dither";
                            };
                        };
                        if (entry_tag_number == 0x0106) //   PhotometricInterpretation
                        {
                            ushort val = 0;
                            try { val = (ushort)entry_value; }
                            catch { return entry_value.ToString().Trim('\0').Trim(); };
                            switch (val)
                            {
                                case 0: return "WhiteIsZero";
                                case 1: return "BlackIsZero";
                                case 2: return "RGB";
                                case 3: return "RGB Palette";
                                case 4: return "Transparency Mask";
                                case 5: return "CMYK";
                                case 6: return "YCbCr";
                                case 8: return "CIELab";
                                case 9: return "ICCLab";
                                case 10: return "ITULab";
                                case 32803: return "Color Filter Array";
                                case 32844: return "Pixar LogL";
                                case 32845: return "Pixar LogLuv";
                                case 32892: return "Sequential Color Filter";
                                case 34892: return "Linear Raw";
                            };
                        };                        
                        return entry_value.ToString().Trim('\0').Trim();
                    }
                }
			
			public string entry_tag_name
                {
                    get
                    {
                        if (entry_IFD_number == 0x8825) // GPSInfo
                            return TagNameForGPS(entry_tag_number);
                        if (entry_IFD_number == 0x927C) // Nikon IFD
                            return TagNameForNikon(entry_tag_number);
                        return TagNameByNumber(entry_tag_number);
                    }
                }

                // https://exiv2.org/tags-nikon.html
                public static string TagNameForNikon(int number)
                {
                    switch (number)
                    {
                        case 0x0001: return "Nikon.Version";
                        case 0x0002: return "Nikon.ISOSpeed";
                        case 0x0003: return "Nikon.ColorMode";
                        case 0x0004: return "Nikon.Quality";
                        case 0x0005: return "Nikon.WhiteBalance";
                        case 0x0006: return "Nikon.Sharpening";
                        case 0x0007: return "Nikon.Focus";
                        case 0x0008: return "Nikon.FlashSetting";
                        case 0x0009: return "Nikon.FlashDevice";
                        case 0x000a: return "Nikon.0x000a";
                        case 0x000b: return "Nikon.WhiteBalanceBias";
                        case 0x000c: return "Nikon.WB_RBLevels";
                        case 0x000d: return "Nikon.ProgramShift";
                        case 0x000e: return "Nikon.ExposureDiff";
                        case 0x000f: return "Nikon.ISOSelection";
                        case 0x0010: return "Nikon.DataDump";
                        case 0x0011: return "Nikon.Preview";
                        case 0x0012: return "Nikon.FlashComp";
                        case 0x0013: return "Nikon.ISOSettings";
                        case 0x0016: return "Nikon.ImageBoundary";
                        case 0x0017: return "Nikon.FlashExposureComp";
                        case 0x0018: return "Nikon.FlashBracketComp";
                        case 0x0019: return "Nikon.ExposureBracketComp";
                        case 0x001a: return "Nikon.ImageProcessing";
                        case 0x001b: return "Nikon.CropHiSpeed";
                        case 0x001c: return "Nikon.ExposureTuning";
                        case 0x001d: return "Nikon.SerialNumber";
                        case 0x001e: return "Nikon.ColorSpace";
                        case 0x001f: return "Nikon.VRInfo";
                        case 0x0020: return "Nikon.ImageAuthentication";
                        case 0x0022: return "Nikon.ActiveDLighting";
                        case 0x0023: return "Nikon.PictureControl";
                        case 0x0024: return "Nikon.WorldTime";
                        case 0x0025: return "Nikon.ISOInfo";
                        case 0x002a: return "Nikon.VignetteControl";
                        case 0x0080: return "Nikon.ImageAdjustment";
                        case 0x0081: return "Nikon.ToneComp";
                        case 0x0082: return "Nikon.AuxiliaryLens";
                        case 0x0083: return "Nikon.LensType";
                        case 0x0084: return "Nikon.Lens";
                        case 0x0085: return "Nikon.FocusDistance";
                        case 0x0086: return "Nikon.DigitalZoom";
                        case 0x0087: return "Nikon.FlashMode";
                        case 0x0088: return "Nikon.AFInfo";
                        case 0x0089: return "Nikon.ShootingMode";
                        case 0x008a: return "Nikon.AutoBracketRelease";
                        case 0x008b: return "Nikon.LensFStops";
                        case 0x008c: return "Nikon.ContrastCurve";
                        case 0x008d: return "Nikon.ColorHue";
                        case 0x008f: return "Nikon.SceneMode";
                        case 0x0090: return "Nikon.LightSource";
                        case 0x0091: return "Nikon.ShotInfo";
                        case 0x0092: return "Nikon.HueAdjustment";
                        case 0x0093: return "Nikon.NEFCompression";
                        case 0x0094: return "Nikon.Saturation";
                        case 0x0095: return "Nikon.NoiseReduction";
                        case 0x0096: return "Nikon.LinearizationTable";
                        case 0x0097: return "Nikon.ColorBalance";
                        case 0x0098: return "Nikon.LensData";
                        case 0x0099: return "Nikon.RawImageCenter";
                        case 0x009a: return "Nikon.SensorPixelSize";
                        case 0x009b: return "Nikon.0x009b";
                        case 0x009c: return "Nikon.SceneAssist";
                        case 0x009e: return "Nikon.RetouchHistory";
                        case 0x009f: return "Nikon.0x009f";
                        case 0x00a0: return "Nikon.SerialNO";
                        case 0x00a2: return "Nikon.ImageDataSize";
                        case 0x00a3: return "Nikon.0x00a3";
                        case 0x00a5: return "Nikon.ImageCount";
                        case 0x00a6: return "Nikon.DeletedImageCount";
                        case 0x00a7: return "Nikon.ShutterCount";
                        case 0x00a8: return "Nikon.FlashInfo";
                        case 0x00a9: return "Nikon.ImageOptimization";
                        case 0x00aa: return "Nikon.Saturation";
                        case 0x00ab: return "Nikon.VariProgram";
                        case 0x00ac: return "Nikon.ImageStabilization";
                        case 0x00ad: return "Nikon.AFResponse";
                        case 0x00b0: return "Nikon.MultiExposure";
                        case 0x00b1: return "Nikon.HighISONoiseReduction";
                        case 0x00b3: return "Nikon.ToningEffect";
                        case 0x00b7: return "Nikon.AFInfo2";
                        case 0x00b8: return "Nikon.FileInfo";
                        case 0x00b9: return "Nikon.AFTune";
                        case 0x00c3: return "Nikon.BarometerInfo";
                        case 0x0e00: return "Nikon.PrintIM";
                        case 0x0e01: return "Nikon.CaptureData";
                        case 0x0e09: return "Nikon.CaptureVersion";
                        case 0x0e0e: return "Nikon.CaptureOffsets";
                        case 0x0e10: return "Nikon.ScanIFD";
                        case 0x0e1d: return "Nikon.ICCProfile";
                        case 0x0e1e: return "Nikon.CaptureOutput";
                    };
                    return "unknown";
                }

                public static string TagNameForGPS(int number)
                {
                    switch (number)
                    {
                        /* GPS */
                        case 0x0000: return "GPSVersionID";
                        case 0x0001: return "GPSLatitudeRef";
                        case 0x0002: return "GPSLatitude";
                        case 0x0003: return "GPSLongitudeRef";
                        case 0x0004: return "GPSLongitude";
                        case 0x0005: return "GPSAltitudeRef";
                        case 0x0006: return "GPSAltitude";
                        case 0x0007: return "GPSTimeStamp";
                        case 0x0008: return "GPSSatellites";
                        case 0x0009: return "GPSStatus";
                        case 0x000A: return "GPSMeasureMode";
                        case 0x000B: return "GPSDOP";
                        case 0x000C: return "GPSSpeedRef";
                        case 0x000D: return "GPSSpeed";
                        case 0x000E: return "GPSTrackRef";
                        case 0x000F: return "GPSTrack";
                        case 0x0010: return "GPSImgDirectionRef";
                        case 0x0011: return "GPSImgDirection";
                        case 0x0012: return "GPSMapDatum";
                        case 0x0013: return "GPSDestLatitudeRef";
                        case 0x0014: return "GPSDestLatitude";
                        case 0x0015: return "GPSDestLongitudeRef";
                        case 0x0016: return "GPSDestLongitude";
                        case 0x0017: return "GPSDestBearingRef";
                        case 0x0018: return "GPSDestBearing";
                        case 0x0019: return "GPSDestDistanceRef";
                        case 0x001A: return "GPSDestDistance";
                        case 0x001B: return "GPSProcessingMethod";
                        case 0x001C: return "GPSAreaInformation";
                        case 0x001D: return "GPSDateStamp";
                        case 0x001E: return "GPSDifferential"; 
                    };
                    return "unknown";
                }

                public static ushort TagNumberByName(string name)
                {
                    string ntl = name.ToLower();
                    for(int i=1;i<=0xFFFF;i++)
                        if(ntl == TagNameByNumber(i).ToLower())
                            return (ushort)i;
                    return 0;
                }

			public static string TagNameByNumber(int number)
                {
                    switch (number)
                    {
                        case 0x0001: return "InteropIndex";
                        case 0x0002: return "InteropVersion";
                        case 0x000b: return "ProcessingSoftware";
                        case 0x00FE: return "SubfileType";
                        case 0x00FF: return "OldSubfileType";
                        case 0x0100: return "ImageWidth";
                        case 0x0101: return "ImageHeight";
                        case 0x0102: return "BitsPerSample";
                        case 0x0103: return "Compression";
                        case 0x0106: return "PhotometricInterpretation";
                        case 0x0107: return "Thresholding";
                        case 0x0108: return "CellWidth";
                        case 0x0109: return "CellLength";
                        case 0x010A: return "FillOrder";
                        case 0x010D: return "DocumentName";
                        case 0x010E: return "ImageDescription";
                        case 0x010F: return "Make";
                        case 0x0110: return "Model";
                        case 0x0111: return "StripOffsets";
                        case 0x0112: return "Orientation";
                        case 0x0115: return "SamplesPerPixel";
                        case 0x0116: return "RowsPerStrip";
                        case 0x0117: return "StripByteCounts";
                        case 0x0118: return "MinSampleValue";
                        case 0x0119: return "MaxSampleValue";
                        case 0x011A: return "XResolution";
                        case 0x011B: return "YResolution";
                        case 0x011C: return "PlanarConfiguration";
                        case 0x011D: return "PageName";
                        case 0x011E: return "XPosition";
                        case 0x011F: return "YPosition";
                        case 0x0120: return "FreeOffsets";
                        case 0x0121: return "FreeByteCounts";
                        case 0x0122: return "GrayResponseUnit";
                        case 0x0123: return "GrayResponseCurve";
                        case 0x0124: return "T4Options";
                        case 0x0125: return "T6Options";
                        case 0x0128: return "ResolutionUnit";
                        case 0x0129: return "PageNumber";
                        case 0x012C: return "ColorResponseUnit";
                        case 0x012D: return "TransferFunction";
                        case 0x0131: return "Software";
                        case 0x0132: return "DateTime";
                        case 0x013B: return "Artist";
                        case 0x013C: return "HostComputer";
                        case 0x013D: return "Predictor";
                        case 0x013E: return "WhitePoint";
                        case 0x013F: return "PrimaryChromaticities";
                        case 0x0140: return "ColorMap";
                        case 0x0141: return "HalftoneHints";
                        case 0x0142: return "TileWidth";
                        case 0x0143: return "TileLength";
                        case 0x0144: return "TileOffsets";
                        case 0x0145: return "TileByteCounts";
                        case 0x0146: return "BadFaxLines";
                        case 0x0147: return "CleanFaxData";
                        case 0x0148: return "ConsecutiveBadFaxLines";
                        case 0x014A: return "SubIFD_A100DataOffset";
                        case 0x014C: return "InkSet";
                        case 0x014D: return "InkNames";
                        case 0x014E: return "NumberofInks";
                        case 0x0150: return "DotRange";
                        case 0x0151: return "TargetPrinter";
                        case 0x0152: return "ExtraSamples";
                        case 0x0153: return "SampleFormat";
                        case 0x0154: return "SMinSampleValue";
                        case 0x0155: return "SMaxSampleValue";
                        case 0x0156: return "TransferRange";
                        case 0x0157: return "ClipPath";
                        case 0x0158: return "XClipPathUnits";
                        case 0x0159: return "YClipPathUnits";
                        case 0x015A: return "Indexed";
                        case 0x015B: return "JPEGTables";
                        case 0x015F: return "OPIProxy";
                        case 0x0190: return "GlobalParametersIFD";
                        case 0x0191: return "ProfileType";
                        case 0x0192: return "FaxProfile";
                        case 0x0193: return "CodingMethods";
                        case 0x0194: return "VersionYear";
                        case 0x0195: return "ModeNumber";
                        case 0x01B1: return "Decode";
                        case 0x01B2: return "DefaultImageColor";
                        case 0x01B3: return "T82Options";
                        case 0x01B5: return "JPEGTables";
                        case 0x0200: return "JPEGProc";
                        case 0x0201: return "JPEGInterchangeFormat";
                        case 0x0202: return "JPEGInterchangeFormatLength";
                        case 0x0203: return "JPEGRestartInterval";
                        case 0x0205: return "JPEGLosslessPredictors";
                        case 0x0206: return "JPEGPointTransforms";
                        case 0x0207: return "JPEGQTables";
                        case 0x0208: return "JPEGDCTables";
                        case 0x0209: return "JPEGACTables";
                        case 0x0211: return "YCbCrCoefficients";
                        case 0x0212: return "YCbCrSubSampling";
                        case 0x0213: return "YCbCrPositioning";
                        case 0x0214: return "ReferenceBlackWhite";
                        case 0x022F: return "StripRowCounts";
                        case 0x02BC: return "ApplicationNotes";
                        case 0x03E7: return "USPTOMiscellaneous";
                        case 0x1000: return "RelatedImageFileFormat";
                        case 0x1001: return "RelatedImageWidth";
                        case 0x1002: return "RelatedImageHeight";
                        case 0x4746: return "Rating";
                        case 0x4747: return "XP_DIP_XML";
                        case 0x4748: return "StitchInfo";
                        case 0x4749: return "RatingPercent";
                        case 0x7000: return "SonyRawFileType";
                        case 0x7010: return "SonyToneCurve";
                        case 0x7031: return "VignettingCorrection";
                        case 0x7032: return "VignettingCorrParams";
                        case 0x7034: return "ChromaticAberrationCorrection";
                        case 0x7035: return "ChromaticAberrationCorrParams";
                        case 0x7036: return "DistortionCorrection";
                        case 0x7037: return "DistortionCorrParams";
                        case 0x74C7: return "SonyCropTopLeft";
                        case 0x74C8: return "SonyCropSize";
                        case 0x800D: return "ImageID";
                        case 0x80A3: return "WangTag1";
                        case 0x80A4: return "WangAnnotation";
                        case 0x80A5: return "WangTag3";
                        case 0x80A6: return "WangTag4"; //
						case 0x80b9: return "ImageReferencePoints";
						case 0x80ba: return "RegionXformTackPoint";
						case 0x80bb: return "WarpQuadrilateral";
						case 0x80bc: return "AffineTransformMat";
						case 0x80e3: return "Matteing";
						case 0x80e4: return "DataType";
						case 0x80e5: return "ImageDepth";
						case 0x80e6: return "TileDepth";
						case 0x8214: return "ImageFullWidth";
						case 0x8215: return "ImageFullHeight";
						case 0x8216: return "TextureFormat";
						case 0x8217: return "WrapModes";
						case 0x8218: return "FovCot";
						case 0x8219: return "MatrixWorldToScreen";
						case 0x821a: return "MatrixWorldToCamera";
						case 0x827d: return "Model2";
						case 0x828d: return "CFARepeatPatternDim";
						case 0x828e: return "CFAPattern2";
						case 0x828f: return "BatteryLevel";
						case 0x8290: return "KodakIFD";
                        case 0x8298: return "Copyright";
                        case 0x829A: return "ExposureTime";
                        case 0x829D: return "FNumber";
						case 0x82a5: return "MDFileTag";
						case 0x82a6: return "MDScalePixel";
						case 0x82a7: return "MDColorTable";
						case 0x82a8: return "MDLabName";
						case 0x82a9: return "MDSampleInfo";
						case 0x82aa: return "MDPrepDate";
						case 0x82ab: return "MDPrepTime";
						case 0x82ac: return "MDFileUnits";
						case 0x830e: return "PixelScale";
						case 0x8335: return "AdventScale";
						case 0x8336: return "AdventRevision";
						case 0x835c: return "UIC1Tag";
						case 0x835d: return "UIC2Tag";
						case 0x835e: return "UIC3Tag";
						case 0x835f: return "UIC4Tag";
						case 0x83bb: return "IPTC-NAA";
						case 0x847e: return "IntergraphPacketData";
						case 0x847f: return "IntergraphFlagRegisters";
						case 0x8480: return "IntergraphMatrix";
						case 0x8481: return "INGRReserved";
						case 0x8482: return "ModelTiePoint";
						case 0x84e0: return "Site";
						case 0x84e1: return "ColorSequence";
						case 0x84e2: return "IT8Header";
						case 0x84e3: return "RasterPadding";
						case 0x84e4: return "BitsPerRunLength";
						case 0x84e5: return "BitsPerExtendedRunLength";
						case 0x84e6: return "ColorTable";
						case 0x84e7: return "ImageColorIndicator";
						case 0x84e8: return "BackgroundColorIndicator";
						case 0x84e9: return "ImageColorValue";
						case 0x84ea: return "BackgroundColorValue";
						case 0x84eb: return "PixelIntensityRange";
						case 0x84ec: return "TransparencyIndicator";
						case 0x84ed: return "ColorCharacterization";
						case 0x84ee: return "HCUsage";
						case 0x84ef: return "TrapIndicator";
						case 0x84f0: return "CMYKEquivalent";
						case 0x8546: return "SEMInfo";
						case 0x8568: return "AFCP_IPTC";
						case 0x85b8: return "PixelMagicJBIGOptions";
						case 0x85d7: return "JPLCartoIFD";
						case 0x85d8: return "ModelTransform";
						case 0x8602: return "WB_GRGBLevels";
						case 0x8606: return "LeafData";
						case 0x8649: return "PhotoshopSettings";
						case 0x8769: return "ExifOffset";
						case 0x8773: return "ICC_Profile";
						case 0x877f: return "TIFF_FXExtensions";
						case 0x8780: return "MultiProfiles";
						case 0x8781: return "SharedData";
						case 0x8782: return "T88Options";
						case 0x87ac: return "ImageLayer";
						case 0x87af: return "GeoTiffDirectory";
						case 0x87b0: return "GeoTiffDoubleParams";
						case 0x87b1: return "GeoTiffAsciiParams";
						case 0x87be: return "JBIGOptions";
                        case 0x8822: return "ExposureProgram";
                        case 0x8824: return "SpectralSensitivity";
                        case 0x8825: return "GPSInfo";
                        case 0x8827: return "ISOSpeedRatings";
                        case 0x8828: return "OECF";
                        case 0x8829: return "Interlace";
                        case 0x882A: return "TimeZoneOffset";
                        case 0x882B: return "SelfTimerMode";
                        case 0x8830: return "SensitivityType";
                        case 0x8831: return "StandardOutputSensitivity";
                        case 0x8832: return "RecommendedExposureIndex";
                        case 0x8833: return "ISOSpeed";
                        case 0x8834: return "ISOSpeedLatitudeyyy";
                        case 0x8835: return "ISOSpeedLatitudezzz";
                        case 0x885C: return "FaxRecvParams";
                        case 0x885D: return "FaxSubAddress";
                        case 0x885E: return "FaxRecvTime";
                        case 0x8871: return "FedexEDR";
                        case 0x9000: return "ExifVersion";
                        case 0x9003: return "DateTimeOriginal";
                        case 0x9004: return "DateTimeDigitized";
                        case 0x9009: return "GooglePlusUploadCode";
                        case 0x9010: return "OffsetTime";
                        case 0x9011: return "OffsetTimeOriginal";
                        case 0x9012: return "OffsetTimeDigitized";
                        case 0x9101: return "ComponentsConfiguration";
                        case 0x9102: return "CompressedBitsPerPixel";
                        case 0x9201: return "ShutterSpeedValue";
                        case 0x9202: return "ApertureValue";
                        case 0x9203: return "BrightnessValue";
                        case 0x9204: return "ExposureBiasValue";
                        case 0x9205: return "MaxApertureValue";
                        case 0x9206: return "SubjectDistance";
                        case 0x9207: return "MeteringMode";
                        case 0x9208: return "LightSource";
                        case 0x9209: return "Flash";
                        case 0x920A: return "FocalLength";
                        case 0x920B: return "FlashEnergy";
                        case 0x920C: return "SpatialFrequencyResponse";
                        case 0x920D: return "Noise";
                        case 0x920E: return "FocalPlaneXResolution";
                        case 0x920F: return "FocalPlaneYResolution";
                        case 0x9210: return "FocalPlaneResolutionUnit";
                        case 0x9211: return "ImageNumber";
                        case 0x9212: return "SecurityClassification";
                        case 0x9213: return "ImageHistory";
                        case 0x9214: return "SubjectArea";
                        case 0x9215: return "ExposureIndex";
                        case 0x9216: return "TIFF-EPStandardID";
                        case 0x9217: return "SensingMethod";
                        case 0x923A: return "CIP3DataFile";
                        case 0x923B: return "CIP3Sheet";
                        case 0x923C: return "CIP3Side";
                        case 0x923F: return "StoNits";
                        case 0x927C: return "MakerNote";
                        case 0x9286: return "UserComment";
                        case 0x9290: return "SubSecTime";
                        case 0x9291: return "SubSecTimeOriginal";
                        case 0x9292: return "SubSecTimeDigitized";
                        case 0x932F: return "MSDocumentText";
                        case 0x9330: return "MSPropertySetStorage";
                        case 0x9331: return "MSDocumentTextPosition";
                        case 0x935C: return "ImageSourceData";
                        case 0x9400: return "AmbientTemperature";
                        case 0x9401: return "Humidity";
                        case 0x9402: return "Pressure";
                        case 0x9403: return "WaterDepth";
                        case 0x9404: return "Acceleration";
                        case 0x9405: return "CameraElevationAngle";
                        case 0x9C9B: return "XPTitle";
                        case 0x9C9C: return "XPComment";
                        case 0x9C9D: return "XPAuthor";
                        case 0x9C9E: return "XPKeywords";
                        case 0x9C9F: return "XPSubject";
                        case 0xA000: return "FlashpixVersion";
                        case 0xA001: return "ColorSpace";
                        case 0xA002: return "PixelXDimension";
                        case 0xA003: return "PixelYDimension";
                        case 0xA004: return "RelatedSoundFile";
                        case 0xA005: return "InteropOffset";
                        case 0xA20B: return "FlashEnergy";
                        case 0xA20C: return "SpatialFrequencyResponse";
                        case 0xA20E: return "FocalPlaneXResolution";
                        case 0xA20F: return "FocalPlaneYResolution";
                        case 0xA210: return "FocalPlaneResolutionUnit";
                        case 0xA211: return "ImageNumber";
                        case 0xA212: return "SecurityClassification";
                        case 0xA213: return "ImageHistory";
                        case 0xA214: return "SubjectLocation";
                        case 0xA215: return "ExposureIndex";
                        case 0xA217: return "SensingMethod";
                        case 0xA300: return "FileSource";
                        case 0xA301: return "SceneType";
                        case 0xA302: return "CFAPattern";
                        case 0xA401: return "CustomRendered";
                        case 0xA402: return "ExposureMode";
                        case 0xA403: return "WhiteBalance";
                        case 0xA404: return "DigitalZoomRatio";
                        case 0xA405: return "FocalLengthIn35mmFilm";
                        case 0xA406: return "SceneCaptureType";
                        case 0xA407: return "GainControl";
                        case 0xA408: return "Contrast";
                        case 0xA409: return "Saturation";
                        case 0xA40A: return "Sharpness";
                        case 0xA40B: return "DeviceSettingDescription";
                        case 0xA40C: return "SubjectDistanceRange";
                        case 0xA420: return "ImageUniqueID";
						case 0xa430: return "OwnerName";
						case 0xa431: return "SerialNumber";
						case 0xa432: return "LensInfo";
						case 0xa433: return "LensMake";
						case 0xa434: return "LensModel";
						case 0xa435: return "LensSerialNumber";
						case 0xa460: return "CompositeImage";
						case 0xa461: return "CompositeImageCount";
						case 0xa462: return "CompositeImageExposureTimes";
						case 0xa480: return "GDALMetadata";
						case 0xa481: return "GDALNoData";
						case 0xa500: return "Gamma";
						case 0xafc0: return "ExpandSoftware";
						case 0xafc1: return "ExpandLens";
						case 0xafc2: return "ExpandFilm";
						case 0xafc3: return "ExpandFilterLens";
						case 0xafc4: return "ExpandScanner";
						case 0xafc5: return "ExpandFlashLamp";
						case 0xbc01: return "PixelFormat";
						case 0xbc02: return "Transformation";
						case 0xbc03: return "Uncompressed";
						case 0xbc04: return "ImageType";
						case 0xbc80: return "ImageWidth";
						case 0xbc81: return "ImageHeight";
						case 0xbc82: return "WidthResolution";
						case 0xbc83: return "HeightResolution";
						case 0xbcc0: return "ImageOffset";
						case 0xbcc1: return "ImageByteCount";
						case 0xbcc2: return "AlphaOffset";
						case 0xbcc3: return "AlphaByteCount";
						case 0xbcc4: return "ImageDataDiscard";
						case 0xbcc5: return "AlphaDataDiscard";
						case 0xc427: return "OceScanjobDesc";
						case 0xc428: return "OceApplicationSelector";
						case 0xc429: return "OceIDNumber";
						case 0xc42a: return "OceImageLogic";
						case 0xc44f: return "Annotations";
						case 0xc4a5: return "PrintIM";
						case 0xc573: return "OriginalFileName";
						case 0xc580: return "USPTOOriginalContentType";
						case 0xc5e0: return "CR2CFAPattern";
						case 0xc612: return "DNGVersion";
						case 0xc613: return "DNGBackwardVersion";
						case 0xc614: return "UniqueCameraModel";
						case 0xc615: return "LocalizedCameraModel";
						case 0xc616: return "CFAPlaneColor";
						case 0xc617: return "CFALayout";
						case 0xc618: return "LinearizationTable";
						case 0xc619: return "BlackLevelRepeatDim";
						case 0xc61a: return "BlackLevel";
						case 0xc61b: return "BlackLevelDeltaH";
						case 0xc61c: return "BlackLevelDeltaV";
						case 0xc61d: return "WhiteLevel";
						case 0xc61e: return "DefaultScale";
						case 0xc61f: return "DefaultCropOrigin";
						case 0xc620: return "DefaultCropSize";
						case 0xc621: return "ColorMatrix1";
						case 0xc622: return "ColorMatrix2";
						case 0xc623: return "CameraCalibration1";
						case 0xc624: return "CameraCalibration2";
						case 0xc625: return "ReductionMatrix1";
						case 0xc626: return "ReductionMatrix2";
						case 0xc627: return "AnalogBalance";
						case 0xc628: return "AsShotNeutral";
						case 0xc629: return "AsShotWhiteXY";
						case 0xc62a: return "BaselineExposure";
						case 0xc62b: return "BaselineNoise";
						case 0xc62c: return "BaselineSharpness";
						case 0xc62d: return "BayerGreenSplit";
						case 0xc62e: return "LinearResponseLimit";
						case 0xc62f: return "CameraSerialNumber";
						case 0xc630: return "DNGLensInfo";
						case 0xc631: return "ChromaBlurRadius";
						case 0xc632: return "AntiAliasStrength";
						case 0xc633: return "ShadowScale";
						case 0xc634: return "SR2Private";
						case 0xc635: return "MakerNoteSafety";
						case 0xc640: return "RawImageSegmentation";
						case 0xc65a: return "CalibrationIlluminant1";
						case 0xc65b: return "CalibrationIlluminant2";
						case 0xc65c: return "BestQualityScale";
						case 0xc65d: return "RawDataUniqueID";
						case 0xc660: return "AliasLayerMetadata";
						case 0xc68b: return "OriginalRawFileName";
						case 0xc68c: return "OriginalRawFileData";
						case 0xc68d: return "ActiveArea";
						case 0xc68e: return "MaskedAreas";
						case 0xc68f: return "AsShotICCProfile";
						case 0xc690: return "AsShotPreProfileMatrix";
						case 0xc691: return "CurrentICCProfile";
						case 0xc692: return "CurrentPreProfileMatrix";
						case 0xc6bf: return "ColorimetricReference";
						case 0xc6c5: return "SRawType";
						case 0xc6d2: return "PanasonicTitle";
						case 0xc6d3: return "PanasonicTitle2";
						case 0xc6f3: return "CameraCalibrationSig";
						case 0xc6f4: return "ProfileCalibrationSig";
						case 0xc6f5: return "ProfileIFD";
						case 0xc6f6: return "AsShotProfileName";
						case 0xc6f7: return "NoiseReductionApplied";
						case 0xc6f8: return "ProfileName";
						case 0xc6f9: return "ProfileHueSatMapDims";
						case 0xc6fa: return "ProfileHueSatMapData1";
						case 0xc6fb: return "ProfileHueSatMapData2";
						case 0xc6fc: return "ProfileToneCurve";
						case 0xc6fd: return "ProfileEmbedPolicy";
						case 0xc6fe: return "ProfileCopyright";
						case 0xc714: return "ForwardMatrix1";
						case 0xc715: return "ForwardMatrix2";
						case 0xc716: return "PreviewApplicationName";
						case 0xc717: return "PreviewApplicationVersion";
						case 0xc718: return "PreviewSettingsName";
						case 0xc719: return "PreviewSettingsDigest";
						case 0xc71a: return "PreviewColorSpace";
						case 0xc71b: return "PreviewDateTime";
						case 0xc71c: return "RawImageDigest";
						case 0xc71d: return "OriginalRawFileDigest";
						case 0xc71e: return "SubTileBlockSize";
						case 0xc71f: return "RowInterleaveFactor";
						case 0xc725: return "ProfileLookTableDims";
						case 0xc726: return "ProfileLookTableData";
						case 0xc740: return "OpcodeList1";
						case 0xc741: return "OpcodeList2";
						case 0xc74e: return "OpcodeList3";
						case 0xc761: return "NoiseProfile";
						case 0xc763: return "TimeCodes";
						case 0xc764: return "FrameRate";
						case 0xc772: return "TStop";
						case 0xc789: return "ReelName";
						case 0xc791: return "OriginalDefaultFinalSize";
						case 0xc792: return "OriginalBestQualitySize";
						case 0xc793: return "OriginalDefaultCropSize";
						case 0xc7a1: return "CameraLabel";
						case 0xc7a3: return "ProfileHueSatMapEncoding";
						case 0xc7a4: return "ProfileLookTableEncoding";
						case 0xc7a5: return "BaselineExposureOffset";
						case 0xc7a6: return "DefaultBlackRender";
						case 0xc7a7: return "NewRawImageDigest";
						case 0xc7a8: return "RawToPreviewGain";
						case 0xc7aa: return "CacheVersion";
						case 0xc7b5: return "DefaultUserCrop";
						case 0xc7d5: return "NikonNEFInfo";
						case 0xea1c: return "Padding";
						case 0xea1d: return "OffsetSchema";
						case 0xfde8: return "OwnerName";
						case 0xfde9: return "SerialNumber";
						case 0xfdea: return "Lens";
						case 0xfe00: return "KDC_IFD";
						case 0xfe4c: return "RawFile";
						case 0xfe4d: return "Converter";
						case 0xfe4e: return "WhiteBalance";
						case 0xfe51: return "Exposure";
						case 0xfe52: return "Shadows";
						case 0xfe53: return "Brightness";
						case 0xfe54: return "Contrast";
						case 0xfe55: return "Saturation";
						case 0xfe56: return "Sharpness";
						case 0xfe57: return "Smoothness";
						case 0xfe58: return "MoireFilter";
                    };
                    return "unknown";
                }
			
            public IFD_Entry(int IFDNo, MyBitConverter bc, byte[] exif_data, int curr_offset)
            {
                _offset = curr_offset;
                entry_IFD_number = IFDNo;

                // Entry size is 12 bytes
                entry_tag_number = bc.ToUInt16(exif_data, curr_offset + 0); // 2 bytes
                entry_data_format = bc.ToUInt16(exif_data, curr_offset + 2); // 2 bytes
                data_number_of_components = bc.ToUInt32(exif_data, curr_offset + 4); // 4 bytes
                data_value_or_offset = bc.ToUInt32(exif_data, curr_offset + 8); // 4 bytes    

                if ((entry_tag_number == 0x8769) || (entry_tag_number == 0x8825) || (entry_tag_number == 0xA005)) // extra data
                {
                    data_length_calculated = 4 * data_number_of_components;
                    entry_value = data_offset_in_exif = (uint)data_value_or_offset;
                    return;
                };

                if ((entry_tag_number == 0x9286) || (entry_tag_number == 0x927C)) // user comment || maker notes
                {
                    data_length_calculated = 1 * data_number_of_components;
                    data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                    byte[] user_data = new byte[data_length_calculated];
                    Array.Copy(exif_data, (int)data_offset_in_exif, user_data, 0, (int)data_length_calculated);
                    entry_value = System.Text.Encoding.GetEncoding(1251).GetString(exif_data, (int)data_offset_in_exif, 8);
                    if (entry_value.ToString().ToLower() == "unicode\0")
                        entry_value = System.Text.Encoding.Unicode.GetString(exif_data, (int)data_offset_in_exif + 8, (int)data_length_calculated - 8).Trim('\0');
                    else if (entry_value.ToString().ToLower() == "ascii\0\0\0")
                        entry_value = System.Text.Encoding.GetEncoding(1251).GetString(exif_data, (int)data_offset_in_exif + 8, (int)data_length_calculated - 8).Trim('\0');
                    else
                        entry_value = user_data;
                    return;
                }

                switch (entry_data_format)
                {
                    case 1: entry_format_type = typeof(byte);     // 1 byte
                        data_length_calculated = 1 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        entry_value = exif_data[data_offset_in_exif];
                        break;
                    case 2: entry_format_type = typeof(char);    // 1 byte
                        data_length_calculated = 1 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        entry_value = System.Text.Encoding.GetEncoding(1251).GetString(exif_data, (int)data_offset_in_exif, (int)data_length_calculated);
                        break; // 1 byte
                    case 3: entry_format_type = typeof(ushort);   // 2 bytes
                        data_length_calculated = 2 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        entry_value = bc.ToUInt16(exif_data, (int)data_offset_in_exif);
                        break;
                    case 4: entry_format_type = typeof(uint);   // 4 bytes
                        data_length_calculated = 4 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        entry_value = bc.ToUInt32(exif_data, (int)data_offset_in_exif);
                        break;
                    case 5: entry_format_type = typeof(object);  // unsigned rational 8 bytes
                        data_length_calculated = 8 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        {
                            string ev = "";
                            for (int i = 0; i < data_number_of_components; i++)
                                ev += bc.ToUInt32(exif_data, (int)data_offset_in_exif + i * 8 + 0) + "/" + bc.ToUInt32(exif_data, (int)data_offset_in_exif + i * 8 + 4) + " ";
                            entry_value = ev.Trim();
                        };
                        break;
                    case 6: entry_format_type = typeof(sbyte);   // 1 byte
                        data_length_calculated = 1 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        entry_value = (sbyte)exif_data[data_offset_in_exif];
                        break;
                    case 7: entry_format_type = typeof(object);  // undefined 1 byte
                        data_length_calculated = 1 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        {
                            byte[] data = new byte[data_length_calculated];
                                Array.Copy(exif_data, (int)data_offset_in_exif, data, 0, data_length_calculated);
                                if (data.Length == 1)
                                    entry_value = data[0];
                                else
                                    entry_value = data;
                        };
                        break;
                    case 8: entry_format_type = typeof(short);    // 2 bytes
                        data_length_calculated = 2 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        entry_value = bc.ToInt16(exif_data, (int)data_offset_in_exif);
                        break;
                    case 9: entry_format_type = typeof(int);    // 4 bytes
                        data_length_calculated = 4 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        entry_value = bc.ToInt32(exif_data, (int)data_offset_in_exif);
                        break;
                    case 10: entry_format_type = typeof(object); // signed rational 8 bytes
                        data_length_calculated = 8 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        {
                            string ev = "";
                            for (int i = 0; i < data_number_of_components; i++)
                                ev += bc.ToInt32(exif_data, (int)data_offset_in_exif + i * 8 + 0) + "/" + bc.ToInt32(exif_data, (int)data_offset_in_exif + i * 8 + 4) + " ";
                            entry_value = ev.Trim();
                        };
                        break;
                    case 11: entry_format_type = typeof(float);  // 4 bytes
                        data_length_calculated = 4 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        entry_value = bc.ToSingle(exif_data, (int)data_offset_in_exif);
                        break;
                    case 12: entry_format_type = typeof(double);  // 8 bytes
                        data_length_calculated = 8 * data_number_of_components;
                        data_offset_in_exif = data_length_calculated <= 4 ? (uint)curr_offset + 8 : (uint)data_value_or_offset;
                        entry_value = bc.ToDouble(exif_data, (int)data_offset_in_exif);
                        break;
                };
            }
        }

        public static void EraseExif(string source_file, string dest_file)
        {
            ExifRewrite(source_file, dest_file, null);
        }

        // Supported tags: 
        //      DateTime, DocumentName, ImageDescription, Make, Model, Software, Artist, Copyright, 
        //      MakerNote, UserComment, DateTimeOriginal, DateTimeDigitized, ImageUniqueID, OwnerName  
        /// <summary>
        ///     Supported tags: DateTime, DocumentName, ImageDescription, Make, Model, Software, Artist, Copyright, 
        ///     MakerNote, UserComment, DateTimeOriginal, DateTimeDigitized, ImageUniqueID, OwnerName
        /// </summary>
        /// <param name="source_file"></param>
        /// <param name="dest_file"></param>
        /// <param name="new_exif">can be null or with supported tags</param>
        public static void ExifRewrite(string source_file, string dest_file, Dictionary<string, object> new_exif)
        {
            FileStream fr = new FileStream(source_file, FileMode.Open, FileAccess.Read);
            FileStream fw = new FileStream(dest_file, FileMode.Create, FileAccess.Write);
            byte[] data = new byte[ushort.MaxValue];
            fr.Read(data, 0, 2);
            fw.Write(data, 0, 2);
            if (new_exif != null) // WRITE NEW EXIF
            {
                data = CreateExif(new_exif);
                if ((data != null) && (data.Length != 0))
                {
					if (data.Length > 0xFEFB)
                    {
                        fr.Close();
                        fw.Close();
                        throw new IOException("Source file is too big, use < 40kb");
                    };
                    fw.WriteByte(0xFF); fw.WriteByte(0xE1);
                    MyBitConverter mbc = new MyBitConverter(false);
                    byte[] dl = mbc.GetBytes((ushort)(data.Length + 2));
                    fw.Write(dl, 0, dl.Length);
                    fw.Write(data, 0, data.Length);
                };
            };
            if (true)// WRITE ALL OTHER FILE DATA
            {
                int len = 0;
                bool loop = true;
                while (loop)
                {
                    int rbyte = fr.ReadByte();
                    if (rbyte == -1) break; // EOF                    

                    if (rbyte == 0xFF)
                    {
                        rbyte = fr.ReadByte();
                        if ((rbyte >= 0xE0) && (rbyte <= 0xEF)) // Skip Extended Data // Exif TAG 0xE1
                        {
                            len = (((byte)fr.ReadByte()) << 8) + fr.ReadByte() - 2;  // Data Length 
                            fr.Position += len;
                        }
                        else if (rbyte >= 0xDB) // Copy Image Data
                        {
                            fw.WriteByte(0xFF);
                            fw.WriteByte((byte)rbyte);
                            data = new byte[ushort.MaxValue];
                            while ((len = fr.Read(data, 0, data.Length)) > 0)
                                fw.Write(data, 0, len);
                            loop = false;
                        }
                        else if (rbyte != 0x00) // Copy Additional Data
                        {
                            fw.WriteByte(0xFF);
                            fw.WriteByte((byte)rbyte);
                            byte b0 = (byte)fr.ReadByte();
                            byte b1 = (byte)fr.ReadByte();
                            len = (b0 << 8) + (b1 - 2);  // Data Length 
                            fw.WriteByte(b0); fw.WriteByte(b1);
                            data = new byte[len];
                            len = fr.Read(data, 0, len);
                            fw.Write(data, 0, len);
                        }
                        else // Copy Data
                            fw.WriteByte((byte)rbyte);
                    }
                    else
                        fw.WriteByte((byte)rbyte);
                };
            };
            fw.Close();
            fr.Close();
        }

        private static byte[] CreateExif(Dictionary<string, object> new_exif)
        {
            if (new_exif == null) return null;
            if (new_exif.Count == 0) return null;

            // IFD0: Make, Model, Software, DateTime, DocumentName, ImageDescription, Artist, Copyright      
            // IFDE: MakerNote, UserComment, DateTimeOriginal, DateTimeDigitized, ImageUniqueID, OwnerName
            int ifd0_c = 0; int ifd1_c = 0;
            foreach (string key in new_exif.Keys)
            {
                switch (key)
                {
                    case "Make":
                    case "Model":
                    case "DateTime":
                    case "DocumentName":
                    case "ImageDescription":
                    case "Artist":
                    case "Copyright":
                    case "Software":
                        ifd0_c++;
                        break;
                    case "MakerNote":
                    case "UserComment":
                    case "DateTimeOriginal":
                    case "DateTimeDigitized":
                    case "ImageUniqueID":
                    case "OwnerName":
                        ifd1_c++;
                        break;
                };
            };
            if ((ifd0_c == 0) && (ifd1_c == 0)) return null;

            MyBitConverter bc = new MyBitConverter(true);

            List<byte> data = new List<byte>();
            List<byte> after_data = new List<byte>();

            // Write Header
            data.AddRange(System.Text.Encoding.ASCII.GetBytes("Exif\0\0")); // Exif Header // 6 bytes
            data.AddRange(System.Text.Encoding.ASCII.GetBytes("II")); // TIFF Header // 8 bytes
            data.Add(0x2A); data.Add(0x00);
            data.Add(8); // IFD0 offset
            data.AddRange(System.Text.Encoding.ASCII.GetBytes("\0\0\0")); // 000

            int pos = 8; /* Exif Header Exif\0\0 */ ;
            int number_of_fdi0 = ifd0_c + (ifd1_c > 0 ? 1 : 0);
            int next_entry_pos = pos + 2 + number_of_fdi0 * 12 + 4;
            data.AddRange(bc.GetBytes((ushort)number_of_fdi0)); // number of entries
            foreach (string key in new_exif.Keys)
            {
                if ((key == "Make") || (key == "Model") || (key == "DateTime") || (key == "DocumentName")|| (key == "ImageDescription") || (key == "Artist") || (key == "Copyright") || (key == "Software"))
                {
                    byte[] val = System.Text.Encoding.GetEncoding(1251).GetBytes(new_exif[key].ToString() + "\0");
                    // tag number
                    {
                        if (key == "Make") data.AddRange(bc.GetBytes((ushort)0x010F)); // tag number 
                        if (key == "Model") data.AddRange(bc.GetBytes((ushort)0x0110)); // tag number
                        if (key == "DateTime") data.AddRange(bc.GetBytes((ushort)0x0132)); // tag number
                        if (key == "DocumentName") data.AddRange(bc.GetBytes((ushort)0x010D)); // tag number
                        if (key == "ImageDescription") data.AddRange(bc.GetBytes((ushort)0x010E)); // tag number
                        if (key == "Artist") data.AddRange(bc.GetBytes((ushort)0x013B)); // tag number
                        if (key == "Copyright") data.AddRange(bc.GetBytes((ushort)0x8298)); // tag number
                        if (key == "Software") data.AddRange(bc.GetBytes((ushort)0x0131)); // tag number
                    };
                    data.AddRange(bc.GetBytes((ushort)0x0002)); // format // 2 (ASCII)
                    data.AddRange(bc.GetBytes((uint)val.Length)); // length // 4 (Length)
                    if (val.Length <= 4) // 4
                    {
                        data.AddRange(val); // value
                        if (val.Length < 4)
                            for (int i = val.Length; i < 4; i++) data.Add(0);
                    }
                    else
                    {
                        data.AddRange(bc.GetBytes((uint)next_entry_pos)); // offset // 4
                        next_entry_pos += val.Length;
                        after_data.AddRange(val); // value
                    };
                };
            };
            if (ifd1_c > 0) // LINK TO EX DATA
            {
                data.AddRange(bc.GetBytes((ushort)0x8769)); // extra exif
                data.AddRange(bc.GetBytes((ushort)0x0004)); // format // 2
                data.AddRange(bc.GetBytes((uint)1)); // length // 4
                data.AddRange(bc.GetBytes((uint)next_entry_pos)); // offset // 4
                next_entry_pos += 2 + ifd1_c * 12 + 4;
            };
            data.AddRange(bc.GetBytes((uint)0)); // offset to next entry // 4
            data.AddRange(after_data.ToArray()); // after IFD data
            after_data.Clear();

            if (ifd1_c > 0) // EX DATA
            {
                data.AddRange(bc.GetBytes((ushort)ifd1_c)); // number of entries
                foreach (string key in new_exif.Keys)
                {
                    if ((key == "MakerNote") || (key == "UserComment"))
                    {
                        byte[] userdata;
                        if (new_exif[key] is string)
                            userdata = System.Text.Encoding.GetEncoding(1251).GetBytes("ASCII\0\0\0" + (string)new_exif[key] + "\0");
                        else
                            userdata = (byte[])new_exif[key];
                        if (key == "MakerNote") // MakerNote
                            data.AddRange(bc.GetBytes((ushort)0x927C)); // type // 2
                        else // UserComment
                            data.AddRange(bc.GetBytes((ushort)0x09286)); // type // 2
                        data.AddRange(bc.GetBytes((ushort)0x0007)); // format // 2
                        data.AddRange(bc.GetBytes((uint)userdata.Length)); // length // 4
                        if (userdata.Length <= 4) // 4
                        {
                            data.AddRange(userdata); // value
                            if (userdata.Length < 4)
                                for (int i = userdata.Length; i < 4; i++) data.Add(0);
                        }
                        else
                        {
                            data.AddRange(bc.GetBytes((uint)next_entry_pos)); // offset // 4
                            next_entry_pos += userdata.Length;
                            after_data.AddRange(userdata); // value
                        };
                    };                    
                    if ((key == "DateTimeOriginal") || (key == "DateTimeDigitized") || (key == "ImageUniqueID") || (key == "OwnerName"))
                    {
                        byte[] val = System.Text.Encoding.GetEncoding(1251).GetBytes(new_exif[key].ToString() + "\0");
                        // tag number 
                        {
                            if (key == "DateTimeOriginal") data.AddRange(bc.GetBytes((ushort)0x9003)); // type 2
                            if (key == "DateTimeDigitized") data.AddRange(bc.GetBytes((ushort)0x9004)); // type 2
                            if (key == "ImageUniqueID") data.AddRange(bc.GetBytes((ushort)0xA420)); // type 2
                            if (key == "OwnerName") data.AddRange(bc.GetBytes((ushort)0xA430)); // type 2
                        };
                        data.AddRange(bc.GetBytes((ushort)0x0002)); // format // 2
                        data.AddRange(bc.GetBytes((uint)val.Length)); // length
                        if (val.Length <= 4) // 4
                        {
                            data.AddRange(val); // value
                            if (val.Length < 4)
                                for (int i = val.Length; i < 4; i++) data.Add(0);
                        }
                        else
                        {
                            data.AddRange(bc.GetBytes((uint)next_entry_pos)); // offset // 4
                            next_entry_pos += val.Length;
                            after_data.AddRange(val); // value
                        };
                    };
                };
                data.AddRange(bc.GetBytes((uint)0)); // offset to next entry // 4
                data.AddRange(after_data.ToArray()); // after IFD data
                after_data.Clear();
            };
            return ExifDataToFF00(data.ToArray());
            }

            public static byte[] ExifDataToFF00(byte[] exif_data)
            {
                List<byte> ff00 = new List<byte>();
                for (int i = 0; i < exif_data.Length; i++)
                {
                    ff00.Add(exif_data[i]);
                    if (exif_data[i] == 0xFF)
                        ff00.Add(0);    
                };
                return ff00.ToArray();
            }

        public class MyBitConverter
        {
            /// <summary>
            ///     Constructor
            /// </summary>
            public MyBitConverter()
            {

            }

            /// <summary>
            ///     Constructor
            /// </summary>
            /// <param name="IsLittleEndian">Indicates the byte order ("endianess") in which data is stored in this computer architecture.</param>
            public MyBitConverter(bool IsLittleEndian)
            {
                this.isLittleEndian = IsLittleEndian;
            }

            /// <summary>
            ///     Indicates the byte order ("endianess") in which data is stored in this computer
            /// architecture.
            /// </summary>
            private bool isLittleEndian = true;

            /// <summary>
            /// Indicates the byte order ("endianess") in which data is stored in this computer
            /// architecture.
            ///</summary>
            public bool IsLittleEndian { get { return isLittleEndian; } set { isLittleEndian = value; } } // should default to false, which is what we want for Empire

            /// <summary>
            /// Converts the specified double-precision floating point number to a 64-bit
            /// signed integer.
            ///
            /// Parameters:
            /// value:
            /// The number to convert.
            ///
            /// Returns:
            /// A 64-bit signed integer whose value is equivalent to value.
            ///</summary>
            public long DoubleToInt64Bits(double value) { throw new NotImplementedException(); }
            ///
            /// <summary>
            /// Returns the specified Boolean value as an array of bytes.
            ///
            /// Parameters:
            /// value:
            /// A Boolean value.
            ///
            /// Returns:
            /// An array of bytes with length 1.
            ///</summary>
            public byte[] GetBytes(bool value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.GetBytes(value);
                }
                else
                {
                    byte[] res = System.BitConverter.GetBytes(value);
                    Array.Reverse(res);
                    return res;
                }
            }
            ///
            /// <summary>
            /// Returns the specified Unicode character value as an array of bytes.
            ///
            /// Parameters:
            /// value:
            /// A character to convert.
            ///
            /// Returns:
            /// An array of bytes with length 2.
            ///</summary>
            public byte[] GetBytes(char value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.GetBytes(value);
                }
                else
                {
                    byte[] res = System.BitConverter.GetBytes(value);
                    Array.Reverse(res);
                    return res;
                }
            }
            ///
            /// <summary>
            /// Returns the specified double-precision floating point value as an array of
            /// bytes.
            ///
            /// Parameters:
            /// value:
            /// The number to convert.
            ///
            /// Returns:
            /// An array of bytes with length 8.
            ///</summary>
            public byte[] GetBytes(double value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.GetBytes(value);
                }
                else
                {
                    byte[] res = System.BitConverter.GetBytes(value);
                    Array.Reverse(res);
                    return res;
                }
            }
            ///
            /// <summary>
            /// Returns the specified single-precision floating point value as an array of
            /// bytes.
            ///
            /// Parameters:
            /// value:
            /// The number to convert.
            ///
            /// Returns:
            /// An array of bytes with length 4.
            ///</summary>
            public byte[] GetBytes(float value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.GetBytes(value);
                }
                else
                {
                    byte[] res = System.BitConverter.GetBytes(value);
                    Array.Reverse(res);
                    return res;
                }
            }
            ///
            /// <summary>
            /// Returns the specified 32-bit signed integer value as an array of bytes.
            ///
            /// Parameters:
            /// value:
            /// The number to convert.
            ///
            /// Returns:
            /// An array of bytes with length 4.
            ///</summary>
            public byte[] GetBytes(int value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.GetBytes(value);
                }
                else
                {
                    byte[] res = System.BitConverter.GetBytes(value);
                    Array.Reverse(res);
                    return res;
                }
            }
            ///
            /// <summary>
            /// Returns the specified 64-bit signed integer value as an array of bytes.
            ///
            /// Parameters:
            /// value:
            /// The number to convert.
            ///
            /// Returns:
            /// An array of bytes with length 8.
            ///</summary>
            public byte[] GetBytes(long value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.GetBytes(value);
                }
                else
                {
                    byte[] res = System.BitConverter.GetBytes(value);
                    Array.Reverse(res);
                    return res;
                }
            }
            ///
            /// <summary>
            /// Returns the specified 16-bit signed integer value as an array of bytes.
            ///
            /// Parameters:
            /// value:
            /// The number to convert.
            ///
            /// Returns:
            /// An array of bytes with length 2.
            ///</summary>
            public byte[] GetBytes(short value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.GetBytes(value);
                }
                else
                {
                    byte[] res = System.BitConverter.GetBytes(value);
                    Array.Reverse(res);
                    return res;
                }
            }
            ///
            /// <summary>
            /// Returns the specified 32-bit unsigned integer value as an array of bytes.
            ///
            /// Parameters:
            /// value:
            /// The number to convert.
            ///
            /// Returns:
            /// An array of bytes with length 4.
            ///</summary>
            public byte[] GetBytes(uint value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.GetBytes(value);
                }
                else
                {
                    byte[] res = System.BitConverter.GetBytes(value);
                    Array.Reverse(res);
                    return res;
                }
            }
            ///
            /// <summary>
            /// Returns the specified 64-bit unsigned integer value as an array of bytes.
            ///
            /// Parameters:
            /// value:
            /// The number to convert.
            ///
            /// Returns:
            /// An array of bytes with length 8.
            ///</summary>
            public byte[] GetBytes(ulong value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.GetBytes(value);
                }
                else
                {
                    byte[] res = System.BitConverter.GetBytes(value);
                    Array.Reverse(res);
                    return res;
                }
            }
            ///
            /// <summary>
            /// Returns the specified 16-bit unsigned integer value as an array of bytes.
            ///
            /// Parameters:
            /// value:
            /// The number to convert.
            ///
            /// Returns:
            /// An array of bytes with length 2.
            ///</summary>
            public byte[] GetBytes(ushort value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.GetBytes(value);
                }
                else
                {
                    byte[] res = System.BitConverter.GetBytes(value);
                    Array.Reverse(res);
                    return res;
                }
            }
            ///
            /// <summary>
            /// Converts the specified 64-bit signed integer to a double-precision floating
            /// point number.
            ///
            /// Parameters:
            /// value:
            /// The number to convert.
            ///
            /// Returns:
            /// A double-precision floating point number whose value is equivalent to value.
            ///</summary>
            public double Int64BitsToDouble(long value) { throw new NotImplementedException(); }
            ///
            /// <summary>
            /// Returns a Boolean value converted from one byte at a specified position in
            /// a byte array.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// true if the byte at startIndex in value is nonzero; otherwise, false.
            ///
            /// Exceptions:
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public bool ToBoolean(byte[] value, int startIndex) { throw new NotImplementedException(); }
            ///
            /// <summary>
            /// Returns a Unicode character converted from two bytes at a specified position
            /// in a byte array.
            ///
            /// Parameters:
            /// value:
            /// An array.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// A character formed by two bytes beginning at startIndex.
            ///
            /// Exceptions:
            /// System.ArgumentException:
            /// startIndex equals the length of value minus 1.
            ///
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public char ToChar(byte[] value, int startIndex) { throw new NotImplementedException(); }
            ///
            /// <summary>
            /// Returns a double-precision floating point number converted from eight bytes
            /// at a specified position in a byte array.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// A double precision floating point number formed by eight bytes beginning
            /// at startIndex.
            ///
            /// Exceptions:
            /// System.ArgumentException:
            /// startIndex is greater than or equal to the length of value minus 7, and is
            /// less than or equal to the length of value minus 1.
            ///
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public double ToDouble(byte[] value, int startIndex) { throw new NotImplementedException(); }
            ///
            /// <summary>
            /// Returns a 16-bit signed integer converted from two bytes at a specified position
            /// in a byte array.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// A 16-bit signed integer formed by two bytes beginning at startIndex.
            ///
            /// Exceptions:
            /// System.ArgumentException:
            /// startIndex equals the length of value minus 1.
            ///
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public short ToInt16(byte[] value, int startIndex)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToInt16(value, startIndex);
                }
                else
                {
                    byte[] res = (byte[])value.Clone();
                    Array.Reverse(res);
                    return System.BitConverter.ToInt16(res, value.Length - sizeof(Int16) - startIndex);
                }
            }
            ///
            /// <summary>
            /// Returns a 32-bit signed integer converted from four bytes at a specified
            /// position in a byte array.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// A 32-bit signed integer formed by four bytes beginning at startIndex.
            ///
            /// Exceptions:
            /// System.ArgumentException:
            /// startIndex is greater than or equal to the length of value minus 3, and is
            /// less than or equal to the length of value minus 1.
            ///
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public int ToInt32(byte[] value, int startIndex)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToInt32(value, startIndex);
                }
                else
                {
                    byte[] res = (byte[])value.Clone();
                    Array.Reverse(res);
                    return System.BitConverter.ToInt32(res, value.Length - sizeof(Int32) - startIndex);
                }
            }
            ///
            /// <summary>
            /// Returns a 64-bit signed integer converted from eight bytes at a specified
            /// position in a byte array.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// A 64-bit signed integer formed by eight bytes beginning at startIndex.
            ///
            /// Exceptions:
            /// System.ArgumentException:
            /// startIndex is greater than or equal to the length of value minus 7, and is
            /// less than or equal to the length of value minus 1.
            ///
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public long ToInt64(byte[] value, int startIndex)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToInt64(value, startIndex);
                }
                else
                {
                    byte[] res = (byte[])value.Clone();
                    Array.Reverse(res);
                    return System.BitConverter.ToInt64(res, value.Length - sizeof(Int64) - startIndex);
                }
            }
            ///
            /// <summary>
            /// Returns a single-precision floating point number converted from four bytes
            /// at a specified position in a byte array.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// A single-precision floating point number formed by four bytes beginning at
            /// startIndex.
            ///
            /// Exceptions:
            /// System.ArgumentException:
            /// startIndex is greater than or equal to the length of value minus 3, and is
            /// less than or equal to the length of value minus 1.
            ///
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public float ToSingle(byte[] value, int startIndex)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToSingle(value, startIndex);
                }
                else
                {
                    byte[] res = (byte[])value.Clone();
                    Array.Reverse(res);
                    return System.BitConverter.ToSingle(res, value.Length - sizeof(Single) - startIndex);
                }
            }
            ///
            /// <summary>
            /// Converts the numeric value of each element of a specified array of bytes
            /// to its equivalent hexadecimal string representation.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// Returns:
            /// A System.String of hexadecimal pairs separated by hyphens, where each pair
            /// represents the corresponding element in value; for example, "7F-2C-4A".
            ///
            /// Exceptions:
            /// System.ArgumentNullException:
            /// value is null.
            ///</summary>
            public string ToString(byte[] value)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToString(value);
                }
                else
                {
                    byte[] res = (byte[])value.Clone();
                    Array.Reverse(res);
                    return System.BitConverter.ToString(res);
                }
            }
            ///
            /// <summary>
            /// Converts the numeric value of each element of a specified subarray of bytes
            /// to its equivalent hexadecimal string representation.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// A System.String of hexadecimal pairs separated by hyphens, where each pair
            /// represents the corresponding element in a subarray of value; for example,
            /// "7F-2C-4A".
            ///
            /// Exceptions:
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public string ToString(byte[] value, int startIndex)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToString(value, startIndex);
                }
                else
                {
                    byte[] res = (byte[])value.Clone();
                    Array.Reverse(res, startIndex, value.Length - startIndex);
                    return System.BitConverter.ToString(res, startIndex);
                }
            }
            ///
            /// <summary>
            /// Converts the numeric value of each element of a specified subarray of bytes
            /// to its equivalent hexadecimal string representation.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// length:
            /// The number of array elements in value to convert.
            ///
            /// Returns:
            /// A System.String of hexadecimal pairs separated by hyphens, where each pair
            /// represents the corresponding element in a subarray of value; for example,
            /// "7F-2C-4A".
            ///
            /// Exceptions:
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex or length is less than zero. -or- startIndex is greater than
            /// zero and is greater than or equal to the length of value.
            ///
            /// System.ArgumentException:
            /// The combination of startIndex and length does not specify a position within
            /// value; that is, the startIndex parameter is greater than the length of value
            /// minus the length parameter.
            ///</summary>
            public string ToString(byte[] value, int startIndex, int length)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToString(value, startIndex, length);
                }
                else
                {
                    byte[] res = (byte[])value.Clone();
                    Array.Reverse(res, startIndex, length);
                    return System.BitConverter.ToString(res, startIndex, length);
                }
            }
            ///
            /// <summary>
            /// Returns a 16-bit unsigned integer converted from two bytes at a specified
            /// position in a byte array.
            ///
            /// Parameters:
            /// value:
            /// The array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// A 16-bit unsigned integer formed by two bytes beginning at startIndex.
            ///
            /// Exceptions:
            /// System.ArgumentException:
            /// startIndex equals the length of value minus 1.
            ///
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public ushort ToUInt16(byte[] value, int startIndex)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToUInt16(value, startIndex);
                }
                else
                {
                    byte[] res = (byte[])value.Clone();
                    Array.Reverse(res);
                    return System.BitConverter.ToUInt16(res, value.Length - sizeof(UInt16) - startIndex);
                }
            }
            ///
            /// <summary>
            /// Returns a 32-bit unsigned integer converted from four bytes at a specified
            /// position in a byte array.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// A 32-bit unsigned integer formed by four bytes beginning at startIndex.
            ///
            /// Exceptions:
            /// System.ArgumentException:
            /// startIndex is greater than or equal to the length of value minus 3, and is
            /// less than or equal to the length of value minus 1.
            ///
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public uint ToUInt32(byte[] value, int startIndex)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToUInt32(value, startIndex);
                }
                else
                {
                    byte[] res = (byte[])value.Clone();
                    Array.Reverse(res);
                    return System.BitConverter.ToUInt32(res, value.Length - sizeof(UInt32) - startIndex);
                }
            }
            ///
            /// <summary>
            /// Returns a 64-bit unsigned integer converted from eight bytes at a specified
            /// position in a byte array.
            ///
            /// Parameters:
            /// value:
            /// An array of bytes.
            ///
            /// startIndex:
            /// The starting position within value.
            ///
            /// Returns:
            /// A 64-bit unsigned integer formed by the eight bytes beginning at startIndex.
            ///
            /// Exceptions:
            /// System.ArgumentException:
            /// startIndex is greater than or equal to the length of value minus 7, and is
            /// less than or equal to the length of value minus 1.
            ///
            /// System.ArgumentNullException:
            /// value is null.
            ///
            /// System.ArgumentOutOfRangeException:
            /// startIndex is less than zero or greater than the length of value minus 1.
            ///</summary>
            public ulong ToUInt64(byte[] value, int startIndex)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToUInt64(value, startIndex);
                }
                else
                {
                    byte[] res = (byte[])value.Clone();
                    Array.Reverse(res);
                    return System.BitConverter.ToUInt64(res, value.Length - sizeof(UInt64) - startIndex);
                }
            }
        }
    }

}
