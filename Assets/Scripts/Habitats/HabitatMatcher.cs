using System;
using System.Collections.Generic;
using System.Linq;

using IronRing.Contracts;

namespace IronRing.Habitats
{
    /// <summary>
    /// 위·아래 구역을 화면 배치에 맞게 연결하고 이미 사용한 칸을 제외해 정확한 재료 수의 연결 묶음을 찾는다.
    /// 패턴을 먼저 판정하고 묶음은 필요 칸 수가 큰 순서와 카탈로그 순서로 판정한다.
    /// </summary>
    public static class HabitatMatcher
    {
        /// <summary>
        /// cells(거점의 모든 칸)에서 완성된 서식지를 결정적 순서로 돌려준다. reserved(스토리 035: 이미 고정된 서식지의 칸)는
        /// 처음부터 차지된 것으로 보아 새 판정에 쓰지 않는다. preferredCell이 있으면 그 칸을 포함한 조합을 먼저 찾는다.
        /// 고정된 서식지는 돌려주지 않고 새 조합 목록만 반환한다.
        /// </summary>
        public static List<HabitatCluster> Evaluate(IReadOnlyList<IInteriorCell> cells, HabitatCatalog catalog, IEnumerable<int> reserved = null, int? preferredCell = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var result = new List<HabitatCluster>();
            if (cells == null || cells.Count == 0) return result;
            Region[] regions = { Region.Build(cells) };
            var claimed = new HashSet<int>();
            if (reserved != null) foreach (int index in reserved) claimed.Add(index);

            IEnumerable<HabitatType> patterns = catalog.Types
                .Select((type, order) => (type, order))
                .Where(entry => entry.type.Kind == HabitatKind.Pattern)
                .OrderByDescending(entry => entry.type.RequiredCount).ThenBy(entry => entry.order)
                .Select(entry => entry.type);
            IEnumerable<HabitatType> groups = catalog.Types
                .Select((type, order) => (type, order))
                .Where(entry => entry.type.Kind == HabitatKind.Cluster || entry.type.Kind == HabitatKind.Set)
                .OrderByDescending(entry => entry.type.RequiredCount).ThenBy(entry => entry.order)
                .Select(entry => entry.type);

            for (int pass = preferredCell.HasValue ? 0 : 1; pass < 2; pass++)
            {
                bool preferredOnly = pass == 0;
                foreach (HabitatType type in patterns)
                {
                    List<string[,]> variants = Variants(type.Pattern);
                    foreach (Region region in regions)
                        for (int top = 0; top < region.Rows; top++)
                            for (int left = 0; left < region.Columns; left++)
                                foreach (string[,] template in variants)
                                {
                                    List<int> match = Match(region, template, top, left, claimed);
                                    if (match == null || (preferredOnly && !match.Contains(preferredCell.Value))) continue;
                                    foreach (int index in match) claimed.Add(index);
                                    result.Add(new HabitatCluster { TypeId = type.Id, Cells = match.AsReadOnly() });
                                    break;
                                }
                }

                foreach (HabitatType type in groups)
                    foreach (Region region in regions)
                        for (int row = 0; row < region.Rows; row++)
                            for (int column = 0; column < region.Columns; column++)
                            {
                                IInteriorCell start = region.At(row, column);
                                if (start == null || !type.AcceptsTile(Tile(start)) || claimed.Contains(start.Index)) continue;
                                if (preferredOnly && start.Index != preferredCell.Value) continue;
                                List<int> group = FindGroup(region, row, column, type, claimed);
                                if (group == null) continue;
                                foreach (int index in group) claimed.Add(index);
                                group.Sort();
                                result.Add(new HabitatCluster { TypeId = type.Id, Cells = group.AsReadOnly() });
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
            if (!Connected(region, cells)) return null;
            cells.Sort();
            return cells;
        }

        /// <summary>
        /// cells의 저장된 cluster가 type의 정확한 재료와 연결 조건에 맞는지 판정한다.
        /// 해당 칸만 사용한 재판정 결과가 저장된 묶음과 같으면 true를 반환한다.
        /// </summary>
        public static bool MatchesCluster(IReadOnlyList<IInteriorCell> cells, HabitatType type, HabitatCluster cluster)
        {
            if (cells == null || type == null || cluster?.Cells == null || cluster.Cells.Count != type.RequiredCount) return false;
            var wanted = new HashSet<int>(cluster.Cells);
            if (wanted.Count != type.RequiredCount) return false;
            var reserved = new HashSet<int>();
            int found = 0;
            foreach (IInteriorCell cell in cells)
            {
                if (cell == null) continue;
                if (wanted.Contains(cell.Index)) found++;
                else reserved.Add(cell.Index);
            }
            if (found != wanted.Count) return false;
            List<HabitatCluster> matches = Evaluate(cells, new HabitatCatalog(new[] { type }), reserved);
            return matches.Count == 1 && matches[0].Cells.Count == wanted.Count && matches[0].Cells.All(wanted.Contains);
        }

        /// <summary>
        /// region에서 start를 포함하고 claimed를 피하는 정확한 type 조합을 찾는다.
        /// 연결된 칸 인덱스 목록을 반환하며 없으면 null을 반환한다.
        /// </summary>
        private static List<int> FindGroup(Region region, int row, int column, HabitatType type, HashSet<int> claimed)
        {
            var limits = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (HabitatTileRequirement tile in type.SetTiles) limits[tile.TileItemId] = tile.MinCount;
            if (limits.Values.Sum() != type.RequiredCount) return null;
            var chosen = new List<IInteriorCell>();
            var chosenIds = new HashSet<int>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            IInteriorCell start = region.At(row, column);
            chosen.Add(start);
            chosenIds.Add(start.Index);
            counts[Tile(start)] = 1;
            if (!ExtendGroup(region, type.RequiredCount, claimed, limits, chosen, chosenIds, counts)) return null;
            return chosen.Select(cell => cell.Index).ToList();
        }

        /// <summary>
        /// chosen에 인접한 미사용 칸을 재료별 limits 이내에서 추가해 requiredCount까지 탐색한다.
        /// 일치하는 연결 조합을 찾으면 chosen을 보존하고 true를 반환한다.
        /// </summary>
        private static bool ExtendGroup(Region region, int requiredCount, HashSet<int> claimed,
            Dictionary<string, int> limits, List<IInteriorCell> chosen, HashSet<int> chosenIds, Dictionary<string, int> counts)
        {
            if (chosen.Count == requiredCount) return true;
            var candidates = new SortedDictionary<int, IInteriorCell>();
            foreach (IInteriorCell member in chosen)
                foreach ((int dr, int dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                {
                    IInteriorCell next = region.At(region.RowOf(member) + dr, member.Column + dc);
                    if (next == null || claimed.Contains(next.Index) || chosenIds.Contains(next.Index)) continue;
                    string tile = Tile(next);
                    if (tile == null || !limits.TryGetValue(tile, out int limit)) continue;
                    if ((counts.TryGetValue(tile, out int count) ? count : 0) >= limit) continue;
                    candidates[next.Index] = next;
                }
            foreach (IInteriorCell next in candidates.Values)
            {
                string tile = Tile(next);
                chosen.Add(next);
                chosenIds.Add(next.Index);
                counts[tile] = (counts.TryGetValue(tile, out int count) ? count : 0) + 1;
                if (ExtendGroup(region, requiredCount, claimed, limits, chosen, chosenIds, counts)) return true;
                counts[tile]--;
                chosenIds.Remove(next.Index);
                chosen.RemoveAt(chosen.Count - 1);
            }
            return false;
        }

        /// <summary>
        /// region의 indices가 상하좌우로 하나의 묶음인지 확인한다.
        /// 모든 인덱스가 연결되어 있으면 true를 반환한다.
        /// </summary>
        private static bool Connected(Region region, List<int> indices)
        {
            if (indices.Count == 0) return false;
            var remaining = new HashSet<int>(indices);
            IInteriorCell start = null;
            for (int row = 0; row < region.Rows && start == null; row++)
                for (int column = 0; column < region.Columns; column++)
                {
                    IInteriorCell cell = region.At(row, column);
                    if (cell != null && remaining.Contains(cell.Index)) { start = cell; break; }
                }
            if (start == null) return false;
            var stack = new Stack<IInteriorCell>();
            stack.Push(start);
            remaining.Remove(start.Index);
            while (stack.Count > 0)
            {
                IInteriorCell cell = stack.Pop();
                foreach ((int dr, int dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                {
                    IInteriorCell next = region.At(region.RowOf(cell) + dr, cell.Column + dc);
                    if (next != null && remaining.Remove(next.Index)) stack.Push(next);
                }
            }
            return remaining.Count == 0;
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

        /// <summary>위쪽과 아래쪽을 화면 순서대로 잇는 줄×칸 격자 보기.</summary>
        private sealed class Region
        {
            private readonly IInteriorCell[,] _grid;
            private readonly int _upperRows;
            public int Rows { get; }
            public int Columns { get; }

            private Region(IInteriorCell[,] grid, int upperRows)
            {
                _grid = grid;
                _upperRows = upperRows;
                Rows = grid.GetLength(0);
                Columns = grid.GetLength(1);
            }

            /// <summary>cell의 구역과 행을 통합 격자의 행으로 바꾸어 반환한다.</summary>
            public int RowOf(IInteriorCell cell) => cell.Row + (cell.IsUpper ? 0 : _upperRows);

            /// <summary>통합 row와 column의 칸을 반환하며 범위 밖이면 null을 반환한다.</summary>
            public IInteriorCell At(int row, int column) =>
                row >= 0 && row < Rows && column >= 0 && column < Columns ? _grid[row, column] : null;

            /// <summary>cells의 위쪽 행 다음에 아래쪽 행을 이어 통합 격자를 반환한다.</summary>
            public static Region Build(IReadOnlyList<IInteriorCell> cells)
            {
                int upperRows = 0, lowerRows = 0, columns = 0;
                foreach (IInteriorCell cell in cells)
                {
                    if (cell == null) continue;
                    if (cell.IsUpper) upperRows = Math.Max(upperRows, cell.Row + 1);
                    else lowerRows = Math.Max(lowerRows, cell.Row + 1);
                    columns = Math.Max(columns, cell.Column + 1);
                }
                var grid = new IInteriorCell[upperRows + lowerRows, columns];
                var region = new Region(grid, upperRows);
                foreach (IInteriorCell cell in cells)
                    if (cell != null && cell.Row >= 0 && cell.Column >= 0)
                        grid[region.RowOf(cell), cell.Column] = cell;
                return region;
            }
        }
    }
}
