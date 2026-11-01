using System;
using System.Collections.Generic;
using System.Linq;

using IronRing.Contracts;

namespace IronRing.Habitats
{
    /// <summary>
    /// 스토리 016. 거점 칸 목록에서 완성된 서식지를 찾는 순수 함수. 위·아래 구역(IInteriorCell.IsUpper)을 따로 보고 선로를 건너
    /// 잇지 않는다. 한 칸은 서식지 하나에만 속한다(같은 칸이 두 서식지로 중복 계산되지 않음):
    ///  1) Pattern 종류를 먼저(타일 칸 수가 많은 것부터, 같으면 카탈로그 순서) 판정한다. 템플릿의 8변형(회전 4 × 거울)을 구역 위에서
    ///     줄 우선으로 밀어 보며, 아직 쓰이지 않은 칸만으로 들어맞으면 그 칸들을 차지한다(겹치는 매칭은 먼저 찾은 하나만).
    ///  2) 남은 칸에서 같은 타일의 상하좌우 묶음을 flood fill로 찾고, 그 타일의 Cluster 종류 중 RequiredCount ≤ 묶음 크기인
    ///     가장 큰 종류(같으면 카탈로그 순서) 하나로 센다. 예: 풀 9칸 묶음은 대초원 하나이고 초원으로 중복 계산되지 않는다.
    /// </summary>
    public static class HabitatMatcher
    {
        /// <summary>cells(거점의 36칸)에서 완성된 서식지를 결정적 순서로 돌려준다.</summary>
        public static List<HabitatCluster> Evaluate(IReadOnlyList<IInteriorCell> cells, HabitatCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var result = new List<HabitatCluster>();
            if (cells == null || cells.Count == 0) return result;
            Region[] regions = { Region.Build(cells, true), Region.Build(cells, false) };
            var claimed = new HashSet<int>();

            IEnumerable<HabitatType> patterns = catalog.Types
                .Select((type, order) => (type, order))
                .Where(entry => entry.type.Kind == HabitatKind.Pattern)
                .OrderByDescending(entry => entry.type.RequiredCount).ThenBy(entry => entry.order)
                .Select(entry => entry.type);
            foreach (HabitatType type in patterns)
            {
                List<string[,]> variants = Variants(type.Pattern);
                foreach (Region region in regions)
                    for (int top = 0; top < region.Rows; top++)
                        for (int left = 0; left < region.Columns; left++)
                            foreach (string[,] template in variants)
                            {
                                List<int> match = Match(region, template, top, left, claimed);
                                if (match == null) continue;
                                foreach (int index in match) claimed.Add(index);
                                result.Add(new HabitatCluster { TypeId = type.Id, Cells = match.AsReadOnly() });
                                break;
                            }
            }

            foreach (Region region in regions)
            {
                var visited = new HashSet<int>();
                for (int row = 0; row < region.Rows; row++)
                    for (int column = 0; column < region.Columns; column++)
                    {
                        IInteriorCell start = region.At(row, column);
                        string tile = Tile(start);
                        if (tile == null || claimed.Contains(start.Index) || visited.Contains(start.Index)) continue;
                        List<int> group = Flood(region, row, column, tile, claimed, visited);
                        HabitatType best = null;
                        foreach (HabitatType type in catalog.Types)
                            if (type.Kind == HabitatKind.Cluster && type.TileItemId == tile && type.RequiredCount <= group.Count
                                && (best == null || type.RequiredCount > best.RequiredCount)) best = type;
                        if (best == null) continue;
                        group.Sort();
                        result.Add(new HabitatCluster { TypeId = best.Id, Cells = group.AsReadOnly() });
                    }
            }
            return result;
        }

        /// <summary>ItemPlaced 칸의 아이템 id, 아니면 null.</summary>
        private static string Tile(IInteriorCell cell) =>
            cell != null && cell.State == InteriorCellState.ItemPlaced && !string.IsNullOrEmpty(cell.ItemId) ? cell.ItemId : null;

        /// <summary>template을 (top, left)에 놓았을 때 "."이 아닌 칸이 모두 같은 타일이고 아직 안 쓰였으면 그 칸 인덱스, 아니면 null.</summary>
        private static List<int> Match(Region region, string[,] template, int top, int left, HashSet<int> claimed)
        {
            int height = template.GetLength(0), width = template.GetLength(1);
            if (top + height > region.Rows || left + width > region.Columns) return null;
            var cells = new List<int>();
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                {
                    string want = template[r, c];
                    if (want == HabitatType.DontCare) continue;
                    IInteriorCell cell = region.At(top + r, left + c);
                    if (cell == null || Tile(cell) != want || claimed.Contains(cell.Index)) return null;
                    cells.Add(cell.Index);
                }
            cells.Sort();
            return cells;
        }

        /// <summary>(row, column)에서 시작해 tile이 같고 아직 안 쓰인 칸을 상하좌우로 모은다.</summary>
        private static List<int> Flood(Region region, int row, int column, string tile, HashSet<int> claimed, HashSet<int> visited)
        {
            var group = new List<int>();
            var stack = new Stack<(int r, int c)>();
            stack.Push((row, column));
            visited.Add(region.At(row, column).Index);
            while (stack.Count > 0)
            {
                (int r, int c) = stack.Pop();
                group.Add(region.At(r, c).Index);
                foreach ((int dr, int dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                {
                    IInteriorCell next = region.At(r + dr, c + dc);
                    if (next == null || visited.Contains(next.Index) || claimed.Contains(next.Index) || Tile(next) != tile) continue;
                    visited.Add(next.Index);
                    stack.Push((r + dr, c + dc));
                }
            }
            return group;
        }

        /// <summary>템플릿의 서로 다른 8변형(원본·거울 각각 0/90/180/270도 회전).</summary>
        public static List<string[,]> Variants(IReadOnlyList<IReadOnlyList<string>> pattern)
        {
            int height = pattern.Count, width = pattern[0].Count;
            var source = new string[height, width];
            for (int r = 0; r < height; r++) for (int c = 0; c < width; c++) source[r, c] = pattern[r][c];
            var result = new List<string[,]>();
            var keys = new HashSet<string>();
            foreach (string[,] start in new[] { source, Mirror(source) })
            {
                string[,] current = start;
                for (int turn = 0; turn < 4; turn++)
                {
                    if (keys.Add(Key(current))) result.Add(current);
                    current = Rotate(current);
                }
            }
            return result;
        }

        /// <summary>시계 방향 90도 회전.</summary>
        private static string[,] Rotate(string[,] grid)
        {
            int height = grid.GetLength(0), width = grid.GetLength(1);
            var rotated = new string[width, height];
            for (int r = 0; r < height; r++) for (int c = 0; c < width; c++) rotated[c, height - 1 - r] = grid[r, c];
            return rotated;
        }

        /// <summary>좌우 거울상.</summary>
        private static string[,] Mirror(string[,] grid)
        {
            int height = grid.GetLength(0), width = grid.GetLength(1);
            var mirrored = new string[height, width];
            for (int r = 0; r < height; r++) for (int c = 0; c < width; c++) mirrored[r, width - 1 - c] = grid[r, c];
            return mirrored;
        }

        private static string Key(string[,] grid)
        {
            var parts = new List<string> { grid.GetLength(0) + "x" + grid.GetLength(1) };
            foreach (string token in grid) parts.Add(token);
            return string.Join("|", parts);
        }

        /// <summary>한 구역(위 또는 아래)의 줄×칸 격자 보기.</summary>
        private sealed class Region
        {
            private readonly IInteriorCell[,] _grid;
            public int Rows { get; }
            public int Columns { get; }

            private Region(IInteriorCell[,] grid) { _grid = grid; Rows = grid.GetLength(0); Columns = grid.GetLength(1); }

            public IInteriorCell At(int row, int column) =>
                row >= 0 && row < Rows && column >= 0 && column < Columns ? _grid[row, column] : null;

            public static Region Build(IReadOnlyList<IInteriorCell> cells, bool upper)
            {
                int rows = 0, columns = 0;
                foreach (IInteriorCell cell in cells)
                    if (cell != null && cell.IsUpper == upper) { rows = Math.Max(rows, cell.Row + 1); columns = Math.Max(columns, cell.Column + 1); }
                var grid = new IInteriorCell[rows, columns];
                foreach (IInteriorCell cell in cells)
                    if (cell != null && cell.IsUpper == upper && cell.Row >= 0 && cell.Column >= 0) grid[cell.Row, cell.Column] = cell;
                return new Region(grid);
            }
        }
    }
}
