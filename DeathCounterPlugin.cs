using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;

namespace SilksongDeathCounter
{
    [BepInPlugin("com.peacestudio.silksongdeathcounter", "Silksong Death Counter", "2.2.0")]
    public class DeathCounterPlugin : BaseUnityPlugin
    {
        internal static DeathCounterPlugin Instance;
        private Harmony _harmony;

        // config for each save file
        private ConfigEntry<int> TotalDeaths;
        private ConfigEntry<KeyboardShortcut> ResetKey;
        private int runDeaths;

        // current profile id (-1 when unknown)
        private int currentProfileID = -1;

        // backup polling every second
        private double nextFallbackCheck = 0.0;
        private const double fallbackIntervalSeconds = 1.0;

        private void Awake()
        {
            Instance = this;

            // default values so GUI has something to show
            TotalDeaths = Config.Bind("General", "TotalDeaths", 0, "Total deaths count (fallback).");
            ResetKey = Config.Bind("Hotkeys", "ResetRunDeaths", new KeyboardShortcut(KeyCode.F10), "Resets run counter.");

            _harmony = new Harmony("com.peacestudio.silksongdeathcounter");

            // try to find death method
            try
            {
                MethodInfo deathMethod = AccessTools.Method("HeroVibrationController:PlayHeroDeath");
                if (deathMethod == null)
                {
                    Logger.LogWarning("DeathCounter: HeroVibrationController.PlayHeroDeath not found - deaths won't be counted until this is fixed.");
                }
                else
                {
                    var postfix = new HarmonyMethod(typeof(DeathCounterPlugin).GetMethod(nameof(Postfix_PlayHeroDeath), BindingFlags.Static | BindingFlags.NonPublic));
                    _harmony.Patch(deathMethod, postfix: postfix);
                    Logger.LogInfo("DeathCounter: applied PlayHeroDeath patch.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("DeathCounter: error patching PlayHeroDeath: " + ex);
            }

            // try to patch all LoadGameFromUI overloads
            TryPatchAllLoadGameFromUIMethods();

            Logger.LogInfo("Silksong Death Counter loaded (Awake).");
        }

        private void Update()
        {
            // reset run counter if player pressed the key
            if (ResetKey.Value.IsDown())
            {
                runDeaths = 0;
                Logger.LogInfo("Run deaths reset.");
            }

            // check every second if something changed
            if (Time.realtimeSinceStartupAsDouble >= nextFallbackCheck)
            {
                nextFallbackCheck = Time.realtimeSinceStartupAsDouble + fallbackIntervalSeconds;
                TryInitFromGameManagerIfChanged();
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(12, 12, 300, 26), $"Deaths: {TotalDeaths?.Value ?? 0} (Run: {runDeaths})");
        }

        internal void AddDeath()
        {
            if (TotalDeaths == null)
            {
                // if no init, try now
                TryInitFromGameManagerIfChanged();
                if (TotalDeaths == null)
                {
                    TotalDeaths = Config.Bind("General", "TotalDeaths", 0, "Total deaths count (fallback).");
                }
            }

            TotalDeaths.Value++;
            runDeaths++;
            Config.Save();
            Logger.LogInfo($"DeathCounter -> ProfileID: {currentProfileID} Section: {TotalDeaths.Definition.Section} Total: {TotalDeaths.Value} Run: {runDeaths}");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        // called when hero dies
        private static void Postfix_PlayHeroDeath()
        {
            try
            {
                Instance?.AddDeath();
            }
            catch (Exception e)
            {
                Instance?.Logger.LogError("DeathCounter: exception in Postfix_PlayHeroDeath: " + e);
            }
        }

        // patch all LoadGameFromUI overloads
        private void TryPatchAllLoadGameFromUIMethods()
        {
            try
            {
                var gmType = AccessTools.TypeByName("GameManager");
                if (gmType == null)
                {
                    Logger.LogInfo("DeathCounter: GameManager type not found (TryPatchAllLoadGameFromUIMethods).");
                    return;
                }

                MethodInfo[] all = gmType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                int patched = 0;
                foreach (var m in all)
                {
                    if (m.Name != "LoadGameFromUI") continue;

                    try
                    {
                        // patch each found overload
                        var postfix = new HarmonyMethod(typeof(DeathCounterPlugin).GetMethod(nameof(GenericPostfix_AfterLoadGameFromUI), BindingFlags.Static | BindingFlags.NonPublic));
                        _harmony.Patch(m, postfix: postfix);
                        patched++;
                        Logger.LogInfo("DeathCounter: patched method " + m);
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning("DeathCounter: failed to patch " + m + " -> " + e.Message);
                    }
                }

                if (patched == 0)
                {
                    Logger.LogInfo("DeathCounter: no LoadGameFromUI found to patch (fallback polling will work).");
                }
                else
                {
                    Logger.LogInfo($"DeathCounter: applied postfix to {patched} LoadGameFromUI overload(s).");
                }
            }
            catch (Exception e)
            {
                Logger.LogError("DeathCounter: exception in TryPatchAllLoadGameFromUIMethods: " + e);
            }
        }

        // called after each LoadGameFromUI
        private static void GenericPostfix_AfterLoadGameFromUI(object __instance)
        {
            try
            {
                if (__instance == null) return;
                var gmType = __instance.GetType();
                var profileField = gmType.GetField("profileID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (profileField == null)
                {
                    Instance?.Logger.LogWarning("DeathCounter: GameManager has no profileID field.");
                    return;
                }

                int profileID = (int)(profileField.GetValue(__instance) ?? -1);
                Instance?.Logger.LogInfo("DeathCounter: GenericPostfix_AfterLoadGameFromUI detected profileID = " + profileID);
                Instance?.InitPerSaveSection(profileID);
            }
            catch (Exception ex)
            {
                Instance?.Logger.LogError("DeathCounter: exception in GenericPostfix_AfterLoadGameFromUI: " + ex);
            }
        }

        // check if profileID changed in GameManager
        private void TryInitFromGameManagerIfChanged()
        {
            try
            {
                var gmType = AccessTools.TypeByName("GameManager");
                if (gmType == null) return;

                var instField = gmType.GetField("_instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object gmInstance = instField?.GetValue(null);

                if (gmInstance == null)
                {
                    // try through property
                    var instProp = gmType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    gmInstance = instProp?.GetValue(null);
                }

                if (gmInstance == null) return;

                var profileField = gmType.GetField("profileID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (profileField == null) return;

                int profileID = (int)(profileField.GetValue(gmInstance) ?? -1);
                if (profileID < 0) return;

                if (profileID != currentProfileID)
                {
                    Logger.LogInfo("DeathCounter: detected profileID change from " + currentProfileID + " to " + profileID);
                    InitPerSaveSection(profileID);
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning("DeathCounter: exception in TryInitFromGameManagerIfChanged: " + e.Message);
            }
        }

        // setup for specific save file
        private void InitPerSaveSection(int profileID)
        {
            try
            {
                currentProfileID = profileID;
                string section = "Save_" + profileID;
                int starting = TotalDeaths != null ? TotalDeaths.Value : 0;
                TotalDeaths = Config.Bind(section, "TotalDeaths", starting, "Total deaths count (per save).");
                runDeaths = 0;
                Logger.LogInfo("DeathCounter: initialized per-save section: " + section);
            }
            catch (Exception e)
            {
                Logger.LogError("DeathCounter: exception in InitPerSaveSection: " + e);
            }
        }
    }
}
