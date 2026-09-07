using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SweepZones
{
	public enum ZoneMode
	{
		Sweep,
		ClearSweep,
		Mop,
		ClearMop,
	}

	/// <summary>
	/// Box-drag tool that paints/erases sweep and mop zones. Zones are shown while the
	/// tool is active through <see cref="GetOverlayColorData"/> — the same per-tool
	/// cell-coloring hook the vanilla Prioritize tool uses — so the overlay system is
	/// never touched and other overlay mods are unaffected.
	/// </summary>
	public sealed class SweepZoneTool : DragTool
	{
		// Cursor textures and the drag-area visualizer are cloned from the vanilla
		// Sweep tool; these DragTool/InterfaceTool fields are Unity-serialized on its
		// prefab and not otherwise reachable (names verified against U59-744825).
		private static readonly FieldInfo CursorField = AccessTools.Field(typeof(InterfaceTool), "cursor");
		private static readonly FieldInfo BoxCursorField = AccessTools.Field(typeof(DragTool), "boxCursor");
		private static readonly FieldInfo AreaVisualizerField = AccessTools.Field(typeof(DragTool), "areaVisualizer");
		private static readonly FieldInfo AreaVisualizerTextPrefabField = AccessTools.Field(typeof(DragTool), "areaVisualizerTextPrefab");
		private static readonly FieldInfo AreaColourField = AccessTools.Field(typeof(DragTool), "areaColour");

		private static readonly Color32 AreaColor = new Color32(byte.MaxValue, 172, 52, 96);

		private ZoneMode mode = ZoneMode.Sweep;
		private ToolParameterMenu.ToggleData[] filters;
		private HoverTextConfiguration hoverCard;
		private readonly HashSet<ToolMenu.CellColorData> cellColors = new HashSet<ToolMenu.CellColorData>();
		private int coloredVersion = -1;
		private bool colorsValid;

		protected override void OnPrefabInit()
		{
			DragTool donor = ClearTool.Instance;
			if (donor != null)
			{
				visualizer = donor.visualizer;
				visualizerLayer = donor.visualizerLayer;
				CopyField(CursorField, donor);
				CopyField(BoxCursorField, donor);
				CopyField(AreaVisualizerField, donor);
				CopyField(AreaVisualizerTextPrefabField, donor);
				if (AreaColourField != null)
					AreaColourField.SetValue(this, AreaColor);
				AddHoverCard(donor);
			}
			base.OnPrefabInit();
			interceptNumberKeysForPriority = true;
		}

		private void CopyField(FieldInfo field, DragTool donor)
		{
			// Tolerate a future game update renaming one of these fields: the tool
			// degrades cosmetically instead of failing to initialize.
			if (field != null)
				field.SetValue(this, field.GetValue(donor));
			else
				Debug.LogWarning("[SweepZones] A donor tool field is missing; cursor/visualizer may look wrong.");
		}

		private void AddHoverCard(DragTool donor)
		{
			HoverTextConfiguration donorCard = donor.GetComponent<HoverTextConfiguration>();
			if (donorCard == null)
				return;
			hoverCard = gameObject.AddComponent<HoverTextConfiguration>();
			hoverCard.ToolName = ModStrings.ToolName.ToUpper();
			hoverCard.ActionName = ModStrings.HoverActionPaintSweep;
			hoverCard.HoverTextStyleSettings = donorCard.HoverTextStyleSettings;
			hoverCard.ToolTitleTextStyle = donorCard.ToolTitleTextStyle;
			hoverCard.Styles_Title = donorCard.Styles_Title;
			hoverCard.Styles_BodyText = donorCard.Styles_BodyText;
			hoverCard.Styles_Instruction = donorCard.Styles_Instruction;
			hoverCard.Styles_Warning = donorCard.Styles_Warning;
			hoverCard.Styles_Values = donorCard.Styles_Values;
		}

		protected override void OnActivateTool()
		{
			base.OnActivateTool();
			filters = new[]
			{
				new ToolParameterMenu.ToggleData(ModStrings.FilterSweep, StateFor(ZoneMode.Sweep)),
				new ToolParameterMenu.ToggleData(ModStrings.FilterClearSweep, StateFor(ZoneMode.ClearSweep)),
				new ToolParameterMenu.ToggleData(ModStrings.FilterMop, StateFor(ZoneMode.Mop)),
				new ToolParameterMenu.ToggleData(ModStrings.FilterClearMop, StateFor(ZoneMode.ClearMop)),
			};
			ToolParameterMenu menu = ToolMenu.Instance.toolParameterMenu;
			menu.PopulateMenu(filters);
			menu.onParametersChanged += OnParametersChanged;
			RefreshModePresentation();
		}

		protected override void OnDeactivateTool(InterfaceTool new_tool)
		{
			ToolParameterMenu menu = ToolMenu.Instance.toolParameterMenu;
			menu.onParametersChanged -= OnParametersChanged;
			menu.ClearMenu();
			ToolMenu.Instance.PriorityScreen.Show(show: false);
			base.OnDeactivateTool(new_tool);
		}

		private ToolParameterMenu.ToggleState StateFor(ZoneMode m)
		{
			return mode == m ? ToolParameterMenu.ToggleState.On : ToolParameterMenu.ToggleState.Off;
		}

		private void OnParametersChanged()
		{
			if (filters == null)
				return;
			foreach (ToolParameterMenu.ToggleData toggle in filters)
			{
				if (!toggle.IsOn)
					continue;
				if (toggle.name == ModStrings.FilterSweep)
					mode = ZoneMode.Sweep;
				else if (toggle.name == ModStrings.FilterClearSweep)
					mode = ZoneMode.ClearSweep;
				else if (toggle.name == ModStrings.FilterMop)
					mode = ZoneMode.Mop;
				else if (toggle.name == ModStrings.FilterClearMop)
					mode = ZoneMode.ClearMop;
				break;
			}
			RefreshModePresentation();
		}

		private void RefreshModePresentation()
		{
			bool painting = mode == ZoneMode.Sweep || mode == ZoneMode.Mop;
			ToolMenu.Instance.PriorityScreen.Show(painting);
			if (hoverCard != null)
			{
				switch (mode)
				{
				case ZoneMode.Sweep:
					hoverCard.ActionName = ModStrings.HoverActionPaintSweep;
					break;
				case ZoneMode.ClearSweep:
					hoverCard.ActionName = ModStrings.HoverActionEraseSweep;
					break;
				case ZoneMode.Mop:
					hoverCard.ActionName = ModStrings.HoverActionPaintMop;
					break;
				case ZoneMode.ClearMop:
					hoverCard.ActionName = ModStrings.HoverActionEraseMop;
					break;
				}
			}
		}

		protected override void OnDragTool(int cell, int distFromOrigin)
		{
			ZoneStore store = ZoneStore.Instance;
			if (store == null)
				return;
			switch (mode)
			{
			case ZoneMode.Sweep:
				store.SetSweep(cell, ToolMenu.Instance.PriorityScreen.GetLastSelectedPriority());
				break;
			case ZoneMode.ClearSweep:
				store.RemoveSweep(cell);
				break;
			case ZoneMode.Mop:
				store.SetMop(cell, ToolMenu.Instance.PriorityScreen.GetLastSelectedPriority());
				break;
			case ZoneMode.ClearMop:
				store.RemoveMop(cell);
				break;
			}
		}

		public override void GetOverlayColorData(out HashSet<ToolMenu.CellColorData> colors)
		{
			colors = cellColors;
			ZoneStore store = ZoneStore.Instance;
			if (store == null)
			{
				cellColors.Clear();
				colorsValid = false;
				return;
			}
			if (colorsValid && coloredVersion == store.Version)
				return;
			coloredVersion = store.Version;
			colorsValid = true;
			cellColors.Clear();
			foreach (KeyValuePair<int, PrioritySetting> kvp in store.SweepCells)
				cellColors.Add(new ToolMenu.CellColorData(kvp.Key, SweepColor(kvp.Value)));
			foreach (KeyValuePair<int, PrioritySetting> kvp in store.MopCells)
				cellColors.Add(new ToolMenu.CellColorData(kvp.Key, MopColor(kvp.Value)));
		}

		// Sweep zones use a warm gradient (low priority amber -> high priority red),
		// mop zones a cool one; yellow-alert priorities get their own bright colors.
		private static Color SweepColor(PrioritySetting priority)
		{
			if (priority.priority_class >= PriorityScreen.PriorityClass.topPriority)
				return new Color(1f, 1f, 0.25f, 0.55f);
			float t = Mathf.Clamp01((priority.priority_value - 1) / 8f);
			Color color = Color.Lerp(new Color(1f, 0.72f, 0.25f), new Color(1f, 0.13f, 0.08f), t);
			color.a = 0.55f;
			return color;
		}

		private static Color MopColor(PrioritySetting priority)
		{
			if (priority.priority_class >= PriorityScreen.PriorityClass.topPriority)
				return new Color(0.45f, 1f, 1f, 0.55f);
			float t = Mathf.Clamp01((priority.priority_value - 1) / 8f);
			Color color = Color.Lerp(new Color(0.35f, 0.75f, 1f), new Color(0.1f, 0.2f, 1f), t);
			color.a = 0.55f;
			return color;
		}
	}
}
