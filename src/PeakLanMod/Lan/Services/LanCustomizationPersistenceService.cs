using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

namespace PeakLanMod.Lan.Services;

internal sealed class LanCustomizationPersistenceService : ILanCustomizationPersistenceService
{
    private const string CachePrefix = "PeakLanMod.CustomizationCache";
    private const string IdentityIndexKey = CachePrefix + ".index";
    private const string ActiveIdentityKey = CachePrefix + ".active_identity";
    private const string MigrationCompletedKey = CachePrefix + ".migration.identityless.completed";
    private static readonly string[] CacheParts =
    [
        "skin",
        "accessory",
        "eyes",
        "mouth",
        "outfit",
        "hat",
        "sash"
    ];

    private readonly ILanPluginOptions _options;
    private readonly HashSet<int> _restoreEvaluatedInstanceIds = [];
    private bool? _lastKnownFeatureEnabled;

    internal LanCustomizationPersistenceService(ILanPluginOptions options)
    {
        _options = options;
        ApplyPersistencePolicy("LanCustomizationPersistenceService.Ctor");
        TryRunIdentitylessMigration("LanCustomizationPersistenceService.Ctor");
    }

    public void TryCaptureLocalCustomization(
        CharacterCustomization customization,
        string source)
    {
        ApplyPersistencePolicy(source);

        if (!IsEnabled())
        {
            return;
        }

        PhotonPlayer? localPlayer = PhotonNetwork.LocalPlayer;

        if (localPlayer is null)
        {
            return;
        }

        CharacterCustomizationData? customizationData =
            CharacterCustomization.GetCustomizationData(localPlayer);

        if (customizationData is null)
        {
            return;
        }

        WriteCache(customizationData);

        Plugin.Log.LogInfo(
            $"{source}: captured local customization cache. " +
            $"Data={FormatCustomizationData(customizationData)}");
    }

    public void TryRestoreLocalCustomization(
        CharacterCustomization customization,
        string source)
    {
        ApplyPersistencePolicy(source);

        if (!IsEnabled())
        {
            return;
        }

        int instanceId = customization.GetInstanceID();

        if (_restoreEvaluatedInstanceIds.Contains(instanceId))
        {
            return;
        }

        if (!TryReadCache(
                out CharacterCustomizationData cachedData))
        {
            return;
        }

        PhotonPlayer? localPlayer = PhotonNetwork.LocalPlayer;

        if (localPlayer is null)
        {
            return;
        }

        CharacterCustomizationData? currentData =
            CharacterCustomization.GetCustomizationData(localPlayer);

        if (currentData is not null
            && !IsLikelyDefaultCustomization(currentData))
        {
            _restoreEvaluatedInstanceIds.Add(instanceId);
            return;
        }

        CharacterCustomization.SetCustomizationData(
            cachedData,
            localPlayer);

        _restoreEvaluatedInstanceIds.Add(instanceId);

        if (_restoreEvaluatedInstanceIds.Count > 512)
        {
            _restoreEvaluatedInstanceIds.Clear();
        }

        Plugin.Log.LogInfo(
            $"{source}: restored local customization from cache. " +
            $"Data={FormatCustomizationData(cachedData)}");
    }

    private bool IsEnabled()
    {
        return LanRuntimeContext.IsLanServerMode
            && _options.PersistCustomizationSelectionOffline.Value;
    }

    private void ApplyPersistencePolicy(string source)
    {
        bool isEnabled = _options.PersistCustomizationSelectionOffline.Value;

        if (_lastKnownFeatureEnabled == isEnabled)
        {
            return;
        }

        _lastKnownFeatureEnabled = isEnabled;

        if (isEnabled)
        {
            Plugin.Log.LogInfo(
                $"{source}: customization cache feature enabled.");
            return;
        }

        int removedKeyCount = ClearAllCachedKeys();

        Plugin.Log.LogInfo(
            $"{source}: customization cache feature disabled. " +
            $"RemovedKeys={removedKeyCount}.");
    }

    private static bool IsLikelyDefaultCustomization(
        CharacterCustomizationData data)
    {
        return data.currentSkin == 0
            && data.currentAccessory == 0
            && data.currentEyes == 0
            && data.currentMouth == 0
            && data.currentOutfit == 0
            && data.currentHat == 0
            && data.currentSash == 0;
    }

    private static void WriteCache(CharacterCustomizationData data)
    {
        PlayerPrefs.SetInt(GetKey("skin"), data.currentSkin);
        PlayerPrefs.SetInt(GetKey("accessory"), data.currentAccessory);
        PlayerPrefs.SetInt(GetKey("eyes"), data.currentEyes);
        PlayerPrefs.SetInt(GetKey("mouth"), data.currentMouth);
        PlayerPrefs.SetInt(GetKey("outfit"), data.currentOutfit);
        PlayerPrefs.SetInt(GetKey("hat"), data.currentHat);
        PlayerPrefs.SetInt(GetKey("sash"), data.currentSash);
        PlayerPrefs.Save();
    }

    private int ClearAllCachedKeys()
    {
        int removed = 0;

        for (int partIndex = 0; partIndex < CacheParts.Length; partIndex++)
        {
            string key = GetKey(CacheParts[partIndex]);

            if (!PlayerPrefs.HasKey(key))
            {
                continue;
            }

            PlayerPrefs.DeleteKey(key);
            removed++;
        }

        removed += DeleteLegacyIdentityScopedKeys();

        PlayerPrefs.Save();
        return removed;
    }

    private void TryRunIdentitylessMigration(string source)
    {
        if (!_options.PersistCustomizationSelectionOffline.Value
            || PlayerPrefs.GetInt(MigrationCompletedKey, 0) == 1)
        {
            return;
        }

        int removed = DeleteLegacyIdentityScopedKeys();
        PlayerPrefs.SetInt(MigrationCompletedKey, 1);
        PlayerPrefs.Save();

        if (removed <= 0)
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"{source}: migrated customization cache to installation scope. " +
            $"RemovedLegacyIdentityKeys={removed}");
    }

    private static int DeleteLegacyIdentityScopedKeys()
    {
        int removed = 0;

        string activeIdentity = PlayerPrefs.GetString(ActiveIdentityKey, string.Empty);

        if (!string.IsNullOrWhiteSpace(activeIdentity))
        {
            removed += DeleteIdentityKeys(activeIdentity);
        }

        string current = PlayerPrefs.GetString(IdentityIndexKey, string.Empty);

        string[] identities = current.Split(
            ['|'],
            StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < identities.Length; i++)
        {
            string identity = identities[i];

            removed += DeleteIdentityKeys(identity);
        }

        PlayerPrefs.DeleteKey(IdentityIndexKey);
        PlayerPrefs.DeleteKey(ActiveIdentityKey);

        return removed;
    }

    private static int DeleteIdentityKeys(string identityFingerprint)
    {
        int removed = 0;

        for (int partIndex = 0; partIndex < CacheParts.Length; partIndex++)
        {
            string key = GetIdentityScopedKey(identityFingerprint, CacheParts[partIndex]);

            if (!PlayerPrefs.HasKey(key))
            {
                continue;
            }

            PlayerPrefs.DeleteKey(key);
            removed++;
        }

        return removed;
    }

    private static bool TryReadCache(out CharacterCustomizationData data)
    {
        data = new CharacterCustomizationData();

        string skinKey = GetKey("skin");

        if (!PlayerPrefs.HasKey(skinKey))
        {
            return false;
        }

        data.currentSkin = PlayerPrefs.GetInt(skinKey, 0);
        data.currentAccessory = PlayerPrefs.GetInt(GetKey("accessory"), 0);
        data.currentEyes = PlayerPrefs.GetInt(GetKey("eyes"), 0);
        data.currentMouth = PlayerPrefs.GetInt(GetKey("mouth"), 0);
        data.currentOutfit = PlayerPrefs.GetInt(GetKey("outfit"), 0);
        data.currentHat = PlayerPrefs.GetInt(GetKey("hat"), 0);
        data.currentSash = PlayerPrefs.GetInt(GetKey("sash"), 0);
        data.CorrectValues();
        return true;
    }

    private static string GetKey(string part)
    {
        return $"{CachePrefix}.{part}";
    }

    private static string GetIdentityScopedKey(
        string identityFingerprint,
        string part)
    {
        return $"{CachePrefix}.{identityFingerprint}.{part}";
    }

    private static string FormatCustomizationData(
        CharacterCustomizationData data)
    {
        return $"skin={data.currentSkin},acc={data.currentAccessory},eyes={data.currentEyes},mouth={data.currentMouth},outfit={data.currentOutfit},hat={data.currentHat},sash={data.currentSash}";
    }
}
