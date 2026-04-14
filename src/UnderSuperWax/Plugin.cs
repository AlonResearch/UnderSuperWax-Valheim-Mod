using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace UnderSuperWax;

[BepInPlugin(ModGuid, ModName, ModVersion)]
[BepInDependency("com.jotunn.jotunn")]
public sealed class UnderSuperWaxPlugin : BaseUnityPlugin
{
    public const string ModGuid = "com.aloncifer.undersuperwax";
    public const string ModName = "UnderSuperWax";
    public const string ModVersion = "0.0.2";

    internal const string ZdoKey = "UnderSuperWax_Waxed_Final_v1";
    internal const string LegacyZdoKey = "Jasterlee_Waxed_Final_v4";
    internal const string ItemPrefabName = "Beewax";
    internal const string ToolPrefabName = "UnderSuperWaxTool";
    internal const string LegacyToolPrefabName = "BeewaxTool";
    internal const string RpcRequestApplyWax = "USW_RPC_RequestApplyWax_v1";
    internal const string RpcWaxApplyResult = "USW_RPC_WaxApplyResult_v1";

    internal static ConfigEntry<float> ShineGlossiness = null!;
    internal static ConfigEntry<float> ShineMetallic = null!;

    private readonly Harmony harmony = new(ModGuid);

    private void Awake()
    {
        ShineGlossiness = Config.Bind("Visuals", "Glossiness", 0.4f, "Wax glossiness (dimmer for subtle shine)");
        ShineMetallic = Config.Bind("Visuals", "Metallic", 0.08f, "Metallic reflection (dimmer for subtle shine)");

        harmony.PatchAll(typeof(UnderSuperWaxPlugin).Assembly);
        PrefabManager.OnPrefabsRegistered += AddCustomContent;
    }

    private void OnDestroy()
    {
        PrefabManager.OnPrefabsRegistered -= AddCustomContent;
        harmony.UnpatchSelf();
    }

    private void AddCustomContent()
    {
        PrefabManager.OnPrefabsRegistered -= AddCustomContent;

        try
        {
            AssetBundle? bundle = LoadAssetBundle("UnderSuperWax.Resources.beewax_bundle");
            if (bundle == null)
            {
                Logger.LogError("[UnderSuperWax] Failed to load beewax bundle.");
                return;
            }

            GameObject modelPrefab = bundle.LoadAsset<GameObject>("assets/beewax_model_v1.prefab");
            if (modelPrefab == null)
            {
                Logger.LogError("[UnderSuperWax] Prefab 'assets/beewax_model_v1.prefab' not found.");
                bundle.Unload(true);
                return;
            }

            Sprite? icon = LoadEmbeddedSprite("UnderSuperWax.Resources.beewax_icon.png");

            ItemConfig itemConfig = new()
            {
                Name = "Beewax",
                Description = "Pure beeswax. Permanent waterproofing for wood and submerged structures.",
                CraftingStation = "piece_workbench",
                Amount = 2,
                Icons = icon == null ? null : new[] { icon }
            };
            itemConfig.AddRequirement(new RequirementConfig("Honey", 2, 0, true));
            itemConfig.AddRequirement(new RequirementConfig("Resin", 4, 0, true));

            CustomItem item = new(ItemPrefabName, "Honey", itemConfig);
            if (item.ItemDrop != null)
            {
                item.ItemDrop.m_itemData.m_shared.m_food = 10f;
                item.ItemDrop.m_itemData.m_shared.m_foodStamina = 42f;

                Transform? defaultModel = item.ItemDrop.transform.Find("model");
                if (defaultModel != null)
                {
                    UnityEngine.Object.DestroyImmediate(defaultModel.gameObject);
                }

                GameObject modelInstance = UnityEngine.Object.Instantiate(modelPrefab, item.ItemDrop.transform);
                modelInstance.name = "model";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                ApplyShine(modelInstance.GetComponentsInChildren<Renderer>(true));
                ItemManager.Instance.AddItem(item);
                Logger.LogInfo("[UnderSuperWax] Beewax registered successfully.");
            }

            PieceConfig pieceConfig = new()
            {
                Name = "Apply Super Wax",
                PieceTable = "_HammerPieceTable",
                Category = "Misc",
                Icon = icon
            };
            pieceConfig.Requirements = new[]
            {
                new RequirementConfig(ItemPrefabName, 1, 0, true)
            };

            PieceManager.Instance.AddPiece(new CustomPiece(ToolPrefabName, "piece_repair", pieceConfig));
            bundle.Unload(false);
        }
        catch (Exception exception)
        {
            Logger.LogError($"[UnderSuperWax] Error registering content: {exception.Message}\n{exception.StackTrace}");
        }
    }

    private static void ApplyShine(Renderer[] renderers)
    {
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            foreach (Material material in materials)
            {
                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", ShineMetallic.Value);
                }

                if (material.HasProperty("_Glossiness"))
                {
                    material.SetFloat("_Glossiness", ShineGlossiness.Value);
                }
            }
        }
    }

    private static AssetBundle? LoadAssetBundle(string resourceName)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        return AssetBundle.LoadFromStream(stream);
    }

    private static Sprite? LoadEmbeddedSprite(string resourceName)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }

        byte[] buffer = new byte[stream.Length];
        _ = stream.Read(buffer, 0, buffer.Length);

        Texture2D texture = new(2, 2);
        if (!ImageConversion.LoadImage(texture, buffer))
        {
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }
}

internal sealed class UnderSuperWaxProtection : MonoBehaviour
{
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

    private WearNTear? wearNTear;
    private Piece? piece;
    private ZNetView? zNetView;
    private Renderer[]? renderers;
    private MaterialPropertyBlock? propertyBlock;
    private bool pendingLocalApply;
    private Vector3 pendingEffectPosition;

    private void Awake()
    {
        wearNTear = GetComponent<WearNTear>();
        piece = GetComponent<Piece>();
        zNetView = GetComponent<ZNetView>();
        renderers = GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();

        if (zNetView != null && zNetView.IsValid())
        {
            zNetView.Register(UnderSuperWaxPlugin.RpcRequestApplyWax, RPC_RequestApplyWax);
            zNetView.Register<string>(UnderSuperWaxPlugin.RpcWaxApplyResult, RPC_WaxApplyResult);
        }
    }

    private void Start()
    {
        RefreshState();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    internal bool IsWaxed()
    {
        if (zNetView == null || !zNetView.IsValid())
        {
            return false;
        }

        ZDO? zdo = zNetView.GetZDO();
        if (zdo == null)
        {
            return false;
        }

        if (zdo.GetBool(UnderSuperWaxPlugin.ZdoKey, false))
        {
            return true;
        }

        bool legacyWaxed = zdo.GetBool(UnderSuperWaxPlugin.LegacyZdoKey, false);
        if (legacyWaxed)
        {
            TryMigrateLegacyWaxedState(zdo);
            return true;
        }

        return false;
    }

    internal void SetWaxed(bool waxed)
    {
        if (zNetView == null || !zNetView.IsValid())
        {
            return;
        }

        ZDO? zdo = zNetView.GetZDO();
        if (zdo == null)
        {
            return;
        }

        zdo.Set(UnderSuperWaxPlugin.ZdoKey, waxed);
        RefreshState();
    }

    internal void RefreshState()
    {
        ApplyVisuals(IsWaxed());
    }

    internal void Cleanup()
    {
        ClearVisuals();
        pendingLocalApply = false;
        wearNTear = null;
        piece = null;
        zNetView = null;
        renderers = null;
        propertyBlock = null;
    }

    internal static bool IsWaxed(WearNTear? wearNTear)
    {
        if (wearNTear == null)
        {
            return false;
        }

        UnderSuperWaxProtection? protection = wearNTear.GetComponent<UnderSuperWaxProtection>();
        if (protection != null)
        {
            return protection.IsWaxed();
        }

        ZNetView? zNetView = wearNTear.GetComponent<ZNetView>();
        if (zNetView == null || !zNetView.IsValid())
        {
            return false;
        }

        ZDO? zdo = zNetView.GetZDO();
        return zdo != null && (zdo.GetBool(UnderSuperWaxPlugin.ZdoKey, false) || zdo.GetBool(UnderSuperWaxPlugin.LegacyZdoKey, false));
    }

    private void TryMigrateLegacyWaxedState(ZDO zdo)
    {
        if (zNetView == null || !zNetView.IsOwner())
        {
            return;
        }

        if (!zdo.GetBool(UnderSuperWaxPlugin.ZdoKey, false))
        {
            zdo.Set(UnderSuperWaxPlugin.ZdoKey, true);
        }
    }

    internal bool TryRequestWaxApply(Vector3 effectPosition)
    {
        if (zNetView == null || !zNetView.IsValid() || IsWaxed() || pendingLocalApply)
        {
            return false;
        }

        pendingLocalApply = true;
        pendingEffectPosition = effectPosition;
        zNetView.InvokeRPC(ZNetView.Everybody, UnderSuperWaxPlugin.RpcRequestApplyWax);
        return true;
    }

    private void RPC_RequestApplyWax(long sender)
    {
        if (zNetView == null || !zNetView.IsValid() || !zNetView.IsOwner())
        {
            return;
        }

        string result = "failed";
        if (wearNTear == null || !UnderSuperWaxRules.IsEligibleMaterial(wearNTear))
        {
            result = "invalid";
        }
        else if (IsWaxed())
        {
            result = "already";
        }
        else
        {
            SetWaxed(true);
            result = "ok";
        }

        zNetView.InvokeRPC(sender, UnderSuperWaxPlugin.RpcWaxApplyResult, result);
    }

    private void RPC_WaxApplyResult(long sender, string result)
    {
        if (!pendingLocalApply)
        {
            return;
        }

        pendingLocalApply = false;
        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer == null)
        {
            return;
        }

        if (result == "ok")
        {
            localPlayer.GetInventory().RemoveItem(UnderSuperWaxPlugin.ItemPrefabName, 1, -1, true);
            TriggerLocalApplyFeedback(localPlayer, pendingEffectPosition);
            localPlayer.Message((MessageHud.MessageType)2, "Piece waxed!", 0, null);
            RefreshState();
            return;
        }

        if (result == "already")
        {
            localPlayer.Message((MessageHud.MessageType)2, "Already waxed!", 0, null);
            return;
        }

        if (result == "invalid")
        {
            localPlayer.Message((MessageHud.MessageType)2, "Can only wax wood pieces", 0, null);
            return;
        }

        localPlayer.Message((MessageHud.MessageType)2, "Wax application failed", 0, null);
    }

    private static void TriggerLocalApplyFeedback(Player player, Vector3 effectPosition)
    {
        if (ZNetScene.instance != null)
        {
            GameObject? hitSparks = ZNetScene.instance.GetPrefab("vfx_HitSparks");
            GameObject? buildWood = ZNetScene.instance.GetPrefab("sfx_build_hammer_wood");

            if (hitSparks != null)
            {
                UnityEngine.Object.Instantiate(hitSparks, effectPosition, Quaternion.identity);
            }

            if (buildWood != null)
            {
                UnityEngine.Object.Instantiate(buildWood, effectPosition, Quaternion.identity);
            }
        }

        GameObject? visual = player.GetVisual();
        if (visual != null)
        {
            visual.SendMessageUpwards("SetTrigger", "attack", SendMessageOptions.DontRequireReceiver);
        }
    }

    private void ApplyVisuals(bool waxed)
    {
        if (renderers == null || propertyBlock == null)
        {
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!waxed)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(MetallicId, UnderSuperWaxPlugin.ShineMetallic.Value);
            propertyBlock.SetFloat(GlossinessId, UnderSuperWaxPlugin.ShineGlossiness.Value);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ClearVisuals()
    {
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.SetPropertyBlock(null);
            }
        }
    }
}

internal static class UnderSuperWaxRules
{
    internal static bool IsEligibleMaterial(WearNTear wearNTear)
    {
        return wearNTear != null && ((int)wearNTear.m_materialType == 0 || (int)wearNTear.m_materialType == 3);
    }
}

[HarmonyPatch(typeof(WearNTear), "Awake")]
internal static class WearNTear_Awake_Patch
{
    [HarmonyPostfix]
    private static void Postfix(WearNTear __instance)
    {
        if (!UnderSuperWaxRules.IsEligibleMaterial(__instance))
        {
            return;
        }

        UnderSuperWaxProtection protection = __instance.GetComponent<UnderSuperWaxProtection>();
        if (protection == null)
        {
            protection = __instance.gameObject.AddComponent<UnderSuperWaxProtection>();
        }

        protection.RefreshState();
    }
}

[HarmonyPatch(typeof(WearNTear), "IsWet")]
internal static class WearNTear_IsWet_Patch
{
    [HarmonyPostfix]
    private static void Postfix(WearNTear __instance, ref bool __result)
    {
        if (UnderSuperWaxProtection.IsWaxed(__instance))
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(WearNTear), "IsUnderWater")]
internal static class WearNTear_IsUnderWater_Patch
{
    [HarmonyPostfix]
    private static void Postfix(WearNTear __instance, ref bool __result)
    {
        if (UnderSuperWaxProtection.IsWaxed(__instance))
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(WearNTear), "HaveRoof")]
internal static class WearNTear_HaveRoof_Patch
{
    [HarmonyPostfix]
    private static void Postfix(WearNTear __instance, ref bool __result)
    {
        if (UnderSuperWaxProtection.IsWaxed(__instance))
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(WearNTear), "OnDestroy")]
internal static class WearNTear_OnDestroy_Patch
{
    [HarmonyPostfix]
    private static void Postfix(WearNTear __instance)
    {
        UnderSuperWaxProtection? protection = __instance.GetComponent<UnderSuperWaxProtection>();
        if (protection != null)
        {
            protection.Cleanup();
        }
    }
}

[HarmonyPatch(typeof(Player), "UpdatePlacement")]
internal static class Player_UpdatePlacement_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(Player __instance)
    {
        if (__instance != Player.m_localPlayer)
        {
            return true;
        }

        Piece? selectedPiece = __instance.GetSelectedPiece();
        if (selectedPiece == null)
        {
            return true;
        }

        string selectedName = selectedPiece.gameObject.name;
        if (selectedName != UnderSuperWaxPlugin.ToolPrefabName && selectedName != UnderSuperWaxPlugin.LegacyToolPrefabName)
        {
            return true;
        }

        // Only intercept the exact left-click wax apply action.
        // Let vanilla placement input (including mouse2 cancel/back) run otherwise.
        if (InventoryGui.IsVisible() || !UnityEngine.Input.GetMouseButtonDown(0))
        {
            return true;
        }

        if (GameCamera.instance == null)
        {
            return false;
        }

        RaycastHit hit = default;
        if (!Physics.Raycast(GameCamera.instance.transform.position, GameCamera.instance.transform.forward, out hit, 15f, LayerMask.GetMask("piece")))
        {
            return false;
        }

        ZNetView? zNetView = hit.collider.GetComponentInParent<ZNetView>();
        WearNTear? wearNTear = hit.collider.GetComponentInParent<WearNTear>();
        if (zNetView == null || !zNetView.IsValid() || wearNTear == null)
        {
            __instance.Message((MessageHud.MessageType)2, "Can only wax wood pieces", 0, null);
            return false;
        }

        if (!UnderSuperWaxRules.IsEligibleMaterial(wearNTear))
        {
            __instance.Message((MessageHud.MessageType)2, "Can only wax wood pieces", 0, null);
            return false;
        }

        if (UnderSuperWaxProtection.IsWaxed(wearNTear))
        {
            __instance.Message((MessageHud.MessageType)2, "Already waxed!", 0, null);
            return false;
        }

        if (!__instance.GetInventory().HaveItem(UnderSuperWaxPlugin.ItemPrefabName, true))
        {
            __instance.Message((MessageHud.MessageType)2, "You need Beewax to apply!", 0, null);
            return false;
        }

        UnderSuperWaxProtection protection = wearNTear.GetComponent<UnderSuperWaxProtection>();
        if (protection == null)
        {
            protection = wearNTear.gameObject.AddComponent<UnderSuperWaxProtection>();
        }

        if (!protection.TryRequestWaxApply(hit.point))
        {
            __instance.Message((MessageHud.MessageType)2, "Wax application failed", 0, null);
        }
        return false;
    }
}
