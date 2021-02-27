﻿using HarmonyLib;
using Sandbox;
using System.Windows.Forms;
using VRage.Input;

namespace avaness.PluginLoader.Patch
{
    [HarmonyPatch(typeof(MySandboxGame), "LoadData")]
    public static class Patch_DisableConfig
    {
		public static void Postfix(MySandboxGame __instance)
		{
			// This is the earliest point in which I can use MyInput.Static
			PluginConfig config = Main.Instance?.Config;
			if(config != null && config.Data.Count > 0 && MyInput.Static is MyVRageInput && MyInput.Static.IsKeyPress(MyKeys.Escape)
				&& MessageBox.Show("Escape pressed. Start the game with all plugins disabled?", "Plugin Loader", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
				config.Disable();
			}
		}

	}
}
