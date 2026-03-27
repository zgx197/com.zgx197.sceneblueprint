#nullable enable
using System.Linq;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Editor.Persistence;
using SceneBlueprint.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Settings
{
    /// <summary>
    /// SceneBlueprint 统一配置中心窗口。
    /// <para>
    /// 这是统一配置中心在 Editor 层的唯一入口，负责将个人配置、项目配置、框架默认配置
    /// 收敛到同一个窗口中展示和编辑。
    /// </para>
    /// <para>
    /// Phase 1 中它先承担配置骨架和统一入口职责；
    /// Phase 2 起，它开始与新的 UserConfig 后端正式协同工作，成为 AI / Prompt / MCP / UI 偏好的主入口之一。
    /// </para>
    /// </summary>
    public sealed class SceneBlueprintSettingsHubWindow : EditorWindow
    {
        /// <summary>
        /// 顶层配置分类。
        /// </summary>
        private enum RootTab
        {
            User = 0,
            Project = 1,
            Framework = 2,
        }

        private Vector2 _scrollPos;
        private readonly AISettingsDrawerState _aiDrawerState = new AISettingsDrawerState();
        private SerializedObject? _projectSerializedObject;
        private SerializedObject? _userSerializedObject;
        private SerializedObject? _frameworkSerializedObject;
        private RootTab _activeTab;
        private GUIStyle? _activeTabStyle;
        private GUIStyle? _inactiveTabStyle;
        /// <summary>
        /// 打开或聚焦统一配置中心窗口。
        /// </summary>
        [MenuItem("SceneBlueprint/设置中心", priority = 995)]
        public static SceneBlueprintSettingsHubWindow ShowWindow()
        {
            var window = GetWindow<SceneBlueprintSettingsHubWindow>();
            window.titleContent = new GUIContent("SceneBlueprint 设置中心");
            window.minSize = new Vector2(520f, 580f);
            window.Show();
            return window;
        }

        /// <summary>
        /// 窗口启用时，确保配置存储存在，并恢复上次打开的页签。
        /// </summary>
        private void OnEnable()
        {
            SceneBlueprintSettingsService.EnsureStorages();
            _activeTab = (RootTab)Mathf.Clamp(SceneBlueprintSettingsService.User.UI.SelectedTabIndex, 0, 2);
            RebuildSerializedObjects();
        }

        /// <summary>
        /// 聚焦窗口时刷新序列化对象，避免外部修改后界面仍持有旧引用。
        /// </summary>
        private void OnFocus()
        {
            RebuildSerializedObjects();
        }

        /// <summary>
        /// 窗口关闭时保存当前页签索引。
        /// </summary>
        private void OnDisable()
        {
            var user = SceneBlueprintSettingsService.User;
            user.UI.SelectedTabIndex = (int)_activeTab;
            user.SaveConfig();
        }

        /// <summary>
        /// 主绘制入口。
        /// </summary>
        private void OnGUI()
        {
            InitStyles();
            DrawHeader();
            DrawTabBar();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            switch (_activeTab)
            {
                case RootTab.User:
                    DrawUserTab();
                    break;
                case RootTab.Project:
                    DrawProjectTab();
                    break;
                case RootTab.Framework:
                    DrawFrameworkTab();
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制窗口头部说明。
        /// </summary>
        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("SceneBlueprint 统一配置中心 Settings Hub", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "当前基于统一配置中心的新配置容器工作；不兼容旧数据，也不自动迁移旧配置。",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static void DrawProjectRuntimeSection(SerializedProperty? property)
        {
            if (property == null)
            {
                EditorGUILayout.HelpBox("未找到项目运行时配置。", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("运行时配置 Runtime", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("控制蓝图运行时 Tick、测试行为与调试开关。", EditorStyles.wordWrappedMiniLabel);

            DrawBilingualProperty(property.FindPropertyRelative("TargetTickRate"), "目标 Tick 频率", "Target Tick Rate", "逻辑每秒执行多少个 Tick。建议保持为项目统一值。");
            DrawBilingualProperty(property.FindPropertyRelative("TicksPerFrame"), "每帧 Tick 数", "Ticks Per Frame", "0 表示自动模式；大于 0 时表示固定每帧执行的 Tick 数。\n通常只在测试或特殊运行模式中手动设置。", false);
            DrawBilingualProperty(property.FindPropertyRelative("TimeRoundingMode"), "时间舍入模式", "Time Rounding Mode", "秒数换算到 Tick 时使用的统一舍入策略。一般推荐 Ceil，避免等待时间短于配置值。", false);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("测试与调试 Test & Debug", EditorStyles.miniBoldLabel);
            DrawBilingualProperty(property.FindPropertyRelative("AutoRunInTestScene"), "测试场景自动运行", "Auto Run In Test Scene", "运行时测试场景启动后，是否自动加载并执行蓝图。", false);
            DrawBilingualProperty(property.FindPropertyRelative("MaxTicksLimit"), "最大 Tick 限制", "Max Ticks Limit", "测试窗口中 Run Until Complete 的最大 Tick 上限，用于防止死循环。", false);
            DrawBilingualProperty(property.FindPropertyRelative("BatchTickCount"), "批量 Tick 数", "Batch Tick Count", "测试窗口里“执行 N Ticks”按钮默认一次执行的 Tick 数。", false);
            DrawBilingualProperty(property.FindPropertyRelative("EnableDetailedLogs"), "启用详细日志", "Enable Detailed Logs", "开启后会输出更完整的运行时诊断日志。", false);
            DrawBilingualProperty(property.FindPropertyRelative("LogSystemExecution"), "记录系统执行", "Log System Execution", "记录各个系统的执行过程，便于问题定位。", false);
            DrawBilingualProperty(property.FindPropertyRelative("ShowPerformanceStats"), "显示性能统计", "Show Performance Stats", "在运行时测试环境中展示性能统计信息。", false);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private void DrawProjectMonsterMappingSection(SceneBlueprintProjectConfig project, SerializedProperty? property)
        {
            if (property == null)
            {
                EditorGUILayout.HelpBox("未找到项目怪物映射配置。", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("怪物映射 Monster Mapping", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "映射资产是唯一数据源。设置中心只展示当前已注册的关卡映射，并提供创建与定位入口；具体映射内容请直接在资产 Inspector 中维护。",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2f);

            var rootFolderPath = property.FindPropertyRelative("RootFolderPath");
            if (rootFolderPath == null)
            {
                EditorGUILayout.HelpBox("怪物映射根目录字段缺失。", MessageType.Warning);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
                return;
            }

            EditorGUILayout.PropertyField(
                rootFolderPath,
                new GUIContent("映射根目录 Root Folder", "分关卡怪物映射资产存放的项目目录。"));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("确保目录存在 Ensure Folder", GUILayout.Width(180f)))
            {
                if (property.serializedObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(project);
                    AssetDatabase.SaveAssets();
                }
                SceneBlueprintMonsterMappingRegistry.EnsureRootFolder();
                SceneBlueprintMonsterMappingRegistry.Invalidate();
            }
            if (GUILayout.Button("新增关卡资产 Add Level Asset", GUILayout.Width(210f)))
            {
                if (property.serializedObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(project);
                    AssetDatabase.SaveAssets();
                }
                int nextLevelId = SceneBlueprintMonsterMappingRegistry
                    .GetAllAssets()
                    .Select(static asset => asset.LevelId)
                    .DefaultIfEmpty(-1)
                    .Max() + 1;
                if (SceneBlueprintMonsterMappingRegistry.TryCreateLevelAsset(nextLevelId, out var createdAsset, out _, out var error))
                {
                    Selection.activeObject = createdAsset;
                    EditorGUIUtility.PingObject(createdAsset);
                }
                else if (!string.IsNullOrWhiteSpace(error))
                {
                    EditorUtility.DisplayDialog("创建失败", error, "确定");
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            var issues = SceneBlueprintMonsterMappingRegistry.GetIssues();
            if (issues.Count > 0)
            {
                EditorGUILayout.Space(2f);
                for (int i = 0; i < issues.Count; i++)
                    EditorGUILayout.HelpBox(issues[i], MessageType.Warning);
            }

            var assets = SceneBlueprintMonsterMappingRegistry.GetAllAssets();
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(
                $"已注册关卡资产 {assets.Count} 个 Registered Level Assets",
                EditorStyles.miniBoldLabel);

            if (assets.Count == 0)
            {
                EditorGUILayout.HelpBox("当前还没有任何分关卡怪物映射资产。", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < assets.Count; i++)
                    DrawMonsterMappingAssetCard(assets[i]);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static void DrawProjectSpawnAuthoringSection(SerializedProperty? property)
        {
            if (property == null)
            {
                EditorGUILayout.HelpBox("未找到项目刷怪编辑约束配置。", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("刷怪编辑约束 Spawn Authoring", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "控制 MonsterPool / Spawn 相关 Inspector 的项目级输入上限。这里是团队共享规则，不是运行时数据。",
                EditorStyles.wordWrappedMiniLabel);

            DrawBilingualProperty(
                property.FindPropertyRelative("MaxMonsterPoolEntryStock"),
                "MonsterPool 条目最大库存",
                "Max MonsterPool Entry Stock",
                "MonsterPool 供给条目“库存”输入框允许的最大值。最小值固定为 0，默认值为 100。",
                false);
            DrawBilingualProperty(
                property.FindPropertyRelative("DefaultVisionRange"),
                "默认视觉范围",
                "Default Vision Range",
                "新建 MonsterPool 条目时使用的默认视觉范围。默认值为 10。",
                false);
            DrawBilingualProperty(
                property.FindPropertyRelative("DefaultHearingRange"),
                "默认听觉范围",
                "Default Hearing Range",
                "新建 MonsterPool 条目时使用的默认听觉范围。默认值为 10。",
                false);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private void DrawMonsterMappingAssetCard(SceneBlueprintLevelMonsterMappingAsset asset)
        {
            if (asset == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(asset);
            var snapshot = asset.CreateSnapshot();
            string summary = BuildMonsterMappingSummary(snapshot);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"关卡 Level {snapshot.LevelId}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("定位资产 Locate", EditorStyles.miniButtonLeft, GUILayout.Width(92f)))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            if (GUILayout.Button("打开编辑 Open Inspector", EditorStyles.miniButtonRight, GUILayout.Width(132f)))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("资产路径 Asset Path", assetPath);
                EditorGUILayout.IntField("关卡 ID Level ID", snapshot.LevelId);
                EditorGUILayout.IntField("条目数 Entry Count", snapshot.Entries.Count);
            }

            if (!string.IsNullOrWhiteSpace(summary))
                EditorGUILayout.LabelField("摘要 Summary", summary, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        private static string BuildMonsterMappingSummary(SceneBlueprintLevelMonsterMapping snapshot)
        {
            if (snapshot.Entries.Count == 0)
                return "当前关卡还没有任何映射条目。";

            return string.Join(
                " | ",
                snapshot.Entries
                    .Take(3)
                    .Select(static entry =>
                    {
                        string label = string.IsNullOrWhiteSpace(entry.ShortName)
                            ? entry.MonsterId.ToString()
                            : entry.ShortName;
                        return $"{entry.MonsterType}->{label}";
                    }));
        }

        private static void DrawProjectFrameworkOverridesSection(SerializedProperty? property)
        {
            if (property == null)
            {
                EditorGUILayout.HelpBox("未找到框架覆盖配置。", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("框架覆盖 Framework Overrides", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("项目层对框架默认能力的选择结果。目前主要承载空间模式。", EditorStyles.wordWrappedMiniLabel);

            var spatialMode = property.FindPropertyRelative("SpatialModeId");
            if (spatialMode != null)
            {
                var descriptors = SpatialModeRegistry.GetAll().OrderBy(d => d.DisplayName).ToList();
                if (descriptors.Count == 0)
                {
                    EditorGUILayout.PropertyField(spatialMode, new GUIContent("空间模式 Spatial Mode"));
                }
                else
                {
                    var options = descriptors.Select(d => $"{d.DisplayName} ({d.ModeId})").ToArray();
                    var ids = descriptors.Select(d => d.ModeId).ToArray();
                    int currentIndex = System.Array.FindIndex(ids, id => string.Equals(id, spatialMode.stringValue, System.StringComparison.OrdinalIgnoreCase));
                    if (currentIndex < 0)
                    {
                        currentIndex = 0;
                    }

                    EditorGUI.BeginChangeCheck();
                    int nextIndex = EditorGUILayout.Popup(new GUIContent("空间模式 Spatial Mode", "影响 SceneView 放置逻辑与导出时使用的空间模式编码。"), currentIndex, options);
                    if (EditorGUI.EndChangeCheck())
                    {
                        spatialMode.stringValue = ids[nextIndex];
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static void DrawProjectIntegrationSection(SerializedProperty? property)
        {
            if (property == null)
            {
                EditorGUILayout.HelpBox("未找到项目集成配置。", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("项目集成 Integration", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("用于承载导出流程、业务桥接和项目侧扩展参数。", EditorStyles.wordWrappedMiniLabel);
            DrawBilingualProperty(property.FindPropertyRelative("ExportProfileId"), "导出配置 ID", "Export Profile ID", "可用于标记当前项目采用的导出配置模板。", false);
            DrawBilingualProperty(property.FindPropertyRelative("Notes"), "备注", "Notes", "记录当前项目集成层的补充说明。", false);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static void DrawUserWorkspaceSection(SerializedProperty? property)
        {
            if (property == null)
            {
                EditorGUILayout.HelpBox("未找到编辑器工作区配置。", MessageType.Warning);
                return;
            }

            var restoreOnOpen = property.FindPropertyRelative("RestoreLastBlueprintOnOpen");
            var enableLocalDraftAutosave = property.FindPropertyRelative("EnableLocalDraftAutosave");
            var draftAutosaveIntervalSeconds = property.FindPropertyRelative("DraftAutosaveIntervalSeconds");
            var draftAutosaveIdleDelaySeconds = property.FindPropertyRelative("DraftAutosaveIdleDelaySeconds");
            var lastGuid = property.FindPropertyRelative("LastOpenedBlueprintAssetGuid");
            var lastPath = property.FindPropertyRelative("LastOpenedBlueprintAssetPath");
            var lastAnonymousDraftId = property.FindPropertyRelative("LastAnonymousDraftId");
            var lastScenePath = property.FindPropertyRelative("LastAnchoredScenePath");
            if (restoreOnOpen == null
                || enableLocalDraftAutosave == null
                || draftAutosaveIntervalSeconds == null
                || draftAutosaveIdleDelaySeconds == null
                || lastGuid == null
                || lastPath == null
                || lastAnonymousDraftId == null
                || lastScenePath == null)
            {
                EditorGUILayout.HelpBox("编辑器工作区配置字段不完整。", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("工作区恢复 Workspace Restore", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "控制蓝图主窗口是否在打开时自动恢复上次蓝图。最近记录由系统维护，仅用于本地恢复，不进入版本控制。",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.PropertyField(
                restoreOnOpen,
                new GUIContent(
                    "打开蓝图编辑器时自动恢复上次蓝图 Restore Last Blueprint On Open",
                    "关闭后，蓝图主窗口从菜单打开时不会自动恢复最近一次蓝图工作区。"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("本地草稿自动保存 Local Draft Autosave", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(
                enableLocalDraftAutosave,
                new GUIContent(
                    "启用本地草稿自动保存 Enable Local Draft Autosave",
                    "自动将当前蓝图工作草稿保存到 UserSettings，本地私有、不进入版本控制，不直接写回正式资产。"));

            using (new EditorGUI.DisabledScope(!enableLocalDraftAutosave.boolValue))
            {
                EditorGUILayout.PropertyField(
                    draftAutosaveIntervalSeconds,
                    new GUIContent(
                        "自动保存间隔秒数 Draft Autosave Interval Seconds",
                        "达到该间隔后，若蓝图存在未保存改动且用户已空闲一段时间，则写入本地草稿。"));
                EditorGUILayout.PropertyField(
                    draftAutosaveIdleDelaySeconds,
                    new GUIContent(
                        "空闲延迟秒数 Draft Autosave Idle Delay Seconds",
                        "用户停止交互达到该时长后，才会触发本地草稿保存，避免频繁打断编辑。"));
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("草稿目录 Draft Directory", BlueprintAutosaveDraftStore.DraftDirectoryDisplayPath);
            }

            EditorGUILayout.Space(2f);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("最近蓝图路径 Last Blueprint Path", lastPath.stringValue);
                EditorGUILayout.TextField("最近蓝图 GUID Last Blueprint GUID", lastGuid.stringValue);
                EditorGUILayout.TextField("最近匿名草稿 ID Last Anonymous Draft ID", lastAnonymousDraftId.stringValue);
                EditorGUILayout.TextField("最近锚定场景 Last Anchored Scene", lastScenePath.stringValue);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("清除最近记录 Clear Recent Workspace", GUILayout.Width(220f)))
            {
                lastGuid.stringValue = string.Empty;
                lastPath.stringValue = string.Empty;
                lastAnonymousDraftId.stringValue = string.Empty;
                lastScenePath.stringValue = string.Empty;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private static void DrawBilingualProperty(SerializedProperty? property, string zh, string en, string tooltip, bool includeChildren = true)
        {
            if (property == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent($"{zh} {en}", tooltip), includeChildren);
        }

        /// <summary>
        /// 绘制顶层页签栏。
        /// </summary>
        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Toggle(_activeTab == RootTab.User, "个人配置 User",
                    _activeTab == RootTab.User ? _activeTabStyle : _inactiveTabStyle,
                    GUILayout.Width(120f)))
            {
                _activeTab = RootTab.User;
            }

            if (GUILayout.Toggle(_activeTab == RootTab.Project, "项目配置 Project",
                    _activeTab == RootTab.Project ? _activeTabStyle : _inactiveTabStyle,
                    GUILayout.Width(130f)))
            {
                _activeTab = RootTab.Project;
            }

            if (GUILayout.Toggle(_activeTab == RootTab.Framework, "框架默认 Framework",
                    _activeTab == RootTab.Framework ? _activeTabStyle : _inactiveTabStyle,
                    GUILayout.Width(150f)))
            {
                _activeTab = RootTab.Framework;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// 绘制“个人配置”页。
        /// <para>
        /// 这里不再只是裸序列化字段展示，而是优先复用 AISettingsDrawer，
        /// 让新的 Settings Hub 与 Phase 2 的用户配置后端直接协同。
        /// </para>
        /// </summary>
        private void DrawUserTab()
        {
            var user = SceneBlueprintSettingsService.User;
            _userSerializedObject ??= new SerializedObject(user);
            _userSerializedObject.Update();

            DrawPathBox("个人配置 User Config", SceneBlueprintSettingsService.UserAssetRelativePath,
                "本地私有，不进版本控制。当前阶段只编辑新 UserConfig 数据，不迁移旧 EditorPrefs。");

            AISettingsDrawer.Draw(_aiDrawerState);
            EditorGUILayout.Space(4f);

            _userSerializedObject.Update();

            DrawSerializedSection(_userSerializedObject, "Mcp", "MCP 用户配置 MCP");
            DrawUserWorkspaceSection(_userSerializedObject.FindProperty("Workspace"));
            DrawSerializedSection(_userSerializedObject, "UI", "编辑器 UI 配置 Editor UI");

            if (_userSerializedObject.ApplyModifiedProperties())
            {
                user.SaveConfig();
            }
        }

        /// <summary>
        /// 绘制“项目配置”页。
        /// </summary>
        private void DrawProjectTab()
        {
            var project = SceneBlueprintSettingsService.Project;
            _projectSerializedObject ??= new SerializedObject(project);
            _projectSerializedObject.Update();

            DrawPathBox("项目配置 Project Config", SceneBlueprintSettingsService.ProjectAssetPath,
                "团队共享、建议提交版本控制。Phase 1 中该资产是新配置中心的正式项目数据源。",
                project);

            DrawProjectRuntimeSection(_projectSerializedObject.FindProperty("_runtime"));
            DrawProjectMonsterMappingSection(project, _projectSerializedObject.FindProperty("_monsterMappingSettings"));
            DrawProjectSpawnAuthoringSection(_projectSerializedObject.FindProperty("_spawnAuthoringSettings"));
            DrawProjectFrameworkOverridesSection(_projectSerializedObject.FindProperty("_frameworkOverrides"));
            DrawProjectIntegrationSection(_projectSerializedObject.FindProperty("_integration"));

            if (_projectSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(project);
                SceneBlueprintMonsterMappingRegistry.EnsureRootFolder();
                SceneBlueprintMonsterMappingRegistry.Invalidate();
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// 绘制“框架默认配置”页。
        /// <para>
        /// 当前阶段只读展示，避免误把用户改动直接写回 Package 资产。
        /// </para>
        /// </summary>
        private void DrawFrameworkTab()
        {
            var framework = SceneBlueprintSettingsService.Framework;
            DrawPathBox("框架默认配置 Framework Defaults", SceneBlueprintSettingsService.FrameworkAssetPath,
                "来自 Package 的默认基线。当前策略为优先读取，不默认写回 Package。",
                framework);

            if (framework == null)
            {
                EditorGUILayout.HelpBox(
                    "未找到 FrameworkConfig 资产。当前阶段将继续使用代码默认值，后续可按需补充 Package 默认资产。",
                    MessageType.Info);
                return;
            }

            _frameworkSerializedObject ??= new SerializedObject(framework);
            _frameworkSerializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                DrawSerializedSection(_frameworkSerializedObject, "_aiProviders", "AI Provider 预设 AI Providers");
                DrawSerializedSection(_frameworkSerializedObject, "_embeddingProviders", "Embedding Provider 预设 Embedding Providers");
                DrawSerializedSection(_frameworkSerializedObject, "_promptTemplates", "Prompt 模板 Prompt Templates");
                DrawSerializedSection(_frameworkSerializedObject, "_defaults", "框架默认值 Framework Defaults");
            }
            _frameworkSerializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 绘制单个序列化 section。
        /// </summary>
        private static void DrawSerializedSection(SerializedObject serializedObject, string propertyName, string label)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox("未找到序列化字段：" + propertyName, MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren: true);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// 绘制配置路径说明盒子。
        /// </summary>
        private static void DrawPathBox(string title, string path, string description, Object? asset = null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            if (asset != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("定位资产", GUILayout.Width(80f)))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// 重新构建所有序列化对象引用。
        /// </summary>
        private void RebuildSerializedObjects()
        {
            _projectSerializedObject = new SerializedObject(SceneBlueprintSettingsService.Project);
            _userSerializedObject = new SerializedObject(SceneBlueprintSettingsService.User);

            var framework = SceneBlueprintSettingsService.Framework;
            _frameworkSerializedObject = framework != null ? new SerializedObject(framework) : null;
        }

        /// <summary>
        /// 初始化页签样式。
        /// </summary>
        private void InitStyles()
        {
            if (_activeTabStyle != null)
            {
                return;
            }

            _activeTabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = FontStyle.Bold,
            };
            _inactiveTabStyle = EditorStyles.toolbarButton;
        }
    }
}
