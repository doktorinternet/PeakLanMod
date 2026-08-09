using HarmonyLib;
using PeakLanMod.Lan.Services;

namespace PeakLanMod.Patches;

[HarmonyPatch(
    typeof(CharacterCustomization),
    nameof(CharacterCustomization.Start))]
internal static class CharacterCustomizationStartPersistencePatch
{
    [HarmonyPostfix]
    private static void Postfix(
        CharacterCustomization __instance)
    {
        LanRuntimeContext.Services.CustomizationPersistence
            .TryRestoreLocalCustomization(
                __instance,
                "CharacterCustomization.Start");
    }
}

[HarmonyPatch(
    typeof(CharacterCustomization),
    nameof(CharacterCustomization.OnPlayerDataChange),
    [typeof(PersistentPlayerData)])]
internal static class CharacterCustomizationOnPlayerDataChangePersistencePatch
{
    [HarmonyPostfix]
    private static void Postfix(
        CharacterCustomization __instance)
    {
        LanRuntimeContext.Services.CustomizationPersistence
            .TryRestoreLocalCustomization(
                __instance,
                "CharacterCustomization.OnPlayerDataChange");
    }
}

[HarmonyPatch]
internal static class CharacterCustomizationCapturePersistencePatch
{
    [HarmonyPatch(
        typeof(CharacterCustomization),
        nameof(CharacterCustomization.SetCharacterSkinColor),
        [typeof(int)])]
    [HarmonyPostfix]
    private static void AfterSetCharacterSkinColor(
        CharacterCustomization __instance)
    {
        Capture(__instance, "SetCharacterSkinColor");
    }

    [HarmonyPatch(
        typeof(CharacterCustomization),
        nameof(CharacterCustomization.SetCharacterEyes),
        [typeof(int)])]
    [HarmonyPostfix]
    private static void AfterSetCharacterEyes(
        CharacterCustomization __instance)
    {
        Capture(__instance, "SetCharacterEyes");
    }

    [HarmonyPatch(
        typeof(CharacterCustomization),
        nameof(CharacterCustomization.SetCharacterMouth),
        [typeof(int)])]
    [HarmonyPostfix]
    private static void AfterSetCharacterMouth(
        CharacterCustomization __instance)
    {
        Capture(__instance, "SetCharacterMouth");
    }

    [HarmonyPatch(
        typeof(CharacterCustomization),
        nameof(CharacterCustomization.SetCharacterAccessory),
        [typeof(int)])]
    [HarmonyPostfix]
    private static void AfterSetCharacterAccessory(
        CharacterCustomization __instance)
    {
        Capture(__instance, "SetCharacterAccessory");
    }

    [HarmonyPatch(
        typeof(CharacterCustomization),
        nameof(CharacterCustomization.SetCharacterOutfit),
        [typeof(int)])]
    [HarmonyPostfix]
    private static void AfterSetCharacterOutfit(
        CharacterCustomization __instance)
    {
        Capture(__instance, "SetCharacterOutfit");
    }

    [HarmonyPatch(
        typeof(CharacterCustomization),
        nameof(CharacterCustomization.SetCharacterHat),
        [typeof(int)])]
    [HarmonyPostfix]
    private static void AfterSetCharacterHat(
        CharacterCustomization __instance)
    {
        Capture(__instance, "SetCharacterHat");
    }

    [HarmonyPatch(
        typeof(CharacterCustomization),
        nameof(CharacterCustomization.SetCharacterSash),
        [typeof(int)])]
    [HarmonyPostfix]
    private static void AfterSetCharacterSash(
        CharacterCustomization __instance)
    {
        Capture(__instance, "SetCharacterSash");
    }

    private static void Capture(
        CharacterCustomization customization,
        string source)
    {
        LanRuntimeContext.Services.CustomizationPersistence
            .TryCaptureLocalCustomization(
                customization,
                $"CharacterCustomization.{source}");
    }
}
