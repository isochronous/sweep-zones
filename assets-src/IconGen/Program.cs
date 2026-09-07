using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Renders the Sweep Zones tool icon (design: assets-src/icon-candidates.svg) at 8x
// supersampling and downsamples to a 64x64 PNG. Windows-only (System.Drawing).
internal static class Program
{
	private const int Size = 64;
	private const int Scale = 8;

	private static readonly Color White = Color.FromArgb(232, 230, 224);
	private static readonly Color Red = Color.FromArgb(194, 59, 63);
	private static readonly Color Outline = Color.FromArgb(38, 42, 48);

	private static void Main(string[] args)
	{
		string outPath = args.Length > 0 ? args[0] : "tool_icon.png";
		using var big = new Bitmap(Size * Scale, Size * Scale, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(big))
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.Clear(Color.Transparent);
			g.ScaleTransform(Scale, Scale);

			// Broom, rotated 35 degrees about (32, 30).
			var state = g.Save();
			g.TranslateTransform(32f, 30f);
			g.RotateTransform(35f);
			g.TranslateTransform(-32f, -30f);

			using var outlinePen = new Pen(Outline, 1.1f) { LineJoin = LineJoin.Round };
			using var whiteBrush = new SolidBrush(White);
			using var redBrush = new SolidBrush(Red);

			using (var handle = RoundedRect(30.1f, 6f, 3.8f, 17f, 1.9f))
			{
				g.FillPath(whiteBrush, handle);
				g.DrawPath(outlinePen, handle);
			}
			using (var collar = RoundedRect(27.2f, 23f, 9.6f, 4.6f, 1.6f))
			{
				g.FillPath(whiteBrush, collar);
				g.DrawPath(outlinePen, collar);
			}
			var head = new PointF[]
			{
				new PointF(26.5f, 27.6f), new PointF(37.5f, 27.6f),
				new PointF(42.5f, 44f), new PointF(21.5f, 44f),
			};
			g.FillPolygon(redBrush, head);
			g.DrawPolygon(outlinePen, head);

			using (var bristlePen = new Pen(Outline, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
			{
				g.DrawLine(bristlePen, 29.4f, 31.5f, 27.8f, 42.5f);
				g.DrawLine(bristlePen, 32f, 31.5f, 32f, 42.5f);
				g.DrawLine(bristlePen, 34.6f, 31.5f, 36.2f, 42.5f);
			}
			g.Restore(state);

			// Dashed zone floor.
			using var dashPen = new Pen(White, 2.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
			for (int i = 0; i < 4; i++)
			{
				float x = 14f + i * 10f;
				g.DrawLine(dashPen, x, 50f, x + 6f, 50f);
			}
		}

		using var final = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(final))
		{
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.Clear(Color.Transparent);
			g.DrawImage(big, new Rectangle(0, 0, Size, Size));
		}
		final.Save(outPath, ImageFormat.Png);
		Console.WriteLine("Wrote " + outPath);
	}

	private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
	{
		var path = new GraphicsPath();
		float d = r * 2f;
		path.AddArc(x, y, d, d, 180f, 90f);
		path.AddArc(x + w - d, y, d, d, 270f, 90f);
		path.AddArc(x + w - d, y + h - d, d, d, 0f, 90f);
		path.AddArc(x, y + h - d, d, d, 90f, 90f);
		path.CloseFigure();
		return path;
	}
}
