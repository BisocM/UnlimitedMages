using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnlimitedMages.Utilities;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches
{
    /// <summary>
    /// Contains Harmony patches for the <see cref="MainMenuManager"/> class.
    /// These patches are responsible for dynamically resizing the lobby UI and fixing related logic.
    /// </summary>
    [HarmonyPatch(typeof(MainMenuManager))]
    public static class MainMenuManagerPatches
    {
        /// <summary>
        /// Postfixes the Start method to resize all UI arrays and instantiate new UI elements.
        /// This dynamically reconstructs the lobby UI to accommodate the configured number of players.
        /// </summary>
        [HarmonyPatch(GameConstants.MainMenuManager.StartMethod)]
        [HarmonyPostfix]
        public static void Start_Postfix(MainMenuManager __instance)
        {
            if (UnlimitedMagesPlugin.TeamSizeConfig?.Value == null) return;
            int teamSize = UnlimitedMagesPlugin.TeamSizeConfig.Value;
            int newLobbySize = teamSize * GameConstants.Game.NumTeams;

            // Resize internal data arrays
            AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.BodiesField)
                .SetValue(__instance, new GameObject[newLobbySize]);
            AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.PlayerNamesField)
                .SetValue(__instance, new string[newLobbySize]);
            AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.PlayerLevelAndRanksField)
                .SetValue(__instance, new string[newLobbySize]);

            // Resize and populate UI element arrays
            ResizeUiList(ref __instance.team1, teamSize);
            ResizeUiList(ref __instance.team2, teamSize);
            ResizeUiList(ref __instance.team1rankandleveltext, teamSize);
            ResizeUiList(ref __instance.team2rankandleveltext, teamSize);
            ResizeUiList(ref __instance.texts, newLobbySize * 2);
            ResizeUiList(ref __instance.rankandleveltext, newLobbySize);
            ResizeAndWrapHats(ref __instance.hats, newLobbySize);
        }

        /// <summary>
        /// Resizes an array of UI Text elements and instantiates new elements based on a template.
        /// It also adjusts the position and scale of the elements to fit them within the existing UI space.
        /// </summary>
        private static void ResizeUiList(ref Text[] array, int newSize)
        {
            if (array.Length >= newSize) return;
            if (array.Length < 1 || array[0] == null) return;

            int originalLength = array.Length;
            var newArray = new Text[newSize];
            Array.Copy(array, newArray, originalLength);

            Text template = array[0];
            Transform parent = template.transform.parent;
            if (parent == null) return;

            // Calculate the original vertical offset between UI elements.
            Vector3 originalOffset = originalLength > 1
                ? array[1].transform.localPosition - array[0].transform.localPosition
                : Vector3.zero;

            for (int i = originalLength; i < newSize; i++)
            {
                Text newUiElement = Object.Instantiate(template, parent);
                newUiElement.name = $"{template.name}_Clone_{i}";
                newUiElement.text = "";
                newArray[i] = newUiElement;
            }

            // Scale down the elements and their spacing to fit more players.
            float scaleFactor = Mathf.Clamp((float)originalLength / newSize, 0.5f, 1.0f);
            Vector3 newOffset = originalOffset * scaleFactor;
            Vector3 basePosition = newArray[0].transform.localPosition;

            for (int i = 0; i < newSize; i++)
            {
                newArray[i].transform.localPosition = basePosition + (newOffset * i);
                newArray[i].transform.localScale = Vector3.one * scaleFactor;
            }

            array = newArray;
        }

        /// <summary>
        /// Resizes the array of player model placeholders ("hats") and arranges them in a wrapping grid.
        /// This prevents UI overflow by creating new rows for additional players.
        /// </summary>
        private static void ResizeAndWrapHats(ref GameObject[] array, int newSize)
        {
            if (array.Length >= newSize) return;
            if (array.Length < 1 || array[0] == null) return;

            int originalLength = array.Length;
            int wrapAfter = GameConstants.Game.OriginalTeamSize;

            var newArray = new GameObject[newSize];
            Array.Copy(array, newArray, originalLength);

            GameObject template = array[0];
            Transform parent = template.transform.parent;
            if (parent == null) return;

            // Calculate horizontal and vertical offsets for grid layout.
            Vector3 horizontalOffset = array.Length > 1 ? array[1].transform.position - array[0].transform.position : Vector3.zero;
            // This vertical offset POSITIVE, so new rows appear ABOVE.
            Vector3 verticalOffset = new Vector3(0, horizontalOffset.magnitude * 2.5f, 0);

            for (int i = originalLength; i < newSize; i++)
            {
                int column = i % wrapAfter;
                int row = i / wrapAfter;

                Vector3 basePosition = array[column].transform.position;

                GameObject newHat = Object.Instantiate(template, parent);
                newHat.name = $"{template.name}_Clone_{i}";

                Vector3 finalPosition = basePosition + (verticalOffset * row);

                // Add a slight horizontal offset to every other row for a staggered/honeycomb effect.
                if (row % 2 != 0)
                {
                    finalPosition += horizontalOffset * 0.5f;
                }

                newHat.transform.position = finalPosition;
                newArray[i] = newHat;
            }

            array = newArray;
        }

        /// <summary>
        /// Transpiles map selection methods to use the dynamic lobby size for setting member limits.
        /// </summary>
        [HarmonyPatch(nameof(MainMenuManager.SmallMap))]
        [HarmonyPatch(nameof(MainMenuManager.LargeMap))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> MapSelect_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>(instructions);

            for (int i = 0; i < newInstructions.Count; i++)
            {
                // Target: ldc.i4.8 (loading the integer constant 8 for the lobby size)
                if (newInstructions[i].opcode != OpCodes.Ldc_I4_8) continue;

                // Replace '8' with '(TeamSizeConfig.Value * NumTeams)'
                newInstructions[i] = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(UnlimitedMagesPlugin), nameof(UnlimitedMagesPlugin.TeamSizeConfig)));
                newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ConfigEntry<int>), "Value")));
                newInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4, GameConstants.Game.NumTeams));
                newInstructions.Insert(i + 3, new CodeInstruction(OpCodes.Mul));
            }

            return newInstructions;
        }

        /// <summary>
        /// Transpiles SyncHats to ensure the player name is clamped before being added to the kick dictionary.
        /// This maintains consistency with the name used when removing the player.
        /// </summary>
        [HarmonyPatch(GameConstants.MainMenuManager.SyncHatsMethod)]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SyncHats_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>(instructions);
            var addToDictMethod = AccessTools.Method(typeof(KickPlayersHolder), "AddToDict");
            var clampStringMethod = AccessTools.Method(typeof(MainMenuManager), GameConstants.MainMenuManager.ClampStringMethod);

            for (int i = 0; i < newInstructions.Count; i++)
            {
                if (!newInstructions[i].Calls(addToDictMethod)) continue;

                // The original code passes the raw player name to AddToDict.
                // We intercept this and insert a call to ClampString first.
                var instructionsToInsert = new List<CodeInstruction>
                {
                    new(OpCodes.Ldarg_0), // Load 'this' (MainMenuManager instance)
                    new(OpCodes.Ldarg_1), // Load 'PlayerName' argument
                    new(OpCodes.Ldc_I4, GameConstants.MainMenuManager.PlayerNameClampLength), // Load clamp length
                    new(OpCodes.Call, clampStringMethod) // Call ClampString
                };

                // Replace the original instruction that loaded the raw name.
                newInstructions.RemoveAt(i - 2);
                newInstructions.InsertRange(i - 2, instructionsToInsert);
                break;
            }

            return newInstructions;
        }

        /// <summary>
        /// Prefixes RemoveHat to handle larger player arrays correctly.
        /// The original method might not be safe with arrays larger than 8.
        /// </summary>
        [HarmonyPatch(GameConstants.MainMenuManager.RemoveHatMethod)]
        [HarmonyPrefix]
        public static bool RemoveHat_Prefix(MainMenuManager __instance, string PlayerName)
        {
            var playerNamesField = AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.PlayerNamesField);
            var bodiesField = AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.BodiesField);

            string[] playerNames = (string[])playerNamesField.GetValue(__instance);
            GameObject[] bodies = (GameObject[])bodiesField.GetValue(__instance);

            for (int i = 0; i < playerNames.Length; i++)
            {
                if (playerNames[i] == PlayerName)
                {
                    if (bodies != null && i < bodies.Length && bodies[i] != null)
                    {
                        Object.Destroy(bodies[i]);
                        bodies[i] = null;
                    }

                    if (__instance.hats != null && i < __instance.hats.Length) __instance.hats[i].SetActive(false);
                    if (__instance.rankandleveltext != null && i < __instance.rankandleveltext.Length) __instance.rankandleveltext[i].text = "";
                    if (__instance.texts != null && i * 2 + 1 < __instance.texts.Length)
                    {
                        __instance.texts[i * 2].text = "";
                        __instance.texts[i * 2 + 1].text = "";
                    }

                    break;
                }
            }

            // Allow the original method to run, which handles other cleanup.
            return true;
        }

        /// <summary>
        /// Postfixes RemoveHat to fix a bug in the base game.
        /// The original game does not remove players from the kick dictionary upon leaving, leading to a stale UI state.
        /// This postfix corrects that by removing the player from the dictionary.
        /// </summary>
        [HarmonyPatch(GameConstants.MainMenuManager.RemoveHatMethod)]
        [HarmonyPostfix]
        public static void RemoveHat_Postfix(MainMenuManager __instance, string PlayerName)
        {
            var clampStringMethod = AccessTools.Method(typeof(MainMenuManager), GameConstants.MainMenuManager.ClampStringMethod);
            string clampedName = (string)clampStringMethod.Invoke(__instance, new object[] { PlayerName, GameConstants.MainMenuManager.PlayerNameClampLength });

            var kickDictionaryField = AccessTools.Field(typeof(KickPlayersHolder), "nametosteamid");
            var kickDictionary = (Dictionary<string, string>)kickDictionaryField.GetValue(__instance.kickplayershold);

            if (kickDictionary != null && kickDictionary.ContainsKey(clampedName))
            {
                kickDictionary.Remove(clampedName);
            }
        }

        [HarmonyPatch(nameof(MainMenuManager.LeaveLobby))]
        [HarmonyPrefix]
        public static bool LeaveLobby_Prefix()
        {
            // By calling LeaveLobby2, we use the robust cleanup coroutine.
            BootstrapManager.LeaveLobby2();

            // This prevents the original, unsafe LeaveLobby method from executing.
            return false;
        }
    }
}