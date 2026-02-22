#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 黑板变量编辑服务——负责 Workbench 面板中"变量声明"区域的全部 UI 绘制与数据操作。
    /// <para>
    /// 职责：
    /// - 绘制用户声明变量列表（增删改 + Undo）
    /// - 扫描图中节点的 OutputVariables 并生成只读条目
    /// - 提供 <see cref="BuildCombinedVariables"/> 供 Inspector 的变量选择器使用
    /// </para>
    /// <para>
    /// 不持有任何持久状态；所有数据访问均通过 <see cref="IBlueprintEditorContext"/>。
    /// </para>
    /// </summary>
    public sealed class BlackboardVariableEditorService
    {
        private readonly IBlueprintReadContext _ctx;

        private static readonly string[] VarTypeOptions  = { "Int", "Float", "Bool", "String" };
        private static readonly string[] VarScopeOptions = { "Local", "Global" };

        private Vector2 _scrollPos;
        private VariableDeclaration[]? _cachedVars;
        private bool _varsDirty = true;

        public BlackboardVariableEditorService(IBlueprintReadContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>通知缓存失效（命令执行或图结构变化时调用）</summary>
        public void MarkVariablesDirty() => _varsDirty = true;

        // ── 面板绘制 ──

        /// <summary>绘制完整的黑板变量面板（含工具栏、声明列表、节点产出只读列表）。</summary>
        public void DrawBlackboardPanel()
        {
            var asset = _ctx.CurrentAsset;
            if (asset == null)
            {
                EditorGUILayout.HelpBox("请先保存蓝图资产（BlueprintAsset）以使用变量面板。", MessageType.Info);
                return;
            }

            var vars = asset.Variables ?? System.Array.Empty<VariableDeclaration>();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                EditorGUILayout.LabelField($"变量声明 ({vars.Length})", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ 添加", EditorStyles.toolbarButton, GUILayout.Width(52)))
                {
                    Undo.RecordObject(asset, "Add Blackboard Variable");
                    var list = new List<VariableDeclaration>(vars);
                    int nextIndex = list.Count > 0 ? list.Max(v => v.Index) + 1 : 0;
                    list.Add(new VariableDeclaration
                    {
                        Index        = nextIndex,
                        Name         = $"var_{nextIndex}",
                        Type         = "Int",
                        Scope        = "Local",
                        InitialValue = "0"
                    });
                    asset.Variables = list.ToArray();
                    EditorUtility.SetDirty(asset);
                }
            }
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            {
                if (vars.Length == 0)
                {
                    EditorGUILayout.HelpBox("暂无变量。点击\"添加\"声明第一个变量。", MessageType.None);
                }
                else
                {
                    int toRemove = -1;
                    for (int i = 0; i < vars.Length; i++)
                    {
                        if (DrawVariableEntry(asset, vars[i]))
                            toRemove = i;
                    }
                    if (toRemove >= 0)
                    {
                        Undo.RecordObject(asset, "Remove Blackboard Variable");
                        var list = new List<VariableDeclaration>(asset.Variables);
                        list.RemoveAt(toRemove);
                        asset.Variables = list.ToArray();
                        EditorUtility.SetDirty(asset);
                        GUIUtility.ExitGUI();
                    }
                }

                var nodeOutVars = CollectNodeOutputVariables();
                if (nodeOutVars.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("节点产出变量（只读）", EditorStyles.miniLabel);
                    foreach (var nov in nodeOutVars)
                        DrawReadonlyVariableEntry(nov);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // ── 变量合并（供 Inspector 选择器使用）──

        /// <summary>合并用户声明变量 + 节点产出变量。内部缓存，仅在脏时重建。</summary>
        public VariableDeclaration[] BuildCombinedVariables()
        {
            if (!_varsDirty && _cachedVars != null) return _cachedVars;
            var userVars  = _ctx.CurrentAsset?.Variables ?? System.Array.Empty<VariableDeclaration>();
            var nodeVars  = CollectNodeOutputVariables();
            _cachedVars = nodeVars.Count == 0 ? userVars : Combine(userVars, nodeVars);
            _varsDirty  = false;
            return _cachedVars;
        }

        private static VariableDeclaration[] Combine(VariableDeclaration[] userVars, List<VariableDeclaration> nodeVars)
        {
            var combined = new List<VariableDeclaration>(userVars);
            combined.AddRange(nodeVars);
            return combined.ToArray();
        }

        // ── 私有辅助 ──

        /// <summary>
        /// 扫描图中所有节点的 OutputVariables，按名称去重，分配稳定的合成 Index。
        /// 合成 Index 范围：10000–19999（避免与用户声明的 0–9999 冲突）。
        /// </summary>
        private List<VariableDeclaration> CollectNodeOutputVariables()
        {
            var result   = new List<VariableDeclaration>();
            var viewModel = _ctx.ViewModel;
            if (viewModel == null) return result;

            var registry = _ctx.ActionRegistry;
            var seen     = new HashSet<string>();

            foreach (var node in viewModel.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var def)) continue;

                foreach (var outVar in def.OutputVariables)
                {
                    if (seen.Contains(outVar.Name)) continue;
                    seen.Add(outVar.Name);
                    result.Add(new VariableDeclaration
                    {
                        Index        = NodeOutputVarIndex(outVar.Name),
                        Name         = outVar.Name,
                        Type         = outVar.Type,
                        Scope        = outVar.Scope,
                        InitialValue = ""
                    });
                }
            }
            return result;
        }

        /// <summary>DJB2 hash of name → 10000–19999（稳定、无 Unity 依赖）。</summary>
        private static int NodeOutputVarIndex(string name)
        {
            uint h = 5381;
            foreach (char c in name) h = ((h << 5) + h) + c;
            return 10000 + (int)(h % 10000);
        }

        /// <summary>绘制一条可编辑的变量条目，返回 true 表示用户点击了删除。</summary>
        private static bool DrawVariableEntry(BlueprintAsset asset, VariableDeclaration decl)
        {
            bool removed = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                // 第一行：[Index] + 名称 + [×]
                EditorGUILayout.BeginHorizontal();
                {
                    GUI.color = new Color(0.7f, 0.9f, 1f);
                    GUILayout.Label($"[{decl.Index}]", EditorStyles.miniLabel, GUILayout.Width(24));
                    GUI.color = Color.white;

                    EditorGUILayout.LabelField("名称", GUILayout.Width(28));
                    string newName = EditorGUILayout.TextField(decl.Name, GUILayout.MinWidth(60));
                    if (newName != decl.Name)
                    {
                        Undo.RecordObject(asset, "Rename Blackboard Variable");
                        decl.Name = newName;
                        EditorUtility.SetDirty(asset);
                    }

                    GUI.color = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20)))
                        removed = true;
                    GUI.color = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                // 第二行：类型 + 作用域 + 初始值
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("类型", GUILayout.Width(28));
                    int typeIdx    = System.Array.IndexOf(VarTypeOptions, decl.Type);
                    int newTypeIdx = EditorGUILayout.Popup(typeIdx < 0 ? 0 : typeIdx, VarTypeOptions, GUILayout.Width(60));
                    if (VarTypeOptions[newTypeIdx] != decl.Type)
                    {
                        Undo.RecordObject(asset, "Edit Variable Type");
                        decl.Type = VarTypeOptions[newTypeIdx];
                        EditorUtility.SetDirty(asset);
                    }

                    EditorGUILayout.LabelField("作用域", GUILayout.Width(36));
                    int scopeIdx    = decl.Scope == "Global" ? 1 : 0;
                    int newScopeIdx = EditorGUILayout.Popup(scopeIdx, VarScopeOptions, GUILayout.Width(54));
                    if (newScopeIdx != scopeIdx)
                    {
                        Undo.RecordObject(asset, "Edit Variable Scope");
                        decl.Scope = VarScopeOptions[newScopeIdx];
                        EditorUtility.SetDirty(asset);
                    }

                    EditorGUILayout.LabelField("初始值", GUILayout.Width(36));
                    string newInit = EditorGUILayout.TextField(decl.InitialValue, GUILayout.MinWidth(40));
                    if (newInit != decl.InitialValue)
                    {
                        Undo.RecordObject(asset, "Edit Variable InitialValue");
                        decl.InitialValue = newInit;
                        EditorUtility.SetDirty(asset);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            return removed;
        }

        /// <summary>以灰色只读样式绘制一条节点产出变量条目。</summary>
        private static void DrawReadonlyVariableEntry(VariableDeclaration decl)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    GUI.color = new Color(0.6f, 0.8f, 1f);
                    GUILayout.Label($"[{decl.Index}]", EditorStyles.miniLabel, GUILayout.Width(44));
                    GUI.color = new Color(0.85f, 0.85f, 0.85f);
                    GUILayout.Label(decl.Name, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    GUI.color = new Color(0.7f, 0.9f, 0.7f);
                    GUILayout.Label($"{decl.Type}  {decl.Scope}", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            GUI.color = prevColor;
        }
    }
}
