using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VolumeHUD.Native
{
    public class Windows11MenuRenderer : ToolStripProfessionalRenderer
    {
        public Windows11MenuRenderer() : base(new Windows11ColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (e.Item.Selected && e.Item.Enabled)
            {
                var rect = new Rectangle(5, 2, e.Item.Width - 10, e.Item.Height - 4);
                using var brush = new SolidBrush(Color.FromArgb(38, 255, 255, 255)); // Windows 11 subtle hover
                using var path = GetRoundedRectangle(rect, 4);
                g.FillPath(brush, path);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw crisp modern checkmark
            int boxY = (e.Item.Height - 14) / 2;
            using var pen = new Pen(Color.FromArgb(240, 240, 245), 2.2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            g.DrawLines(pen, new Point[]
            {
                new(12, boxY + 7),
                new(16, boxY + 11),
                new(23, boxY + 3)
            });
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var g = e.Graphics;
            int y = e.Item.Height / 2;
            using var pen = new Pen(Color.FromArgb(40, 255, 255, 255), 1);
            g.DrawLine(pen, 12, y, e.Item.Width - 12, y);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using var pen = new Pen(Color.FromArgb(55, 255, 255, 255), 1);
            g.DrawRectangle(pen, rect);
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }

    public class Windows11ColorTable : ProfessionalColorTable
    {
        private static readonly Color DarkBg = Color.FromArgb(28, 28, 30);
        private static readonly Color DarkBorder = Color.FromArgb(50, 50, 54);

        public override Color ToolStripDropDownBackground => DarkBg;
        public override Color MenuBorder => DarkBorder;
        public override Color MenuItemSelected => Color.Transparent;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuStripGradientBegin => DarkBg;
        public override Color MenuStripGradientEnd => DarkBg;
        public override Color CheckBackground => Color.Transparent;
        public override Color CheckSelectedBackground => Color.Transparent;
        public override Color CheckPressedBackground => Color.Transparent;
        public override Color ImageMarginGradientBegin => DarkBg;
        public override Color ImageMarginGradientMiddle => DarkBg;
        public override Color ImageMarginGradientEnd => DarkBg;
        public override Color SeparatorDark => Color.FromArgb(40, 255, 255, 255);
        public override Color SeparatorLight => Color.Transparent;
    }
}
