using System;
using System.Collections.Generic;

namespace GridGenerator;

public class GridGenerator : IGridGenerator
{
    // Internal state for generation
    private int _width;
    private int _height;
    private CellType[,]? _cells;
    private (int x, int y) _start;
    private (int x, int y) _end;
    private readonly Random _rng = new();
    private List<string> _moves = new();

    public Grid Generate(int width, int height)
    {
        _width = width;
        _height = height;

        for (int attempt = 0; attempt < 150; attempt++)
        {
            _cells = new CellType[_width, _height];
            _moves = new List<string>();

            PickStartEnd();

            if (!TryBuildGuaranteedPath(out var pathCells, out var blockers))
                continue;

            int minMoves = Math.Min(_width, _height) <= 8 ? 6 : (Math.Min(_width, _height) <= 16 ? 7 : 8);
            int maxMoves = Math.Min(_width, _height) <= 8 ? 9 : (Math.Min(_width, _height) <= 16 ? 10 : 12);
            if (_moves.Count < minMoves || _moves.Count > maxMoves)
                continue;

            foreach (var c in pathCells)
                _cells[c.x, c.y] = CellType.Path;

            foreach (var b in blockers)
                if (InGrid(b.x, b.y) && _cells[b.x, b.y] == CellType.Empty)
                    _cells[b.x, b.y] = CellType.Obstacle;

            _cells[_start.x, _start.y] = CellType.Start;
            _cells[_end.x, _end.y] = CellType.End;

            AddRandomObstacles(pathCells, blockers);

            if (!EnforceUniqueness(blockers, pathCells))
                continue;

            return new Grid(_width, _height, _cells, _start, _end, new List<string>(_moves));
        }

        // Fallback empty grid
        _cells = new CellType[width, height];
        _start = (0, 0);
        _end = (width - 1, height - 1);
        return new Grid(width, height, _cells, _start, _end, new List<string>());
    }

    #region Private helpers

    private void PickStartEnd()
    {
        int startSide = _rng.Next(4); // 0=top,1=right,2=bottom,3=left
        int endSide = (startSide + 2) % 4; // opposite side

        _start = RandomCellOnSide(startSide);

        for (int tries = 0; tries < 100; tries++)
        {
            var e = RandomCellOnSide(endSide);
            if (startSide % 2 == 0)
            {
                if (e.x == _start.x) continue;
            }
            else
            {
                if (e.y == _start.y) continue;
            }
            _end = e;
            return;
        }

        var forced = RandomCellOnSide(endSide);
        if (startSide % 2 == 0)
        {
            forced.x = (forced.x + 1) % _width;
            if (forced.x == _start.x) forced.x = (forced.x + 1) % _width;
        }
        else
        {
            forced.y = (forced.y + 1) % _height;
            if (forced.y == _start.y) forced.y = (forced.y + 1) % _height;
        }
        _end = forced;
    }

    private (int x, int y) RandomCellOnSide(int side)
    {
        return side switch
        {
            0 => (_rng.Next(_width), 0),
            1 => (_width - 1, _rng.Next(_height)),
            2 => (_rng.Next(_width), _height - 1),
            _ => (0, _rng.Next(_height))
        };
    }

    private bool TryBuildGuaranteedPath(out List<(int x, int y)> path, out HashSet<(int x, int y)> blockers)
    {
        path = new();
        blockers = new();

        int minMoves = Math.Min(_width, _height) <= 8 ? 6 : (Math.Min(_width, _height) <= 16 ? 7 : 8);
        int maxMoves = Math.Min(_width, _height) <= 8 ? 9 : (Math.Min(_width, _height) <= 16 ? 10 : 12);
        int targetMoves = _rng.Next(minMoves, maxMoves + 1);

        var anchors = BuildZigZagAnchors(targetMoves);
        if (anchors is null || anchors.Count < 2) return false;

        for (int i = 0; i < anchors.Count - 1; i++)
        {
            var a = anchors[i];
            var b = anchors[i + 1];
            if (!(a.x == b.x || a.y == b.y)) return false;

            int dx = Math.Sign(b.x - a.x);
            int dy = Math.Sign(b.y - a.y);

            int sx = b.x + dx, sy = b.y + dy;
            if (InGrid(sx, sy) && (sx, sy) != _start && (sx, sy) != _end)
                blockers.Add((sx, sy));
        }

        var linearCells = new List<(int x, int y)>();
        var cur = anchors[0];
        linearCells.Add(cur);
        for (int i = 1; i < anchors.Count; i++)
        {
            var nxt = anchors[i];
            int dx = Math.Sign(nxt.x - cur.x);
            int dy = Math.Sign(nxt.y - cur.y);
            int x = cur.x, y = cur.y;
            while (x != nxt.x || y != nxt.y)
            {
                x += dx; y += dy;
                if (!InGrid(x, y)) return false;
                linearCells.Add((x, y));
            }
            cur = nxt;
        }

        _moves.Clear();
        DeriveMoves(linearCells);
        if (_moves.Count < minMoves || _moves.Count > maxMoves)
            return false;

        if (!SimulateMovesAndTrace(_moves, blockers, out var traced))
            return false;
        if (traced.Count == 0 || traced[^1] != _end)
            return false;

        path = traced;
        return true;
    }

    private void DeriveMoves(List<(int x, int y)> path)
    {
        if (path.Count < 2) return;
        int i = 1;
        while (i < path.Count)
        {
            var prev = path[i - 1];
            var cur = path[i];
            int dx = Math.Sign(cur.x - prev.x);
            int dy = Math.Sign(cur.y - prev.y);
            char dir = dx switch { > 0 => 'R', < 0 => 'L', _ => (dy > 0 ? 'D' : 'U') };
            while (i < path.Count)
            {
                var p = path[i - 1]; var c = path[i];
                int dx2 = Math.Sign(c.x - p.x); int dy2 = Math.Sign(c.y - p.y);
                char dir2 = dx2 switch { > 0 => 'R', < 0 => 'L', _ => (dy2 > 0 ? 'D' : 'U') };
                if (dir2 != dir) break;
                i++;
            }
            _moves.Add(dir.ToString());
        }
    }

    private void AddRandomObstacles(List<(int x, int y)> pathCells, HashSet<(int x, int y)> mandatoryBlockers)
    {
        if (_cells is null) return;
        var forbidden = new HashSet<(int x, int y)>(pathCells);
        foreach (var b in mandatoryBlockers) forbidden.Add(b);
        forbidden.Add(_start);
        forbidden.Add(_end);

        double capPercent = Math.Min(_width, _height) <= 8 ? 0.07 : (Math.Min(_width, _height) <= 16 ? 0.09 : 0.11);
        int maxTotalObstacles = (int)Math.Floor(_width * _height * capPercent);

        int decoysTarget = Math.Max(2, Math.Min(_width, _height) / 4);
        int decoysPlaced = 0;
        int guardDecoy = _width * _height;
        while (decoysPlaced < decoysTarget && guardDecoy-- > 0)
        {
            if (CountObstacles() >= maxTotalObstacles) break;

            bool placeOnRow = _rng.NextDouble() < 0.5;
            int x = placeOnRow ? _rng.Next(1, _width - 1) : (_rng.NextDouble() < 0.5 ? 1 : _width - 2);
            int y = placeOnRow ? (_rng.NextDouble() < 0.5 ? 1 : _height - 2) : _rng.Next(1, _height - 1);

            if (forbidden.Contains((x, y))) continue;

            bool nearCritical = false;
            foreach (var p in pathCells)
            {
                if (Math.Abs(p.x - x) + Math.Abs(p.y - y) <= 1) { nearCritical = true; break; }
            }
            if (nearCritical) continue;
            if (Math.Abs(_start.x - x) + Math.Abs(_start.y - y) <= 1) continue;
            if (Math.Abs(_end.x - x) + Math.Abs(_end.y - y) <= 1) continue;

            if (CanPlaceIsolated(x, y, forbidden))
            {
                _cells[x, y] = CellType.Obstacle;
                forbidden.Add((x, y));
                decoysPlaced++;
            }
        }

        double baseDensity = Math.Min(_width, _height) <= 8 ? 0.02 : (Math.Min(_width, _height) <= 16 ? 0.035 : 0.05);
        int desiredFiller = (int)Math.Round(_width * _height * baseDensity);

        int remaining = Math.Max(0, maxTotalObstacles - CountObstacles());
        int toPlace = Math.Min(desiredFiller, remaining);

        int placed = 0; int guard = _width * _height * 5;
        while (placed < toPlace && guard-- > 0)
        {
            if (CountObstacles() >= maxTotalObstacles) break;

            int x = _rng.Next(_width); int y = _rng.Next(_height);
            if (forbidden.Contains((x, y))) continue;

            bool nearCritical2 = false;
            foreach (var p in pathCells)
            {
                if (Math.Abs(p.x - x) + Math.Abs(p.y - y) == 1)
                { nearCritical2 = true; break; }
            }
            if (nearCritical2 && _rng.NextDouble() < 0.7) continue;

            if (CanPlaceIsolated(x, y, forbidden))
            {
                _cells[x, y] = CellType.Obstacle;
                placed++;
            }
        }
    }

    private bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;

    private bool CanPlaceIsolated(int x, int y, HashSet<(int x, int y)>? forbidden = null)
    {
        if (_cells is null) return false;
        if (!InGrid(x, y)) return false;
        if (x == 0 || y == 0 || x == _width - 1 || y == _height - 1) return false;
        if (forbidden is not null && forbidden.Contains((x, y))) return false;
        if (_cells[x, y] != CellType.Empty) return false;
        return !(InGrid(x + 1, y) && _cells[x + 1, y] == CellType.Obstacle
                 || InGrid(x - 1, y) && _cells[x - 1, y] == CellType.Obstacle
                 || InGrid(x, y + 1) && _cells[x, y + 1] == CellType.Obstacle
                 || InGrid(x, y - 1) && _cells[x, y - 1] == CellType.Obstacle);
    }

    private int CountObstacles()
    {
        if (_cells is null) return 0;
        int c = 0;
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                if (_cells[x, y] == CellType.Obstacle) c++;
        return c;
    }

    private bool SimulateMovesAndTrace(List<string> moves, HashSet<(int x, int y)> blockers, out List<(int x, int y)> traced)
    {
        traced = new List<(int x, int y)>();
        if (moves.Count == 0) return false;

        bool Blocked(int x, int y)
        {
            return !InGrid(x, y) || blockers.Contains((x, y));
        }

        var pos = _start;
        traced.Add(pos);

        foreach (var m in moves)
        {
            int dx = 0, dy = 0;
            switch (m)
            {
                case "U": dy = -1; break;
                case "D": dy = 1; break;
                case "L": dx = -1; break;
                case "R": dx = 1; break;
                default: return false;
            }

            if (!InGrid(pos.x + dx, pos.y + dy) || Blocked(pos.x + dx, pos.y + dy))
                return false;

            while (true)
            {
                int nx = pos.x + dx;
                int ny = pos.y + dy;
                if (!InGrid(nx, ny) || Blocked(nx, ny))
                    break;
                pos = (nx, ny);
                traced.Add(pos);
            }
        }

        return true;
    }

    private bool SimulateMovesOnFullGrid(List<string> moves, out List<(int x, int y)> traced)
    {
        traced = new List<(int x, int y)>();
        if (_cells is null || moves.Count == 0) return false;
        var pos = _start; traced.Add(pos);

        bool IsBlockedFull(int x, int y)
        {
            if (!InGrid(x, y)) return true;
            return _cells![x, y] == CellType.Obstacle;
        }

        foreach (var m in moves)
        {
            int dx = 0, dy = 0;
            switch (m)
            {
                case "U": dy = -1; break;
                case "D": dy = 1; break;
                case "L": dx = -1; break;
                case "R": dx = 1; break;
                default: return false;
            }

            if (IsBlockedFull(pos.x + dx, pos.y + dy)) return false;
            while (true)
            {
                int nx = pos.x + dx, ny = pos.y + dy;
                if (IsBlockedFull(nx, ny)) break;
                pos = (nx, ny);
                traced.Add(pos);
            }
        }

        return pos == _end;
    }

    private bool EnforceUniqueness(HashSet<(int x, int y)> mandatoryBlockers, List<(int x, int y)> primaryPath)
    {
        if (_cells is null) return false;
        if (!SimulateMovesOnFullGrid(_moves, out _)) return false;

        var forbidden = new HashSet<(int x, int y)>(primaryPath);
        foreach (var b in mandatoryBlockers) forbidden.Add(b);
        forbidden.Add(_start); forbidden.Add(_end);

        for (int iter = 0; iter < _width * _height; iter++)
        {
            if (!FindAlternativeSolutionDistinctFromPrimary(out var altMoves, out var altStops))
            {
                return true;
            }

            var candidates = new List<(int x, int y)>();
            for (int i = 1; i < altStops.Count; i++)
            {
                var a = altStops[i - 1];
                var b = altStops[i];
                int dx = Math.Sign(b.x - a.x);
                int dy = Math.Sign(b.y - a.y);
                int x = a.x, y = a.y;
                while (true)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!InGrid(nx, ny)) break;
                    if (_cells[nx, ny] == CellType.Obstacle) break;
                    if (nx == b.x && ny == b.y) break;
                    candidates.Add((nx, ny));
                    x = nx; y = ny;
                }
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            bool placed = false;
            foreach (var c in candidates)
            {
                if (forbidden.Contains(c)) continue;
                if (_cells[c.x, c.y] != CellType.Empty && _cells[c.x, c.y] != CellType.Path) continue;

                var prev = _cells[c.x, c.y];

                if (!CanPlaceIsolated(c.x, c.y, forbidden))
                    continue;

                _cells[c.x, c.y] = CellType.Obstacle;

                if (SimulateMovesOnFullGrid(_moves, out _))
                {
                    placed = true;
                    forbidden.Add(c);
                    break;
                }
                else
                {
                    _cells[c.x, c.y] = prev;
                }
            }

            if (!placed)
            {
                return false;
            }
        }

        return false;
    }

    private bool FindAlternativeSolutionDistinctFromPrimary(out List<string> altMoves, out List<(int x, int y)> altStops)
    {
        altMoves = new List<string>();
        altStops = new List<(int x, int y)>();
        if (_cells is null) return false;

        var transitions = new Dictionary<(int x, int y), List<((int x, int y) to, char m)>>();
        List<((int x, int y) to, char m)> GetTrans((int x, int y) p)
        {
            if (transitions.TryGetValue(p, out var list)) return list;
            list = new List<((int x, int y), char)>();
            foreach (var (dx, dy, ch) in new (int, int, char)[] { (0,-1,'U'), (0,1,'D'), (-1,0,'L'), (1,0,'R') })
            {
                int x = p.x, y = p.y;
                if (!InGrid(x + dx, y + dy) || _cells[x + dx, y + dy] == CellType.Obstacle) continue;
                while (InGrid(x + dx, y + dy) && _cells[x + dx, y + dy] != CellType.Obstacle)
                {
                    x += dx; y += dy;
                }
                var to = (x, y);
                if (to != p) list.Add((to, ch));
            }
            transitions[p] = list;
            return list;
        }

        var dist = new Dictionary<(int x, int y), int>();
        var parents = new Dictionary<(int x, int y), List<((int x, int y) prev, char m)>>();
        var q = new Queue<(int x, int y)>();
        dist[_start] = 0; parents[_start] = new(); q.Enqueue(_start);

        while (q.Count > 0)
        {
            var u = q.Dequeue();
            int du = dist[u];
            foreach (var tr in GetTrans(u))
            {
                var v = tr.to; char m = tr.m;
                if (!dist.ContainsKey(v))
                {
                    dist[v] = du + 1;
                    parents[v] = new List<((int x, int y), char)> { (u, m) };
                    q.Enqueue(v);
                }
                else if (dist[v] == du + 1)
                {
                    var list = parents[v];
                    bool already = false;
                    foreach (var pr in list) if (pr.prev == u && pr.m == m) { already = true; break; }
                    if (!already) list.Add((u, m));
                }
            }
        }

        if (!dist.ContainsKey(_end)) return false;

        List<string> primary = _moves;

        (List<string> seq, List<(int x, int y)> stops) ReconstructOne()
        {
            var seq = new List<char>();
            var stops = new List<(int x, int y)>();
            var cur = _end; stops.Add(cur);
            while (cur != _start)
            {
                var pr = parents[cur][0];
                seq.Add(pr.m);
                cur = pr.prev; stops.Add(cur);
            }
            seq.Reverse(); stops.Reverse();
            return (new List<string>(seq.ConvertAll(c => c.ToString())), stops);
        }

        bool TryReconstructDifferent(List<string> primarySeq, out List<string> alt, out List<(int x, int y)> stops)
        {
            alt = new List<string>(); stops = new();
            var stack = new Stack<((int x, int y) node, int idx, List<char> seq)>();
            stack.Push((_end, 0, new List<char>()));

            var visitedBranch = new HashSet<((int x, int y), int)>();

            while (stack.Count > 0)
            {
                var (node, _, seq) = stack.Pop();
                if (node == _start)
                {
                    var s = new List<char>(seq); s.Reverse();
                    var sStr = s.ConvertAll(c => c.ToString());
                    if (sStr.Count == primarySeq.Count)
                    {
                        bool equal = true;
                        for (int i = 0; i < sStr.Count; i++) if (sStr[i] != primarySeq[i]) { equal = false; break; }
                        if (!equal)
                        {
                            alt = sStr;
                            SimulateMovesOnFullGrid(alt, out var traced);
                            stops = traced;
                            return true;
                        }
                    }
                    continue;
                }

                if (!parents.ContainsKey(node)) continue;
                var parList = parents[node];
                for (int i = 0; i < parList.Count; i++)
                {
                    var (prev, m) = parList[i];
                    var nextSeq = new List<char>(seq) { m };
                    var key = (prev, nextSeq.Count);
                    if (visitedBranch.Add(key))
                        stack.Push((prev, 0, nextSeq));
                }
            }

            alt = new List<string>(); stops = new();
            return false;
        }

        var one = ReconstructOne();
        if (one.seq.Count == primary.Count)
        {
            bool same = true; for (int i = 0; i < one.seq.Count; i++) if (one.seq[i] != primary[i]) { same = false; break; }
            if (!same)
            {
                altMoves = one.seq; altStops = one.stops; return true;
            }
        }

        if (TryReconstructDifferent(primary, out var alt, out var stops2))
        {
            altMoves = alt; altStops = stops2; return true;
        }

        return false;
    }

    private List<(int x, int y)>? BuildZigZagAnchors(int targetMoves)
    {
        var anchors = new List<(int x, int y)> { _start };

        bool startTopBottom = _start.y == 0 || _start.y == _height - 1;
        bool horizontal = startTopBottom;

        int min = 1, maxX = _width - 2, maxY = _height - 2;

        var current = _start;
        for (int i = 0; i < targetMoves - 1; i++)
        {
            if (horizontal)
            {
                int col = _rng.Next(min, maxX + 1);
                int guard = 200;
                while (guard-- > 0 && (col == current.x || col == _end.x))
                    col = _rng.Next(min, maxX + 1);
                var next = (col, current.y);
                anchors.Add(next);
                current = next;
            }
            else
            {
                int row = _rng.Next(min, maxY + 1);
                int guard = 200;
                while (guard-- > 0 && (row == current.y || row == _end.y))
                    row = _rng.Next(min, maxY + 1);
                var next = (current.x, row);
                anchors.Add(next);
                current = next;
            }
            horizontal = !horizontal;
        }

        var last = anchors[^1];
        if (!(last.x == _end.x || last.y == _end.y))
        {
            if (horizontal)
                anchors[^1] = (last.x, _end.y);
            else
                anchors[^1] = (_end.x, last.y);
        }
        anchors.Add(_end);

        for (int i = 0; i < anchors.Count - 1; i++)
        {
            var a = anchors[i]; var b = anchors[i + 1];
            if (!(a.x == b.x || a.y == b.y)) return null;

            int dx = Math.Sign(b.x - a.x), dy = Math.Sign(b.y - a.y);
            int sx = b.x + dx, sy = b.y + dy;
            if (InGrid(sx, sy) == false)
            {
                if (b != _end) return null;
            }
            if ((sx, sy) == _start || (sx, sy) == _end) return null;
        }

        return anchors;
    }

    #endregion
}