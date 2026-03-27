#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Interpreter;
using UnityEngine;

namespace SceneBlueprint.Editor.Interpreter
{
    /// <summary>
    /// BlueprintTestWindow 的 Runner bootstrap 辅助器。
    /// <para>
    /// 目标：
    /// 1. 在编辑器测试窗口中显式确保业务 Provider 已注册
    /// 2. 输出当前 Runner 的 System 注册诊断
    /// 3. 对蓝图中的业务 ActionType 做最小覆盖检查，避免节点无声卡在 Running
    /// </para>
    /// </summary>
    public static class BlueprintTestRunnerBootstrap
    {
        private const string DiGameProviderTypeName = "SceneBlueprintUser.Systems.DiGameSystemProvider";
        private const string ProjectBootstrapTypeName = "SceneBlueprintUser.Systems.BlueprintRuntimeProjectBootstrap";

        private static readonly RequiredActionSystemRule[] RequiredActionSystemRules =
        {
            new("Trigger.EnterArea", "TriggerEnterAreaSystem"),
            new("Interaction.ApproachTarget", "InteractionApproachTargetSystem"),
            new("Spawn.Preset", "SpawnPresetSystem"),
            new("Spawn.Wave", "SpawnWaveSystem"),
            new("VFX.ShowWarning", "ShowWarningSystem"),
            new("VFX.CameraShake", "CameraShakeSystem"),
            new("VFX.ScreenFlash", "ScreenFlashSystem"),
        };

        public static BlueprintRunner CreateRunner(Action<string>? log = null, Action<string>? logWarning = null)
        {
            log ??= UnityEngine.Debug.Log;
            logWarning ??= UnityEngine.Debug.LogWarning;

            EnsureProviderRegistered(DiGameProviderTypeName, log, logWarning);

            var runner = CreateRunnerWithProjectBaseline(log, logWarning);
            LogRegisteredSystems(runner, log);
            return runner;
        }

        public static IReadOnlyList<string> AnalyzeCoverage(BlueprintRunner runner, BlueprintFrame? frame)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            if (frame == null)
            {
                return Array.Empty<string>();
            }

            var warnings = new List<string>();
            var systemNames = new HashSet<string>(runner.GetRegisteredSystemNames(), StringComparer.Ordinal);
            var actionTypeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < frame.Actions.Length; index++)
            {
                var typeId = frame.Actions[index].TypeId;
                if (!string.IsNullOrWhiteSpace(typeId))
                {
                    actionTypeIds.Add(typeId);
                }
            }

            for (var index = 0; index < RequiredActionSystemRules.Length; index++)
            {
                var rule = RequiredActionSystemRules[index];
                if (!actionTypeIds.Contains(rule.ActionTypeId) || systemNames.Contains(rule.SystemName))
                {
                    continue;
                }

                warnings.Add(
                    $"[BlueprintTestWindow] 当前蓝图包含 {rule.ActionTypeId}，" +
                    $"但 Runner 未注册 {rule.SystemName}。该节点可能会被激活后长期停留在 Running。");
            }

            return warnings;
        }

        private static void EnsureProviderRegistered(
            string providerTypeName,
            Action<string> log,
            Action<string> logWarning)
        {
            if (string.IsNullOrWhiteSpace(providerTypeName))
            {
                return;
            }

            var registeredProviders = BlueprintSystemRegistry.GetRegisteredProviderTypeNames();
            if (registeredProviders.Contains(providerTypeName, StringComparer.Ordinal))
            {
                log($"[BlueprintTestWindow] Provider 已就绪: {providerTypeName}");
                return;
            }

            var providerType = ResolveType(providerTypeName);
            if (providerType == null)
            {
                logWarning($"[BlueprintTestWindow] 未找到 Provider 类型 {providerTypeName}，将继续使用当前 Registry 状态。");
                return;
            }

            if (!typeof(IBlueprintSystemProvider).IsAssignableFrom(providerType))
            {
                logWarning($"[BlueprintTestWindow] 类型 {providerTypeName} 不是 IBlueprintSystemProvider，跳过显式注册。");
                return;
            }

            try
            {
                if (Activator.CreateInstance(providerType) is IBlueprintSystemProvider provider)
                {
                    BlueprintSystemRegistry.Register(provider);
                    log($"[BlueprintTestWindow] 已显式注册 Provider: {providerTypeName}");
                }
                else
                {
                    logWarning($"[BlueprintTestWindow] 无法实例化 Provider: {providerTypeName}");
                }
            }
            catch (Exception ex)
            {
                logWarning($"[BlueprintTestWindow] 显式注册 Provider 失败: {providerTypeName} - {ex.Message}");
            }
        }

        private static void LogRegisteredSystems(BlueprintRunner runner, Action<string> log)
        {
            var systemNames = runner.GetRegisteredSystemNames();
            var joined = systemNames.Count == 0
                ? "(none)"
                : string.Join(", ", systemNames);
            log($"[BlueprintTestWindow] Runner 已注册 {systemNames.Count} 个 System: {joined}");
        }

        private static BlueprintRunner CreateRunnerWithProjectBaseline(
            Action<string> log,
            Action<string> logWarning)
        {
            var bootstrapType = ResolveType(ProjectBootstrapTypeName);
            var createRunnerMethod = bootstrapType?.GetMethod(
                "CreateRunner",
                BindingFlags.Public | BindingFlags.Static);

            if (createRunnerMethod?.Invoke(null, null) is BlueprintRunner runner)
            {
                log($"[BlueprintTestWindow] 已通过项目基线 bootstrap 创建 Runner: {ProjectBootstrapTypeName}");
                return runner;
            }

            logWarning($"[BlueprintTestWindow] 未能解析项目基线 bootstrap {ProjectBootstrapTypeName}，将回退 BlueprintRunnerFactory.CreateProjectBaselineDefault().");
            return BlueprintRunnerFactory.CreateProjectBaselineDefault();
        }

        private static Type? ResolveType(string fullTypeName)
        {
            var resolved = Type.GetType(fullTypeName, throwOnError: false);
            if (resolved != null)
            {
                return resolved;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                resolved = assemblies[index].GetType(fullTypeName, throwOnError: false);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return null;
        }

        private readonly struct RequiredActionSystemRule
        {
            public RequiredActionSystemRule(string actionTypeId, string systemName)
            {
                ActionTypeId = actionTypeId ?? string.Empty;
                SystemName = systemName ?? string.Empty;
            }

            public string ActionTypeId { get; }

            public string SystemName { get; }
        }
    }
}
