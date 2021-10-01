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
        private static Dictionary<string, string> gost = new Dictionary<string, string>(); //���� 16876-71
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
            gost.Add("�", "EH");
            gost.Add("�", "I");
            gost.Add("�", "i");
            gost.Add("�", "#");
            gost.Add("�", "eh");
            gost.Add("�", "A");
            gost.Add("�", "B");
            gost.Add("�", "V");
            gost.Add("�", "G");
            gost.Add("�", "D");
            gost.Add("�", "E");
            gost.Add("�", "JO");
            gost.Add("�", "ZH");
            gost.Add("�", "Z");
            gost.Add("�", "I");
            gost.Add("�", "JJ");
            gost.Add("�", "K");
            gost.Add("�", "L");
            gost.Add("�", "M");
            gost.Add("�", "N");
            gost.Add("�", "O");
            gost.Add("�", "P");
            gost.Add("�", "R");
            gost.Add("�", "S");
            gost.Add("�", "T");
            gost.Add("�", "U");
            gost.Add("�", "F");
            gost.Add("�", "KH");
            gost.Add("�", "C");
            gost.Add("�", "CH");
            gost.Add("�", "SH");
            gost.Add("�", "SHH");
            gost.Add("�", "'");
            gost.Add("�", "Y");
            gost.Add("�", "");
            gost.Add("�", "EH");
            gost.Add("�", "YU");
            gost.Add("�", "YA");
            gost.Add("�", "a");
            gost.Add("�", "b");
            gost.Add("�", "v");
            gost.Add("�", "g");
            gost.Add("�", "d");
            gost.Add("�", "e");
            gost.Add("�", "jo");
            gost.Add("�", "zh");
            gost.Add("�", "z");
            gost.Add("�", "i");
            gost.Add("�", "jj");
            gost.Add("�", "k");
            gost.Add("�", "l");
            gost.Add("�", "m");
            gost.Add("�", "n");
            gost.Add("�", "o");
            gost.Add("�", "p");
            gost.Add("�", "r");
            gost.Add("�", "s");
            gost.Add("�", "t");
            gost.Add("�", "u");

            gost.Add("�", "f");
            gost.Add("�", "kh");
            gost.Add("�", "c");
            gost.Add("�", "ch");
            gost.Add("�", "sh");
            gost.Add("�", "shh");
            gost.Add("�", "");
            gost.Add("�", "y");
            gost.Add("�", "");
            gost.Add("�", "eh");
            gost.Add("�", "yu");
            gost.Add("�", "ya");
            gost.Add("�", "");
            gost.Add("�", "");
            gost.Add("�", "-");
            gost.Add(" ", "-");

            iso.Add("�", "YE");
            iso.Add("�", "I");
            iso.Add("�", "G");
            iso.Add("�", "i");
            iso.Add("�", "#");
            iso.Add("�", "ye");
            iso.Add("�", "g");
            iso.Add("�", "A");
            iso.Add("�", "B");
            iso.Add("�", "V");
            iso.Add("�", "G");
            iso.Add("�", "D");
            iso.Add("�", "E");
            iso.Add("�", "YO");
            iso.Add("�", "ZH");
            iso.Add("�", "Z");
            iso.Add("�", "I");
            iso.Add("�", "J");
            iso.Add("�", "K");
            iso.Add("�", "L");
            iso.Add("�", "M");
            iso.Add("�", "N");
            iso.Add("�", "O");
            iso.Add("�", "P");
            iso.Add("�", "R");
            iso.Add("�", "S");
            iso.Add("�", "T");
            iso.Add("�", "U");
            iso.Add("�", "F");
            iso.Add("�", "X");
            iso.Add("�", "C");
            iso.Add("�", "CH");
            iso.Add("�", "SH");
            iso.Add("�", "SHH");
            iso.Add("�", "'");
            iso.Add("�", "Y");
            iso.Add("�", "");
            iso.Add("�", "E");
            iso.Add("�", "YU");
            iso.Add("�", "YA");
            iso.Add("�", "a");
            iso.Add("�", "b");
            iso.Add("�", "v");
            iso.Add("�", "g");
            iso.Add("�", "d");
            iso.Add("�", "e");
            iso.Add("�", "yo");
            iso.Add("�", "zh");
            iso.Add("�", "z");
            iso.Add("�", "i");
            iso.Add("�", "j");
            iso.Add("�", "k");
            iso.Add("�", "l");
            iso.Add("�", "m");
            iso.Add("�", "n");
            iso.Add("�", "o");
            iso.Add("�", "p");
            iso.Add("�", "r");
            iso.Add("�", "s");
            iso.Add("�", "t");
            iso.Add("�", "u");
            iso.Add("�", "f");
            iso.Add("�", "x");
            iso.Add("�", "c");
            iso.Add("�", "ch");
            iso.Add("�", "sh");
            iso.Add("�", "shh");
            iso.Add("�", "");
            iso.Add("�", "y");
            iso.Add("�", "");
            iso.Add("�", "e");
            iso.Add("�", "yu");
            iso.Add("�", "ya");
            iso.Add("�", "");
            iso.Add("�", "");
            iso.Add("�", "-");
            iso.Add(" ", "-");
        }
    }
}