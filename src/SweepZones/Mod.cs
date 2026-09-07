using HarmonyLib;
using KMod;

namespace SweepZones
{
	public sealed class SweepZonesMod : UserMod2
	{
		public override void OnLoad(Harmony harmony)
		{
			base.OnLoad(harmony);
			Debug.Log("[SweepZones] Loaded version " + typeof(SweepZonesMod).Assembly.GetName().Version);
		}
	}
}
