using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace KMZViewer
{
    public partial class SelectIcon : Form
    {
        private string sIcon = "/[";

        public SelectIcon()
        {
            InitializeComponent();
        }

        public string GetIcon()
        {
            return sIcon;
        }

        public void SetIcon(string symbol)
        {
            int imsz = 24;
            string symb = sIcon = symbol;
            string prose = "primary";
            if (symb.Length == 2)
            {
                if (symb[0] == '\\') prose = "secondary";
                symb = symb.Substring(1);
            };
            string symbtable = "!\"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            int idd = symbtable.IndexOf(symb);
            int itop = (int)Math.Truncate(idd / 16.0) * imsz;
            int ileft = (idd % 16) * imsz;

            Rectangle r = new Rectangle(ileft, itop, 24, 24);
            if (prose == "primary")
            {
                Image im = global::KMZViewer.Properties.Resources.aprs1st;
                
                Bitmap ic = new Bitmap(24, 24);
                Graphics icg = Graphics.FromImage(ic);
                icg.FillRectangle(new SolidBrush(Color.White), r);
                icg.DrawImage(im, 0, 0, r, GraphicsUnit.Pixel);           
                this.Icon = Icon.FromHandle(ic.GetHicon());
                icg.Dispose();
                
                Graphics g = Graphics.FromImage(im);
                g.DrawRectangle(new Pen(new SolidBrush(Color.Black), 2), r);
                g.FillRectangle(new SolidBrush(Color.FromArgb(50, Color.Lime)), r);
                g.Dispose();
                primary.Image = im;
                secondary.Image = global::KMZViewer.Properties.Resources.aprs2nd;
            }
            else
            {
                Image im = global::KMZViewer.Properties.Resources.aprs2nd;

                Bitmap ic = new Bitmap(24, 24);
                Graphics icg = Graphics.FromImage(ic);
                icg.FillRectangle(new SolidBrush(Color.White), r);
                icg.DrawImage(im, 0, 0, r, GraphicsUnit.Pixel);
                this.Icon = Icon.FromHandle(ic.GetHicon());
                icg.Dispose();

                Graphics g = Graphics.FromImage(im);
                g.DrawRectangle(new Pen(new SolidBrush(Color.Black), 2), r);
                g.FillRectangle(new SolidBrush(Color.FromArgb(50, Color.Lime)), r);
                g.Dispose();
                secondary.Image = im;
                primary.Image = global::KMZViewer.Properties.Resources.aprs1st;
            };
        }

        private void Select(int x, int y, string prose)
        {
            int ileft = (int)Math.Truncate((float)x / 24.0);
            int itop = (int)Math.Truncate((float)y / 24.0);            
            int index = itop * 16 + ileft;
            string symbtable = "!\"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~~~";
            string icon = (prose == "primary" ? "/" : @"\") + symbtable.Substring(index,1);
            Text = "Выбранная иконка: " + icon;
            SetIcon(icon);
        }

        private void primary_MouseClick(object sender, MouseEventArgs e)
        {
            Select(e.X, e.Y, "primary");
        }

        private void secondary_MouseClick(object sender, MouseEventArgs e)
        {
            Select(e.X, e.Y, "secondary");
        }

        private void primary_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void secondary_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}