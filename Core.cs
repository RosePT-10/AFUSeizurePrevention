using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2CppView_Equipment;
using Il2CppView_Main;
using Il2CppCustomUIRendering_Access;
using Il2CppQuantum;
using Il2CppQuantum_Game;
using Il2CppPhoton.Deterministic;
using UnityEngine.EventSystems;
using Il2Cpp;
using Il2CppUI_LEGACY_PlayerHUD;

[assembly: MelonInfo(typeof(AFUSeizurePrevention.Core), "AFUSeizurePrevention", "1.0.0", "taldo", null)]
[assembly: MelonGame("Videocult", "Airframe")]

namespace AFUSeizurePrevention
{
    public class Core : MelonMod
    {
        private MelonPreferences_Category SeizPrevCat;
        private MelonPreferences_Entry<bool> IsEMP;
        private MelonPreferences_Entry<bool> IsRiotStick;
        private MelonPreferences_Entry<bool> IsFlashbang;
        private MelonPreferences_Entry<bool> IsScreenEffect;


        [HarmonyPatch(typeof(EMPgrenade_View), "Draw", [typeof(float)])]
        public class EMPFix
        {
            public static void Postfix(EMPgrenade_View __instance)
            {
                //Melon<Core>.Logger.Msg("detected");
                if (Melon<Core>.Instance.IsEMP.Value == true)
                {
                    __instance.myLight.range = 0;
                }
            }
        }

        [HarmonyPatch(typeof(RiotStick_View), "Draw", [typeof(float)])]
        public class RiotStickFix
        {
            public static void Postfix(RiotStick_View __instance)
            {
                //Melon<Core>.Logger.Msg("detected");
                if (Melon<Core>.Instance.IsRiotStick.Value == true)
                {
                    __instance.myLight.range = 0;
                }
            }
        }
        
        [HarmonyPatch(typeof(Flashbang_View), "Draw", [typeof(float)])]
        public class FlashbangLightFix
        {
            public static void Postfix(Flashbang_View __instance)
            {
                //Melon<Core>.Logger.Msg("detected");
                if (Melon<Core>.Instance.IsFlashbang.Value == true)
                {
                    __instance.myLight.range = 0;
                }
            }
        }

        internal static Camera_View Camera;
        internal static ASCIILabel CoverScreen = null;
        internal static float Darkness = 0.0f;
        internal static bool LinearFade = false;
        const float FADE_TIME = 520.0f;
        public static ASCIILabel? InitScreenCover()
        {
            var hud = GameObject.Find("Scoreboard_Scorebox(Clone)").transform;
            ASCIILabel txt = null;

            foreach (var child in hud)
            {
                if (child.TryCast<Transform>() is Transform tra)
                {
                    if (tra.name == "NameLabel" && tra.TryGetComponent<ASCIILabel>(out var agh))
                    {
                        txt = agh;
                        Melon<Core>.Logger.Msg("Found a match!");
                    }
                }
                else
                {
                    Melon<Core>.Logger.Error("Object not transform");
                }
            }

            if (txt is not null)
            {
                var obj = UnityEngine.Object.Instantiate(txt.transform.gameObject, hud);

                obj.transform.SetParent(hud.parent.parent.parent);
                
                // Position text to cover the entire screen
                obj.transform.localPosition = new Vector3(-1042.966f, 39.8648f, 0f);
                obj.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
                obj.transform.localScale = new Vector3(200f, 100f, 1f);

                var newText = obj.GetComponent<ASCIILabel>();

                // We need to cover the whole screen with a single character, so don't be square!
                newText.Text = "■■■■■■■■■";

                newText.freeColorMode = true;

                newText.gameObject.SetActive(false);
                newText.gameObject.SetActive(true);

                newText.freeColor = Color.black;

                return newText;
            }
            else
            {
                Melon<Core>.Logger.Msg("Could not match text object :(");
            }

            return null;
        }

        [HarmonyPatch(typeof(Camera_View), "UpdateTick")]
        private class GetCam
        {

            public static void Prefix(Camera_View __instance)
            {
                if (!Melon<Core>.Instance.IsFlashbang.Value) return;
                Camera = __instance;

                // If we haven't yet, create a new text object to cover the screen
                if (CoverScreen is null)
                    try
                    {
                        CoverScreen = InitScreenCover();
                    }
                    catch (System.Exception ex)
                    {
                        Melon<Core>.Logger.Error(ex);
                    }

                // __instance.addFlashBang = null;

                if (Darkness > 0.0f)
                {
                    // Melon<Core>.Logger.Msg(Darkness);
                    Darkness = Mathf.Max(0.0f, Darkness - 1.0f);
                }

                if (CoverScreen is not null)
                {
                    CoverScreen.freeColor = new Color(0.0f, 0.0f, 0.0f, Mathf.Min(1.0f, ((float)Darkness) / (LinearFade ? FADE_TIME : 450.0f)));

                    CoverScreen.Resized();
                    CoverScreen.gameObject.SetActive(false);
                    CoverScreen.gameObject.SetActive(true);
                }
            } 
        }

        [HarmonyPatch(typeof(Flashbang_View), "UpdateTick")]
        private class Flash
        {
            public static void Prefix(Flashbang_View __instance)
            {
                if (!Melon<Core>.Instance.IsFlashbang.Value) return;
                // Melon<Core>.Logger.Msg($"flashbangLinear {__instance.explodeFrames} | {__instance.fuse}");

                // Disable flare effects
                foreach (var flare in __instance.flares)
                    flare.enabled = false;

                if (
                    Camera.CheckOnScreen(__instance.transform.position)
                    &&
                    !CustomRaycast.CheckRay
                    (
                        Camera.transform.position,
                        __instance.transform.position,
                        1 << 20 // 20 is the "Terrain" Layer
                    )
                ) {
                    if (__instance.fuse > 0) // About to explode!!!
                    {
                        var max = Mathf.RoundToInt(FADE_TIME);
                        var d = __instance.explodeFrames / 20f;

                        Darkness = Mathf.Min(max, Darkness + d*d * 100.0f);
                        LinearFade = true;
                    }
                    else if (__instance.fuse == 0) // EXPLODED!!
                    {
                        Melon<Core>.Logger.Msg($"flashbang SPLLODED!!!");
                        Darkness = FADE_TIME;
                        LinearFade = false;
                    }
                }
            } 
        }
        
        [HarmonyPatch(typeof(Flashbang_View), "PreRenderEvent")]
        private class PreRender
        {
            public static bool Prefix()
            {
                return false;
            } 
        }
        
        [HarmonyPatch(typeof(DamageImageEffects), "UpdateTick")]
        private class HealthPulseRemoval
        {
            public static void Postfix(DamageImageEffects __instance)
            {
                if (!Melon<Core>.Instance.IsScreenEffect.Value) return;
                __instance.ResetAllEffects();
            } 
        }

        [HarmonyPatch(typeof(DamageImageEffects), "Damage", [typeof(EventDamageApplied)])]
        private class BuggedHealthPulseRemoval
        {
            public static bool Prefix(DamageImageEffects __instance)
            {
                if (!Melon<Core>.Instance.IsScreenEffect.Value) return true;
                return false;
            } 
        }
        
        [HarmonyPatch(typeof(SessionRunner), "Shutdown")]
        private class Shutdown1
        {
            public static void Postfix() // Shutting down runner
            {
                Melon<Core>.Logger.Msg("UnloadScene!");
                CoverScreen = null;
            }
        }


        public override void OnInitializeMelon()
        {
            SeizPrevCat = MelonPreferences.CreateCategory("AFUSeizurePrevention");
            IsEMP = SeizPrevCat.CreateEntry<bool>("EMPFlashDisabled", true);
            IsRiotStick = SeizPrevCat.CreateEntry<bool>("RiotStickFlashDisabled", true);
            IsFlashbang = SeizPrevCat.CreateEntry<bool>("FlashbangsRemoved", true);
            IsScreenEffect = SeizPrevCat.CreateEntry<bool>("RemoveScreenEffects", true);

            LoggerInstance.Msg("Initialized. Bye bye flashbangs!");
        }
    }
}