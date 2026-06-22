using HarmonyLib;

using AquaMai.Config.Attributes;
using UnityEngine;

namespace AquaMai.Mods.GameSystem;

[ConfigSection(
    name: "耳机音量覆盖",
    en: "Override 1P/2P headphone volume to a fixed value, ignoring the user's in-game setting. Set to -1 to disable (use user setting). Range: 0–20.",
    zh: "将 1P/2P 耳机音量强制覆盖为固定值，忽略用户的游戏内设置。设为 -1 则禁用（使用用户设置）。范围：0–20。")]
public static class HeadphoneVolumeOverride
{
    [ConfigEntry(
        name: "1P 音量覆盖",
        en: "1P headphone volume override. -1 = use user setting, 0–20 = fixed volume (20 is max).",
        zh: "1P 耳机音量覆盖值。-1 = 使用用户设置，0–20 = 固定音量（20 为最大值）。")]
    private static readonly float p1HeadphoneVolumeOverride = -1f;

    [ConfigEntry(
        name: "2P 音量覆盖",
        en: "2P headphone volume override. -1 = use user setting, 0–20 = fixed volume (20 is max).",
        zh: "2P 耳机音量覆盖值。-1 = 使用用户设置，0–20 = 固定音量（20 为最大值）。")]
    private static readonly float p2HeadphoneVolumeOverride = -1f;

    private const uint P1HeadphoneAisacControlId = 2;
    private const uint P2HeadphoneAisacControlId = 3;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CriAtomExPlayer), "SetAisacControl", [typeof(uint), typeof(float)])]
    public static void PreSetAisacControl(uint controlId, ref float value)
    {
        if (controlId == P1HeadphoneAisacControlId && p1HeadphoneVolumeOverride >= 0f)
        {
            value = Mathf.Clamp(p1HeadphoneVolumeOverride / 20f, 0f, 1f);
        }
        else if (controlId == P2HeadphoneAisacControlId && p2HeadphoneVolumeOverride >= 0f)
        {
            value = Mathf.Clamp(p2HeadphoneVolumeOverride / 20f, 0f, 1f);
        }
    }
}
