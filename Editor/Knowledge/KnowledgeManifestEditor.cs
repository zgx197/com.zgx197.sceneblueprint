#nullable enable
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Contract.Knowledge;
using SceneBlueprint.Runtime.Knowledge;

namespace SceneBlueprint.Editor.Knowledge
{
    /// <summary>
    /// KnowledgeManifest 的自定义 Inspector。
    /// 分 Tab 显示共享层、程序视角、策划视角和 Prompt 配置。
    /// </summary>
    [CustomEditor(typeof(KnowledgeManifest))]
    public class KnowledgeManifestEditor : UnityEditor.Editor
    {
        private enum Tab { Shared, Developer, Designer, Prompt }
        private Tab _currentTab = Tab.Shared;

        // 序列化属性缓存
        private SerializedProperty? _coreConcepts;
        private SerializedProperty? _architecture;
        private SerializedProperty? _coreLogic;
        private SerializedProperty? _decisions;
        private SerializedProperty? _workflow;
        private SerializedProperty? _actionGuides;
        private SerializedProperty? _markerGuide;
        private SerializedProperty? _faq;
        private SerializedProperty? _promptConfigJson;

        private void OnEnable()
        {
            _coreConcepts    = serializedObject.FindProperty("CoreConcepts");
            _architecture    = serializedObject.FindProperty("Architecture");
            _coreLogic       = serializedObject.FindProperty("CoreLogic");
            _decisions       = serializedObject.FindProperty("Decisions");
            _workflow        = serializedObject.FindProperty("Workflow");
            _actionGuides    = serializedObject.FindProperty("ActionGuides");
            _markerGuide     = serializedObject.FindProperty("MarkerGuide");
            _faq             = serializedObject.FindProperty("FAQ");
            _promptConfigJson = serializedObject.FindProperty("PromptConfigJson");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            DrawTabs();

            switch (_currentTab)
            {
                case Tab.Shared:    DrawSharedTab();    break;
                case Tab.Developer: DrawDeveloperTab(); break;
                case Tab.Designer:  DrawDesignerTab();  break;
                case Tab.Prompt:    DrawPromptTab();    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ══════════════════════════════════════
        //  Header
        // ══════════════════════════════════════

        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("SceneBlueprint Knowledge Base", style);
            EditorGUILayout.Space(2);

            var manifest = (KnowledgeManifest)target;
            int docCount = CountDocs(manifest);
            EditorGUILayout.HelpBox($"共索引 {docCount} 篇知识文档。\n程序在 .md 文件中编写内容，此处管理索引和元数据。", MessageType.Info);
            EditorGUILayout.Space(4);
        }

        // ══════════════════════════════════════
        //  Tab Bar
        // ══════════════════════════════════════

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            if (DrawTabButton("共享 (S)", Tab.Shared))   _currentTab = Tab.Shared;
            if (DrawTabButton("程序 (D)", Tab.Developer)) _currentTab = Tab.Developer;
            if (DrawTabButton("策划 (P)", Tab.Designer))  _currentTab = Tab.Designer;
            if (DrawTabButton("Prompt",   Tab.Prompt))    _currentTab = Tab.Prompt;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private bool DrawTabButton(string label, Tab tab)
        {
            var style = _currentTab == tab
                ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold }
                : GUI.skin.button;
            return GUILayout.Button(label, style);
        }

        // ══════════════════════════════════════
        //  Tab Content
        // ══════════════════════════════════════

        private void DrawSharedTab()
        {
            EditorGUILayout.LabelField("S0: 核心概念词汇表", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("统一术语表，程序和策划共用。", MessageType.None);
            DrawDocRef(_coreConcepts);
        }

        private void DrawDeveloperTab()
        {
            EditorGUILayout.LabelField("D0: 框架架构设计", EditorStyles.boldLabel);
            DrawDocRef(_architecture);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("D1: 核心代码逻辑", EditorStyles.boldLabel);
            DrawDocRef(_coreLogic);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("D2: 设计决策记录 (ADR)", EditorStyles.boldLabel);
            DrawDocRef(_decisions);
        }

        private void DrawDesignerTab()
        {
            EditorGUILayout.LabelField("P0: 工作流全景", EditorStyles.boldLabel);
            DrawDocRef(_workflow);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("P1: 节点使用手册", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("每个 Action 节点一条文档引用。", MessageType.None);
            if (_actionGuides != null)
                EditorGUILayout.PropertyField(_actionGuides, new GUIContent("Action 文档列表"), true);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("P2: Marker 使用手册", EditorStyles.boldLabel);
            DrawDocRef(_markerGuide);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("P3: FAQ / 问题排查", EditorStyles.boldLabel);
            DrawDocRef(_faq);
        }

        private void DrawPromptTab()
        {
            EditorGUILayout.LabelField("Prompt 配置文件", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("指向 default_prompt.json（TextAsset）。\n包含双角色的 SystemPrompt、ContextTemplate 和 Few-shot 示例。", MessageType.None);
            if (_promptConfigJson != null)
                EditorGUILayout.PropertyField(_promptConfigJson, new GUIContent("Prompt Config JSON"));

            EditorGUILayout.Space(8);

            // 预览按钮
            var manifest = (KnowledgeManifest)target;
            if (manifest.PromptConfigJson != null && GUILayout.Button("预览 Prompt 配置"))
            {
                var config = manifest.LoadPromptConfig();
                if (config != null)
                {
                    string preview = $"Version: {config.Version}\n\n";
                    if (config.Developer != null)
                        preview += $"=== 程序角色 ===\n{TruncatePreview(config.Developer.SystemPrompt, 200)}\n\n";
                    if (config.Designer != null)
                        preview += $"=== 策划角色 ===\n{TruncatePreview(config.Designer.SystemPrompt, 200)}\n\n";
                    UnityEngine.Debug.Log($"[Knowledge] Prompt 配置预览:\n{preview}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[Knowledge] Prompt 配置解析失败，请检查 JSON 格式。");
                }
            }
        }

        // ══════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════

        private static void DrawDocRef(SerializedProperty? prop)
        {
            if (prop == null) return;
            EditorGUILayout.PropertyField(prop, true);
        }

        private static int CountDocs(KnowledgeManifest manifest)
        {
            int count = 0;
            if (!string.IsNullOrEmpty(manifest.CoreConcepts?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.Architecture?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.CoreLogic?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.Decisions?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.Workflow?.Entry.Title)) count++;
            if (manifest.ActionGuides != null)
            {
                foreach (var g in manifest.ActionGuides)
                    if (!string.IsNullOrEmpty(g?.Entry.Title)) count++;
            }
            if (!string.IsNullOrEmpty(manifest.MarkerGuide?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.FAQ?.Entry.Title)) count++;
            return count;
        }

        private static string TruncatePreview(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "(空)";
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
        }
    }
}
