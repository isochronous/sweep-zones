namespace SweepZones
{
	/// <summary>
	/// The mod's text, registered under the game's STRINGS tree by Isochronous.Translations
	/// (ModStrings.UI.TOOLS.FILTERLAYERS.X.NAME is the key STRINGS.UI.TOOLS.FILTERLAYERS.X.NAME,
	/// which is how ToolParameterMenu finds a toggle's label) and replaced from
	/// translations/&lt;locale&gt;.po. Code reads the fields when it needs the text, so a
	/// translation loaded later is what the player sees.
	/// </summary>
	public static class ModStrings
	{
		// Toggle ids for the mode menu; ToolParameterMenu resolves their labels from
		// STRINGS.UI.TOOLS.FILTERLAYERS.<id>.NAME/.TOOLTIP, declared below under the same names.
		public const string FilterSweep = "SWEEPZONES_SWEEP";
		public const string FilterClearSweep = "SWEEPZONES_SWEEP_CLEAR";
		public const string FilterMop = "SWEEPZONES_MOP";
		public const string FilterClearMop = "SWEEPZONES_MOP_CLEAR";

		public static class UI
		{
			public static class TOOLS
			{
				public static class SWEEPZONES
				{
					public static LocString NAME = "Sweep Zones";
					public static LocString TOOLTIP = "Paint zones where debris is automatically marked for sweeping and puddles for mopping";
					// Key-binding screen (options -> game -> controls -> Mods).
					public static LocString TOOLNAME = "Sweep Zones tool";
					// Hover card action line, one per mode.
					public static LocString ACTION_PAINT_SWEEP = "PAINT SWEEP ZONE";
					public static LocString ACTION_ERASE_SWEEP = "ERASE SWEEP ZONE";
					public static LocString ACTION_PAINT_MOP = "PAINT MOP ZONE";
					public static LocString ACTION_ERASE_MOP = "ERASE MOP ZONE";
				}

				public static class FILTERLAYERS
				{
					public static class SWEEPZONES_SWEEP
					{
						public static LocString NAME = "Sweep zone";
						public static LocString TOOLTIP = "Debris inside the zone is continuously marked for sweeping at the selected priority";
					}

					public static class SWEEPZONES_SWEEP_CLEAR
					{
						public static LocString NAME = "Erase sweep zone";
						public static LocString TOOLTIP = "Remove sweep zone cells (existing sweep errands are kept)";
					}

					public static class SWEEPZONES_MOP
					{
						public static LocString NAME = "Mop zone";
						public static LocString TOOLTIP = "Moppable puddles inside the zone continuously receive mop errands at the selected priority";
					}

					public static class SWEEPZONES_MOP_CLEAR
					{
						public static LocString NAME = "Erase mop zone";
						public static LocString TOOLTIP = "Remove mop zone cells (existing mop errands are kept)";
					}
				}
			}
		}
	}
}
