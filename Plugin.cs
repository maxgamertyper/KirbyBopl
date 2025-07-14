using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

namespace Kirby
{
    [BepInPlugin("com.maxgamertyper1.kirby", "Kirby", "1.0.0")]
    public class Kirby : BaseUnityPlugin
    {
        internal static ConfigFile config;
        internal static ConfigEntry<bool> OnLastClone;
        internal static ConfigEntry<bool> TakeAbilities;
        internal static ConfigEntry<bool> TakeSize;
        internal static ConfigEntry<bool> TakeColor;

        private void Log(string message)
        {
            Logger.LogInfo(message);
        }

        private void Awake()
        {
            // Plugin startup logic
            Log($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            DoPatching();
            config = ((BaseUnityPlugin)this).Config;
            OnLastClone = config.Bind<bool>("Absolute", "On Last Revive", true, "whether to apply the changes to the player eating if the victim still has revives");
            TakeAbilities = config.Bind<bool>("Relative", "Take Abilities", true, "when the victim is dead or full dead, whether to take their abilities and replace the current ones");
            TakeColor = config.Bind<bool>("Relative", "Take Color", false, "when the victim is dead or full dead, whether to take their color");
            TakeSize = config.Bind<bool>("Relative", "Take Size", true, "when the victim is dead or full dead, whether to change the size of the killer with the victim");
        }

        private void DoPatching()
        {
            var harmony = new Harmony("com.maxgamertyper1.kirby");

            Patch(harmony, typeof(SlimeController), "Chew", "MunchingPatch", true, false);
            Patch(harmony, typeof(SlimeController), "DropAbilities", "IgnoreDropPatch", true, false);
            Patch(harmony, typeof(PlayerCollision), "killPlayer", "PlayerDeathPatch", true, false);
        }

        private void Patch(Harmony harmony, Type OriginalClass, string OriginalMethod, string PatchMethod, bool prefix, bool transpiler)
        {
            MethodInfo MethodToPatch = AccessTools.Method(OriginalClass, OriginalMethod); // the method to patch
            MethodInfo Patch = AccessTools.Method(typeof(Patches), PatchMethod);

            if (prefix)
            {
                harmony.Patch(MethodToPatch, new HarmonyMethod(Patch));
            }
            else
            {
                if (transpiler)
                {
                    harmony.Patch(MethodToPatch, null, null, new HarmonyMethod(Patch));
                }
                else
                {
                    harmony.Patch(MethodToPatch, null, new HarmonyMethod(Patch));
                }
            }
            Log($"Patched {OriginalMethod} in {OriginalClass.ToString()}");
        }
    }

    public class Patches
    {
        public static bool IgnoreDropThisFrame = false;

        public static bool IgnoreDropPatch(ref SlimeController __instance)
        {
            if (IgnoreDropThisFrame)
            {
                IgnoreDropThisFrame = false;
                return false;
            }
            return true;
        }
        public static void PlayerDeathPatch(ref PlayerCollision __instance, CauseOfDeath causeOfDeath = CauseOfDeath.Other)
        {
            if (causeOfDeath == CauseOfDeath.Eaten)
            {
                IgnoreDropThisFrame = true;
            }
        }
        public static void MunchingPatch(ref SlimeController __instance)
        {
            SlimeController slimeControllerTarget = __instance.playerToEat;
            PlayerCollision playerCollisionTarget = (slimeControllerTarget != null) ? slimeControllerTarget.GetActivePlayerCollision() : null;
            Player me = PlayerHandler.Get().GetPlayer(__instance.playerNumber);
            Player yummyPerson = PlayerHandler.Get().GetPlayer(slimeControllerTarget.GetPlayerId());

            if (Kirby.OnLastClone.Value && yummyPerson.RespawnPositions.Count > 0)
            {
                return;
            }
            System.Diagnostics.Debug.Print(me.playersAndClonesStillAlive.ToString());

            if (Kirby.OnLastClone.Value && me.playersAndClonesStillAlive >= 1)
            {
                return;
            }



            if (Kirby.TakeColor.Value)
            {
                Material DeadColor = yummyPerson.Color;
                __instance.GetPlayerSprite().material = DeadColor;
            }

            if (Kirby.TakeSize.Value)
            {
                Fix deadPlayerScale = yummyPerson.Scale;
                me.Scale = deadPlayerScale;
            }

            if (Kirby.TakeAbilities.Value)
            {
                __instance.abilities = new List<AbilityMonoBehaviour>();
                me.CurrentAbilities = yummyPerson.CurrentAbilities;

                foreach (AbilityReadyIndicator abilityReadyIndicator in __instance.AbilityReadyIndicators)
                {
                    UnityEngine.Object.Destroy(abilityReadyIndicator.gameObject);
                }

                for (int i = 0; i < slimeControllerTarget.abilities.Count(); i++)
                {
                    string abilityname = slimeControllerTarget.abilities[i].gameObject.name.Replace("(Clone)", "");
                    NamedSprite abilitySprite;
                    try
                    {
                        abilitySprite = __instance.abilityIcons.sprites[__instance.abilityIcons.IndexOf(abilityname)];
                    }
                    catch { System.Diagnostics.Debug.Print("Could not find ability"); return; }

                    AbilityMonoBehaviour mono = FixTransform.InstantiateFixed(abilitySprite.associatedGameObject, Vec2.zero, Fix.Zero).GetComponent<AbilityMonoBehaviour>();
                    __instance.AddAdditionalAbility(mono, abilitySprite.sprite, abilitySprite.associatedGameObject);
                }
            }
        }
    }
}
