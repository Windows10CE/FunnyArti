using BepInEx;
using BepInEx.Configuration;
using EntityStates.Mage;
using HarmonyLib;
using HG;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2.ContentManagement;
using RoR2.Skills;
using UnityEngine;

namespace FunnyArti;

[BepInPlugin(ModGuid, "FunnyArti", "1.0.1")]
// doesnt actually depend on R2API, i just want to be in the network modlist and this is the easiest way
[BepInDependency(R2API.R2API.PluginGUID)]
[HarmonyPatch]
public sealed class FunnyArtiPlugin : BaseUnityPlugin
{
    public const string ModGuid = "com.Windows10CE.FunnyArti";

    private readonly Harmony HarmonyInstance = new(ModGuid);

    private Action<ReadOnlyArray<ReadOnlyContentPack>> contentPackLoadHookAction;

    private float? backupRechargeTime;

    private static ConfigEntry<float> configRechargeTime;
    private static ConfigEntry<float> configVelocityMultiplier;

    private void Awake()
    {
        contentPackLoadHookAction = ContentPackLoadHook;
        configRechargeTime = Config.Bind("IonSurge", "RechargeTime", 6f, "Recharge time for ion surge.");
        configVelocityMultiplier = Config.Bind("IonSurge", "VelocityMultiplier", 1.25f, 
            """
            Multiplier for velocity.
            Based on the length of base game ion surge.
            """
        );
    }
    
    private void OnEnable()
    {
        HarmonyInstance.PatchAll(typeof(FunnyArtiPlugin).Assembly);

        ContentManager.onContentPacksAssigned += contentPackLoadHookAction;
    }

    private void OnDisable()
    {
        HarmonyInstance.UnpatchSelf();

        ContentManager.onContentPacksAssigned -= contentPackLoadHookAction;

        if (backupRechargeTime != null)
        {
            SkillDef skill = ContentManager.skillDefs?.FirstOrDefault(s => s.skillNameToken == "MAGE_SPECIAL_LIGHTNING_NAME");
            if (skill != null)
            {
                skill.baseRechargeInterval = backupRechargeTime.Value;
                backupRechargeTime = null;
            }
        }
    }

    private void ContentPackLoadHook(ReadOnlyArray<ReadOnlyContentPack> packs)
    {
        SkillDef skill = ContentManager.skillDefs.First(s => s.skillNameToken == "MAGE_SPECIAL_LIGHTNING_NAME");
        backupRechargeTime = skill.baseRechargeInterval;
        skill.baseRechargeInterval = configRechargeTime.Value;
    }

    [HarmonyILManipulator]
    [HarmonyPatch(typeof(FlyUpState), nameof(FlyUpState.OnEnter))]
    private static void FixFlyVectorIL(ILContext il)
    {
        ILCursor c = new(il);

        c.GotoNext(x => x.MatchStfld(AccessTools.DeclaredField(typeof(FlyUpState), nameof(FlyUpState.flyVector))));

        c.Emit(OpCodes.Pop);
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate(Vector3 (FlyUpState state) => state.GetAimRay().direction * configVelocityMultiplier.Value);
    }
}
