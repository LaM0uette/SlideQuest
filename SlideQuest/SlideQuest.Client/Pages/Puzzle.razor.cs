using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace SlideQuest.Client.Pages;

public class PuzzlePresenter : ComponentBase
{
    protected enum CellType { Empty, Obstacle, Path, Start, End }

    protected readonly int[] _sizes = { 8, 16, 32 };
    protected int _selectedSize = 8;

    protected int _n;
    protected CellType[,]? _grid;
    protected (int x, int y) _start;
    protected (int x, int y) _end;
    protected (int x, int y) _player;
    protected bool _won;
    protected List<string> _moves = new();
    protected readonly Random _rng = new();

    protected override void OnInitialized()
    {
        _n = _selectedSize;
    }

    protected void Generate()
    {
        // Tougher generation: allow more attempts to satisfy stricter constraints
        for (int attempt = 0; attempt < 150; attempt++)
        {
            _n = _selectedSize;
            _grid = new CellType[_n, _n];
            _moves.Clear();
            _won = false;

            PickStartEnd();

            var success = TryBuildGuaranteedPath(out var pathCells, out var blockers);
            if (!success) continue;

            // Enforce difficulty: require more segments (Pokémon-like complexity)
            // Define desired window based on size (number of moves/segments)
            int minMoves = _n <= 8 ? 6 : (_n <= 16 ? 7 : 8);
            int maxMoves = _n <= 8 ? 9 : (_n <= 16 ? 10 : 12);
            if (_moves.Count < minMoves || _moves.Count > maxMoves)
                continue;

            foreach (var c in pathCells)
                _grid[c.x, c.y] = CellType.Path;

            foreach (var b in blockers)
                if (InGrid(b.x, b.y) && _grid[b.x, b.y] == CellType.Empty)
                    _grid[b.x, b.y] = CellType.Obstacle;

            _grid[_start.x, _start.y] = CellType.Start;
            _grid[_end.x, _end.y] = CellType.End;

            // Place player at start
            _player = _start;

            AddRandomObstacles(pathCells, blockers);

            StateHasChanged();
            return;
        }

        _grid = new CellType[_selectedSize, _selectedSize];
        _moves.Clear();
        _won = false;
        StateHasChanged();
    }

    protected void PickStartEnd()
    {
        // Choose start on a random side, and end on the opposite side (mandatory)
        int startSide = _rng.Next(4); // 0=top,1=right,2=bottom,3=left
        int endSide = (startSide + 2) % 4; // opposite side

        _start = RandomCellOnSide(startSide);

        // Ensure start and end are not aligned to avoid trivial straight line
        for (int tries = 0; tries < 100; tries++)
        {
            var e = RandomCellOnSide(endSide);
            if (startSide % 2 == 0)
            {
                // top/bottom opposite: avoid same column
                if (e.x == _start.x) continue;
            }
            else
            {
                // left/right opposite: avoid same row
                if (e.y == _start.y) continue;
            }
            _end = e;
            return;
        }

        // Fallback: force a slight offset if random attempts failed
        var forced = RandomCellOnSide(endSide);
        if (startSide % 2 == 0)
        {
            forced.x = (forced.x + 1) % _n;
            if (forced.x == _start.x) forced.x = (forced.x + 1) % _n;
        }
        else
        {
            forced.y = (forced.y + 1) % _n;
            if (forced.y == _start.y) forced.y = (forced.y + 1) % _n;
        }
        _end = forced;
    }

    private (int x, int y) RandomCellOnSide(int side)
    {
        return side switch
        {
            0 => (_rng.Next(_n), 0),      // top
            1 => (_n - 1, _rng.Next(_n)), // right
            2 => (_rng.Next(_n), _n - 1), // bottom
            _ => (0, _rng.Next(_n))       // left
        };
    }

    protected (int x, int y) RandomBorderCell()
    {
        int side = _rng.Next(4);
        return side switch
        {
            0 => (_rng.Next(_n), 0),
            1 => (_n - 1, _rng.Next(_n)),
            2 => (_rng.Next(_n), _n - 1),
            _ => (0, _rng.Next(_n))
        };
    }

    protected bool TryBuildGuaranteedPath(out List<(int x, int y)> path, out HashSet<(int x, int y)> blockers)
    {
        // New stopper-driven zigzag path builder more faithful to Pokémon puzzles
        path = new();
        blockers = new();

        // Determine desired move count window by size (segments = moves)
        int minMoves = _n <= 8 ? 6 : (_n <= 16 ? 7 : 8);
        int maxMoves = _n <= 8 ? 9 : (_n <= 16 ? 10 : 12);
        int targetMoves = _rng.Next(minMoves, maxMoves + 1);

        // Build alternating orthogonal anchors between start and end
        var anchors = BuildZigZagAnchors(targetMoves);
        if (anchors is null || anchors.Count < 2) return false;

        // Convert anchors to mandatory stoppers (do not trust linear path for painting)
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            var a = anchors[i];
            var b = anchors[i + 1];
            if (!(a.x == b.x || a.y == b.y)) return false;

            int dx = System.Math.Sign(b.x - a.x);
            int dy = System.Math.Sign(b.y - a.y);

            // Place stopper just beyond landing if in-grid and not start/end
            int sx = b.x + dx, sy = b.y + dy;
            if (InGrid(sx, sy) && (sx, sy) != _start && (sx, sy) != _end)
                blockers.Add((sx, sy));
        }

        // Derive the move sequence from anchors by synthetizing the straight segments
        var linearCells = new List<(int x, int y)>();
        var cur = anchors[0];
        linearCells.Add(cur);
        for (int i = 1; i < anchors.Count; i++)
        {
            var nxt = anchors[i];
            int dx = System.Math.Sign(nxt.x - cur.x);
            int dy = System.Math.Sign(nxt.y - cur.y);
            int x = cur.x, y = cur.y;
            while (x != nxt.x || y != nxt.y)
            {
                x += dx; y += dy;
                if (!InGrid(x, y)) return false;
                linearCells.Add((x, y));
            }
            cur = nxt;
        }

        // Safety: derive moves and ensure the count matches our target window
        _moves.Clear();
        DeriveMoves(linearCells);
        if (_moves.Count < minMoves || _moves.Count > maxMoves)
            return false;

        // Simulate the sliding using only the computed mandatory blockers and walls.
        // Trace every visited cell. This becomes the only path we paint.
        if (!SimulateMovesAndTrace(_moves, blockers, out var traced))
            return false;
        // Must reach the end cell exactly
        if (traced.Count == 0 || traced[^1] != _end)
            return false;

        path = traced;
        return true;
    }

    protected bool AddSegment((int x, int y) a, (int x, int y) b, List<(int x, int y)> path, HashSet<(int x, int y)> blockers)
    {
        if (!(a.x == b.x || a.y == b.y)) return false;
        int dx = Math.Sign(b.x - a.x);
        int dy = Math.Sign(b.y - a.y);

        int x = a.x; int y = a.y;
        while (x != b.x || y != b.y)
        {
            x += dx; y += dy; if (!InGrid(x, y)) return false; path.Add((x, y));
        }
        int bx = b.x + dx, by = b.y + dy;
        if (InGrid(bx, by) && (bx, by) != _start && (bx, by) != _end)
            blockers.Add((bx, by));
        return true;
    }

    protected void DeriveMoves(List<(int x, int y)> path)
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

    protected void AddRandomObstacles(List<(int x, int y)> pathCells, HashSet<(int x, int y)> mandatoryBlockers)
    {
        if (_grid is null) return;
        var forbidden = new HashSet<(int x, int y)>(pathCells);
        foreach (var b in mandatoryBlockers) forbidden.Add(b);
        forbidden.Add(_start);
        forbidden.Add(_end);

        // 1) Structured decoy stoppers: add aligned obstacles that create meaningful stops
        int decoysTarget = Math.Max(4, _n / 2);
        int decoysPlaced = 0;
        int guardDecoy = _n * _n;
        while (decoysPlaced < decoysTarget && guardDecoy-- > 0)
        {
            // Pick random interior row or column and a position on it
            bool placeOnRow = _rng.NextDouble() < 0.5;
            int x = placeOnRow ? _rng.Next(1, _n - 1) : (_rng.NextDouble() < 0.5 ? 1 : _n - 2);
            int y = placeOnRow ? (_rng.NextDouble() < 0.5 ? 1 : _n - 2) : _rng.Next(1, _n - 1);

            if (forbidden.Contains((x, y))) continue;

            // Keep a small buffer from start/end and from guaranteed path
            bool nearCritical = false;
            foreach (var p in pathCells)
            {
                if (Math.Abs(p.x - x) + Math.Abs(p.y - y) <= 1) { nearCritical = true; break; }
            }
            if (nearCritical) continue;
            if (Math.Abs(_start.x - x) + Math.Abs(_start.y - y) <= 1) continue;
            if (Math.Abs(_end.x - x) + Math.Abs(_end.y - y) <= 1) continue;

            if (_grid[x, y] == CellType.Empty)
            {
                _grid[x, y] = CellType.Obstacle;
                forbidden.Add((x, y));
                decoysPlaced++;
            }
        }

        // 2) Random filler obstacles with moderated density
        double density = Math.Clamp(0.10 + (_n / 80.0), 0.10, 0.22);
        int toPlace = (int)Math.Round(_n * _n * density);

        int placed = 0; int guard = _n * _n * 5;
        while (placed < toPlace && guard-- > 0)
        {
            int x = _rng.Next(_n); int y = _rng.Next(_n);
            if (forbidden.Contains((x, y))) continue;

            bool nearCritical2 = false;
            foreach (var p in pathCells)
            {
                if (Math.Abs(p.x - x) + Math.Abs(p.y - y) == 1)
                { nearCritical2 = true; break; }
            }
            if (nearCritical2 && _rng.NextDouble() < 0.7) continue;

            if (_grid[x, y] == CellType.Empty)
            {
                _grid[x, y] = CellType.Obstacle;
                placed++;
            }
        }
    }

    protected bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < _n && y < _n;

    private static int Sgn(int v) => v == 0 ? 0 : (v > 0 ? 1 : -1);

    // Simulate slide gameplay using only borders and the provided blockers; trace the exact cells visited
    private bool SimulateMovesAndTrace(List<string> moves, HashSet<(int x, int y)> blockers, out List<(int x, int y)> traced)
    {
        traced = new List<(int x, int y)>();
        if (moves.Count == 0) return false;

        // Local predicate for blockers
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

            // If immediately blocked, invalid
            if (!InGrid(pos.x + dx, pos.y + dy) || Blocked(pos.x + dx, pos.y + dy))
                return false;

            // Slide and trace
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

    // Build anchors for a zigzag path with a given number of moves
    private List<(int x, int y)>? BuildZigZagAnchors(int targetMoves)
    {
        // Anchors include start and end, with targetMoves segments between them
        // We alternate orientation to ensure turns; place anchors on interior to force stoppers.
        var anchors = new List<(int x, int y)> { _start };

        // Determine initial orientation: if start on top/bottom, we begin horizontal, else vertical
        bool startTopBottom = _start.y == 0 || _start.y == _n - 1;
        bool horizontal = startTopBottom; // first move parallel to the opposite side

        // Working bounds inside the border so we can place stoppers beyond landings
        int min = 1, max = _n - 2;

        // Build intermediate anchors
        var current = _start;
        for (int i = 0; i < targetMoves - 1; i++)
        {
            if (horizontal)
            {
                // Choose a column different from current.x and not aligned with end (to avoid trivial finish too early)
                int col = _rng.Next(min, max + 1);
                int guard = 200;
                while (guard-- > 0 && (col == current.x || col == _end.x))
                    col = _rng.Next(min, max + 1);
                var next = (col, current.y);
                anchors.Add(next);
                current = next;
            }
            else
            {
                int row = _rng.Next(min, max + 1);
                int guard = 200;
                while (guard-- > 0 && (row == current.y || row == _end.y))
                    row = _rng.Next(min, max + 1);
                var next = (current.x, row);
                anchors.Add(next);
                current = next;
            }
            horizontal = !horizontal;
        }

        // Final anchor must be aligned with _end. Adjust last interior anchor if necessary.
        var last = anchors[^1];
        if (!(last.x == _end.x || last.y == _end.y))
        {
            if (horizontal)
                anchors[^1] = (last.x, _end.y);
            else
                anchors[^1] = (_end.x, last.y);
        }
        anchors.Add(_end);

        // Validate: ensure every step stays in-grid and allows a stopper beyond landing
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            var a = anchors[i]; var b = anchors[i + 1];
            if (!(a.x == b.x || a.y == b.y)) return null;

            int dx = Sgn(b.x - a.x), dy = Sgn(b.y - a.y);
            int sx = b.x + dx, sy = b.y + dy;
            if (InGrid(sx, sy) == false) {
                // If stopper would be off-grid, only acceptable if b is the end and the wall acts as stopper
                if (b != _end) return null;
            }
            if ((sx, sy) == _start || (sx, sy) == _end) return null;
        }

        return anchors;
    }

    // --- Interactive sliding logic ---
    protected void OnKeyDown(KeyboardEventArgs e)
    {
        if (_grid is null || _won) return;
        var key = e.Key?.ToLowerInvariant();
        switch (key)
        {
            case "arrowup": case "w": case "z": Slide(0, -1); break;
            case "arrowdown": case "s": Slide(0, 1); break;
            case "arrowleft": case "a": case "q": Slide(-1, 0); break;
            case "arrowright": case "d": Slide(1, 0); break;
        }
    }

    protected void SlideButton(string dir)
    {
        if (_grid is null || _won) return;
        switch (dir)
        {
            case "U": Slide(0, -1); break;
            case "D": Slide(0, 1); break;
            case "L": Slide(-1, 0); break;
            case "R": Slide(1, 0); break;
        }
    }

    protected void Slide(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;
        var (x, y) = _player;

        // slide until next cell would be blocked or out of grid
        while (true)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!InGrid(nx, ny) || IsBlocked(nx, ny))
                break;
            x = nx; y = ny;
        }

        _player = (x, y);
        if (_player == _end) _won = true;
        StateHasChanged();
    }

    protected bool IsBlocked(int x, int y)
    {
        if (_grid is null) return true;
        var cell = _grid[x, y];
        // Only obstacles and borders block sliding; start/end/path/empty are slideable
        return cell == CellType.Obstacle;
    }
}
