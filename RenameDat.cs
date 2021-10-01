using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;

namespace KMZViewer
{
    public partial class RenameDat : Form
    {
        public Image ImageNone = null;
        public List<string> ns = new List<string>();

        public RenameDat()
        {
            InitializeComponent();

            System.Resources.ResourceManager rm = new global::System.Resources.ResourceManager("KMZViewer.Properties.Resources", typeof(KMZViewer.Properties.Resources).Assembly);
            object obj = rm.GetObject("progorod00", System.Globalization.CultureInfo.InvariantCulture);
            ImageNone = ((System.Drawing.Bitmap)(obj));
            ImageNone = ResizeImage(ImageNone, 16, 16);

            for (int i = 0; i < 20; i++) ns.Add(((KMZ_Viewer.ProGorodPOI.TType)i).ToString());
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            Rectangle destRect = new Rectangle(0, 0, width, height);
            Bitmap destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (ImageAttributes wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        private void listView2_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
            ListView listView = (ListView)sender;
            if(e.ColumnIndex == 1)
            {
                e.DrawDefault = false;

                Rectangle rowBounds = e.SubItem.Bounds;
                Rectangle labelBounds = e.Item.GetBounds(ItemBoundsPortion.Label);
                int leftMargin = labelBounds.Left - 1;
                Rectangle bounds = new Rectangle(rowBounds.Left + leftMargin, rowBounds.Top,rowBounds.Width - leftMargin, rowBounds.Height);

                int sel = -1;
                if (listView.SelectedItems.Count > 0) sel = listView.SelectedItems[0].Index;
                if (e.Item.Index == sel)
                {
                    e.Graphics.FillRectangle(SystemBrushes.Highlight, rowBounds);
                    {
                        if (e.SubItem.Text == "None")
                            e.Graphics.DrawImage(ImageNone, rowBounds.Left + 1, rowBounds.Top);
                        else
                        {
                            int index = ns.IndexOf(e.SubItem.Text);
                            System.Resources.ResourceManager rm = new global::System.Resources.ResourceManager("KMZViewer.Properties.Resources", typeof(KMZViewer.Properties.Resources).Assembly);
                            object obj = rm.GetObject("progorod" + index.ToString("00"), System.Globalization.CultureInfo.InvariantCulture);
                            Image im = ((System.Drawing.Bitmap)(obj));
                            im = ResizeImage(im, 16, 16);
                            e.Graphics.DrawImage(im, rowBounds.Left + 1, rowBounds.Top);
                        };
                    };
                    e.Graphics.DrawString(e.SubItem.Text, listView.Font, SystemBrushes.HighlightText, bounds);
                }
                else
                {
                    Brush brd = SystemBrushes.Window;
                    int bi = ns.IndexOf(e.SubItem.Text);
                    if (bi >= 0)
                    {
                        Brush[] sb = new Brush[] { SystemBrushes.Window, Brushes.AliceBlue, Brushes.LightCoral, Brushes.LightCyan, Brushes.LightGreen, Brushes.LightPink, Brushes.LightSalmon, Brushes.LightSkyBlue, Brushes.LightYellow, Brushes.Lime, Brushes.MistyRose, Brushes.Orange, Brushes.Orchid, Brushes.Pink, Brushes.Red, Brushes.RoyalBlue, Brushes.Violet, Brushes.Tan, Brushes.YellowGreen, Brushes.Yellow};
                        brd = sb[bi];
                    };
                    e.Graphics.FillRectangle(brd, rowBounds);
                    {
                        if (e.SubItem.Text == "None")
                            e.Graphics.DrawImage(ImageNone, rowBounds.Left + 1, rowBounds.Top);
                        else
                        {
                            int index = ns.IndexOf(e.SubItem.Text);
                            System.Resources.ResourceManager rm = new global::System.Resources.ResourceManager("KMZViewer.Properties.Resources", typeof(KMZViewer.Properties.Resources).Assembly);
                            object obj = rm.GetObject("progorod" + index.ToString("00"), System.Globalization.CultureInfo.InvariantCulture);
                            Image im = ((System.Drawing.Bitmap)(obj));
                            im = ResizeImage(im, 16, 16);
                            e.Graphics.DrawImage(im, rowBounds.Left + 1, rowBounds.Top);
                        };
                    };
                    e.Graphics.DrawString(e.SubItem.Text, listView.Font, SystemBrushes.WindowText, bounds);
                };
            };
        }

        private void listView2_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void listView2_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
        }

        private void listView2_KeyPress(object sender, KeyPressEventArgs e)
        {
            
        }

        private void listView2_KeyDown(object sender, KeyEventArgs e)
        {
            if (listView2.SelectedItems.Count != 1) return;
            if ((e.KeyValue == 37) || (e.KeyValue == 39))
            {
                int index = ns.IndexOf(listView2.SelectedItems[0].SubItems[1].Text);
                if (e.KeyValue == 37) index -= 1;
                if (e.KeyValue == 39) index += 1;
                if (index < 0) index = 19;
                if (index > 19) index = 0;
                listView2.SelectedItems[0].SubItems[1].Text = ((KMZ_Viewer.ProGorodPOI.TType)index).ToString();
            };
        }

        private void listView2_DoubleClick(object sender, EventArgs e)
        {
            if (listView2.Items.Count == 0) return;
            if (listView2.SelectedItems.Count != 1) return;

            string txt = listView2.SelectedItems[0].SubItems[1].Text;
            if (InputBox.Show("“ËÔ POI", "¬‚Â‰ËÚÂ ÚËÔ œ–Œ√Œ–Œƒ:", ns.ToArray(), ref txt) == DialogResult.OK)
                listView2.SelectedItems[0].SubItems[1].Text = txt;
        }

        private void ÔËÏÂÌËÚ¸ Ó¬ÒÂÏToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView2.Items.Count == 0) return;

            string txt = "";
            if (listView2.SelectedItems.Count == 1) txt = listView2.SelectedItems[0].SubItems[1].Text;
            if (InputBox.Show("“ËÔ POI", "¬‚Â‰ËÚÂ ÚËÔ œ–Œ√Œ–Œƒ:", ns.ToArray(), ref txt) == DialogResult.OK)
            {
                for(int i=0;i<listView2.Items.Count;i++)
                    listView2.Items[i].SubItems[1].Text = txt;
            };
        }
    }
}