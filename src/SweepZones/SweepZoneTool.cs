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
		private readonly List<System.Guid> priorityLabels = new List<System.Guid>();
		private int labeledVersion = -1;
		private GameObject outlineObject;
		private Mesh outlineMesh;
		private readonly List<Vector3> outlineVertices = new List<Vector3>();
		private readonly List<int> outlineTriangles = new List<int>();

		private const float StrokeThickness = 0.05f;
		private static readonly Color StrokeColor = new Color(1f, 1f, 1f, 0.5f);

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
			ClearPriorityLabels();
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

		private bool InMopMode => mode == ZoneMode.Mop || mode == ZoneMode.ClearMop;

		private IReadOnlyDictionary<int, PrioritySetting> ActiveCells(ZoneStore store)
		{
			return InMopMode ? store.MopCells : store.SweepCells;
		}

		private void RefreshModePresentation()
		{
			// Zone visibility follows the mode selector: sweep modes show sweep zones,
			// mop modes show mop zones — never both at once.
			colorsValid = false;
			labeledVersion = -1;
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
			bool mop = InMopMode;
			foreach (KeyValuePair<int, PrioritySetting> kvp in ActiveCells(store))
				cellColors.Add(new ToolMenu.CellColorData(kvp.Key, mop ? MopColor(kvp.Value) : SweepColor(kvp.Value)));
		}

		public override void LateUpdate()
		{
			base.LateUpdate();
			RefreshPriorityLabels();
		}

		/// <summary>
		/// While the tool is active, each contiguous (4-connected) same-priority region
		/// shows its priority number centered on the region. Regions are recomputed from
		/// the painted cells whenever they change, so painting adjacent to an existing
		/// zone of the same priority merges into a single labeled region.
		/// </summary>
		private void RefreshPriorityLabels()
		{
			ZoneStore store = ZoneStore.Instance;
			NameDisplayScreen screen = NameDisplayScreen.Instance;
			if (store == null || screen == null)
				return;
			if (labeledVersion == store.Version)
				return;
			ClearPriorityLabels();
			labeledVersion = store.Version;
			GameObject textPrefab = AreaVisualizerTextPrefabField != null
				? AreaVisualizerTextPrefabField.GetValue(this) as GameObject : null;
			if (textPrefab == null)
				return;
			AddRegionLabels(ActiveCells(store), textPrefab, isMop: InMopMode);
			RebuildOutline(store);
		}

		private void ClearPriorityLabels()
		{
			NameDisplayScreen screen = NameDisplayScreen.Instance;
			if (screen != null)
				foreach (System.Guid guid in priorityLabels)
					screen.RemoveWorldText(guid);
			priorityLabels.Clear();
			labeledVersion = -1;
			if (outlineObject != null)
				outlineObject.SetActive(value: false);
		}

		/// <summary>
		/// Draws a light perimeter stroke (white, 50% opacity) just inside the boundary
		/// of every zone region, rendered as a thin quad mesh in front of the game's
		/// cell-tint plane (which is one flat color per cell and cannot draw strokes).
		/// </summary>
		private void RebuildOutline(ZoneStore store)
		{
			if (outlineObject == null)
			{
				outlineObject = new GameObject("SweepZonesOutline");
				outlineObject.layer = LayerMask.NameToLayer("Overlay");
				if (World.Instance != null)
					outlineObject.transform.SetParent(World.Instance.transform);
				// The tool-tint plane sits at z = -6 on the Overlay layer; draw just in front.
				outlineObject.transform.SetLocalPosition(new Vector3(0f, 0f, -6.1f));
				outlineMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
				outlineObject.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
				MeshRenderer renderer = outlineObject.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = StrokeColor };
				renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				renderer.receiveShadows = false;
				renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
			}
			outlineVertices.Clear();
			outlineTriangles.Clear();
			AddOutlineQuads(ActiveCells(store));
			outlineMesh.Clear();
			outlineMesh.SetVertices(outlineVertices);
			outlineMesh.SetTriangles(outlineTriangles, 0);
			outlineObject.SetActive(outlineTriangles.Count > 0);
		}

		private void AddOutlineQuads(IReadOnlyDictionary<int, PrioritySetting> zone)
		{
			foreach (KeyValuePair<int, PrioritySetting> kvp in zone)
			{
				Grid.CellToXY(kvp.Key, out int x, out int y);
				bool left = !SamePriorityAt(zone, x - 1, y, kvp.Value);
				bool right = !SamePriorityAt(zone, x + 1, y, kvp.Value);
				bool down = !SamePriorityAt(zone, x, y - 1, kvp.Value);
				bool up = !SamePriorityAt(zone, x, y + 1, kvp.Value);
				if (!(left || right || down || up))
					continue;
				float x0 = x, x1 = x + 1f, y0 = y, y1 = y + 1f;
				if (left)
					AddQuad(x0, y0, x0 + StrokeThickness, y1);
				if (right)
					AddQuad(x1 - StrokeThickness, y0, x1, y1);
				// Trim horizontal strokes where a vertical stroke already covers the corner.
				float trimX0 = x0 + (left ? StrokeThickness : 0f);
				float trimX1 = x1 - (right ? StrokeThickness : 0f);
				if (down)
					AddQuad(trimX0, y0, trimX1, y0 + StrokeThickness);
				if (up)
					AddQuad(trimX0, y1 - StrokeThickness, trimX1, y1);
			}
		}

		private static bool SamePriorityAt(IReadOnlyDictionary<int, PrioritySetting> zone, int x, int y, PrioritySetting priority)
		{
			if (x < 0 || y < 0 || x >= Grid.WidthInCells || y >= Grid.HeightInCells)
				return false;
			return zone.TryGetValue(Grid.XYToCell(x, y), out PrioritySetting other) && other == priority;
		}

		private void AddQuad(float minX, float minY, float maxX, float maxY)
		{
			int baseIndex = outlineVertices.Count;
			outlineVertices.Add(new Vector3(minX, minY, 0f));
			outlineVertices.Add(new Vector3(minX, maxY, 0f));
			outlineVertices.Add(new Vector3(maxX, maxY, 0f));
			outlineVertices.Add(new Vector3(maxX, minY, 0f));
			outlineTriangles.Add(baseIndex);
			outlineTriangles.Add(baseIndex + 1);
			outlineTriangles.Add(baseIndex + 2);
			outlineTriangles.Add(baseIndex);
			outlineTriangles.Add(baseIndex + 2);
			outlineTriangles.Add(baseIndex + 3);
		}

		private void AddRegionLabels(IReadOnlyDictionary<int, PrioritySetting> zone, GameObject textPrefab, bool isMop)
		{
			NameDisplayScreen screen = NameDisplayScreen.Instance;
			foreach ((PrioritySetting priority, List<int> cells) in FindRegions(zone))
			{
				System.Guid guid = screen.AddAreaText(LabelText(priority), textPrefab);
				GameObject label = screen.GetWorldText(guid);
				if (label != null)
				{
					LocText text = label.GetComponentInChildren<LocText>();
					if (text != null)
						text.color = LabelColor(priority, isMop);
					label.transform.SetPosition(LabelPosition(cells));
				}
				priorityLabels.Add(guid);
			}
		}

		private static List<(PrioritySetting priority, List<int> cells)> FindRegions(IReadOnlyDictionary<int, PrioritySetting> zone)
		{
			var regions = new List<(PrioritySetting, List<int>)>();
			var visited = new HashSet<int>();
			var frontier = new Queue<int>();
			foreach (KeyValuePair<int, PrioritySetting> kvp in zone)
			{
				if (!visited.Add(kvp.Key))
					continue;
				var cells = new List<int>();
				frontier.Enqueue(kvp.Key);
				while (frontier.Count > 0)
				{
					int cell = frontier.Dequeue();
					cells.Add(cell);
					Grid.CellToXY(cell, out int x, out int y);
					TryExpand(zone, kvp.Value, visited, frontier, x - 1, y);
					TryExpand(zone, kvp.Value, visited, frontier, x + 1, y);
					TryExpand(zone, kvp.Value, visited, frontier, x, y - 1);
					TryExpand(zone, kvp.Value, visited, frontier, x, y + 1);
				}
				regions.Add((kvp.Value, cells));
			}
			return regions;
		}

		private static void TryExpand(IReadOnlyDictionary<int, PrioritySetting> zone, PrioritySetting priority,
			HashSet<int> visited, Queue<int> frontier, int x, int y)
		{
			if (x < 0 || y < 0 || x >= Grid.WidthInCells || y >= Grid.HeightInCells)
				return;
			int cell = Grid.XYToCell(x, y);
			if (zone.TryGetValue(cell, out PrioritySetting other) && other == priority && visited.Add(cell))
				frontier.Enqueue(cell);
		}

		private static Vector3 LabelPosition(List<int> cells)
		{
			float sumX = 0f;
			float sumY = 0f;
			foreach (int cell in cells)
			{
				Grid.CellToXY(cell, out int x, out int y);
				sumX += x;
				sumY += y;
			}
			float centerX = sumX / cells.Count;
			float centerY = sumY / cells.Count;
			int centroidCell = Grid.XYToCell(Mathf.RoundToInt(centerX), Mathf.RoundToInt(centerY));
			Vector3 position = Grid.CellToPosCBC(cells[0], Grid.SceneLayer.Move);
			if (cells.Contains(centroidCell))
			{
				position.x = centerX + 0.5f;
				position.y = centerY + 0.5f;
				return position;
			}
			// Concave region whose centroid falls outside it: use the nearest zone cell.
			int best = cells[0];
			float bestDistance = float.MaxValue;
			foreach (int cell in cells)
			{
				Grid.CellToXY(cell, out int x, out int y);
				float distance = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					best = cell;
				}
			}
			Grid.CellToXY(best, out int bestX, out int bestY);
			position.x = bestX + 0.5f;
			position.y = bestY + 0.5f;
			return position;
		}

		private static string LabelText(PrioritySetting priority)
		{
			return priority.priority_class >= PriorityScreen.PriorityClass.topPriority
				? "!!" : priority.priority_value.ToString();
		}

		private static Color LabelColor(PrioritySetting priority, bool isMop)
		{
			if (priority.priority_class >= PriorityScreen.PriorityClass.topPriority)
				return new Color(1f, 1f, 0.4f);
			return isMop ? new Color(0.65f, 0.85f, 1f) : new Color(1f, 0.92f, 0.75f);
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
