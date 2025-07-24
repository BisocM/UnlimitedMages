using System.Collections;
using System.Reflection.Emit;
using BepInEx.Configuration;
using Dissonance;
using Dissonance.Integrations.FishNet;
using FishNet.Managing;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnlimitedMages.Utilities;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches
{
    /// <summary>
    /// Contains Harmony patches for the <see cref="BootstrapManager"/> class.
    /// These patches are critical for adjusting lobby size limits and ensuring stable network cleanup.
    /// </summary>
   
    public static class BootstrapManagerPatches
    {
        /// <summary>
        /// Transpiles the CreateLobby method to replace the hardcoded lobby size with a dynamic value.
        /// </summary>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>The modified IL instructions.</returns>
       
       
        public static IEnumerable<CodeInstruction> CreateLobby_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>(instructions);
            for (int i = 0; i < newInstructions.Count; i++)
            {
                // Target: ldc.i4.8 (loading the integer constant 8)
                if (newInstructions[i].opcode!= OpCodes.Ldc_I4_8) continue;

                // Replace '8' with '(TeamSizeConfig.Value * NumTeams)'
                newInstructions[i] = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(UnlimitedMagesPlugin), nameof(UnlimitedMagesPlugin.TeamSizeConfig)));
                newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ConfigEntry<int>), "Value")));
                newInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4, GameConstants.Game.NumTeams));
                newInstructions.Insert(i + 3, new CodeInstruction(OpCodes.Mul));
            }
            return newInstructions;
        }

        /// <summary>
        /// Transpiles the OnGetLobbyList method to adjust lobby size filtering logic.
        /// </summary>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>The modified IL instructions.</returns>
       
       
        public static IEnumerable<CodeInstruction> OnGetLobbyList_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>(instructions);
            for (int i = 0; i < newInstructions.Count; i++)
            {
                // Target: ldc.i4.8 (loading the integer constant 8)
                if (newInstructions[i].opcode!= OpCodes.Ldc_I4_8) continue;

                // Replace '8' with '(TeamSizeConfig.Value * NumTeams)'
                newInstructions[i] = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(UnlimitedMagesPlugin), nameof(UnlimitedMagesPlugin.TeamSizeConfig)));
                newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ConfigEntry<int>), "Value")));
                newInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4, GameConstants.Game.NumTeams));
                newInstructions.Insert(i + 3, new CodeInstruction(OpCodes.Mul));
            }
            return newInstructions;
        }

        /// <summary>
        /// Transpiles the OnLobbyEntered method to replace the hardcoded team size check.
        /// </summary>
        /// <param name="instructions">The original IL instructions.</param>
        /// <returns>The modified IL instructions.</returns>
       
       
        public static IEnumerable<CodeInstruction> OnLobbyEntered_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>(instructions);
            for (int i = 0; i < newInstructions.Count; i++)
            {
                // Target: ldc.i4.4 (loading the integer constant 4, the original team size)
                if (newInstructions[i].opcode!= OpCodes.Ldc_I4_4) continue;

                // Replace '4' with 'TeamSizeConfig.Value'
                newInstructions[i] = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(UnlimitedMagesPlugin), nameof(UnlimitedMagesPlugin.TeamSizeConfig)));
                newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ConfigEntry<int>), "Value")));
            }
            return newInstructions;
        }

        /// <summary>
        /// Prefixes the ChangeSceneAfterCleanup coroutine to replace it entirely.
        /// The original method is not robust enough for larger player counts, leading to instability when leaving lobbies.
        /// This custom implementation ensures all network and voice systems are shut down in the correct order.
        /// </summary>
        /// <param name="sceneName">The name of the scene to load after cleanup.</param>
        /// <param name="__result">The return value of the original method, which we replace with our custom coroutine.</param>
        /// <returns>Returns false to prevent the original method from executing.</returns>
       
        [HarmonyPrefix]
        public static bool ChangeSceneAfterCleanup_Prefix(string sceneName, ref IEnumerator __result)
        {
            __result = CustomChangeSceneAfterCleanup(sceneName);
            return false;
        }

        /// <summary>
        /// A custom, robust implementation of the network cleanup and scene change logic.
        /// </summary>
        private static IEnumerator CustomChangeSceneAfterCleanup(string sceneName)
        {
            var networkManager = Object.FindFirstObjectByType<NetworkManager>();
            var fishySteamworks = Object.FindFirstObjectByType<FishySteamworks.FishySteamworks>();
            var dissonanceComms = Object.FindFirstObjectByType<DissonanceFishNetComms>();

            // Leave the Steam Lobby first.
            if (BootstrapManager.CurrentLobbyID != 0uL)
            {
                SteamMatchmaking.LeaveLobby(new CSteamID(BootstrapManager.CurrentLobbyID));
                BootstrapManager.CurrentLobbyID = 0uL;
            }

            // Stop the core FishNet networking. This allows "player left" messages to be sent.
            if (networkManager != null)
            {
                if (networkManager.ServerManager.Started)
                {
                    networkManager.ServerManager.StopConnection(true);
                }
                if (networkManager.ClientManager.Started)
                {
                    networkManager.ClientManager.StopConnection();
                }
            }

            // Stop the underlying transport.
            if (fishySteamworks != null)
            {
                fishySteamworks.StopConnection(false);
                fishySteamworks.StopConnection(true);
            }

            // Wait for network messages to process.
            yield return new WaitForSeconds(0.5f);

            // Now that the network is down, safely stop Dissonance.
            if (dissonanceComms != null)
                dissonanceComms.stopit();
    
            var dissonanceRoot = Object.FindFirstObjectByType<DissonanceComms>();
            if (dissonanceRoot != null)
                Object.Destroy(dissonanceRoot.gameObject);

            // Clean up any other objects.
            GameObject[] playbackPrefabs = GameObject.FindGameObjectsWithTag("playbackprefab");
            foreach (var prefab in playbackPrefabs)
                Object.Destroy(prefab);

            yield return null;

            // Load the next scene.
            SceneManager.LoadScene(sceneName);

            if (BootstrapManager.instance == null) yield break;
            BootstrapManager.instance.GoToMenu();
            AccessTools.Field(typeof(BootstrapManager), "hasLeaveGameFinished").SetValue(BootstrapManager.instance, true);
        }
    }
}