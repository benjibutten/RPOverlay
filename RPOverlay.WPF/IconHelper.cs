using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RPOverlay.WPF
{
    public static class IconHelper
    {
        /// <summary>
        /// Skapar en enkel ikon med ett medicinskt kors (first aid)
        /// </summary>
        public static Icon CreateMedicalCrossIcon()
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // Rita en mörkblå cirkel som bakgrund
            using (var brush = new SolidBrush(Color.FromArgb(47, 157, 255)))
            {
                graphics.FillEllipse(brush, 2, 2, size - 4, size - 4);
            }

            // Rita ett vitt medicinskt kors
            using (var pen = new Pen(Color.White, 5))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                
                // Vertikal linje
                graphics.DrawLine(pen, size / 2, 8, size / 2, size - 8);
                
                // Horisontell linje
                graphics.DrawLine(pen, 8, size / 2, size - 8, size / 2);
            }

            return Icon.FromHandle(bitmap.GetHicon());
        }
    }
}
