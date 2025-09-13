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
using Process.UserDataNet.State.UserDataULState;


[assembly: MelonInfo(typeof(default_namespace.K041_Fix), "K041_Fix", "1.2.0", "Simon273")]
[assembly: MelonGame("sega-interactive", "Sinmai")]

namespace default_namespace {
    public class K041_Fix : MelonMod
    {
        // Dedicated Harmony instance to allow patching from static contexts
        private static HarmonyLib.Harmony _harmony;
        public override void OnInitializeMelon()
        {
            // Bind our Harmony instance
            _harmony = this.HarmonyInstance ?? new HarmonyLib.Harmony("K041_Fix");
            // Apply attribute-based patches in this class
            _harmony.PatchAll(typeof(K041_Fix));
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










        // patch AquaMai.Mods.Fix.FixLevelDisplay.FixLevelShiftMusicChainCardObejct
        private static bool _aquamaiPatched = false;
        private static void TryPatchAquaMaiFixLevelDisplay()
        {
            try
            {
                if (_aquamaiPatched) return;
                var AquaMai_fixType = AccessTools.TypeByName("AquaMai.Mods.Fix.FixLevelDisplay");
                if (AquaMai_fixType == null)
                {
                    MelonLogger.Msg("AquaMai.FixLevelDisplay not found or not enabled, skip patching.");
                    return;
                }

                var target = AccessTools.Method(AquaMai_fixType, "FixLevelShiftMusicChainCardObejct");
                if (target == null)
                {
                    MelonLogger.Msg("AquaMai.FixLevelDisplay.FixLevelShiftMusicChainCardObejct not found or not enabled, skip patching.");
                    return;
                }

                var prefix = new HarmonyMethod(AccessTools.Method(typeof(K041_Fix), nameof(BlockAquaMai_FixLevelDisplay_Prefix)));
                if (_harmony == null) _harmony = new HarmonyLib.Harmony("K041_Fix.Runtime");
                _harmony.Patch(target, prefix: prefix);
                MelonLogger.Msg("Applied selective disable for AquaMai.FixLevelDisplay (KLD 表/里).");
                _aquamaiPatched = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to patch AquaMai.FixLevelDisplay: {ex}");
            }
        }

        // prefix 禁用 AquaMai.FixLevelDisplay
        private static bool BlockAquaMai_FixLevelDisplay_Prefix()
        {
            if (!GameManager.IsKaleidxScopeMode) return true;
            if (!_shouldBlock) return true;
            return false;
        }

        // 在玩家确认进入门后，获取门的 id
        private static bool _shouldBlock = false;
        [HarmonyPostfix]
        [HarmonyPatch(typeof(KaleidxScopeState.TimeUp), "OnEnter")]
        public static void KaleidxScope_TimeUp_OnEnter_Postfix(KaleidxScopeState.TimeUp __instance)
        {
            int id = Singleton<KaleidxScopeManager>.Instance.gateId;

            if (id == 8 || id == 10)
                _shouldBlock = true;
            else
                _shouldBlock = false;

            if (id == 8)
                MelonLogger.Msg($"AquaMai.FixLevelDisplay will be disabled in KLD 表");
            if (id == 10)
                MelonLogger.Msg($"AquaMai.FixLevelDisplay will be disabled in KLD 里");
        }








        // Remove 1879 from the Normal notes list
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NotesListManager), "CreateNormalNotesList")]
        public static void CreateNormalNotesList_Postfix()
        {
            try
            {
                if (!GameManager.IsNormalMode) return; // 仅在 Normal 模式下生效

                var dm = DataManager.Instance;
                if (dm == null) return;
                var musicInfo = dm.GetMusic(011879);
                if (musicInfo == null) return;

                var instance = NotesListManager.Instance;
                if (instance == null) return;
                var notesList = instance.GetNotesList();
                if (notesList != null && notesList.ContainsKey(011879))
                {
                    notesList.Remove(011879);
                    MelonLogger.Msg($"Hide glitch Xaleid◆scopiX in normal mode");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Hide glitch Xaleid◆scopiX Error: {ex}");
            }
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
                    MelonLogger.Msg("[Gate10_KeySim] stateMachine is null.");
                    return;
                }
                // 拿到 monitors
                var smTrav = Traverse.Create(stateMachine);
                var monitors = smTrav.Field("monitorList").GetValue() as System.Collections.IList;
                if (monitors == null)
                {
                    MelonLogger.Msg("[Gate10_KeySim] monitors is null.");
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
                MelonLogger.Error($"[Gate10_KeySim] error: {ex}");
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
                MelonLogger.Msg($"[Gate10_KeySim] P{playerIndex+1} open Gate 10.");
                playerHasOpened[playerIndex] = true;
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[Gate10_KeySim] P{playerIndex+1} open gate error: {e}");
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
            TryPatchAquaMaiFixLevelDisplay();
        }    

        private static bool _simpleSefDone;
        private static bool _isPatchEnabled;
        
        private static void EnsureSimpleSpecialEffectParamTable()
        {
            if (_simpleSefDone) return;
            try
            {
                var commonType = AccessTools.TypeByName("CommonScriptable");
                var tableType = AccessTools.TypeByName("SpecialEffectParamTable");
                var innerType = AccessTools.TypeByName("SpecialEffectParamTable+KaleidGlitchNoise_BlackOut");
                if (commonType == null || tableType == null || innerType == null) return;

                // 如果资源存在则不使用此patch
                try
                {
                    var getMethod = commonType.GetMethod("GetSpecialEffectParamTable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (getMethod != null)
                    {
                        var loaded = getMethod.Invoke(null, null);
                        if (loaded != null)
                        {
                            _isPatchEnabled = false;
                            _simpleSefDone = true;
                            return;
                        }
                    }
                }
                catch { /* ignore and fallback */ }


                var fiTable = commonType.GetField("_specialEffectParamTable", BindingFlags.NonPublic | BindingFlags.Static);
                if (fiTable == null) return;
                if (fiTable.GetValue(null) != null) { _simpleSefDone = true; return; }
                var fiInit = commonType.GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static);
                var innerField = tableType.GetField("_kaleidGlitchNoise_BlackOutParam", BindingFlags.NonPublic | BindingFlags.Instance);
                if (innerField == null) return;
                var inst = ScriptableObject.CreateInstance(tableType);
                var boxed = Activator.CreateInstance(innerType);
                // 字段引用
                var fShader = innerType.GetField("_angleGlitchShader", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var fCurve = innerType.GetField("_noiseCurvce", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var fThick = innerType.GetField("_lineThickness", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var fSpeed = innerType.GetField("_speed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var fTime = innerType.GetField("_totalTime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                // 定义参数
                float lineThickness = 10f;
                float speed = 5f;
                float totalTime = 4f;
                var curve = BuildCurve();
                // 写入字段
                fShader?.SetValue(boxed, null);
                fCurve?.SetValue(boxed, curve);
                fThick?.SetValue(boxed, lineThickness);
                fSpeed?.SetValue(boxed, speed);
                fTime?.SetValue(boxed, totalTime);
                innerField.SetValue(inst, boxed);
                fiTable.SetValue(null, inst);
                if (fiInit != null && !(bool)fiInit.GetValue(null)) fiInit.SetValue(null, true);

                MelonLogger.Msg("[BlueScreenEffect] SpecialEffectParamTable not exist, use custom fallback.");
                _isPatchEnabled = true;
                _simpleSefDone = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SEF Simple] Create failed: {ex}");
            }
        }

        // glitch 曲线
        private static AnimationCurve BuildCurve()
        {
            var keys = new Keyframe[]
            {
                new Keyframe(0.00f, 0.0f, 0f, 0f),
                new Keyframe(0.05f, 0.2f, 0f, 0f),
                new Keyframe(0.24f, 0.0f, 0f, 0f),
                new Keyframe(0.34f, 0.0f, 0f, 0f),
                new Keyframe(0.53f, 0.2f, 0f, 0f),
                new Keyframe(0.73f, 0.4f, 0f, 0f),
                new Keyframe(0.84f, 0.0f, 0.5f, 0.5f),
                new Keyframe(0.94f, 0.7f, 0f, 0f),
                new Keyframe(1.00f, 0.9f, 0f, 0f)
            };
            var curve = new AnimationCurve(keys)
            {
                preWrapMode = WrapMode.ClampForever,
                postWrapMode = WrapMode.ClampForever
            };
            return curve;
        }

        // 仿 glitch 效果
        private static readonly HashSet<GlitchNoiseEffect> _cpuStripeFallback = new HashSet<GlitchNoiseEffect>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GlitchNoiseEffect), "Initialize")]
        private static bool GlitchNoiseEffect_Initialize_Prefix(GlitchNoiseEffect __instance, Shader shader, float lineThickness, float speed)
        {
            if (!_isPatchEnabled) return true; // 未启用patch
            if (shader != null) return true;   // 有 shader 正常执行
            var trav = Traverse.Create(__instance);
            trav.Field("_lineThickness").SetValue(lineThickness);
            trav.Field("_speed").SetValue(speed);
            __instance.enabled = true; // 保持脚本启用
            _cpuStripeFallback.Add(__instance);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GlitchNoiseEffect), "OnRenderImage")]
        private static bool GlitchNoiseEffect_OnRenderImage_Prefix(GlitchNoiseEffect __instance, RenderTexture source, RenderTexture destination)
        {
            if (!_isPatchEnabled) return true; // 未启用patch
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
            // 翻转Y轴，保持画面正确
            var srcRect = new Rect(0f, 1f - (y + sh) / (float)h, 1f, sh / (float)h);
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
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StateULUserAime), "Init")]
        public static void StateULUserAime_Init_Postfix(StateULUserAime __instance)
        {
            if (_isLastEvent) _isLastEvent = false;
        }
    }
}
