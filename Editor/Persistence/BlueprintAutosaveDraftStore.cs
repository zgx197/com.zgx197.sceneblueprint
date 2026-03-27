#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime;
using SceneBlueprint.Runtime.Snapshot;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Persistence
{
    [Serializable]
    internal sealed class BlueprintAutosaveDraftPayload
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string DraftKey = string.Empty;
        public bool IsAnonymousDraft;
        public string DraftDisplayName = string.Empty;
        public string BlueprintAssetPath = string.Empty;
        public string BlueprintId = string.Empty;
        public string AnchoredScenePath = string.Empty;
        public long CapturedAtUtcTicks;
        public string GraphJson = string.Empty;
        public int LevelId;
        public VariableDeclaration[] Variables = Array.Empty<VariableDeclaration>();
        public BindingSnapshot[] SceneSnapshots = Array.Empty<BindingSnapshot>();
    }

    internal static class BlueprintAutosaveDraftStore
    {
        private const string DraftDirectoryRelativePath = "UserSettings/SceneBlueprintSettings/AutosaveDrafts";

        private static readonly object WriteLock = new();
        private static readonly ConcurrentDictionary<string, long> LatestWriteSerialByKey = new(StringComparer.Ordinal);
        private static long s_writeSerial;

        public static string DraftDirectoryAbsolutePath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(projectRoot, DraftDirectoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        public static string DraftDirectoryDisplayPath => DraftDirectoryRelativePath.Replace('\\', '/');

        public static bool TryBuildPayload(
            BlueprintAsset asset,
            string graphJson,
            string anchoredScenePath,
            int levelId,
            VariableDeclaration[] variables,
            BindingSnapshot[] sceneSnapshots,
            out BlueprintAutosaveDraftPayload? payload,
            out string error)
        {
            payload = null;
            error = string.Empty;

            if (!TryGetDraftKey(asset, out var draftKey, out var assetPath, out error))
                return false;

            payload = BuildPayloadCore(
                draftKey,
                isAnonymousDraft: false,
                draftDisplayName: string.IsNullOrWhiteSpace(asset.BlueprintName) ? "Blueprint Asset Draft" : asset.BlueprintName,
                blueprintAssetPath: assetPath,
                blueprintId: asset.BlueprintId ?? string.Empty,
                graphJson: graphJson,
                anchoredScenePath: anchoredScenePath,
                levelId: levelId,
                variables: variables,
                sceneSnapshots: sceneSnapshots);
            return true;
        }

        public static bool TryBuildAnonymousPayload(
            string anonymousDraftId,
            string graphJson,
            string anchoredScenePath,
            BindingSnapshot[] sceneSnapshots,
            out BlueprintAutosaveDraftPayload? payload,
            out string error)
        {
            payload = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(anonymousDraftId))
            {
                error = "匿名草稿 ID 为空。";
                return false;
            }

            payload = BuildPayloadCore(
                draftKey: BuildAnonymousDraftKey(anonymousDraftId),
                isAnonymousDraft: true,
                draftDisplayName: "未保存蓝图草稿",
                blueprintAssetPath: string.Empty,
                blueprintId: anonymousDraftId.Trim(),
                graphJson: graphJson,
                anchoredScenePath: anchoredScenePath,
                levelId: 0,
                variables: Array.Empty<VariableDeclaration>(),
                sceneSnapshots: sceneSnapshots);
            return true;
        }

        public static bool HasDraft(BlueprintAsset asset)
        {
            if (!TryGetDraftFilePath(asset, out var draftPath))
                return false;

            return File.Exists(draftPath);
        }

        public static bool HasAnonymousDraft(string anonymousDraftId)
        {
            if (!TryGetAnonymousDraftFilePath(anonymousDraftId, out var draftPath))
                return false;

            return File.Exists(draftPath);
        }

        public static bool TryLoadDraft(BlueprintAsset asset, out BlueprintAutosaveDraftPayload? payload, out string error)
        {
            payload = null;
            error = string.Empty;

            if (!TryGetDraftFilePath(asset, out var draftPath))
                return false;

            return TryLoadDraftFile(draftPath, out payload, out error);
        }

        public static bool TryLoadAnonymousDraft(string anonymousDraftId, out BlueprintAutosaveDraftPayload? payload, out string error)
        {
            payload = null;
            error = string.Empty;

            if (!TryGetAnonymousDraftFilePath(anonymousDraftId, out var draftPath))
                return false;

            return TryLoadDraftFile(draftPath, out payload, out error);
        }

        public static void QueueWrite(BlueprintAutosaveDraftPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.DraftKey))
                return;

            string json = JsonUtility.ToJson(payload, prettyPrint: false);
            string draftKey = payload.DraftKey;
            string draftPath = BuildDraftFilePath(draftKey);
            long serial = Interlocked.Increment(ref s_writeSerial);
            LatestWriteSerialByKey[draftKey] = serial;

            _ = Task.Run(() =>
            {
                try
                {
                    lock (WriteLock)
                    {
                        if (!LatestWriteSerialByKey.TryGetValue(draftKey, out var latestSerial) || latestSerial != serial)
                            return;

                        Directory.CreateDirectory(DraftDirectoryAbsolutePath);
                        string tempPath = draftPath + ".tmp";
                        File.WriteAllText(tempPath, json, Encoding.UTF8);
                        ReplaceDraftFile(tempPath, draftPath);
                    }
                }
                catch
                {
                    // 本地草稿写入失败不应影响编辑器主流程。
                }
            });
        }

        public static void WriteNow(BlueprintAutosaveDraftPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.DraftKey))
                return;

            string json = JsonUtility.ToJson(payload, prettyPrint: false);
            string draftPath = BuildDraftFilePath(payload.DraftKey);

            try
            {
                lock (WriteLock)
                {
                    Directory.CreateDirectory(DraftDirectoryAbsolutePath);
                    string tempPath = draftPath + ".tmp";
                    File.WriteAllText(tempPath, json, Encoding.UTF8);
                    ReplaceDraftFile(tempPath, draftPath);
                }
            }
            catch
            {
                // 本地草稿写入失败不应影响编辑器主流程。
            }
        }

        public static void DeleteDraft(BlueprintAsset asset)
        {
            if (!TryGetDraftFilePath(asset, out var draftPath))
                return;

            DeleteDraftFile(draftPath);
        }

        public static void DeleteAnonymousDraft(string anonymousDraftId)
        {
            if (!TryGetAnonymousDraftFilePath(anonymousDraftId, out var draftPath))
                return;

            DeleteDraftFile(draftPath);
        }

        private static bool TryLoadDraftFile(string draftPath, out BlueprintAutosaveDraftPayload? payload, out string error)
        {
            payload = null;
            error = string.Empty;

            if (!File.Exists(draftPath))
                return false;

            try
            {
                string json = File.ReadAllText(draftPath, Encoding.UTF8);
                payload = JsonUtility.FromJson<BlueprintAutosaveDraftPayload>(json);
                if (payload == null)
                {
                    error = "本地草稿文件解析结果为空。";
                    return false;
                }

                if (payload.SchemaVersion != BlueprintAutosaveDraftPayload.CurrentSchemaVersion)
                {
                    error = $"本地草稿版本不兼容: {payload.SchemaVersion}";
                    payload = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                payload = null;
                return false;
            }
        }

        private static void DeleteDraftFile(string draftPath)
        {
            if (!File.Exists(draftPath))
                return;

            try
            {
                File.Delete(draftPath);
            }
            catch
            {
                // 草稿删除失败不影响正式资产保存链路。
            }
        }

        private static void ReplaceDraftFile(string tempPath, string draftPath)
        {
            if (File.Exists(draftPath))
            {
                File.Replace(tempPath, draftPath, destinationBackupFileName: null);
                return;
            }

            File.Move(tempPath, draftPath);
        }

        private static BlueprintAutosaveDraftPayload BuildPayloadCore(
            string draftKey,
            bool isAnonymousDraft,
            string draftDisplayName,
            string blueprintAssetPath,
            string blueprintId,
            string graphJson,
            string anchoredScenePath,
            int levelId,
            VariableDeclaration[] variables,
            BindingSnapshot[] sceneSnapshots)
        {
            return new BlueprintAutosaveDraftPayload
            {
                DraftKey = draftKey,
                IsAnonymousDraft = isAnonymousDraft,
                DraftDisplayName = draftDisplayName ?? string.Empty,
                BlueprintAssetPath = blueprintAssetPath ?? string.Empty,
                BlueprintId = blueprintId ?? string.Empty,
                AnchoredScenePath = anchoredScenePath ?? string.Empty,
                CapturedAtUtcTicks = DateTime.UtcNow.Ticks,
                GraphJson = graphJson ?? string.Empty,
                LevelId = Mathf.Max(0, levelId),
                Variables = variables ?? Array.Empty<VariableDeclaration>(),
                SceneSnapshots = sceneSnapshots ?? Array.Empty<BindingSnapshot>()
            };
        }

        private static bool TryGetDraftFilePath(BlueprintAsset asset, out string draftPath)
        {
            draftPath = string.Empty;
            if (!TryGetDraftKey(asset, out var draftKey, out _, out _))
                return false;

            draftPath = BuildDraftFilePath(draftKey);
            return true;
        }

        private static bool TryGetAnonymousDraftFilePath(string anonymousDraftId, out string draftPath)
        {
            draftPath = string.Empty;
            if (string.IsNullOrWhiteSpace(anonymousDraftId))
                return false;

            draftPath = BuildDraftFilePath(BuildAnonymousDraftKey(anonymousDraftId));
            return true;
        }

        private static string BuildDraftFilePath(string draftKey)
        {
            return Path.Combine(DraftDirectoryAbsolutePath, $"{draftKey}.sbautosave.json");
        }

        private static string BuildAnonymousDraftKey(string anonymousDraftId)
        {
            return $"anonymous.{anonymousDraftId.Trim()}";
        }

        private static bool TryGetDraftKey(
            BlueprintAsset asset,
            out string draftKey,
            out string assetPath,
            out string error)
        {
            draftKey = string.Empty;
            assetPath = string.Empty;
            error = string.Empty;

            if (asset == null)
            {
                error = "BlueprintAsset 为空。";
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "当前蓝图尚未保存为项目资产，无法生成本地草稿键。";
                return false;
            }

            draftKey = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.IsNullOrWhiteSpace(draftKey))
                return true;

            if (!string.IsNullOrWhiteSpace(asset.BlueprintId))
            {
                draftKey = asset.BlueprintId.Trim();
                return true;
            }

            error = $"无法为蓝图生成草稿键: {assetPath}";
            return false;
        }
    }
}
