using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AquaMai.Config.Attributes;
using HarmonyLib;
using MAI2.Util;
using Manager;
using GameProcess = global::Process;

namespace AquaMai.Mods.Utils.EarlyContinue;

[ConfigCollapseNamespace]
[ConfigSection(
    name: "更早的续关",
    zh: "在最后一首歌结束的时候显示续关界面，确认续关后立即增加指定 Track 数")]
public class EarlyContinue
{
    [ConfigEntry(name: "增加的曲目数", zh: "设为 0 则为 1P 3首，2P 4首", en: "Number of tracks to add. Set to 0 to add 3 tracks for 1P and 3 tracks for 2P.")]
    public static readonly uint addTrackCount = 0;

    // ResultProcess.ToNextProcess() 里把 new MapResultProcess(container) 换成自己的 Process，
    // 并强制走 FadeProcess 分支
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameProcess.ResultProcess), "ToNextProcess")]
    public static IEnumerable<CodeInstruction> ToNextProcessTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Constructor(typeof(GameProcess.MapResultProcess), new[] { typeof(GameProcess.ProcessDataContainer) });
        var replacement = AccessTools.Method(typeof(EarlyContinue), nameof(CreateNextProcess));

        var codes = new List<CodeInstruction>(instructions);

        // 替换所有 new MapResultProcess(...) 为 GetMapOrEarlyContinueProcess(...) 的调用，并记录第一个出现位置
        var firstNewObj = -1;
        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Newobj && codes[i].operand as ConstructorInfo == original)
            {
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = replacement;
                if (firstNewObj < 0) firstNewObj = i;
            }
        }

        // flag8==true 的分支体里有第一个 newobj，往前找守卫它的条件跳转，
        // 把跳转前加载 flag8 的指令改成 ldc.i4.0：无论 brtrue/brfalse 都会落到 else（FadeProcess）
        for (var i = firstNewObj - 1; i >= 1; i--)
        {
            if (codes[i].opcode == OpCodes.Brfalse || codes[i].opcode == OpCodes.Brfalse_S ||
                codes[i].opcode == OpCodes.Brtrue || codes[i].opcode == OpCodes.Brtrue_S)
            {
                // 原地改 opcode/operand，保留指令上的 labels 和 blocks
                codes[i - 1].opcode = OpCodes.Ldc_I4_0;
                codes[i - 1].operand = null;
                break;
            }
        }

        return codes;
    }

    public static uint currentAddTrackCount = 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SetMaxTrack))]
    public static void SetMaxTrack()
    {
        GameManager.TempMaxTrackCount += currentAddTrackCount;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Clear))]
    public static void GameManagerClear()
    {
        currentAddTrackCount = 0;
    }

    public static GameProcess.ProcessBase CreateNextProcess(GameProcess.ProcessDataContainer container)
    {
        bool isTaskTrack = GameManager.CategoryIndex == 195 || GameManager.CategoryIndex == 196;
        bool anySurvived = false;

        if (isTaskTrack)
        {
            for (int i = 0; i < 2; i++)
            {
                var userData = Singleton<UserDataManager>.Instance.GetUserData(i);
                if (userData != null && userData.IsEntry && !userData.IsGuest())
                {
                    var score = Singleton<GamePlayManager>.Instance.GetGameScore(i);
                    if (score != null)
                    {
                        // 参照 MapResultMonitor.CheckChallengeStatus 的判定逻辑
                        if (GameManager.IsPerfectChallenge)
                        {
                            if (score.Life > 0)
                            {
                                anySurvived = true;
                                break;
                            }
                        }
                        else
                        {
                            // Map Task (课题曲) 要求 ClearRank >= 5 (Rank A)
                            uint achievement = Singleton<GamePlayManager>.Instance.GetAchivement(i, -1);
                            if ((int)GameManager.GetClearRank((int)achievement, score.SessionInfo.isUtageCoop) >= 5)
                            {
                                anySurvived = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        // 课题曲如果成功存活，无论是不是最后一首，都必须进 MapResultProcess 播动画
        // 否则如果在最后一首成功，会被替换为 EarlyContinue，从而跳过结算动画
        if (isTaskTrack && anySurvived)
        {
            return new GameProcess.MapResultProcess(container);
        }

        // 如果不是最后一首歌，且游戏没有结束（比如某些其他原因中途弹出的 MapResultProcess）
        // 不应该显示续关界面
        if (GameManager.MusicTrackNumber < GameManager.GetMaxTrackCount() && !GameManager.IsGotoGameOver())
        {
            return new GameProcess.MapResultProcess(container);
        }

        return new Process(container);
    }
}
