using HarmonyLib;
using Isochronous;
using KMod;
using PeterHan.PLib.Actions;
using PeterHan.PLib.Core;
using TOOL = SweepZones.ModStrings.UI.TOOLS.SWEEPZONES;

namespace SweepZones
{
	public sealed class SweepZonesMod : UserMod2
	{
		/// <summary>
		/// Rebindable hotkey (options -> game -> controls -> Mods) that activates the
		/// Sweep Zones tool. PAction.MaxAction is the runtime "no action" value, used
		/// until OnLoad registers the real action.
		/// </summary>
		public static Action ToolAction { get; private set; } = PAction.MaxAction;

		public override void OnLoad(Harmony harmony)
		{
			base.OnLoad(harmony);
			PUtil.InitLibrary(false);
			// Registering first applies the saved language, so the action title PLib
			// copies below is already translated.
			Translations.Register(harmony, typeof(ModStrings));
			PAction action = new PActionManager().CreateAction(
				"Isochronous.SweepZones.Tool",
				TOOL.TOOLNAME,
				new PKeyBinding(KKeyCode.Z, Modifier.Shift));
			ToolAction = action.GetKAction();
			Debug.Log("[SweepZones] Loaded version " + typeof(SweepZonesMod).Assembly.GetName().Version);
		}
	}
}
