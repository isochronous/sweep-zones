using System;
using PeterHan.PLib.UI;
using UnityEngine;

namespace SweepZones
{
	/// <summary>
	/// Registers the toolbar icon: a two-tone broom-over-zone glyph matching the
	/// game's tool-icon style. Authored as vector art (assets-src/icon-candidates.svg),
	/// rasterized at dev time by assets-src/IconGen, embedded in the DLL, and loaded
	/// here; a simple procedurally drawn glyph remains as a fallback.
	/// </summary>
	internal static class ToolIcon
	{
		public const string SpriteName = "isochronous_sweepzones_tool";

		private const string ResourceName = "SweepZones.assets.tool_icon.png";

		private const int Size = 64;

		public static void Register()
		{
			if (Assets.Sprites == null || Assets.Sprites.ContainsKey(SpriteName))
				return;
			Sprite sprite = LoadEmbedded() ?? DrawFallback();
			sprite.name = SpriteName;
			Assets.Sprites.Add(SpriteName, sprite);
		}

		private static Sprite LoadEmbedded()
		{
			try
			{
				// PLib decodes the embedded PNG; going through it avoids referencing
				// UnityEngine.ImageConversionModule, whose Unity 6 build cannot be
				// referenced from a net48 compile (netstandard 2.1 dependency).
				return PUIUtils.LoadSprite(ResourceName);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[SweepZones] Failed to load embedded tool icon, using fallback: " + ex.Message);
				return null;
			}
		}

		private static Sprite DrawFallback()
		{
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
			return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f));
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
