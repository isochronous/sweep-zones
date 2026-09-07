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
		if (args.Length > 1 && args[0] == "preview")
		{
			RenderPreview(args[1]);
			return;
		}
		if (args.Length > 2 && args[0] == "card")
		{
			RenderCardFromImage(args[1], args[2]);
			return;
		}
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

	/// <summary>
	/// 256x256 workshop preview: the broom glyph at card scale on the same rounded-card
	/// treatment as the Motion Sensor Range preview (radial gradient, 3px white border).
	/// </summary>
	private static void RenderPreview(string outPath)
	{
		const int canvas = 256;
		using var final = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(final))
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.Clear(Color.Transparent);

			var card = new RectangleF(1.5f, 1.5f, canvas - 3f, canvas - 3f);
			using var cardPath = RoundedRectCard(card, 16f);
			g.SetClip(cardPath);

			using (var bgEllipse = new GraphicsPath())
			{
				bgEllipse.AddEllipse(-60f, -80f, canvas + 120f, canvas + 140f);
				using var bg = new PathGradientBrush(bgEllipse)
				{
					CenterColor = Color.FromArgb(78, 78, 82),
					CenterPoint = new PointF(canvas / 2f, canvas * 0.42f),
					SurroundColors = new[] { Color.FromArgb(40, 40, 43) },
				};
				g.FillRectangle(bg, 0, 0, canvas, canvas);
			}

			// Glyph content spans roughly (12..52, 4..52) in its 64-unit space.
			const float scale = 3.4f;
			g.TranslateTransform(128f - 32f * scale, 122f - 28f * scale);
			g.ScaleTransform(scale, scale);
			DrawGlyph(g);
			g.ResetTransform();

			g.ResetClip();
			using var border = new Pen(Color.White, 3f);
			g.DrawPath(border, cardPath);
		}
		final.Save(outPath, ImageFormat.Png);
		Console.WriteLine("Wrote " + outPath);
	}

	private static void DrawGlyph(Graphics g)
	{
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

		using var dashPen = new Pen(White, 2.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
		for (int i = 0; i < 4; i++)
		{
			float x = 14f + i * 10f;
			g.DrawLine(dashPen, x, 50f, x + 6f, 50f);
		}
	}

	/// <summary>
	/// Keys the flat background out of externally produced art and composes it onto
	/// the standard card treatment (radial gradient, 3px white border).
	/// </summary>
	private static void RenderCardFromImage(string inPath, string outPath)
	{
		var bg = Color.FromArgb(46, 46, 46);
		const float keyStart = 10f, keyFull = 40f;
		using var source = new Bitmap(inPath);
		using var keyed = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
		int minX = source.Width, minY = source.Height, maxX = 0, maxY = 0;
		for (int y = 0; y < source.Height; y++)
			for (int x = 0; x < source.Width; x++)
			{
				Color c = source.GetPixel(x, y);
				float distance = MathF.Sqrt(
					(c.R - bg.R) * (c.R - bg.R) + (c.G - bg.G) * (c.G - bg.G) + (c.B - bg.B) * (c.B - bg.B));
				float a = Math.Clamp((distance - keyStart) / (keyFull - keyStart), 0f, 1f);
				if (a <= 0f)
					continue;
				keyed.SetPixel(x, y, Color.FromArgb((byte)(a * 255f), c.R, c.G, c.B));
				minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
				minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
			}
		var content = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);

		const int canvas = 256;
		using var final = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(final))
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.Clear(Color.Transparent);

			var card = new RectangleF(1.5f, 1.5f, canvas - 3f, canvas - 3f);
			using var cardPath = RoundedRectCard(card, 16f);
			g.SetClip(cardPath);
			using (var bgEllipse = new GraphicsPath())
			{
				bgEllipse.AddEllipse(-60f, -80f, canvas + 120f, canvas + 140f);
				using var bgBrush = new PathGradientBrush(bgEllipse)
				{
					CenterColor = Color.FromArgb(78, 78, 82),
					CenterPoint = new PointF(canvas / 2f, canvas * 0.42f),
					SurroundColors = new[] { Color.FromArgb(40, 40, 43) },
				};
				g.FillRectangle(bgBrush, 0, 0, canvas, canvas);
			}

			float scale = Math.Min(212f / content.Width, 212f / content.Height);
			int w = (int)(content.Width * scale), h = (int)(content.Height * scale);
			g.DrawImage(keyed, new Rectangle((canvas - w) / 2, (canvas - h) / 2, w, h), content, GraphicsUnit.Pixel);

			g.ResetClip();
			using var border = new Pen(Color.White, 3f);
			g.DrawPath(border, cardPath);
		}
		final.Save(outPath, ImageFormat.Png);
		Console.WriteLine("Wrote " + outPath);
	}

	private static GraphicsPath RoundedRectCard(RectangleF r, float radius)
	{
		float d = radius * 2f;
		var path = new GraphicsPath();
		path.AddArc(r.X, r.Y, d, d, 180f, 90f);
		path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
		path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
		path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
		path.CloseFigure();
		return path;
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
