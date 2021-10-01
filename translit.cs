using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using System.Text.RegularExpressions;

namespace KMZ_Viewer
{
    public enum TransliterationType
    {
        Gost,
        ISO
    }
    public static class Transliteration
    {
        private static Dictionary<string, string> gost = new Dictionary<string, string>(); //√Œ—“ 16876-71
        private static Dictionary<string, string> iso = new Dictionary<string, string>(); //ISO 9-95
        public static string Front(string text)
        {
            return Front(text, TransliterationType.ISO);
        }
        public static string Front(string text, TransliterationType type)
        {
            string output = text;

            output = Regex.Replace(output, @"\s|\.|\(", " ");
            output = Regex.Replace(output, @"\s+", " ");
            output = Regex.Replace(output, @"[^\s\w\d-]", "");
            output = output.Trim();

            Dictionary<string, string> tdict = GetDictionaryByType(type);

            foreach (KeyValuePair<string, string> key in tdict)
            {
                output = output.Replace(key.Key, key.Value);
            }
            return output;
        }
        public static string Back(string text)
        {
            return Back(text, TransliterationType.ISO);
        }
        public static string Back(string text, TransliterationType type)
        {
            string output = text;
            Dictionary<string, string> tdict = GetDictionaryByType(type);

            foreach (KeyValuePair<string, string> key in tdict)
            {
                output = output.Replace(key.Value, key.Key);
            }
            return output;
        }

        private static Dictionary<string, string> GetDictionaryByType(TransliterationType type)
        {
            Dictionary<string, string> tdict = iso;
            if (type == TransliterationType.Gost) tdict = gost;
            return tdict;
        }

        static Transliteration()
        {
            gost.Add("™", "EH");
            gost.Add("≤", "I");
            gost.Add("≥", "i");
            gost.Add("π", "#");
            gost.Add("∫", "eh");
            gost.Add("¿", "A");
            gost.Add("¡", "B");
            gost.Add("¬", "V");
            gost.Add("√", "G");
            gost.Add("ƒ", "D");
            gost.Add("≈", "E");
            gost.Add("®", "JO");
            gost.Add("∆", "ZH");
            gost.Add("«", "Z");
            gost.Add("»", "I");
            gost.Add("…", "JJ");
            gost.Add(" ", "K");
            gost.Add("À", "L");
            gost.Add("Ã", "M");
            gost.Add("Õ", "N");
            gost.Add("Œ", "O");
            gost.Add("œ", "P");
            gost.Add("–", "R");
            gost.Add("—", "S");
            gost.Add("“", "T");
            gost.Add("”", "U");
            gost.Add("‘", "F");
            gost.Add("’", "KH");
            gost.Add("÷", "C");
            gost.Add("◊", "CH");
            gost.Add("ÿ", "SH");
            gost.Add("Ÿ", "SHH");
            gost.Add("⁄", "'");
            gost.Add("€", "Y");
            gost.Add("‹", "");
            gost.Add("›", "EH");
            gost.Add("ﬁ", "YU");
            gost.Add("ﬂ", "YA");
            gost.Add("‡", "a");
            gost.Add("·", "b");
            gost.Add("‚", "v");
            gost.Add("„", "g");
            gost.Add("‰", "d");
            gost.Add("Â", "e");
            gost.Add("∏", "jo");
            gost.Add("Ê", "zh");
            gost.Add("Á", "z");
            gost.Add("Ë", "i");
            gost.Add("È", "jj");
            gost.Add("Í", "k");
            gost.Add("Î", "l");
            gost.Add("Ï", "m");
            gost.Add("Ì", "n");
            gost.Add("Ó", "o");
            gost.Add("Ô", "p");
            gost.Add("", "r");
            gost.Add("Ò", "s");
            gost.Add("Ú", "t");
            gost.Add("Û", "u");

            gost.Add("Ù", "f");
            gost.Add("ı", "kh");
            gost.Add("ˆ", "c");
            gost.Add("˜", "ch");
            gost.Add("¯", "sh");
            gost.Add("˘", "shh");
            gost.Add("˙", "");
            gost.Add("˚", "y");
            gost.Add("¸", "");
            gost.Add("˝", "eh");
            gost.Add("˛", "yu");
            gost.Add("ˇ", "ya");
            gost.Add("´", "");
            gost.Add("ª", "");
            gost.Add("ó", "-");
            gost.Add(" ", "-");

            iso.Add("™", "YE");
            iso.Add("≤", "I");
            iso.Add("Å", "G");
            iso.Add("≥", "i");
            iso.Add("π", "#");
            iso.Add("∫", "ye");
            iso.Add("É", "g");
            iso.Add("¿", "A");
            iso.Add("¡", "B");
            iso.Add("¬", "V");
            iso.Add("√", "G");
            iso.Add("ƒ", "D");
            iso.Add("≈", "E");
            iso.Add("®", "YO");
            iso.Add("∆", "ZH");
            iso.Add("«", "Z");
            iso.Add("»", "I");
            iso.Add("…", "J");
            iso.Add(" ", "K");
            iso.Add("À", "L");
            iso.Add("Ã", "M");
            iso.Add("Õ", "N");
            iso.Add("Œ", "O");
            iso.Add("œ", "P");
            iso.Add("–", "R");
            iso.Add("—", "S");
            iso.Add("“", "T");
            iso.Add("”", "U");
            iso.Add("‘", "F");
            iso.Add("’", "X");
            iso.Add("÷", "C");
            iso.Add("◊", "CH");
            iso.Add("ÿ", "SH");
            iso.Add("Ÿ", "SHH");
            iso.Add("⁄", "'");
            iso.Add("€", "Y");
            iso.Add("‹", "");
            iso.Add("›", "E");
            iso.Add("ﬁ", "YU");
            iso.Add("ﬂ", "YA");
            iso.Add("‡", "a");
            iso.Add("·", "b");
            iso.Add("‚", "v");
            iso.Add("„", "g");
            iso.Add("‰", "d");
            iso.Add("Â", "e");
            iso.Add("∏", "yo");
            iso.Add("Ê", "zh");
            iso.Add("Á", "z");
            iso.Add("Ë", "i");
            iso.Add("È", "j");
            iso.Add("Í", "k");
            iso.Add("Î", "l");
            iso.Add("Ï", "m");
            iso.Add("Ì", "n");
            iso.Add("Ó", "o");
            iso.Add("Ô", "p");
            iso.Add("", "r");
            iso.Add("Ò", "s");
            iso.Add("Ú", "t");
            iso.Add("Û", "u");
            iso.Add("Ù", "f");
            iso.Add("ı", "x");
            iso.Add("ˆ", "c");
            iso.Add("˜", "ch");
            iso.Add("¯", "sh");
            iso.Add("˘", "shh");
            iso.Add("˙", "");
            iso.Add("˚", "y");
            iso.Add("¸", "");
            iso.Add("˝", "e");
            iso.Add("˛", "yu");
            iso.Add("ˇ", "ya");
            iso.Add("´", "");
            iso.Add("ª", "");
            iso.Add("ó", "-");
            iso.Add(" ", "-");
        }
    }
}