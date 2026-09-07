using System;
using HarmonyLib;
using UnityEngine;

namespace SweepZones
{
	public static class Patches
	{
		[HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
		public static class Db_Initialize_Patch
		{
			public static void Postfix()
			{
				ModStrings.Register();
				ToolIcon.Register();
			}
		}

		[HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
		public static class SaveGame_OnPrefabInit_Patch
		{
			public static void Postfix(SaveGame __instance)
			{
				__instance.gameObject.AddOrGet<ZoneStore>();
			}
		}

		[HarmonyPatch(typeof(PlayerController), "OnPrefabInit")]
		public static class PlayerController_OnPrefabInit_Patch
		{
			public static void Postfix(PlayerController __instance)
			{
				foreach (InterfaceTool existing in __instance.tools)
					if (existing is SweepZoneTool)
						return;
				GameObject go = new GameObject("SweepZoneTool");
				go.transform.SetParent(__instance.gameObject.transform);
				go.SetActive(value: false);
				SweepZoneTool tool = go.AddComponent<SweepZoneTool>();
				// Force OnPrefabInit now, the same way PlayerController initializes
				// the vanilla tools (activate then park inactive until selected).
				go.SetActive(value: true);
				go.SetActive(value: false);
				InterfaceTool[] tools = new InterfaceTool[__instance.tools.Length + 1];
				Array.Copy(__instance.tools, tools, __instance.tools.Length);
				tools[tools.Length - 1] = tool;
				__instance.tools = tools;
			}
		}

		[HarmonyPatch(typeof(ToolMenu), "CreateBasicTools")]
		public static class ToolMenu_CreateBasicTools_Patch
		{
			public static void Postfix(ToolMenu __instance)
			{
				__instance.basicTools.Add(ToolMenu.CreateToolCollection(
					ModStrings.ToolName, ToolIcon.SpriteName, SweepZonesMod.ToolAction,
					"SweepZoneTool", ModStrings.ToolTooltip, largeIcon: false));
			}
		}
	}
}
