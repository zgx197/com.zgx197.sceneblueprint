#nullable enable
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SceneBlueprint.Runtime.Test;

namespace SceneBlueprint.Editor.Test
{
    /// <summary>
    /// 蓝图场景快捷切换工具。
    /// <para>
    /// 菜单路径：
    /// - SceneBlueprint / 打开运行时测试场景  —— 打开（或首次创建）运行时测试场景
    /// - SceneBlueprint / 打开编辑器关卡       —— 打开关卡编辑场景
    /// </para>
    /// <para>切换前会自动提示保存当前场景的修改。</para>
    /// </summary>
    public static class RuntimeTestSceneBuilder
    {
        private const string SceneSavePath = "Assets/Extensions/SceneBlueprint/Runtime/Test/BlueprintRuntimeTest.unity";
        private const string EditorLevelScenePath = "Assets/ResOriginSources/CustomDungeon/Scene/PuzzleMap_Lv31.unity";
        private const string TestMaterialDir = "Assets/Extensions/SceneBlueprint/Runtime/Test/Materials";

        // ═══════════════════════════════════════════
        //  打开运行时测试场景
        // ═══════════════════════════════════════════

        [MenuItem("SceneBlueprint/打开运行时测试场景", false, 200)]
        public static void OpenOrBuildScene()
        {
            if (!SaveCurrentSceneIfNeeded())
                return;

            // 场景已存在 → 直接打开
            var existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneSavePath);
            if (existingScene != null)
            {
                EditorSceneManager.OpenScene(SceneSavePath);
                UnityEngine.Debug.Log($"[RuntimeTestSceneBuilder] 已打开测试场景: {SceneSavePath}");
                return;
            }

            // 场景不存在 → 新建
            BuildScene();
        }

        // ═══════════════════════════════════════════
        //  打开编辑器关卡
        // ═══════════════════════════════════════════

        [MenuItem("SceneBlueprint/打开编辑器关卡", false, 201)]
        public static void OpenEditorLevel()
        {
            if (!SaveCurrentSceneIfNeeded())
                return;

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorLevelScenePath);
            if (sceneAsset == null)
            {
                EditorUtility.DisplayDialog("错误",
                    $"未找到编辑器关卡场景:\n{EditorLevelScenePath}", "确定");
                return;
            }

            EditorSceneManager.OpenScene(EditorLevelScenePath);
            UnityEngine.Debug.Log($"[RuntimeTestSceneBuilder] 已打开编辑器关卡: {EditorLevelScenePath}");
        }

        // ═══════════════════════════════════════════
        //  通用：保存当前场景
        // ═══════════════════════════════════════════

        /// <summary>
        /// 检查当前场景是否有未保存修改，提示用户保存。
        /// 返回 true 表示可以继续切换，false 表示用户取消。
        /// </summary>
        private static bool SaveCurrentSceneIfNeeded()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                // 弹出保存/不保存/取消三选对话框
                int choice = EditorUtility.DisplayDialogComplex(
                    "保存场景",
                    $"当前场景 \"{activeScene.name}\" 有未保存的修改。\n是否在切换前保存？",
                    "保存",      // 0
                    "取消",      // 1
                    "不保存"     // 2
                );

                switch (choice)
                {
                    case 0: // 保存
                        EditorSceneManager.SaveScene(activeScene);
                        break;
                    case 1: // 取消
                        return false;
                    case 2: // 不保存 — 继续切换
                        break;
                }
            }
            return true;
        }

        private static void BuildScene()
        {
            // 确保材质目录存在
            if (!System.IO.Directory.Exists(TestMaterialDir))
                System.IO.Directory.CreateDirectory(TestMaterialDir);

            // 使用 Unlit/Color 内置 Shader 创建测试专用材质
            var groundMat = CreateTestMaterial("RT_Ground", new Color(0.65f, 0.70f, 0.65f));
            var borderMat = CreateTestMaterial("RT_Border", new Color(0.35f, 0.40f, 0.35f));
            var playerMat = CreateTestMaterial("RT_Player", new Color(0.2f, 0.5f, 1f));

            // 创建新空场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 1. 环境 ──
            CreateEnvironment(groundMat, borderMat);

            // ── 2. 灯光 ──
            CreateLighting();

            // ── 3. 玩家 ──
            CreatePlayer(playerMat);

            // ── 4. 管理器 ──
            CreateManager();

            // 保存场景
            var sceneDir = System.IO.Path.GetDirectoryName(SceneSavePath);
            if (!string.IsNullOrEmpty(sceneDir) && !System.IO.Directory.Exists(sceneDir))
                System.IO.Directory.CreateDirectory(sceneDir);

            EditorSceneManager.SaveScene(scene, SceneSavePath);
            AssetDatabase.Refresh();

            UnityEngine.Debug.Log($"[RuntimeTestSceneBuilder] 测试场景已创建: {SceneSavePath}");
            EditorUtility.DisplayDialog("完成", $"运行时测试场景已创建并保存到:\n{SceneSavePath}", "确定");
        }

        // ─────────────────────────────────────────
        //  材质工具
        // ─────────────────────────────────────────

        /// <summary>
        /// 基于参考材质创建测试专用材质（继承其 Shader），保存为 Asset。
        /// 若参考材质为 null 则使用 Standard Shader 兜底。
        /// </summary>
        /// <summary>
        /// 使用 Unlit/Color 内置 Shader 创建纯色材质（任何渲染管线下都能正常工作）。
        /// </summary>
        private static Material CreateTestMaterial(string name, Color color)
        {
            var path = $"{TestMaterialDir}/{name}.mat";

            // 若已存在则更新颜色
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = Shader.Find("Unlit/Color");
                existing.SetColor("_Color", color);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var mat = new Material(Shader.Find("Unlit/Color")!);
            mat.name = name;
            mat.SetColor("_Color", color);

            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        // ─────────────────────────────────────────
        //  环境
        // ─────────────────────────────────────────

        private static void CreateEnvironment(Material groundMat, Material borderMat)
        {
            // Flat Plane 地面 — 1000x1000 米
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(100, 1, 100);

            var renderer = ground.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = groundMat;

            // 四周边界标识线
            float halfSize = 500f; // 1000 / 2
            CreateBorderMarker("Border_N", new Vector3(0, 0.05f, halfSize), new Vector3(halfSize * 2, 0.1f, 0.5f), borderMat);
            CreateBorderMarker("Border_S", new Vector3(0, 0.05f, -halfSize), new Vector3(halfSize * 2, 0.1f, 0.5f), borderMat);
            CreateBorderMarker("Border_E", new Vector3(halfSize, 0.05f, 0), new Vector3(0.5f, 0.1f, halfSize * 2), borderMat);
            CreateBorderMarker("Border_W", new Vector3(-halfSize, 0.05f, 0), new Vector3(0.5f, 0.1f, halfSize * 2), borderMat);
        }

        private static void CreateBorderMarker(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = mat;
        }

        // ─────────────────────────────────────────
        //  灯光
        // ─────────────────────────────────────────

        private static void CreateLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.6f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);
        }

        // ─────────────────────────────────────────
        //  玩家
        // ─────────────────────────────────────────

        private static void CreatePlayer(Material playerMat)
        {
            var playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player";
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0, 1, 0);

            var renderer = playerGo.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = playerMat;

            // 移除默认 Collider（CharacterController 自带）
            var capsuleCollider = playerGo.GetComponent<CapsuleCollider>();
            if (capsuleCollider != null) Object.DestroyImmediate(capsuleCollider);

            var cc = playerGo.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0, 0);
            cc.height = 2f;
            cc.radius = 0.5f;

            playerGo.AddComponent<SimplePlayerController>();
        }

        // ─────────────────────────────────────────
        //  管理器
        // ─────────────────────────────────────────

        private static void CreateManager()
        {
            var managerGo = new GameObject("[BlueprintRuntimeManager]");

            var mgr = managerGo.AddComponent<BlueprintRuntimeManager>();
            var spawner = managerGo.AddComponent<MonsterSpawner>();

            // 绑定 MonsterSpawner 引用
            var so = new SerializedObject(mgr);
            var spawnerProp = so.FindProperty("_monsterSpawner");
            if (spawnerProp != null)
            {
                spawnerProp.objectReferenceValue = spawner;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 尝试自动查找蓝图 JSON
            var guids = AssetDatabase.FindAssets("预设怪物测试蓝图 t:TextAsset",
                new[] { "Assets/GameAssets/SceneBlueprint" });
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                var jsonProp = so.FindProperty("_blueprintJson");
                if (jsonProp != null && asset != null)
                {
                    jsonProp.objectReferenceValue = asset;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    UnityEngine.Debug.Log($"[RuntimeTestSceneBuilder] 自动绑定蓝图: {path}");
                }
            }
        }
    }
}
