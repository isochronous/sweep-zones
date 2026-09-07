using UnityEngine;

namespace SweepZones
{
	/// <summary>
	/// Draws the toolbar icon at runtime (corner brackets suggesting a zone, with
	/// slanted sweep strokes inside) so the mod ships no image assets.
	/// </summary>
	internal static class ToolIcon
	{
		public const string SpriteName = "isochronous_sweepzones_tool";

		private const int Size = 64;

		public static void Register()
		{
			if (Assets.Sprites == null || Assets.Sprites.ContainsKey(SpriteName))
				return;
			Color32[] pixels = new Color32[Size * Size];

			// Corner brackets, 20 long and 5 thick, inset 2px.
			FillRect(pixels, 2, 57, 20, 5);
			FillRect(pixels, 2, 42, 5, 20);
			FillRect(pixels, 42, 57, 20, 5);
			FillRect(pixels, 57, 42, 5, 20);
			FillRect(pixels, 2, 2, 20, 5);
			FillRect(pixels, 2, 2, 5, 20);
			FillRect(pixels, 42, 2, 20, 5);
			FillRect(pixels, 57, 2, 5, 20);

			// Three slanted sweep strokes.
			Stroke(pixels, 14, 38, 50, 44);
			Stroke(pixels, 10, 26, 46, 32);
			Stroke(pixels, 16, 14, 52, 20);

			Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
			{
				name = SpriteName,
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
			};
			texture.SetPixels32(pixels);
			texture.Apply();
			Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f));
			sprite.name = SpriteName;
			Assets.Sprites.Add(SpriteName, sprite);
		}

		private static void FillRect(Color32[] pixels, int x, int y, int width, int height)
		{
			Color32 white = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
			for (int row = y; row < y + height; row++)
				for (int col = x; col < x + width; col++)
					if (col >= 0 && col < Size && row >= 0 && row < Size)
						pixels[row * Size + col] = white;
		}

		private static void Stroke(Color32[] pixels, int x0, int y0, int x1, int y1)
		{
			for (int x = x0; x <= x1; x++)
			{
				int y = y0 + (y1 - y0) * (x - x0) / (x1 - x0);
				FillRect(pixels, x, y, 1, 4);
			}
		}
	}
}
