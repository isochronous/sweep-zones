namespace SweepZones
{
	public static class ModStrings
	{
		public const string ToolNameKey = "STRINGS.UI.TOOLS.SWEEPZONES.NAME";
		public const string ToolTooltipKey = "STRINGS.UI.TOOLS.SWEEPZONES.TOOLTIP";

		public const string ToolName = "Sweep Zones";
		public const string ToolTooltip = "Paint zones where debris is automatically marked for sweeping and puddles for mopping";

		// ToolParameterMenu resolves labels from STRINGS.UI.TOOLS.FILTERLAYERS.<name>.NAME/.TOOLTIP
		public const string FilterSweep = "SWEEPZONES_SWEEP";
		public const string FilterClearSweep = "SWEEPZONES_SWEEP_CLEAR";
		public const string FilterMop = "SWEEPZONES_MOP";
		public const string FilterClearMop = "SWEEPZONES_MOP_CLEAR";

		public const string HoverActionPaintSweep = "PAINT SWEEP ZONE";
		public const string HoverActionEraseSweep = "ERASE SWEEP ZONE";
		public const string HoverActionPaintMop = "PAINT MOP ZONE";
		public const string HoverActionEraseMop = "ERASE MOP ZONE";

		public static void Register()
		{
			Strings.Add(ToolNameKey, ToolName);
			Strings.Add(ToolTooltipKey, ToolTooltip);
			AddFilter(FilterSweep, "Sweep zone",
				"Debris inside the zone is continuously marked for sweeping at the selected priority");
			AddFilter(FilterClearSweep, "Erase sweep zone",
				"Remove sweep zone cells (existing sweep errands are kept)");
			AddFilter(FilterMop, "Mop zone",
				"Moppable puddles inside the zone continuously receive mop errands at the selected priority");
			AddFilter(FilterClearMop, "Erase mop zone",
				"Remove mop zone cells (existing mop errands are kept)");
		}

		private static void AddFilter(string id, string name, string tooltip)
		{
			Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS." + id + ".NAME", name);
			Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS." + id + ".TOOLTIP", tooltip);
		}
	}
}
