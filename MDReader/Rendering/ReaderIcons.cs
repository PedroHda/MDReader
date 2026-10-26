namespace MDReader.Rendering;

internal static class ReaderIcons
{
    public static Image Create(char glyph, Color color)
    {
        var bitmap = new Bitmap(24, 24);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font("Segoe MDL2 Assets", 13);
        using var brush = new SolidBrush(color);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(glyph.ToString(), font, brush, new RectangleF(0, 0, 24, 24),
            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        return bitmap;
    }
}
