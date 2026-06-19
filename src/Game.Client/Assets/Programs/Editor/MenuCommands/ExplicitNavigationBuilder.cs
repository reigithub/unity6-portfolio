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

        // 各 Selectable の実座標から行・列を推定し、上下左右を循環付きで接続する。Grid（縦横両対応）。
        [MenuItem(MenuPathRoot + "Build Navigation(World Position)", false, 0)]
        private static void BuildWorldPosition(MenuCommand command)
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

            // 上下は隣接行で X が最も近い要素へ接続する（行ごとに列数が異なっても自然に繋がる）。
            ApplyGridNavigation(rows, (neighborRow, current, _) => NearestByX(neighborRow, current.transform.position.x));

            var columnInfo = string.Join("x", rows.Select(r => r.Count));
            Debug.Log($"[ExplicitNavigationBuilder] Built Explicit Grid navigation for {selectables.Length} Selectables ({rows.Count} rows: {columnInfo}) under '{target.name}'");
        }

        // GridLayoutGroup の constraint / startAxis / startCorner から論理的に行・列を確定し、上下左右を循環付きで接続する。
        // 実座標推定を介さないため、ラグドな最終行も含めて曖昧さなく接続できる。
        [MenuItem(MenuPathRoot + "Build Navigation(Grid Layout Group)", false, 0)]
        private static void BuildGridLayout(MenuCommand command)
        {
            var target = command.context as GameObject;
            if (target == null) return;

            var grid = target.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                Debug.LogWarning($"[ExplicitNavigationBuilder] No GridLayoutGroup found on '{target.name}'");
                return;
            }

            // Flexible は列数が親の実幅・cellSize・spacing に依存し、メタ情報だけでは確定できないため非対応。
            if (grid.constraint == GridLayoutGroup.Constraint.Flexible)
            {
                Debug.LogWarning($"[ExplicitNavigationBuilder] GridLayoutGroup.Constraint.Flexible is not supported on '{target.name}'. Use FixedColumnCount or FixedRowCount.");
                return;
            }

            var cells = CollectGridCells(target.transform);
            if (cells.Length == 0)
            {
                Debug.LogWarning($"[ExplicitNavigationBuilder] No Selectable found under '{target.name}'");
                return;
            }

            Undo.RecordObjects(cells, "Build Grid Layout Navigation");

            var rows = BuildGridLayoutRows(grid, cells);

            // 論理グリッドでは列インデックスが既知なので、上下は隣接行の同一列へ直結する（ラグド行は末尾へクランプ）。
            ApplyGridNavigation(rows, (neighborRow, _, c) => neighborRow[Mathf.Min(c, neighborRow.Count - 1)]);

            var columnInfo = string.Join("x", rows.Select(r => r.Count));
            Debug.Log($"[ExplicitNavigationBuilder] Built Explicit GridLayout navigation for {cells.Length} Selectables ({rows.Count} rows: {columnInfo}, {grid.constraint}) under '{target.name}'");
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
        /// 行クラスタ（画面上→下・各行は左→右に正規化済み）に上下左右を循環付きで接続する。
        /// 左右は行内循環で共通。上下の遷移先選択のみ <paramref name="pickVertical"/> に委譲する
        /// （World Position は X 最近傍、GridLayout は同一列インデックスを用いる）。
        /// </summary>
        /// <param name="pickVertical">隣接行・現在の Selectable・現在の列インデックスから上下の遷移先を選ぶ。</param>
        private static void ApplyGridNavigation(
            List<List<Selectable>> rows,
            System.Func<List<Selectable>, Selectable, int, Selectable> pickVertical)
        {
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                // 上下は行をまたいで循環。
                var upRow = rows[(r - 1 + rows.Count) % rows.Count];
                var downRow = rows[(r + 1) % rows.Count];

                for (int c = 0; c < row.Count; c++)
                {
                    var up = rows.Count == 1 ? null : pickVertical(upRow, row[c], c);
                    var down = rows.Count == 1 ? null : pickVertical(downRow, row[c], c);
                    // 左右は行内で循環。
                    var left = row.Count == 1 ? null : (c > 0 ? row[c - 1] : row[row.Count - 1]);
                    var right = row.Count == 1 ? null : (c < row.Count - 1 ? row[c + 1] : row[0]);
                    ApplyExplicit(row[c], up, down, left, right);
                }
            }
        }

        /// <summary>
        /// GridLayoutGroup が実際にレイアウトする直接の子（sibling order）から Selectable を収集する。
        /// 非アクティブ・LayoutElement.ignoreLayout の子、および Navigation.Mode.None は除外し、
        /// 各子はサブツリー先頭の Selectable を採用する（直接の子自体が Selectable でなくても可）。
        /// </summary>
        private static Selectable[] CollectGridCells(Transform parent)
        {
            var cells = new List<Selectable>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;

                var layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement != null && layoutElement.ignoreLayout) continue;

                var selectable = child.GetComponentInChildren<Selectable>(includeInactive: false);
                if (selectable == null || selectable.navigation.mode == Navigation.Mode.None) continue;

                cells.Add(selectable);
            }

            return cells.ToArray();
        }

        /// <summary>
        /// GridLayoutGroup のメタ情報（constraint / constraintCount / startAxis / startCorner）から
        /// セルを論理的な行・列へ割り付ける。返す行リストは画面上→下・各行は左→右に正規化済み。
        /// 算出式は UnityCsReference の GridLayoutGroup.SetCellsAlongAxis に準拠。Flexible は呼び出し側で除外済み前提。
        /// </summary>
        private static List<List<Selectable>> BuildGridLayoutRows(GridLayoutGroup grid, Selectable[] cells)
        {
            int n = cells.Length;

            int columns;
            int rows;
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                columns = Mathf.Max(1, grid.constraintCount);
                rows = Mathf.CeilToInt(n / (float)columns - 0.001f);
            }
            else
            {
                rows = Mathf.Max(1, grid.constraintCount);
                columns = Mathf.CeilToInt(n / (float)rows - 0.001f);
            }

            // startAxis が主軸（充填方向）を決める。Horizontal は行優先、Vertical は列優先。
            bool horizontal = grid.startAxis == GridLayoutGroup.Axis.Horizontal;
            int cellsPerMainAxis;
            int actualColumns;
            int actualRows;
            if (horizontal)
            {
                cellsPerMainAxis = columns;
                actualColumns = Mathf.Clamp(columns, 1, n);
                actualRows = Mathf.Clamp(rows, 1, Mathf.CeilToInt(n / (float)cellsPerMainAxis));
            }
            else
            {
                cellsPerMainAxis = rows;
                actualRows = Mathf.Clamp(rows, 1, n);
                actualColumns = Mathf.Clamp(columns, 1, Mathf.CeilToInt(n / (float)cellsPerMainAxis));
            }

            // startCorner: X は 0=左/1=右、Y は 0=上/1=下。右始まり・下始まりは対応軸を反転して正規化する。
            int cornerX = (int)grid.startCorner % 2;
            int cornerY = (int)grid.startCorner / 2;

            var placements = new List<(int Row, int Col, Selectable Selectable)>(n);
            for (int i = 0; i < n; i++)
            {
                int col;
                int row;
                if (horizontal)
                {
                    col = i % cellsPerMainAxis;
                    row = i / cellsPerMainAxis;
                }
                else
                {
                    col = i / cellsPerMainAxis;
                    row = i % cellsPerMainAxis;
                }

                if (cornerX == 1) col = actualColumns - 1 - col;
                if (cornerY == 1) row = actualRows - 1 - row;

                placements.Add((row, col, cells[i]));
            }

            var result = new List<List<Selectable>>();
            for (int r = 0; r < actualRows; r++)
            {
                var rowItems = placements
                    .Where(p => p.Row == r)
                    .OrderBy(p => p.Col)
                    .Select(p => p.Selectable)
                    .ToList();
                if (rowItems.Count > 0)
                {
                    result.Add(rowItems);
                }
            }

            return result;
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
