using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace SweepZones
{
	/// <summary>
	/// Holds the painted zones (attached to the SaveGame object so they serialize into
	/// the save file) and periodically applies them: debris in sweep zones is marked
	/// for clearing, moppable puddles in mop zones receive mop errand placers.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class ZoneStore : KMonoBehaviour, ISim1000ms
	{
		private static readonly Tag MopPlacerTag = new Tag("MopPlacer");

		public static ZoneStore Instance { get; private set; }

		[Serialize]
		private Dictionary<int, PrioritySetting> sweepCells = new Dictionary<int, PrioritySetting>();

		[Serialize]
		private Dictionary<int, PrioritySetting> mopCells = new Dictionary<int, PrioritySetting>();

		/// <summary>Bumped on every mutation so renderers can cache.</summary>
		public int Version { get; private set; }

		public IReadOnlyDictionary<int, PrioritySetting> SweepCells => sweepCells;

		public IReadOnlyDictionary<int, PrioritySetting> MopCells => mopCells;

		protected override void OnPrefabInit()
		{
			base.OnPrefabInit();
			Instance = this;
		}

		protected override void OnCleanUp()
		{
			if (Instance == this)
				Instance = null;
			base.OnCleanUp();
		}

		public void SetSweep(int cell, PrioritySetting priority)
		{
			sweepCells[cell] = priority;
			Version++;
		}

		public void RemoveSweep(int cell)
		{
			if (sweepCells.Remove(cell))
				Version++;
		}

		public void SetMop(int cell, PrioritySetting priority)
		{
			mopCells[cell] = priority;
			Version++;
		}

		public void RemoveMop(int cell)
		{
			if (mopCells.Remove(cell))
				Version++;
		}

		/// <summary>
		/// Zones live on open cells directly above solid ground: air, liquid, or
		/// plant-occupied cells over a floor. Grid.Solid is the sim's material
		/// solidity, so surfaces that only Duplicants can stand on (for example
		/// open pneumatic doors) do not count as ground.
		/// </summary>
		public static bool IsValidZoneCell(int cell)
		{
			if (!Grid.IsValidCell(cell) || Grid.Solid[cell])
				return false;
			int below = Grid.CellBelow(cell);
			return Grid.IsValidCell(below) && Grid.Solid[below];
		}

		/// <summary>
		/// Drops zone cells that no longer pass <see cref="IsValidZoneCell"/> (floor dug
		/// out, cell built over, ...). Called when the tool brings up the zone display,
		/// so the terrain can change freely in between without zones flickering away.
		/// </summary>
		public void PruneInvalidCells()
		{
			if (Prune(sweepCells) | Prune(mopCells))
				Version++;
		}

		private static bool Prune(Dictionary<int, PrioritySetting> cells)
		{
			List<int> invalid = null;
			foreach (int cell in cells.Keys)
			{
				if (IsValidZoneCell(cell))
					continue;
				if (invalid == null)
					invalid = new List<int>();
				invalid.Add(cell);
			}
			if (invalid == null)
				return false;
			foreach (int cell in invalid)
				cells.Remove(cell);
			return true;
		}

		public void Sim1000ms(float dt)
		{
			MarkSweepZones();
			PlaceMopErrands();
		}

		private void MarkSweepZones()
		{
			foreach (KeyValuePair<int, PrioritySetting> kvp in sweepCells)
			{
				int cell = kvp.Key;
				if (!Grid.IsValidCell(cell))
					continue;
				GameObject first = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
				if (first == null)
					continue;
				Pickupable firstPickupable = first.GetComponent<Pickupable>();
				if (firstPickupable == null)
					continue;
				for (ObjectLayerListItem item = firstPickupable.objectLayerListItem; item != null; item = item.nextItem)
				{
					GameObject go = item.gameObject;
					Pickupable pickupable = item.pickupable;
					if (go == null || pickupable == null)
						continue;
					KPrefabID prefabID = pickupable.KPrefabID;
					// Skip Duplicants/robots, and anything already marked (Garbage tag is
					// added by MarkForClear and removed by CancelClearing) so we don't
					// stomp priorities the player changed by hand.
					if (prefabID.HasTag(GameTags.BaseMinion) || prefabID.HasTag(GameTags.Garbage))
						continue;
					// Cached-field checks first: things that sit in a zone indefinitely
					// without ever being clearable (critters, for example) are rejected
					// here every pass, so the GetComponent below only runs for an item
					// on the one pass that actually marks it.
					Clearable clearable = pickupable.Clearable;
					if (clearable == null || !clearable.isClearable)
						continue;
					if (go.GetComponent<MinionIdentity>() != null)
						continue;
					clearable.MarkForClear();
					Prioritizable prioritizable = go.GetComponent<Prioritizable>();
					if (prioritizable != null)
						prioritizable.SetMasterPriority(kvp.Value);
				}
			}
		}

		private void PlaceMopErrands()
		{
			if (mopCells.Count == 0)
				return;
			GameObject placerPrefab = Assets.GetPrefab(MopPlacerTag);
			if (placerPrefab == null)
				return;
			foreach (KeyValuePair<int, PrioritySetting> kvp in mopCells)
			{
				int cell = kvp.Key;
				// Same conditions the vanilla mop tool enforces.
				if (!Grid.IsValidCell(cell) || Grid.Solid[cell])
					continue;
				if (Grid.Objects[cell, (int)ObjectLayer.MopPlacer] != null)
					continue;
				if (!Grid.Element[cell].IsLiquid)
					continue;
				int below = Grid.CellBelow(cell);
				if (!Grid.IsValidCell(below) || !Grid.Solid[below])
					continue;
				if (Grid.Mass[cell] > MopTool.maxMopAmt)
					continue;
				GameObject placer = Util.KInstantiate(placerPrefab);
				Grid.Objects[cell, (int)ObjectLayer.MopPlacer] = placer;
				Grid.SceneLayer layer = MopTool.Instance != null ? MopTool.Instance.visualizerLayer : Grid.SceneLayer.Move;
				Vector3 position = Grid.CellToPosCBC(cell, layer);
				position.z -= 0.15f;
				placer.transform.SetPosition(position);
				placer.SetActive(value: true);
				Prioritizable prioritizable = placer.GetComponent<Prioritizable>();
				if (prioritizable != null)
					prioritizable.SetMasterPriority(kvp.Value);
			}
		}
	}
}
