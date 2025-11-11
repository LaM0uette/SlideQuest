using SlideQuest.Shared.Enums;

namespace GridGenerator;

public class GridGenerator : IGridGenerator
{
    // Internal state for generation
    private int _width;
    private int _height;
    private CellType[,]? _cells;
    private Cell _start;
    private Cell _end;
    private readonly Random _rng = new();
    private List<Direction> _moves = [];

    public Grid Generate(int width, int height)
    {
        // 1) Générer la carte comme avant (sans bordure), avec chemin garanti, obstacles et mouvements
        _width = width;
        _height = height;

        for (int attempt = 0; attempt < 150; attempt++)
        {
            _cells = new CellType[_width, _height];
            _moves = [];

            PickStartEnd();

            if (!TryBuildGuaranteedPath(out List<Cell> pathCells, out HashSet<Cell> blockers))
                continue;

            int minMoves = Math.Min(_width, _height) <= 8 ? 6 : (Math.Min(_width, _height) <= 16 ? 7 : 8);
            int maxMoves = Math.Min(_width, _height) <= 8 ? 9 : (Math.Min(_width, _height) <= 16 ? 10 : 12);
            if (_moves.Count < minMoves || _moves.Count > maxMoves)
                continue;

            foreach (Cell c in pathCells)
                _cells[c.X, c.Y] = CellType.Path;

            foreach (Cell b in blockers)
                if (InGrid(b.X, b.Y) && _cells[b.X, b.Y] == CellType.Empty)
                    _cells[b.X, b.Y] = CellType.Obstacle;

            _cells[_start.X, _start.Y] = CellType.Start;
            _cells[_end.X, _end.Y] = CellType.End;

            AddRandomObstacles(pathCells, blockers);

            if (!EnforceUniqueness(blockers, pathCells))
                continue;

            // 2) Ajouter une bordure supplémentaire tout autour (+1 de chaque côté)
            int outW = _width + 2;
            int outH = _height + 2;
            CellType[,] withBorder = new CellType[outW, outH];

            // initialiser à Empty
            for (int y = 0; y < outH; y++)
                for (int x = 0; x < outW; x++)
                    withBorder[x, y] = CellType.Empty;

            // Bordure en obstacles
            for (int x = 0; x < outW; x++)
            {
                withBorder[x, 0] = CellType.Obstacle;               // haut
                withBorder[x, outH - 1] = CellType.Obstacle;        // bas
            }
            for (int y = 0; y < outH; y++)
            {
                withBorder[0, y] = CellType.Obstacle;               // gauche
                withBorder[outW - 1, y] = CellType.Obstacle;        // droite
            }

            // Copier la grille générée au centre avec un décalage (+1, +1)
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    withBorder[x + 1, y + 1] = _cells[x, y];

            // Placer Start/End sur les cases de la bordure
            // 1) Nettoyer les positions intérieures copiées (+1,+1)
            withBorder[_start.X + 1, _start.Y + 1] = CellType.Path;
            withBorder[_end.X + 1, _end.Y + 1] = CellType.Path;

            // 2) Calculer les positions sur la bordure correspondantes au côté d'origine
            Cell startBorder;
            if (_start.X == 0) startBorder = new Cell(0, _start.Y + 1);               // côté gauche → x=0
            else if (_start.X == _width - 1) startBorder = new Cell(outW - 1, _start.Y + 1); // côté droit → x=max
            else if (_start.Y == 0) startBorder = new Cell(_start.X + 1, 0);           // côté haut → y=0
            else startBorder = new Cell(_start.X + 1, outH - 1);                        // côté bas → y=max

            Cell endBorder;
            if (_end.X == 0) endBorder = new Cell(0, _end.Y + 1);
            else if (_end.X == _width - 1) endBorder = new Cell(outW - 1, _end.Y + 1);
            else if (_end.Y == 0) endBorder = new Cell(_end.X + 1, 0);
            else endBorder = new Cell(_end.X + 1, outH - 1);

            // 3) Marquer Start/End sur la bordure (écrase l'obstacle à ces emplacements)
            withBorder[startBorder.X, startBorder.Y] = CellType.Start;
            withBorder[endBorder.X, endBorder.Y] = CellType.End;

            // 4) Ajuster la suite de mouvements pour démarrer au Start de bordure et finir à l'End de bordure
            List<Direction> finalMoves = new List<Direction>(_moves);
            Direction InwardFromStart()
            {
                if (_start.Y == 0) return Direction.Bottom;
                if (_start.Y == _height - 1) return Direction.Top;
                if (_start.X == 0) return Direction.Right;
                return Direction.Left; // _start.X == _width-1
            }
            Direction OutwardToEnd()
            {
                if (_end.Y == 0) return Direction.Top;
                if (_end.Y == _height - 1) return Direction.Bottom;
                if (_end.X == 0) return Direction.Left;
                return Direction.Right; // _end.X == _width-1
            }
            if (finalMoves.Count == 0 || finalMoves[0] != InwardFromStart())
                finalMoves.Insert(0, InwardFromStart());
            if (finalMoves.Count == 0 || finalMoves[^1] != OutwardToEnd())
                finalMoves.Add(OutwardToEnd());

            // 5) Simuler les mouvements sur la grille avec bordure pour garantir la faisabilité
            bool SimulateOn(CellType[,] grid, Cell s, Cell e, List<Direction> moves, out List<Cell> traced)
            {
                traced = new List<Cell>();
                int w = grid.GetLength(0), h = grid.GetLength(1);
                bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
                bool IsBlocked(int x, int y)
                {
                    if (!InBounds(x, y)) return true;
                    return grid[x, y] == CellType.Obstacle;
                }
                Cell pos = s;
                traced.Add(pos);
                foreach (Direction m in moves)
                {
                    int dx = 0, dy = 0;
                    switch (m)
                    {
                        case Direction.Top: dy = -1; break;
                        case Direction.Bottom: dy = 1; break;
                        case Direction.Left: dx = -1; break;
                        case Direction.Right: dx = 1; break;
                        default: return false;
                    }
                    // Le premier pas doit être possible
                    if (IsBlocked(pos.X + dx, pos.Y + dy)) return false;
                    while (true)
                    {
                        int nx = pos.X + dx, ny = pos.Y + dy;
                        if (IsBlocked(nx, ny)) break;
                        pos = new Cell(nx, ny);
                        traced.Add(pos);
                    }
                }
                return pos == e;
            }

            if (!SimulateOn(withBorder, startBorder, endBorder, finalMoves, out List<Cell> tracedBorder))
            {
                // Séquence non faisable (ex: premier pas impossible ou fin != End) → retenter une génération
                continue;
            }

            // 6) Nettoyer les anciens chemins et peindre le chemin réellement parcouru
            for (int y = 0; y < outH; y++)
                for (int x = 0; x < outW; x++)
                    if (withBorder[x, y] == CellType.Path)
                        withBorder[x, y] = CellType.Empty;
            foreach (Cell c in tracedBorder)
            {
                if ((c.X == startBorder.X && c.Y == startBorder.Y) || (c.X == endBorder.X && c.Y == endBorder.Y))
                    continue;
                if (withBorder[c.X, c.Y] == CellType.Empty)
                    withBorder[c.X, c.Y] = CellType.Path;
            }

            // Retourner la grille finale (avec bordure) et les mouvements validés
            return new Grid(outW, outH, withBorder, startBorder, endBorder, finalMoves);
        }

        // En cas d'échec après plusieurs tentatives, ne pas retourner une grille vide non jouable.
        // On signale l'échec au caller pour qu'il relance une génération.
        throw new InvalidOperationException("Grid generation failed after maximum attempts");
    }

    #region Private helpers

    private void PickStartEnd()
    {
        int startSide = _rng.Next(4); // 0=top,1=right,2=bottom,3=left
        int endSide = (startSide + 2) % 4; // opposite side

        _start = RandomCellOnSide(startSide);

        for (int tries = 0; tries < 100; tries++)
        {
            Cell e = RandomCellOnSide(endSide);
            if (startSide % 2 == 0)
            {
                if (e.X == _start.X) continue;
            }
            else
            {
                if (e.Y == _start.Y) continue;
            }
            _end = e;
            return;
        }

        Cell forced = RandomCellOnSide(endSide);
        if (startSide % 2 == 0)
        {
            int newX = (forced.X + 1) % _width;
            if (newX == _start.X) newX = (newX + 1) % _width;
            forced = new Cell(newX, forced.Y);
        }
        else
        {
            int newY = (forced.Y + 1) % _height;
            if (newY == _start.Y) newY = (newY + 1) % _height;
            forced = new Cell(forced.X, newY);
        }
        _end = forced;
    }

    private Cell RandomCellOnSide(int side)
    {
        return side switch
        {
            0 => new Cell(_rng.Next(_width), 0),
            1 => new Cell(_width - 1, _rng.Next(_height)),
            2 => new Cell(_rng.Next(_width), _height - 1),
            _ => new Cell(0, _rng.Next(_height))
        };
    }

    private bool TryBuildGuaranteedPath(out List<Cell> path, out HashSet<Cell> blockers)
    {
        path = [];
        blockers = [];

        int minMoves = Math.Min(_width, _height) <= 8 ? 6 : (Math.Min(_width, _height) <= 16 ? 7 : 8);
        int maxMoves = Math.Min(_width, _height) <= 8 ? 9 : (Math.Min(_width, _height) <= 16 ? 10 : 12);
        int targetMoves = _rng.Next(minMoves, maxMoves + 1);

        List<Cell>? anchors = BuildZigZagAnchors(targetMoves);
        if (anchors is null || anchors.Count < 2) return false;

        for (int i = 0; i < anchors.Count - 1; i++)
        {
            Cell a = anchors[i];
            Cell b = anchors[i + 1];
            if (!(a.X == b.X || a.Y == b.Y)) return false;

            int dx = Math.Sign(b.X - a.X);
            int dy = Math.Sign(b.Y - a.Y);

            int sx = b.X + dx, sy = b.Y + dy;
            Cell aroundB = new Cell(sx, sy);
            if (InGrid(sx, sy) && aroundB != _start && aroundB != _end)
                blockers.Add(aroundB);
        }

        List<Cell> linearCells = [];
        Cell cur = anchors[0];
        linearCells.Add(cur);
        for (int i = 1; i < anchors.Count; i++)
        {
            Cell nxt = anchors[i];
            int dx = Math.Sign(nxt.X - cur.X);
            int dy = Math.Sign(nxt.Y - cur.Y);
            int x = cur.X, y = cur.Y;
            while (x != nxt.X || y != nxt.Y)
            {
                x += dx; y += dy;
                if (!InGrid(x, y)) return false;
                linearCells.Add(new Cell(x, y));
            }
            cur = nxt;
        }

        _moves.Clear();
        DeriveMoves(linearCells);
        if (_moves.Count < minMoves || _moves.Count > maxMoves)
            return false;

        if (!SimulateMovesAndTrace(_moves, blockers, out List<Cell> traced))
            return false;
        if (traced.Count == 0 || traced[^1] != _end)
            return false;

        path = traced;
        return true;
    }

    private void DeriveMoves(List<Cell> path)
    {
        if (path.Count < 2) return;
        int i = 1;
        while (i < path.Count)
        {
            Cell prev = path[i - 1];
            Cell cur = path[i];
            int dx = Math.Sign(cur.X - prev.X);
            int dy = Math.Sign(cur.Y - prev.Y);
            Direction dir = dx switch { > 0 => Direction.Right, < 0 => Direction.Left, _ => (dy > 0 ? Direction.Bottom : Direction.Top) };
            while (i < path.Count)
            {
                Cell p = path[i - 1]; Cell c = path[i];
                int dx2 = Math.Sign(c.X - p.X); int dy2 = Math.Sign(c.Y - p.Y);
                Direction dir2 = dx2 switch { > 0 => Direction.Right, < 0 => Direction.Left, _ => (dy2 > 0 ? Direction.Bottom : Direction.Top) };
                if (dir2 != dir) break;
                i++;
            }
            _moves.Add(dir);
        }
    }

    private void AddRandomObstacles(List<Cell> pathCells, HashSet<Cell> mandatoryBlockers)
    {
        if (_cells is null) return;
        HashSet<Cell> forbidden = new HashSet<Cell>(pathCells);
        foreach (Cell b in mandatoryBlockers) forbidden.Add(b);
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

            if (forbidden.Contains(new Cell(x, y))) continue;

            bool nearCritical = false;
            foreach (Cell p in pathCells)
            {
                if (Math.Abs(p.X - x) + Math.Abs(p.Y - y) <= 1) { nearCritical = true; break; }
            }
            if (nearCritical) continue;
            if (Math.Abs(_start.X - x) + Math.Abs(_start.Y - y) <= 1) continue;
            if (Math.Abs(_end.X - x) + Math.Abs(_end.Y - y) <= 1) continue;

            if (CanPlaceIsolated(x, y, forbidden))
            {
                _cells[x, y] = CellType.Obstacle;
                forbidden.Add(new Cell(x, y));
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
            if (forbidden.Contains(new Cell(x, y))) continue;

            bool nearCritical2 = false;
            foreach (Cell p in pathCells)
            {
                if (Math.Abs(p.X - x) + Math.Abs(p.Y - y) == 1)
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

    private bool CanPlaceIsolated(int x, int y, HashSet<Cell>? forbidden = null)
    {
        if (_cells is null) return false;
        if (!InGrid(x, y)) return false;
        if (x == 0 || y == 0 || x == _width - 1 || y == _height - 1) return false;
        if (forbidden is not null && forbidden.Contains(new Cell(x, y))) return false;
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

    private bool SimulateMovesAndTrace(List<Direction> moves, HashSet<Cell> blockers, out List<Cell> traced)
    {
        traced = [];
        if (moves.Count == 0) return false;

        bool Blocked(int x, int y)
        {
            return !InGrid(x, y) || blockers.Contains(new Cell(x, y));
        }

        Cell pos = _start;
        traced.Add(pos);

        foreach (Direction m in moves)
        {
            int dx = 0, dy = 0;
            switch (m)
            {
                case Direction.Top: dy = -1; break;
                case Direction.Bottom: dy = 1; break;
                case Direction.Left: dx = -1; break;
                case Direction.Right: dx = 1; break;
                default: return false;
            }

            if (!InGrid(pos.X + dx, pos.Y + dy) || Blocked(pos.X + dx, pos.Y + dy))
                return false;

            while (true)
            {
                int nx = pos.X + dx;
                int ny = pos.Y + dy;
                if (!InGrid(nx, ny) || Blocked(nx, ny))
                    break;
                pos = new Cell(nx, ny);
                traced.Add(pos);
            }
        }

        return true;
    }

    private bool SimulateMovesOnFullGrid(List<Direction> moves, out List<Cell> traced)
    {
        traced = [];
        if (_cells is null || moves.Count == 0) return false;
        Cell pos = _start; traced.Add(pos);

        bool IsBlockedFull(int x, int y)
        {
            if (!InGrid(x, y)) return true;
            return _cells![x, y] == CellType.Obstacle;
        }

        foreach (Direction m in moves)
        {
            int dx = 0, dy = 0;
            switch (m)
            {
                case Direction.Top: dy = -1; break;
                case Direction.Bottom: dy = 1; break;
                case Direction.Left: dx = -1; break;
                case Direction.Right: dx = 1; break;
                default: return false;
            }

            if (IsBlockedFull(pos.X + dx, pos.Y + dy)) return false;
            while (true)
            {
                int nx = pos.X + dx, ny = pos.Y + dy;
                if (IsBlockedFull(nx, ny)) break;
                pos = new Cell(nx, ny);
                traced.Add(pos);
            }
        }

        return pos == _end;
    }

    private bool EnforceUniqueness(HashSet<Cell> mandatoryBlockers, List<Cell> primaryPath)
    {
        if (_cells is null) return false;
        if (!SimulateMovesOnFullGrid(_moves, out _)) return false;

        HashSet<Cell> forbidden = new HashSet<Cell>(primaryPath);
        foreach (Cell b in mandatoryBlockers) forbidden.Add(b);
        forbidden.Add(_start); forbidden.Add(_end);

        for (int iter = 0; iter < _width * _height; iter++)
        {
            if (!FindAlternativeSolutionDistinctFromPrimary(out List<Direction> altMoves, out List<Cell> altStops))
            {
                return true;
            }

            List<Cell> candidates = [];
            for (int i = 1; i < altStops.Count; i++)
            {
                Cell a = altStops[i - 1];
                Cell b = altStops[i];
                int dx = Math.Sign(b.X - a.X);
                int dy = Math.Sign(b.Y - a.Y);
                int x = a.X, y = a.Y;
                while (true)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!InGrid(nx, ny)) break;
                    if (_cells[nx, ny] == CellType.Obstacle) break;
                    if (nx == b.X && ny == b.Y) break;
                    candidates.Add(new Cell(nx, ny));
                    x = nx; y = ny;
                }
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            bool placed = false;
            foreach (Cell c in candidates)
            {
                if (forbidden.Contains(c)) continue;
                if (_cells[c.X, c.Y] != CellType.Empty && _cells[c.X, c.Y] != CellType.Path) continue;

                CellType prev = _cells[c.X, c.Y];

                if (!CanPlaceIsolated(c.X, c.Y, forbidden))
                    continue;

                _cells[c.X, c.Y] = CellType.Obstacle;

                if (SimulateMovesOnFullGrid(_moves, out _))
                {
                    placed = true;
                    forbidden.Add(c);
                    break;
                }
                else
                {
                    _cells[c.X, c.Y] = prev;
                }
            }

            if (!placed)
            {
                return false;
            }
        }

        return false;
    }

    private bool FindAlternativeSolutionDistinctFromPrimary(out List<Direction> altMoves, out List<Cell> altStops)
    {
        altMoves = [];
        altStops = [];
        if (_cells is null) return false;

        Dictionary<Cell, List<(Cell to, char m)>> transitions = new Dictionary<Cell, List<(Cell to, char m)>>();
        List<(Cell to, char m)> GetTrans(Cell p)
        {
            if (transitions.TryGetValue(p, out List<(Cell to, char m)>? list)) return list;
            list = [];
            foreach ((int dx, int dy, char ch) in new (int, int, char)[] { (0,-1,'U'), (0,1,'D'), (-1,0,'L'), (1,0,'R') })
            {
                int x = p.X, y = p.Y;
                if (!InGrid(x + dx, y + dy) || _cells[x + dx, y + dy] == CellType.Obstacle) continue;
                while (InGrid(x + dx, y + dy) && _cells[x + dx, y + dy] != CellType.Obstacle)
                {
                    x += dx; y += dy;
                }
                Cell to = new Cell(x, y);
                if (to != p) list.Add((to, ch));
            }
            transitions[p] = list;
            return list;
        }

        Dictionary<Cell, int> dist = new Dictionary<Cell, int>();
        Dictionary<Cell, List<(Cell prev, char m)>> parents = new Dictionary<Cell, List<(Cell prev, char m)>>() ;
        Queue<Cell> q = new Queue<Cell>();
        dist[_start] = 0; parents[_start] = []; q.Enqueue(_start);

        while (q.Count > 0)
        {
            Cell u = q.Dequeue();
            int du = dist[u];
            foreach ((Cell to, char m) tr in GetTrans(u))
            {
                Cell v = tr.to; char m = tr.m;
                if (!dist.ContainsKey(v))
                {
                    dist[v] = du + 1;
                    parents[v] = [(u, m)];
                    q.Enqueue(v);
                }
                else if (dist[v] == du + 1)
                {
                    List<(Cell prev, char m)> list = parents[v];
                    bool already = false;
                    foreach ((Cell prev, char m) pr in list) if (pr.prev == u && pr.m == m) { already = true; break; }
                    if (!already) list.Add((u, m));
                }
            }
        }

        if (!dist.ContainsKey(_end)) return false;

        List<Direction> primary = _moves;

        (List<Direction> seq, List<Cell> stops) ReconstructOne()
        {
            List<char> seq = [];
            List<Cell> stops = [];
            Cell cur = _end; stops.Add(cur);
            while (cur != _start)
            {
                (Cell prev, char m) pr = parents[cur][0];
                seq.Add(pr.m);
                cur = pr.prev; stops.Add(cur);
            }
            seq.Reverse(); stops.Reverse();
            List<Direction> seqDir = seq.ConvertAll(c => c switch { 'U' => Direction.Top, 'D' => Direction.Bottom, 'L' => Direction.Left, 'R' => Direction.Right, _ => Direction.Top });
            return (seqDir, stops);
        }

        bool TryReconstructDifferent(List<Direction> primarySeq, out List<Direction> alt, out List<Cell> stops)
        {
            alt = []; stops = [];
            Stack<(Cell node, int idx, List<char> seq)> stack = new Stack<(Cell node, int idx, List<char> seq)>();
            stack.Push((_end, 0, []));

            HashSet<(Cell, int)> visitedBranch = [];

            while (stack.Count > 0)
            {
                (Cell node, _, List<char> seq) = stack.Pop();
                if (node == _start)
                {
                    List<char> s = new List<char>(seq); s.Reverse();
                    List<Direction> sDir = s.ConvertAll(c => c switch { 'U' => Direction.Top, 'D' => Direction.Bottom, 'L' => Direction.Left, 'R' => Direction.Right, _ => Direction.Top });
                    if (sDir.Count == primarySeq.Count)
                    {
                        bool equal = true;
                        for (int i = 0; i < sDir.Count; i++) if (sDir[i] != primarySeq[i]) { equal = false; break; }
                        if (!equal)
                        {
                            alt = sDir;
                            SimulateMovesOnFullGrid(alt, out List<Cell> traced);
                            stops = traced;
                            return true;
                        }
                    }
                    continue;
                }

                if (!parents.ContainsKey(node)) continue;
                List<(Cell prev, char m)> parList = parents[node];
                for (int i = 0; i < parList.Count; i++)
                {
                    (Cell prev, char m) = parList[i];
                    List<char> nextSeq = [..seq, m];
                    (Cell prev, int Count) key = (prev, nextSeq.Count);
                    if (visitedBranch.Add(key))
                        stack.Push((prev, 0, nextSeq));
                }
            }

            alt = []; stops = [];
            return false;
        }

        (List<Direction> seq, List<Cell> stops) one = ReconstructOne();
        if (one.seq.Count == primary.Count)
        {
            bool same = true; for (int i = 0; i < one.seq.Count; i++) if (one.seq[i] != primary[i]) { same = false; break; }
            if (!same)
            {
                altMoves = one.seq; altStops = one.stops; return true;
            }
        }

        if (TryReconstructDifferent(primary, out List<Direction> alt, out List<Cell> stops2))
        {
            altMoves = alt; altStops = stops2; return true;
        }

        return false;
    }

    private List<Cell>? BuildZigZagAnchors(int targetMoves)
    {
        List<Cell> anchors = [_start];

        bool startTopBottom = _start.Y == 0 || _start.Y == _height - 1;
        bool horizontal = startTopBottom;

        int min = 1, maxX = _width - 2, maxY = _height - 2;

        Cell current = _start;
        for (int i = 0; i < targetMoves - 1; i++)
        {
            if (horizontal)
            {
                int col = _rng.Next(min, maxX + 1);
                int guard = 200;
                while (guard-- > 0 && (col == current.X || col == _end.X))
                    col = _rng.Next(min, maxX + 1);
                Cell next = new Cell(col, current.Y);
                anchors.Add(next);
                current = next;
            }
            else
            {
                int row = _rng.Next(min, maxY + 1);
                int guard = 200;
                while (guard-- > 0 && (row == current.Y || row == _end.Y))
                    row = _rng.Next(min, maxY + 1);
                Cell next = new Cell(current.X, row);
                anchors.Add(next);
                current = next;
            }
            horizontal = !horizontal;
        }

        Cell last = anchors[^1];
        if (!(last.X == _end.X || last.Y == _end.Y))
        {
            if (horizontal)
                anchors[^1] = new Cell(last.X, _end.Y);
            else
                anchors[^1] = new Cell(_end.X, last.Y);
        }
        anchors.Add(_end);

        for (int i = 0; i < anchors.Count - 1; i++)
        {
            Cell a = anchors[i]; Cell b = anchors[i + 1];
            if (!(a.X == b.X || a.Y == b.Y)) return null;

            int dx = Math.Sign(b.X - a.X), dy = Math.Sign(b.Y - a.Y);
            int sx = b.X + dx, sy = b.Y + dy;
            if (InGrid(sx, sy) == false)
            {
                if (b != _end) return null;
            }
            Cell aroundB = new Cell(sx, sy);
            if (aroundB == _start || aroundB == _end) return null;
        }

        return anchors;
    }

    #endregion
}