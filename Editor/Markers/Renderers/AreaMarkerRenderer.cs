#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Geometry;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Editor.Preview;

namespace SceneBlueprint.Editor.Markers.Renderers
{
    /// <summary>
    /// AreaMarker 的 Gizmo 渲染器。
    /// <para>
    /// 支持四种形状：Box / Circle / Capsule / Polygon。
    /// 锚点约定：底面 y=0，顶面 y=Height（局部空间）。
    /// Interactive Phase：选中时绘制编辑 Handle。
    /// </para>
    /// </summary>
    public class AreaMarkerRenderer : IMarkerGizmoRenderer
    {
        public Type TargetType => typeof(AreaMarker);

        // Circle/Capsule 绘制精度
        private const int CircleSegments = 64;

        // ─── Phase 0: Fill ───

        public void DrawFill(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;

            switch (am.Shape)
            {
                case AreaShape.Box:     DrawBoxFill(am, ctx); break;
                case AreaShape.Circle:  DrawCircleFill(am, ctx); break;
                case AreaShape.Capsule: DrawCapsuleFill(am, ctx); break;
                default:                DrawPolygonFill(am, ctx); break;
            }
        }

        private void DrawBoxFill(AreaMarker am, in GizmoDrawContext ctx)
        {
            var matrix = Handles.matrix;
            Handles.matrix = ctx.Transform.localToWorldMatrix;

            var fillColor = GetFillColor(ctx);
            DrawAllBoxFaces(am.BoxSize.x, am.BoxSize.y, am.Height, fillColor);

            Handles.matrix = matrix;
        }

        private void DrawCircleFill(AreaMarker am, in GizmoDrawContext ctx)
        {
            var fillColor = GetFillColor(ctx);
            var pos = ctx.Transform.position;

            // 底面圆盘
            Handles.color = fillColor;
            Handles.DrawSolidDisc(pos, Vector3.up, am.Radius);
            // 顶面圆盘
            Handles.DrawSolidDisc(pos + Vector3.up * am.Height, Vector3.up, am.Radius);
        }

        private void DrawCapsuleFill(AreaMarker am, in GizmoDrawContext ctx)
        {
            // Capsule 的填充较复杂，用底面和顶面的胶囊轮廓填充
            // 简化处理：绘制底面和顶面的近似多边形填充
            var fillColor = GetFillColor(ctx);
            Handles.color = fillColor;

            var bottomVerts = BuildCapsuleFloorVertices(am, ctx.Transform, 0f);
            var topVerts = BuildCapsuleFloorVertices(am, ctx.Transform, am.Height);

            if (bottomVerts.Length >= 3)
                Handles.DrawAAConvexPolygon(bottomVerts);
            if (topVerts.Length >= 3)
                Handles.DrawAAConvexPolygon(topVerts);
        }

        private void DrawPolygonFill(AreaMarker am, in GizmoDrawContext ctx)
        {
            var shape = am.GetShape();
            var triangles = shape.GetTriangles();
            if (triangles.Length == 0) return;

            var fillColor = ctx.IsHighlighted
                ? new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, ctx.PulseAlpha * 0.35f)
                : ctx.FillColor;

            Handles.color = fillColor;
            float heightOffset = am.Height;
            var triVerts = new Vector3[3];
            var triVertsTop = new Vector3[3];

            foreach (var tri in triangles)
            {
                // 底面三角形
                triVerts[0] = tri.A; triVerts[1] = tri.B; triVerts[2] = tri.C;
                Handles.DrawAAConvexPolygon(triVerts);

                // 顶面三角形
                triVertsTop[0] = tri.A + Vector3.up * heightOffset;
                triVertsTop[1] = tri.B + Vector3.up * heightOffset;
                triVertsTop[2] = tri.C + Vector3.up * heightOffset;
                Handles.DrawAAConvexPolygon(triVertsTop);
            }
        }

        private static void DrawSolidFace(Color color, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Handles.DrawSolidRectangleWithOutline(
                new[] { a, b, c, d }, color, Color.clear);
        }

        /// <summary>
        /// 绘制 Box 的完整 6 面半透明填充。
        /// 锚点约定：底面 y=0，顶面 y=height。XZ 以中心为原点。
        /// </summary>
        private static void DrawAllBoxFaces(float width, float depth, float height, Color fillColor)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            // 底面 y=0
            DrawSolidFace(fillColor,
                new Vector3(-hw, 0, -hd), new Vector3(hw, 0, -hd),
                new Vector3(hw, 0, hd), new Vector3(-hw, 0, hd));
            // 顶面 y=height
            DrawSolidFace(fillColor,
                new Vector3(-hw, height, -hd), new Vector3(hw, height, -hd),
                new Vector3(hw, height, hd), new Vector3(-hw, height, hd));
            // 前面 (z-)
            DrawSolidFace(fillColor,
                new Vector3(-hw, 0, -hd), new Vector3(hw, 0, -hd),
                new Vector3(hw, height, -hd), new Vector3(-hw, height, -hd));
            // 后面 (z+)
            DrawSolidFace(fillColor,
                new Vector3(-hw, 0, hd), new Vector3(hw, 0, hd),
                new Vector3(hw, height, hd), new Vector3(-hw, height, hd));
            // 左面 (x-)
            DrawSolidFace(fillColor,
                new Vector3(-hw, 0, -hd), new Vector3(-hw, 0, hd),
                new Vector3(-hw, height, hd), new Vector3(-hw, height, -hd));
            // 右面 (x+)
            DrawSolidFace(fillColor,
                new Vector3(hw, 0, -hd), new Vector3(hw, 0, hd),
                new Vector3(hw, height, hd), new Vector3(hw, height, -hd));
        }

        /// <summary>
        /// 用 Handles.DrawLine 手动绘制 Box 的 12 条边线。
        /// 锚点约定：底面 y=0，顶面 y=height。XZ 以中心为原点。
        /// </summary>
        private static void DrawBoxWireLines(float width, float depth, float height, Color wireColor)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            Handles.color = wireColor;

            // 底面 4 条边 y=0
            var b0 = new Vector3(-hw, 0, -hd);
            var b1 = new Vector3( hw, 0, -hd);
            var b2 = new Vector3( hw, 0,  hd);
            var b3 = new Vector3(-hw, 0,  hd);
            Handles.DrawLine(b0, b1); Handles.DrawLine(b1, b2);
            Handles.DrawLine(b2, b3); Handles.DrawLine(b3, b0);

            // 顶面 4 条边 y=height
            var t0 = new Vector3(-hw, height, -hd);
            var t1 = new Vector3( hw, height, -hd);
            var t2 = new Vector3( hw, height,  hd);
            var t3 = new Vector3(-hw, height,  hd);
            Handles.DrawLine(t0, t1); Handles.DrawLine(t1, t2);
            Handles.DrawLine(t2, t3); Handles.DrawLine(t3, t0);

            // 竖边 4 条
            Handles.DrawLine(b0, t0); Handles.DrawLine(b1, t1);
            Handles.DrawLine(b2, t2); Handles.DrawLine(b3, t3);
        }

        // ─── Phase 1: Wireframe ───

        public void DrawWireframe(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;

            switch (am.Shape)
            {
                case AreaShape.Box:     DrawBoxWireframe(am, ctx); break;
                case AreaShape.Circle:  DrawCircleWireframe(am, ctx); break;
                case AreaShape.Capsule: DrawCapsuleWireframe(am, ctx); break;
                default:                DrawPolygonWireframe(am, ctx); break;
            }
        }

        private void DrawBoxWireframe(AreaMarker am, in GizmoDrawContext ctx)
        {
            var matrix = Handles.matrix;
            Handles.matrix = ctx.Transform.localToWorldMatrix;

            var wireColor = GetWireColor(ctx);
            DrawBoxWireLines(am.BoxSize.x, am.BoxSize.y, am.Height, wireColor);

            if (ctx.IsHighlighted)
            {
                var glowColor = new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, ctx.PulseAlpha * 0.5f);
                DrawBoxWireLines(am.BoxSize.x * 1.1f, am.BoxSize.y * 1.1f, am.Height, glowColor);
            }

            Handles.matrix = matrix;
        }

        private void DrawCircleWireframe(AreaMarker am, in GizmoDrawContext ctx)
        {
            var wireColor = GetWireColor(ctx);
            var pos = ctx.Transform.position;
            var topPos = pos + Vector3.up * am.Height;

            // 底面圆（实线）
            Handles.color = wireColor;
            Handles.DrawWireDisc(pos, Vector3.up, am.Radius);
            // 顶面圆（降低透明度）
            Handles.color = new Color(wireColor.r, wireColor.g, wireColor.b, wireColor.a * 0.5f);
            Handles.DrawWireDisc(topPos, Vector3.up, am.Radius);

            // 4 条竖线（等分圆周）
            var vertColor = new Color(wireColor.r, wireColor.g, wireColor.b, wireColor.a * 0.4f);
            Handles.color = vertColor;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                var offset = new Vector3(Mathf.Cos(angle) * am.Radius, 0, Mathf.Sin(angle) * am.Radius);
                Handles.DrawLine(pos + offset, topPos + offset);
            }
        }

        private void DrawCapsuleWireframe(AreaMarker am, in GizmoDrawContext ctx)
        {
            var wireColor = GetWireColor(ctx);
            var pos = ctx.Transform.position;
            var topPos = pos + Vector3.up * am.Height;

            // 底面胶囊轮廓
            Handles.color = wireColor;
            DrawCapsuleOutlineXZ(am, ctx.Transform, 0f);
            // 顶面胶囊轮廓（降低透明度）
            Handles.color = new Color(wireColor.r, wireColor.g, wireColor.b, wireColor.a * 0.5f);
            DrawCapsuleOutlineXZ(am, ctx.Transform, am.Height);

            // 竖线连接底面和顶面的关键点
            var vertColor = new Color(wireColor.r, wireColor.g, wireColor.b, wireColor.a * 0.4f);
            Handles.color = vertColor;
            var (pA, pB) = am.GetCapsuleWorldPoints();
            var right = Vector3.Cross(Vector3.up, (pB - pA).normalized);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right = right.normalized * am.CapsuleRadius;

            var heightOffset = Vector3.up * am.Height;
            Handles.DrawLine(pA + right, pA + right + heightOffset);
            Handles.DrawLine(pA - right, pA - right + heightOffset);
            Handles.DrawLine(pB + right, pB + right + heightOffset);
            Handles.DrawLine(pB - right, pB - right + heightOffset);
        }

        private void DrawPolygonWireframe(AreaMarker am, in GizmoDrawContext ctx)
        {
            var verts = am.GetWorldVertices();
            if (verts.Count < 2) return;

            var edgeColor = (ctx.IsSelected || ctx.IsHighlighted)
                ? ctx.EffectiveColor
                : new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, 0.6f);

            Handles.color = edgeColor;
            for (int i = 0; i < verts.Count; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % verts.Count];

                // 底面线
                Handles.DrawLine(a, b);

                // 顶面线
                var aTop = a + Vector3.up * am.Height;
                var bTop = b + Vector3.up * am.Height;
                Handles.DrawLine(aTop, bTop);

                // 竖线
                Handles.color = new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b,
                    ctx.IsHighlighted ? 0.6f : 0.3f);
                Handles.DrawLine(a, aTop);
                Handles.color = edgeColor;
            }

            // 选中或高亮时显示顶点 + 索引标签
            if (ctx.IsSelected || ctx.IsHighlighted)
            {
                Handles.color = Color.white;
                float vertSize = 0.15f;
                for (int i = 0; i < verts.Count; i++)
                {
                    Handles.DotHandleCap(0, verts[i], Quaternion.identity, vertSize * 0.5f, EventType.Repaint);
                    GizmoLabelUtil.DrawCustomLabel($"[{i}]", verts[i] + Vector3.up * 0.3f, Color.white, 9);
                }
            }

            // ── 洞轮廓线 ──
            DrawHoleWireframes(am, ctx);
        }

        /// <summary>绘制所有洞的轮廓线（橙色虚线）</summary>
        private void DrawHoleWireframes(AreaMarker am, in GizmoDrawContext ctx)
        {
            if (am.Holes == null || am.Holes.Count == 0) return;
            var transform = ctx.Transform;

            var holeColor = new Color(1f, 0.6f, 0.1f, ctx.IsSelected || ctx.IsHighlighted ? 1f : 0.6f);
            Handles.color = holeColor;

            foreach (var hole in am.Holes)
            {
                if (hole.Vertices == null || hole.Vertices.Count < 2) continue;
                int n = hole.Vertices.Count;
                for (int i = 0; i < n; i++)
                {
                    var a = transform.TransformPoint(hole.Vertices[i]);
                    var b = transform.TransformPoint(hole.Vertices[(i + 1) % n]);

                    // 底面虚线
                    Handles.DrawDottedLine(a, b, 4f);
                    // 顶面虚线
                    Handles.DrawDottedLine(
                        a + Vector3.up * am.Height,
                        b + Vector3.up * am.Height, 4f);
                }

                // 洞顶点（选中时显示）
                if (ctx.IsSelected || ctx.IsHighlighted)
                {
                    float vertSize = 0.12f;
                    Handles.color = new Color(1f, 0.6f, 0.1f, 1f);
                    foreach (var v in hole.Vertices)
                    {
                        var wp = transform.TransformPoint(v);
                        Handles.DotHandleCap(0, wp, Quaternion.identity, vertSize * 0.5f, EventType.Repaint);
                    }
                    Handles.color = holeColor;
                }
            }
        }

        // ─── Phase 3: Interactive ───

        public void DrawInteractive(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;

            switch (am.Shape)
            {
                case AreaShape.Box:     DrawBoxEditHandles(am, ctx); break;
                case AreaShape.Circle:  DrawCircleEditHandles(am, ctx); break;
                case AreaShape.Capsule: DrawCapsuleEditHandles(am, ctx); break;
                default:                DrawPolygonEditHandles(am, ctx); break;
            }

            // 所有形状共用的高度 Handle
            DrawHeightHandle(am, ctx);
        }

        /// <summary>Box 编辑：4 个边中点 Slider2D 调节宽/深</summary>
        private void DrawBoxEditHandles(AreaMarker am, in GizmoDrawContext ctx)
        {
            var t = ctx.Transform;
            float hw = am.BoxSize.x * 0.5f;
            float hd = am.BoxSize.y * 0.5f;

            // 四个边中点（局部 XZ 平面）
            var right  = t.TransformPoint(new Vector3( hw, 0, 0));
            var left   = t.TransformPoint(new Vector3(-hw, 0, 0));
            var front  = t.TransformPoint(new Vector3(0, 0,  hd));
            var back   = t.TransformPoint(new Vector3(0, 0, -hd));

            float handleSize = HandleUtility.GetHandleSize(t.position) * 0.06f;
            var slideDir = Vector3.up; // Slider2D 法线

            Handles.color = Color.red; // X 轴方向 = 红色
            EditorGUI.BeginChangeCheck();
            var newRight = Handles.Slider2D(right, slideDir, t.right, t.forward, handleSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(am, "调整 Box 宽度");
                float delta = Vector3.Dot(newRight - right, t.right);
                am.BoxSize = new Vector2(Mathf.Max(0.1f, am.BoxSize.x + delta), am.BoxSize.y);
                am.IncrementGeometryVersionEditor();
                EditorUtility.SetDirty(am);
            }

            EditorGUI.BeginChangeCheck();
            var newLeft = Handles.Slider2D(left, slideDir, -t.right, t.forward, handleSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(am, "调整 Box 宽度");
                float delta = Vector3.Dot(newLeft - left, -t.right);
                am.BoxSize = new Vector2(Mathf.Max(0.1f, am.BoxSize.x + delta), am.BoxSize.y);
                am.IncrementGeometryVersionEditor();
                EditorUtility.SetDirty(am);
            }

            Handles.color = Color.blue; // Z 轴方向 = 蓝色
            EditorGUI.BeginChangeCheck();
            var newFront = Handles.Slider2D(front, slideDir, t.forward, t.right, handleSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(am, "调整 Box 深度");
                float delta = Vector3.Dot(newFront - front, t.forward);
                am.BoxSize = new Vector2(am.BoxSize.x, Mathf.Max(0.1f, am.BoxSize.y + delta));
                am.IncrementGeometryVersionEditor();
                EditorUtility.SetDirty(am);
            }

            EditorGUI.BeginChangeCheck();
            var newBack = Handles.Slider2D(back, slideDir, -t.forward, t.right, handleSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(am, "调整 Box 深度");
                float delta = Vector3.Dot(newBack - back, -t.forward);
                am.BoxSize = new Vector2(am.BoxSize.x, Mathf.Max(0.1f, am.BoxSize.y + delta));
                am.IncrementGeometryVersionEditor();
                EditorUtility.SetDirty(am);
            }
        }

        /// <summary>Circle 编辑：径向拖拽调节半径</summary>
        private void DrawCircleEditHandles(AreaMarker am, in GizmoDrawContext ctx)
        {
            var t = ctx.Transform;
            var center = t.position;
            float handleSize = HandleUtility.GetHandleSize(center) * 0.06f;

            // 4 个径向 Handle（±X, ±Z）
            Handles.color = Color.cyan;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var handlePos = center + dir * am.Radius;

                EditorGUI.BeginChangeCheck();
                var newPos = Handles.Slider2D(handlePos, Vector3.up, dir, Vector3.Cross(Vector3.up, dir), handleSize, Handles.DotHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(am, "调整圆形半径");
                    float newRadius = Vector3.Distance(new Vector3(newPos.x, center.y, newPos.z),
                        new Vector3(center.x, center.y, center.z));
                    am.Radius = Mathf.Max(0.1f, newRadius);
                    am.IncrementGeometryVersionEditor();
                    EditorUtility.SetDirty(am);
                }
            }
        }

        /// <summary>Capsule 编辑：两端点拖拽 + 半径调节</summary>
        private void DrawCapsuleEditHandles(AreaMarker am, in GizmoDrawContext ctx)
        {
            var t = ctx.Transform;
            var (pA, pB) = am.GetCapsuleWorldPoints();
            float handleSize = HandleUtility.GetHandleSize(t.position) * 0.06f;

            // 端点 A 拖拽（沿局部 Z 轴）
            Handles.color = Color.yellow;
            EditorGUI.BeginChangeCheck();
            var newA = Handles.Slider2D(pA, Vector3.up, t.forward, t.right, handleSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(am, "调整 Capsule 端点");
                float newHalfLen = Vector3.Distance(newA, t.position);
                am.CapsuleLength = Mathf.Max(0.1f, newHalfLen * 2f);
                am.IncrementGeometryVersionEditor();
                EditorUtility.SetDirty(am);
            }

            // 端点 B 拖拽（沿局部 Z 轴）
            EditorGUI.BeginChangeCheck();
            var newB = Handles.Slider2D(pB, Vector3.up, t.forward, t.right, handleSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(am, "调整 Capsule 端点");
                float newHalfLen = Vector3.Distance(newB, t.position);
                am.CapsuleLength = Mathf.Max(0.1f, newHalfLen * 2f);
                am.IncrementGeometryVersionEditor();
                EditorUtility.SetDirty(am);
            }

            // 半径调节（两侧对称）
            var axisDir = (pB - pA).normalized;
            var right = Vector3.Cross(Vector3.up, axisDir);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right = right.normalized;
            var midPoint = (pA + pB) * 0.5f;

            Handles.color = Color.cyan;
            var radiusPosR = midPoint + right * am.CapsuleRadius;
            EditorGUI.BeginChangeCheck();
            var newR = Handles.Slider2D(radiusPosR, Vector3.up, right, axisDir, handleSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(am, "调整 Capsule 半径");
                float nr = Vector3.Dot(newR - midPoint, right);
                am.CapsuleRadius = Mathf.Max(0.1f, nr);
                am.IncrementGeometryVersionEditor();
                EditorUtility.SetDirty(am);
            }

            var radiusPosL = midPoint - right * am.CapsuleRadius;
            EditorGUI.BeginChangeCheck();
            var newL = Handles.Slider2D(radiusPosL, Vector3.up, -right, axisDir, handleSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(am, "调整 Capsule 半径");
                float nr = Vector3.Dot(newL - midPoint, -right);
                am.CapsuleRadius = Mathf.Max(0.1f, nr);
                am.IncrementGeometryVersionEditor();
                EditorUtility.SetDirty(am);
            }
        }

        /// <summary>所有形状共用的高度 Handle（Y 轴向上拖拽）</summary>
        private void DrawHeightHandle(AreaMarker am, in GizmoDrawContext ctx)
        {
            var t = ctx.Transform;
            var topCenter = t.position + Vector3.up * am.Height;
            float handleSize = HandleUtility.GetHandleSize(topCenter) * 0.06f;

            Handles.color = Color.green;
            EditorGUI.BeginChangeCheck();
#if UNITY_2022_1_OR_NEWER
            var newTop = Handles.Slider(topCenter, Vector3.up, handleSize * 2f, Handles.ArrowHandleCap, 0f);
#else
            var newTop = Handles.Slider(topCenter, Vector3.up, handleSize * 2f, Handles.ArrowHandleCap, 0f);
#endif
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(am, "调整区域高度");
                am.Height = Mathf.Max(0.1f, newTop.y - t.position.y);
                am.IncrementGeometryVersionEditor();
                EditorUtility.SetDirty(am);
            }
        }

        /// <summary>Polygon 顶点编辑（含地面 Raycast 吸附）</summary>
        private void DrawPolygonEditHandles(AreaMarker am, in GizmoDrawContext ctx)
        {
            if (am.Vertices.Count == 0) return;

            var transform = ctx.Transform;
            var verts = am.Vertices;

            for (int i = 0; i < verts.Count; i++)
            {
                var worldPos = transform.TransformPoint(verts[i]);
                float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.08f;

                // Shift+点击删除顶点
                if (Event.current.shift)
                {
                    Handles.color = Color.red;
                    if (Handles.Button(worldPos, Quaternion.identity, handleSize * 1.5f, handleSize * 2f, Handles.DotHandleCap))
                    {
                        if (verts.Count > 3)
                        {
                            Undo.RecordObject(am, "删除区域顶点");
                            verts.RemoveAt(i);
                            am.IncrementGeometryVersionEditor();
                            EditorUtility.SetDirty(am);
                            return;
                        }
                    }
                }
                else
                {
                    // 拖拽顶点——XZ 平面约束 + 地面 Raycast 吸附
                    Handles.color = Color.white;
                    EditorGUI.BeginChangeCheck();
                    var newWorldPos = Handles.Slider2D(
                        worldPos, Vector3.up,
                        Vector3.right, Vector3.forward,
                        handleSize, Handles.DotHandleCap, 0f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        // 地面 Raycast 吸附 Y 值
                        newWorldPos = SnapToGround(newWorldPos);
                        Undo.RecordObject(am, "移动区域顶点");
                        verts[i] = transform.InverseTransformPoint(newWorldPos);
                        am.IncrementGeometryVersionEditor();
                        EditorUtility.SetDirty(am);
                    }
                }

                // 顶点序号标签
                Handles.color = Color.white;
                GizmoLabelUtil.DrawCustomLabel($"[{i}]", worldPos + Vector3.up * 0.3f, Color.white, 9);
            }

            // 边中点（添加顶点）
            if (!Event.current.shift)
                DrawEdgeMidpoints(am, transform, verts);

            // ── 洞顶点编辑 ──
            DrawHoleEditHandles(am, ctx);
        }

        /// <summary>洞顶点编辑 Handle（橙色，操作方式与外轮廓一致）</summary>
        private void DrawHoleEditHandles(AreaMarker am, in GizmoDrawContext ctx)
        {
            if (am.Holes == null || am.Holes.Count == 0) return;
            var transform = ctx.Transform;
            var holeHandleColor = new Color(1f, 0.6f, 0.1f, 1f);
            var holeLabelColor = new Color(1f, 0.8f, 0.3f);

            for (int h = 0; h < am.Holes.Count; h++)
            {
                var hole = am.Holes[h];
                if (hole.Vertices == null || hole.Vertices.Count < 3) continue;
                var hVerts = hole.Vertices;

                for (int i = 0; i < hVerts.Count; i++)
                {
                    var worldPos = transform.TransformPoint(hVerts[i]);
                    float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.07f;

                    if (Event.current.shift)
                    {
                        // Shift+点击删除洞顶点（保留至少 3 个）
                        Handles.color = new Color(1f, 0.3f, 0f, 1f);
                        if (Handles.Button(worldPos, Quaternion.identity, handleSize * 1.5f, handleSize * 2f, Handles.DotHandleCap))
                        {
                            if (hVerts.Count > 3)
                            {
                                Undo.RecordObject(am, "删除洞顶点");
                                hVerts.RemoveAt(i);
                                am.IncrementGeometryVersionEditor();
                                EditorUtility.SetDirty(am);
                                return;
                            }
                        }
                    }
                    else
                    {
                        // 拖拽洞顶点（XZ 平面约束 + 地面吸附）
                        Handles.color = holeHandleColor;
                        EditorGUI.BeginChangeCheck();
                        var newWorldPos = Handles.Slider2D(
                            worldPos, Vector3.up,
                            Vector3.right, Vector3.forward,
                            handleSize, Handles.DotHandleCap, 0f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            newWorldPos = SnapToGround(newWorldPos);
                            Undo.RecordObject(am, "移动洞顶点");
                            hVerts[i] = transform.InverseTransformPoint(newWorldPos);
                            am.IncrementGeometryVersionEditor();
                            EditorUtility.SetDirty(am);
                        }
                    }

                    // 洞顶点序号标签
                    GizmoLabelUtil.DrawCustomLabel($"H{h}[{i}]", worldPos + Vector3.up * 0.3f, holeLabelColor, 8);
                }

                // 洞边中点（插入新顶点）
                if (!Event.current.shift)
                    DrawHoleEdgeMidpoints(am, transform, h, hVerts);
            }
        }

        /// <summary>洞边中点按钮——点击插入新洞顶点</summary>
        private void DrawHoleEdgeMidpoints(AreaMarker am, Transform transform, int holeIdx, List<Vector3> hVerts)
        {
            Handles.color = new Color(1f, 0.8f, 0.3f, 0.8f);

            for (int i = 0; i < hVerts.Count; i++)
            {
                int next = (i + 1) % hVerts.Count;
                var midLocal = (hVerts[i] + hVerts[next]) * 0.5f;
                var midWorld = transform.TransformPoint(midLocal);
                float handleSize = HandleUtility.GetHandleSize(midWorld) * 0.04f;

                if (Handles.Button(midWorld, Quaternion.identity, handleSize, handleSize * 1.5f, Handles.DotHandleCap))
                {
                    Undo.RecordObject(am, "插入洞顶点");
                    hVerts.Insert(next, midLocal);
                    am.IncrementGeometryVersionEditor();
                    EditorUtility.SetDirty(am);
                    return;
                }
            }
        }

        private void DrawEdgeMidpoints(AreaMarker am, Transform transform, List<Vector3> verts)
        {
            Handles.color = new Color(0.3f, 1f, 0.3f, 0.8f);

            for (int i = 0; i < verts.Count; i++)
            {
                int next = (i + 1) % verts.Count;
                var midLocal = (verts[i] + verts[next]) * 0.5f;
                var midWorld = transform.TransformPoint(midLocal);
                float handleSize = HandleUtility.GetHandleSize(midWorld) * 0.05f;

                if (Handles.Button(midWorld, Quaternion.identity, handleSize, handleSize * 1.5f, Handles.DotHandleCap))
                {
                    Undo.RecordObject(am, "插入区域顶点");
                    verts.Insert(next, midLocal);
                    am.IncrementGeometryVersionEditor();
                    EditorUtility.SetDirty(am);
                    return;
                }
            }
        }

        // ─── Phase 4: Highlight ───

        public void DrawHighlight(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;
            var glowColor = new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, ctx.PulseAlpha * 0.5f);

            switch (am.Shape)
            {
                case AreaShape.Box:
                {
                    var matrix = Handles.matrix;
                    Handles.matrix = ctx.Transform.localToWorldMatrix;
                    DrawBoxWireLines(am.BoxSize.x * ctx.PulseScale, am.BoxSize.y * ctx.PulseScale, am.Height, glowColor);
                    Handles.matrix = matrix;
                    break;
                }
                case AreaShape.Circle:
                {
                    Handles.color = glowColor;
                    Handles.DrawWireDisc(ctx.Transform.position, Vector3.up, am.Radius * ctx.PulseScale);
                    break;
                }
                case AreaShape.Capsule:
                {
                    Handles.color = glowColor;
                    // 简化的脉冲外扩：底面胶囊轮廓（略大）
                    DrawCapsuleOutlineXZ(am, ctx.Transform, 0f, am.CapsuleRadius * ctx.PulseScale);
                    break;
                }
            }

            // 中心光晕
            var center = am.GetRepresentativePosition();
            Handles.color = new Color(
                ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b,
                ctx.PulseAlpha * 0.25f);
            Handles.SphereHandleCap(0, center, Quaternion.identity, 4f, EventType.Repaint);
        }

        /// <summary>
        /// 绘制 Blueprint 预览（位置点、路径等）
        /// </summary>
        private void DrawBlueprintPreview(in GizmoDrawContext ctx)
        {
            // 检查预览图层是否可见
            if (!MarkerLayerSystem.IsPreviewVisible())
                return;

            // 聚合所有已注册 PreviewManager 的预览（支持多窗口，A3）
            var previews = BlueprintPreviewManager.GetAllRegisteredPreviews();
            int previewCount = previews.Count();
            SBLog.Debug(
                SBLogTags.Pipeline,
                "DrawBlueprintPreview: marker={0}, previewCount={1}",
                ctx.Marker.MarkerId,
                previewCount);
            
            foreach (var preview in previews)
            {
                // 只绘制属于当前 Marker 的预览
                if (ctx.Marker.MarkerId != preview.SourceMarkerId)
                    continue;
                
                // 根据预览类型绘制
                if (preview.PreviewType == PreviewType.SpawnPositions && preview.Positions != null)
                {
                    DrawSpawnPositionsPreview(preview.Positions);
                }
            }
        }

        /// <summary>
        /// 绘制生成位置预览
        /// </summary>
        private void DrawSpawnPositionsPreview(Vector3[] positions)
        {
            if (positions == null || positions.Length == 0) return;

            var cubeSize = Vector3.one * 0.5f;
            var cubeColor = new Color(0.3f, 1f, 0.3f, 0.4f); // 绿色半透明
            var lineColor = new Color(0.3f, 1f, 0.3f, 0.8f);

            // 1. 绘制立方体
            for (int i = 0; i < positions.Length; i++)
            {
                var pos = positions[i];

                // 填充立方体
                Handles.color = cubeColor;
                DrawCubeFilled(pos, cubeSize);

                // 线框
                Handles.color = lineColor;
                DrawCubeWireframe(pos, cubeSize);
            }

            // 2. 连接线（虚线）
            if (positions.Length > 1)
            {
                Handles.color = lineColor;
                for (int i = 0; i < positions.Length - 1; i++)
                {
                    DrawDashedLine(positions[i], positions[i + 1], 3f);
                }
            }

            // 3. 序号标签
            for (int i = 0; i < positions.Length; i++)
            {
                var labelPos = positions[i] + Vector3.up * 0.7f;
                GizmoLabelUtil.DrawCustomLabel($"#{i + 1}", labelPos, Color.white, 10);
            }

            SBLog.Debug(
                SBLogTags.Pipeline,
                "DrawSpawnPositionsPreview: count={0}",
                positions.Length);
        }

        /// <summary>
        /// 绘制填充立方体（用于预览点绘制，以 center 为中心）
        /// </summary>
        private void DrawCubeFilled(Vector3 center, Vector3 size)
        {
            var matrix = Handles.matrix;
            // 将原点放在 cube 底部中心偏下半高，使得 DrawAllBoxFaces 的 y=0..height 能以 center 为中心
            Handles.matrix = Matrix4x4.TRS(center - Vector3.up * size.y * 0.5f, Quaternion.identity, Vector3.one);
            DrawAllBoxFaces(size.x, size.z, size.y, Handles.color);
            Handles.matrix = matrix;
        }

        /// <summary>
        /// 绘制立方体线框（用于预览点绘制，以 center 为中心）
        /// </summary>
        private void DrawCubeWireframe(Vector3 center, Vector3 size)
        {
            var matrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(center - Vector3.up * size.y * 0.5f, Quaternion.identity, Vector3.one);
            DrawBoxWireLines(size.x, size.z, size.y, Handles.color);
            Handles.matrix = matrix;
        }

        /// <summary>
        /// 绘制虚线
        /// </summary>
        private void DrawDashedLine(Vector3 from, Vector3 to, float dashSize)
        {
            var dir = to - from;
            var distance = dir.magnitude;
            var dashCount = Mathf.CeilToInt(distance / dashSize);

            for (int i = 0; i < dashCount; i += 2)
            {
                var t0 = i / (float)dashCount;
                var t1 = Mathf.Min((i + 1) / (float)dashCount, 1f);
                Handles.DrawLine(
                    Vector3.Lerp(from, to, t0),
                    Vector3.Lerp(from, to, t1));
            }
        }

        // ─── Phase 5: Label ───

        public void DrawLabel(in GizmoDrawContext ctx)
        {
            // 预览应独立于“高亮态”存在，避免首次绑定后必须重选节点才可见。
            DrawBlueprintPreview(ctx);

            var am = (AreaMarker)ctx.Marker;
            var labelPos = am.GetRepresentativePosition() + Vector3.up * (am.Height + 0.5f);
            GizmoLabelUtil.DrawStandardLabel(ctx.Marker, labelPos, ctx.EffectiveColor);
        }

        // ─── Phase 6: Pick ───

        public PickBounds GetPickBounds(in GizmoDrawContext ctx)
        {
            var am = (AreaMarker)ctx.Marker;
            var center = am.GetRepresentativePosition();
            float radius;

            switch (am.Shape)
            {
                case AreaShape.Box:
                    radius = Mathf.Max(am.BoxSize.x, am.BoxSize.y) * 0.5f;
                    break;
                case AreaShape.Circle:
                    radius = am.Radius;
                    break;
                case AreaShape.Capsule:
                    radius = am.CapsuleLength * 0.5f + am.CapsuleRadius;
                    break;
                default: // Polygon
                    radius = 2f;
                    if (am.Vertices.Count > 0)
                    {
                        float maxR = 0;
                        foreach (var v in am.Vertices)
                            maxR = Mathf.Max(maxR, v.magnitude);
                        radius = maxR;
                    }
                    break;
            }

            return new PickBounds { Center = center, Radius = radius };
        }

        // ═══════════════════════════════════════════════════════
        // 辅助方法
        // ═══════════════════════════════════════════════════════

        /// <summary>根据高亮状态返回填充颜色</summary>
        private static Color GetFillColor(in GizmoDrawContext ctx)
        {
            return ctx.IsHighlighted
                ? new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, ctx.PulseAlpha * 0.4f)
                : ctx.FillColor;
        }

        /// <summary>根据选中/高亮状态返回线框颜色</summary>
        private static Color GetWireColor(in GizmoDrawContext ctx)
        {
            return (ctx.IsSelected || ctx.IsHighlighted)
                ? ctx.EffectiveColor
                : new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, 0.6f);
        }

        /// <summary>地面 Raycast 吸附——向下投射取实际地面 Y 值</summary>
        private static Vector3 SnapToGround(Vector3 worldPos)
        {
            var rayOrigin = new Vector3(worldPos.x, worldPos.y + 100f, worldPos.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 200f))
                return hit.point;
            return worldPos;
        }

        /// <summary>
        /// 构建胶囊底面/顶面轮廓顶点（世界坐标）。
        /// yOffset=0 为底面，yOffset=Height 为顶面。
        /// </summary>
        private static Vector3[] BuildCapsuleFloorVertices(AreaMarker am, Transform t, float yOffset)
        {
            var (pA, pB) = am.GetCapsuleWorldPoints();
            var axisDir = (pB - pA).normalized;
            var right = Vector3.Cross(Vector3.up, axisDir);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right = right.normalized;

            float r = am.CapsuleRadius;
            int halfSegs = CircleSegments / 2;
            var verts = new List<Vector3>(CircleSegments + 2);
            var up = Vector3.up * yOffset;

            // 半圆 A（pA 端，从 +right 到 -right，逆时针绕 -axisDir）
            for (int i = 0; i <= halfSegs; i++)
            {
                float angle = Mathf.PI * i / halfSegs;
                var offset = right * (Mathf.Cos(angle) * r) + (-axisDir) * (Mathf.Sin(angle) * r);
                verts.Add(pA + offset + up);
            }

            // 半圆 B（pB 端，从 -right 到 +right，逆时针绕 +axisDir）
            for (int i = 0; i <= halfSegs; i++)
            {
                float angle = Mathf.PI * i / halfSegs;
                var offset = (-right) * (Mathf.Cos(angle) * r) + axisDir * (Mathf.Sin(angle) * r);
                verts.Add(pB + offset + up);
            }

            return verts.ToArray();
        }

        /// <summary>绘制胶囊 XZ 平面轮廓线（使用 am 自身的 CapsuleRadius）</summary>
        private static void DrawCapsuleOutlineXZ(AreaMarker am, Transform t, float yOffset)
        {
            DrawCapsuleOutlineXZ(am, t, yOffset, am.CapsuleRadius);
        }

        /// <summary>绘制胶囊 XZ 平面轮廓线（可指定半径，用于高亮脉冲外扩）</summary>
        private static void DrawCapsuleOutlineXZ(AreaMarker am, Transform t, float yOffset, float radius)
        {
            var (pA, pB) = am.GetCapsuleWorldPoints();
            var axisDir = (pB - pA).normalized;
            var right = Vector3.Cross(Vector3.up, axisDir);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right = right.normalized;

            int halfSegs = CircleSegments / 2;
            var up = Vector3.up * yOffset;

            // 半圆 A
            Vector3 prev = pA + right * radius + up;
            for (int i = 1; i <= halfSegs; i++)
            {
                float angle = Mathf.PI * i / halfSegs;
                var offset = right * (Mathf.Cos(angle) * radius) + (-axisDir) * (Mathf.Sin(angle) * radius);
                var cur = pA + offset + up;
                Handles.DrawLine(prev, cur);
                prev = cur;
            }

            // 直线 A(-right) → B(-right)
            var lineStart = prev;
            // 半圆 B
            prev = pB - right * radius + up;
            Handles.DrawLine(lineStart, prev);

            for (int i = 1; i <= halfSegs; i++)
            {
                float angle = Mathf.PI * i / halfSegs;
                var offset = (-right) * (Mathf.Cos(angle) * radius) + axisDir * (Mathf.Sin(angle) * radius);
                var cur = pB + offset + up;
                Handles.DrawLine(prev, cur);
                prev = cur;
            }

            // 直线 B(+right) → A(+right)  闭合
            Handles.DrawLine(prev, pA + right * radius + up);
        }
    }
}
