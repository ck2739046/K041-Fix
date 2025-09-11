using HarmonyLib;
using Monitor;
using UnityEngine;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using Manager;
using System.Linq;
using MAI2.Util;
using Process;
using Process.Entry;


[assembly: MelonInfo(typeof(default_namespace.K041_Fix), "K041_Fix", "1.0.1", "Simon273")]
[assembly: MelonGame("sega-interactive", "Sinmai")]

namespace default_namespace {
    public class K041_Fix : MelonMod
    {
        public override void OnInitializeMelon()
        {
            HarmonyInstance.PatchAll(typeof(K041_Fix));
        }


        // 修复开门闪退
        [HarmonyPrefix]
        [HarmonyPatch(typeof(KaleidxScopeDifficultyGateController), "ChangeSprite")]
        public static bool ChangeSprite_Prefix(KaleidxScopeDifficultyGateController __instance, KaleidxScopeDifficultyGateController.SpriteType spriteType)
        {
            // 检查 null
            var traverse = Traverse.Create(__instance);
            if (traverse.Field("multipleGate").GetValue() == null) MelonLogger.Msg("multipleGate is null");
            if (traverse.Field("multipleIcon").GetValue() == null) MelonLogger.Msg("multipleIcon is null");
            if (traverse.Field("multipleIconEffect").GetValue() == null) MelonLogger.Msg("multipleIconEffect is null");
            if (traverse.Field("centerObject").GetValue() != null) return true; // 正常执行

            // 只在必要时干预
            if ((int)spriteType != 7 && (int)spriteType != 9 && (int)spriteType != 10) return true;
            // KaleidxScopeDifficultyGateController.SpriteType.LoseEvent+LastEvent+Cleared

            // MelonLogger.Msg($"[GateFix] Fix centerObject is null: {spriteType}");
            var existingObj = __instance.GetComponentInChildren<KaleidxScopeCenterTitleController>(true);
            traverse.Field("centerObject").SetValue(existingObj);
            return true;
        }













        // 模拟最后Boss钥匙刷卡功能
        [HarmonyPostfix]
        [HarmonyPatch(typeof(KaleidxScopeProcess), "OnUpdate")]
        public static void KaleidxScope_Key_Postfix(KaleidxScopeProcess __instance)
        {
            if (!UnityEngine.Input.GetKeyDown(KeyCode.Return) && !UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)) return;

            try
            {
                // 判断玩家的 userData 阶段/entry 状态
                bool[] isEntryArray = new bool[2] { false, false };
                for (int p = 0; p < 2; p++)
                {
                    var ud = Singleton<UserDataManager>.Instance.GetUserData(p);
                    if (!ud.IsEntry) continue;
                    var phaseDbg = Singleton<KaleidxScopeManager>.Instance.GetUserKaleidxScopePhase(ud);
                    if ((int)phaseDbg != 5) continue; // KaleidxScopeManager.KaleidxScopePhase.ClearHopeGate
                    isEntryArray[p] = true;
                }
                if (!isEntryArray[0] && !isEntryArray[1]) return; // 没有符合条件的玩家，跳过


                // 反射拿到内部 stateMachine
                var procTrav = Traverse.Create(__instance);
                var stateMachine = procTrav.Field("stateMachine").GetValue<KaleidxScopeState>();
                if (stateMachine == null)
                {
                    MelonLogger.Msg("[LastBossKeySim-Process] stateMachine is null.");
                    return;
                }
                // 拿到 monitors
                var smTrav = Traverse.Create(stateMachine);
                var monitors = smTrav.Field("monitorList").GetValue() as System.Collections.IList;
                if (monitors == null)
                {
                    MelonLogger.Msg("[LastBossKeySim-Process] monitors is null.");
                    return;
                }
                // 判断触发开门
                bool triggered = false;
                for (int i = 0; i < monitors.Count && !triggered; i++) // 一次enter只给一人开门
                {
                    if (!isEntryArray[i]) continue; // 该玩家不符合条件，跳过
                    var userData = Singleton<UserDataManager>.Instance.GetUserData(i);
                    var mon = monitors[i];
                    var monTrav = mon != null ? Traverse.Create(mon) : null;
                    bool monitorEntry = monTrav?.Field("entry").GetValue<bool>() ?? false;
                    if (monitorEntry) continue; // 已经entry了，不需要刷钥匙

                    var keyData = userData.GetUserKaleidxScopeData(10);
                    bool hasKey = keyData != null && keyData.isKeyFound;
                    if (hasKey) continue; // 已经有钥匙了，不需要刷钥匙

                    // 尝试触发开门
                    if (TriggerOpenLastBoss(stateMachine, i)) continue;
                    triggered = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[LastBossKeySim-Process] 异常: {ex}");
            }
        }

        // 全局变量 防止多次开门
        private static bool[] playerHasOpened = new bool[2] { false, false };
        // 触发开门
        private static bool TriggerOpenLastBoss(KaleidxScopeState stateMachine, int playerIndex)
        {
            try
            {
                if (playerHasOpened[playerIndex]) return false;
                var openType = typeof(KaleidxScopeState).GetNestedType("OpenLastBoss", BindingFlags.Public | BindingFlags.NonPublic);
                var ctor = openType?.GetConstructor(new[] { typeof(KaleidxScopeState), typeof(KaleidxScopeState.StateType), typeof(int) });
                object newState = ctor.Invoke(new object[] { stateMachine, 3, playerIndex }); // KaleidxScopeState.StateType.OpenLastBoss
                var changeStateMethod = typeof(StateMachineBase<,>).MakeGenericType(typeof(KaleidxScopeState), typeof(KaleidxScopeState.StateType))
                    .GetMethod("ChangeState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                changeStateMethod.Invoke(stateMachine, new object[] { newState });
                MelonLogger.Msg($"[LastBossKeySim-Process] P{playerIndex} 成功开门");
                playerHasOpened[playerIndex] = true;
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[LastBossKeySim-Process] P{playerIndex} 触发开门异常: {e}");
                return false;
            }
        }
        
        // 重置开门状态
        [HarmonyPrefix]
        [HarmonyPatch(typeof(KaleidxScopeProcess), "OnStart")]
        public static void KaleidxScope_OnStart_Prefix()
        {
            playerHasOpened[0] = false;
            playerHasOpened[1] = false;
        }











        [HarmonyPostfix]
        [HarmonyPatch(typeof(CommonProcess), "OnStart")]
        public static void CommonProcess_OnStart_Postfix(PowerOnMonitor[] ____monitors)
        {
            EnsureSimpleSpecialEffectParamTable();
        }    

        private static bool _simpleSefDone;
        private static void EnsureSimpleSpecialEffectParamTable()
        {
            if (_simpleSefDone) return;
            try
            {
                var commonType = AccessTools.TypeByName("CommonScriptable");
                var tableType = AccessTools.TypeByName("SpecialEffectParamTable");
                var innerType = AccessTools.TypeByName("SpecialEffectParamTable+KaleidGlitchNoise_BlackOut");
                if (commonType == null || tableType == null || innerType == null) return;
                var fiTable = commonType.GetField("_specialEffectParamTable", BindingFlags.NonPublic | BindingFlags.Static);
                if (fiTable == null) return;
                if (fiTable.GetValue(null) != null) { _simpleSefDone = true; return; }
                var fiInit = commonType.GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static);
                var innerField = tableType.GetField("_kaleidGlitchNoise_BlackOutParam", BindingFlags.NonPublic | BindingFlags.Instance);
                if (innerField == null) return;
                var inst = ScriptableObject.CreateInstance(tableType);
                var boxed = Activator.CreateInstance(innerType);
                // 字段引用
                var fShader = innerType.GetField("_angleGlitchShader");
                var fCurve = innerType.GetField("_noiseCurvce");
                var fThick = innerType.GetField("_lineThickness");
                var fSpeed = innerType.GetField("_speed");
                var fTime = innerType.GetField("_totalTime");
                // 固定常量
                float lineThickness = 10f;
                float speed = 2.0f;
                float totalTime = 2.8f;
                // 曲线
                var curve = new AnimationCurve(
                    new Keyframe(0.0f,  0.2f), // Jump 1 peak
                    new Keyframe(0.02f, 0f),   // Jump 1 drop

                    new Keyframe(0.23f, 0f),
                    new Keyframe(0.3f,  0.4f), // Jump 2 peak
                    new Keyframe(0.32f, 0f),   // Jump 2 drop

                    new Keyframe(0.43f, 0f),
                    new Keyframe(0.52f, 0.6f), // Jump 3 peak
                    new Keyframe(0.55f, 0f),   // Jump 3 drop

                    new Keyframe(0.74f, 0f),
                    new Keyframe(0.86f, 0.8f), // Jump 4 peak
                    new Keyframe(0.92f, 0f),   // Jump 4 drop
                    
                    new Keyframe(0.95f,  1.0f)  // Jump 5 peak
                );
                // 写入结构，shader直接为null以触发CPU回退
                fShader?.SetValue(boxed, null);
                fCurve?.SetValue(boxed, curve);
                fThick?.SetValue(boxed, lineThickness);
                fSpeed?.SetValue(boxed, speed);
                fTime?.SetValue(boxed, totalTime);
                innerField.SetValue(inst, boxed);
                fiTable.SetValue(null, inst);
                if (fiInit != null && !(bool)fiInit.GetValue(null)) fiInit.SetValue(null, true);
                _simpleSefDone = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SEF Simple] Create failed: {ex}");
            }
        }

        // 仿 glitch 效果
        private static readonly HashSet<GlitchNoiseEffect> _cpuStripeFallback = new HashSet<GlitchNoiseEffect>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GlitchNoiseEffect), "Initialize")]
        private static bool GlitchNoiseEffect_Initialize_Prefix(GlitchNoiseEffect __instance, Shader shader, float lineThickness, float speed)
        {
            if (shader != null) return true; // 有 shader 正常执行
            var trav = Traverse.Create(__instance);
            trav.Field("_lineThickness").SetValue(lineThickness);
            trav.Field("_speed").SetValue(speed);
            __instance.enabled = true;
            _cpuStripeFallback.Add(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GlitchNoiseEffect), "OnRenderImage")]
        private static bool GlitchNoiseEffect_OnRenderImage_Prefix(GlitchNoiseEffect __instance, RenderTexture source, RenderTexture destination)
        {
            if (!_cpuStripeFallback.Contains(__instance)) return true; // 非回退实例
            var trav = Traverse.Create(__instance);
            bool active = trav.Field("_isActive").GetValue<bool>();
            bool isEnd = trav.Field("_isEnd").GetValue<bool>();
            if (!active || isEnd)
            {
                Graphics.Blit(source, destination);
                return false;
            }
            float intensity = trav.Field("_noiseValue").GetValue<float>();
            float stripeThickness = Mathf.Max(4f, trav.Field("_lineThickness").GetValue<float>());
            int w = source.width; int h = source.height;
            RenderTexture.active = destination;
            GL.Clear(false, true, Color.black);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, w, h, 0);
            int y = 0;
            // 最大水平偏移随强度变化
            int maxOffset = Mathf.RoundToInt(w * 0.18f * Mathf.Clamp01(intensity));
            if (maxOffset < 2) maxOffset = 2;
            System.Func<int, int, int> rand = UnityEngine.Random.Range; // 简写
            while (y < h)
            {
                int sh = (int)Mathf.Min(stripeThickness, h - y);
                int offset = rand(-maxOffset, maxOffset);
                DrawStripe(source, w, h, y, sh, offset);
                if (offset > 0) DrawStripe(source, w, h, y, sh, offset - w); // 右移溢出补环绕
                else if (offset < 0) DrawStripe(source, w, h, y, sh, offset + w);
                y += sh;
            }
            GL.PopMatrix();
            return false; // 拦截原渲染
        }

        private static void DrawStripe(RenderTexture src, int w, int h, int y, int sh, int offset)
        {
            if (offset <= -w || offset >= w) return; // 全部在外不绘制
            var dstRect = new Rect(offset, y, w, sh);
            // 同时翻转X和Y轴，保持画面正确
            var srcRect = new Rect(1f, 1f - (y + sh) / (float)h, -1f, sh / (float)h);
            Graphics.DrawTexture(dstRect, src, srcRect, 0, 0, 0, 0);
        }
        


















        
        // 检测是不是里KaleidxScope
        private static bool _isLastEvent = false;
        [HarmonyPostfix]
        [HarmonyPatch(typeof(KaleidxScopeState.ConfirmWindow), "PlayGateConfirmAnim")]
        public static void KaleidxScopeState_ConvertPositionIdToGateIndex_Postfix(KaleidxScopeState.ConfirmWindow __instance, int gateId)
        {
            if (gateId == 10)
            {
                _isLastEvent = true;
            }
        }
        
        // 修改maxTrack显示值
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "GetMaxTrackCount")]
        public static int GameManager_GetMaxTrackCount_Postfix(int __result)
        {
            return _isLastEvent ? 1 : __result;
        }
        
        // 重置状态
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ResultProcess), "OnStart")]
        public static void ResultProcess_OnStart_Prefix(ResultProcess __instance)
        {
            if (_isLastEvent) _isLastEvent = false;
        }
    }
}
