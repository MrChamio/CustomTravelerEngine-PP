using MelonLoader;
using HarmonyLib;
using Il2Cppplay.day;
using Il2Cppdata;
using Il2CppInterop.Runtime;
using System;
using System.Reflection;

[assembly: MelonInfo(typeof(TravelerDefinitiveScraper.Core), "Definitive Traveler Scraper", "2.0.0", "MrChamio")]

namespace TravelerDefinitiveScraper
{
    public class Core : MelonMod 
    {
        public static int travelerCounter = 0;

        public override void OnInitializeMelon() 
        {
            MelonLogger.Msg("==================================================");
            MelonLogger.Msg("DEFINITIVE TRAVELER SCRAPER ACTIVATED");
            MelonLogger.Msg("Read-only mode. Waiting for travelers...");
            MelonLogger.Msg("==================================================");
        }
    }

    // PHASE 1: Seed Birth (Vars and IDs extraction)
    [HarmonyPatch(typeof(TravelerContext), nameof(TravelerContext.makeTravelerSpec))]
    public class ScraperPhase1_Seed
    {
        public static void Postfix(ref TravelerSpec __result)
        {
            if (__result == null || __result.vars == null) return;
            Core.travelerCounter++;

            MelonLogger.Msg($"\n\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\");
            MelonLogger.Msg($"========== [NPC #{Core.travelerCounter}] PHASE 1: SEED ==========");
            
            MelonLogger.Msg($"[INTERNAL ID] {__result.id}");
            
            try { MelonLogger.Msg($"[SPEC ERROR] {__result.get_error() ?? "None"}"); } catch { }
            if (__result.customError != null) MelonLogger.Msg($"[CUSTOM ERROR ID] {__result.customError.id}");

            MelonLogger.Msg("\n--- NATIVE VARIABLES (VARS) ---");
            var keys = __result.vars._keys;
            var vals = __result.vars.vals;
            bool isSpecial = false;

            for (int i = 0; i < __result.vars.nBuckets; i++) 
            {
                if (!string.IsNullOrEmpty(keys[i])) 
                {
                    string valStr = "null";
                    if (vals[i] != null) 
                    {
                        try { valStr = IL2CPP.Il2CppStringToManaged(vals[i].Pointer); } 
                        catch { valStr = "[Complex Object/Pointer]" ; }
                    }
                    if (keys[i] == "special" && !string.IsNullOrEmpty(valStr)) isSpecial = true;

                    MelonLogger.Msg($"[{keys[i]}] = {valStr}");
                }
            }

            if (isSpecial) MelonLogger.Msg("\n>>> THIS IS A SPECIAL STORY NPC <<<");
            MelonLogger.Msg("=================================================");
        }
    }

    // PHASE 2 & 3: Errors and Appearance
    [HarmonyPatch(typeof(Traveler), nameof(Traveler.__hx_ctor_play_day_Traveler))]
    public class ScraperPhase2and3_ErrorsAndFace
    {
        public static void Prefix(TravelerSpec spec, Il2Cppdata.Error error)
        {
            MelonLogger.Msg($"\n========== [NPC #{Core.travelerCounter}] PHASE 2: ERRORS ==========");
            MelonLogger.Msg($"[ERROR ID] {(error != null ? error.id : "None")}");
            MelonLogger.Msg($"[ERROR GROUP] {(error != null ? error.groupId : "None")}");
            MelonLogger.Msg("=================================================");
        }

        public static void Postfix(Traveler __0)
        {
            if (__0 == null || __0.face == null) return;

            MelonLogger.Msg($"\n========== [NPC #{Core.travelerCounter}] PHASE 3: FACE & FACESPEC ==========");
            try
            {
                if (__0.face.spec != null)
                {
                    var spec = __0.face.spec;
                    MelonLogger.Msg("--- FACESPEC PROPERTIES ---");
                    MelonLogger.Msg($"male = {spec.male}");
                    MelonLogger.Msg($"shoulders = {spec.shoulders}");
                    MelonLogger.Msg($"head = {spec.head}");
                    MelonLogger.Msg($"eyes = {spec.eyes}");
                    MelonLogger.Msg($"noseMouth = {spec.noseMouth}");
                    MelonLogger.Msg($"palette = {spec.palette}");
                    MelonLogger.Msg($"flip = {spec.flip}");

                    string genderLetter = spec.male ? "M" : "F";
                    string jsonFaceString = $"{genderLetter}-{spec.shoulders}-{spec.head}-{spec.eyes}-{spec.noseMouth}-{spec.palette}";

                    MelonLogger.Msg("\n--- JSON FACE STRING ---");
                    MelonLogger.Msg($"\"face\": \"{jsonFaceString}\"");
                }
            }
            catch (Exception e) { MelonLogger.Error($"Error reading Face: {e.Message}"); }
            MelonLogger.Msg("=================================================");
            MelonLogger.Msg($"//////////////////////////////////////////////////\n");
        }
    }
}