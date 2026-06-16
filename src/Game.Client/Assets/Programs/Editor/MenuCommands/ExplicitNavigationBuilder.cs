using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor.MenuCommands
{
    /// <summary>
    /// 選択した GameObject の子階層にある Selectable に対して、 Explicit Navigationを一括設定する Editor ツール。
    /// </summary>
    public static class ExplicitNavigationBuilder
    {
        private const string MenuPathRoot = "GameObject/Navigation/Explicit/";

        // priority = 0 は GameObject/ メニューを Hierarchy 右クリックメニューに伝播させるために必須
        // 参照: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MenuItem.html
        [MenuItem(MenuPathRoot + "Build Navigation(Horizontal)", false, 0)]
        private static void BuildHorizontal(MenuCommand command)
        {
            var target = command.context as GameObject;
            if (target == null) return;

            var selectables = CollectSelectables(target);
            if (selectables.Length == 0) return;

            Undo.RecordObjects(selectables, "Build Horizontal Navigation");

            for (int i = 0; i < selectables.Length; i++)
            {
                var left = i > 0 ? selectables[i - 1] : selectables[selectables.Length - 1];
                var right = i < selectables.Length - 1 ? selectables[i + 1] : selectables[0];
                ApplyExplicit(selectables[i], null, null, left, right);
            }

            Debug.Log($"[ExplicitNavigationBuilder] Built Explicit Left/Right navigation for {selectables.Length} Selectables under '{target.name}'");
        }

        [MenuItem(MenuPathRoot + "Build Navigation(Vertical)", false, 0)]
        private static void BuildVertical(MenuCommand command)
        {
            var target = command.context as GameObject;
            if (target == null) return;

            var selectables = CollectSelectables(target);
            if (selectables.Length == 0) return;

            Undo.RecordObjects(selectables, "Build Vertical Navigation");

            for (int i = 0; i < selectables.Length; i++)
            {
                var up = i > 0 ? selectables[i - 1] : selectables[selectables.Length - 1];
                var down = i < selectables.Length - 1 ? selectables[i + 1] : selectables[0];
                ApplyExplicit(selectables[i], up, down, null, null);
            }

            Debug.Log($"[ExplicitNavigationBuilder] Built Explicit Up/Down navigation for {selectables.Length} Selectables under '{target.name}'");
        }

        // Grid（縦横両対応）。各 Selectable の実座標から行・列を推定し、上下左右を循環付きで接続する。
        [MenuItem(MenuPathRoot + "Build Navigation(Grid)", false, 0)]
        private static void BuildGrid(MenuCommand command)
        {
            var target = command.context as GameObject;
            if (target == null) return;

            var selectables = CollectSelectables(target);
            if (selectables.Length == 0) return;

            Undo.RecordObjects(selectables, "Build Grid Navigation");

            // LayoutGroup 配下では anchoredPosition がレイアウト計算結果に依存するため、
            // 先にレイアウトを確定させてからワールド座標で行・列を判定する。
            var targetRect = target.transform as RectTransform;
            if (targetRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(targetRect);
            }

            var rows = BuildRows(selectables);

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                // 上下は循環。隣接行で X が最も近い要素へ接続する（行ごとに列数が異なっても自然に繋がる）。
                var upRow = rows[(r - 1 + rows.Count) % rows.Count];
                var downRow = rows[(r + 1) % rows.Count];

                for (int c = 0; c < row.Count; c++)
                {
                    float x = row[c].transform.position.x;
                    var up = rows.Count == 1 ? null : NearestByX(upRow, x);
                    var down = rows.Count == 1 ? null : NearestByX(downRow, x);
                    // 左右は行内で循環。
                    var left = row.Count == 1 ? null : (c > 0 ? row[c - 1] : row[row.Count - 1]);
                    var right = row.Count == 1 ? null : (c < row.Count - 1 ? row[c + 1] : row[0]);
                    ApplyExplicit(row[c], up, down, left, right);
                }
            }

            var columnInfo = string.Join("x", rows.Select(r => r.Count));
            Debug.Log($"[ExplicitNavigationBuilder] Built Explicit Grid navigation for {selectables.Length} Selectables ({rows.Count} rows: {columnInfo}) under '{target.name}'");
        }

        [MenuItem(MenuPathRoot, true)]
        private static bool Validate()
        {
            return Selection.activeGameObject != null;
        }

        /// <summary>
        /// 対象配下から Explicit 設定対象となる Selectable を収集する。Navigation.Mode.None は除外。
        /// 見つからなければ警告を出して空配列を返す。
        /// </summary>
        private static Selectable[] CollectSelectables(GameObject target)
        {
            var selectables = target.GetComponentsInChildren<Selectable>(includeInactive: false)
                .Where(x => x.navigation.mode != Navigation.Mode.None)
                .ToArray();
            if (selectables.Length == 0)
            {
                Debug.LogWarning($"[ExplicitNavigationBuilder] No Selectable found under '{target.name}'");
            }

            return selectables;
        }

        /// <summary>
        /// Explicit モードへ切り替え、上下左右の遷移先を設定して変更をマークする。
        /// </summary>
        private static void ApplyExplicit(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            var nav = selectable.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = up;
            nav.selectOnDown = down;
            nav.selectOnLeft = left;
            nav.selectOnRight = right;
            selectable.navigation = nav;
            EditorUtility.SetDirty(selectable);
        }

        /// <summary>
        /// ワールド Y 座標で Selectable を行クラスタリングする。
        /// 画面上方（Y 大）を先頭行とし、各行は X 昇順（左→右）に並べる。
        /// 同一行判定の許容誤差は要素高さ中央値の半分。
        /// </summary>
        private static List<List<Selectable>> BuildRows(Selectable[] selectables)
        {
            // Y 降順（画面上が先頭）に並べてから、近接する Y をまとめて1行にする。
            var ordered = selectables.OrderByDescending(s => s.transform.position.y).ToArray();
            float tolerance = RowTolerance(selectables);

            var rows = new List<List<Selectable>>();
            var current = new List<Selectable> { ordered[0] };
            float rowY = ordered[0].transform.position.y;

            for (int i = 1; i < ordered.Length; i++)
            {
                float y = ordered[i].transform.position.y;
                if (rowY - y <= tolerance)
                {
                    current.Add(ordered[i]);
                }
                else
                {
                    rows.Add(current);
                    current = new List<Selectable> { ordered[i] };
                    rowY = y;
                }
            }

            rows.Add(current);

            foreach (var row in rows)
            {
                row.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            }

            return rows;
        }

        /// <summary>
        /// 行クラスタリングの許容誤差。要素の高さ（ワールド換算）中央値の半分を用いる。
        /// 高さが取得できない場合のフォールバックとして十分小さい固定値を返す。
        /// </summary>
        private static float RowTolerance(Selectable[] selectables)
        {
            var heights = selectables
                .Select(s => s.transform as RectTransform)
                .Where(rt => rt != null)
                .Select(rt => rt.rect.height * rt.lossyScale.y)
                .Where(h => h > 0f)
                .OrderBy(h => h)
                .ToArray();

            if (heights.Length == 0) return 0.1f;

            float median = heights[heights.Length / 2];
            return median * 0.5f;
        }

        /// <summary>
        /// 行内で指定 X 座標に最も近い Selectable を返す。
        /// </summary>
        private static Selectable NearestByX(List<Selectable> row, float x)
        {
            Selectable nearest = row[0];
            float best = Mathf.Abs(row[0].transform.position.x - x);
            for (int i = 1; i < row.Count; i++)
            {
                float d = Mathf.Abs(row[i].transform.position.x - x);
                if (d < best)
                {
                    best = d;
                    nearest = row[i];
                }
            }

            return nearest;
        }
    }
}
