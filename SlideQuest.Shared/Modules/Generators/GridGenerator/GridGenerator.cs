using SlideQuest.Shared.Enums;

namespace GridGenerator;

public class GridGenerator : IGridGenerator
{
    #region Statements

    private int _width;
    private int _height;
    private int _seed;
    private Random _random = new();
    
    private Cell[,]? _cells;
    private Cell _start = new(0, 0);
    private Cell _end = new(0, 0);
    
    private List<Direction> _movesForWin = [];

    #endregion
    
    #region Methods

    public Grid Generate(int width, int height, int? seed = null)
    {
        _width = width;
        _height = height;

        _seed = seed ?? Random.Shared.Next(int.MinValue, int.MaxValue);
        _random = new Random(_seed);

        for (int attempt = 0; attempt < 150; attempt++)
        {
            _cells = new Cell[_width, _height];
            
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _cells[x, y] = new Cell(x, y);
                }
            }
            
            _movesForWin = [];

            (_start, _end) = CalculateStartAndEnd();

            if (!TryBuildGuaranteedPath(out List<Cell> pathCells, out HashSet<Cell> blockers))
                continue;

            int minMoves = Math.Min(_width, _height) <= 8 ? 6 : Math.Min(_width, _height) <= 16 ? 7 : 8;
            int maxMoves = Math.Min(_width, _height) <= 8 ? 9 : Math.Min(_width, _height) <= 16 ? 10 : 12;
            
            if (_movesForWin.Count < minMoves || _movesForWin.Count > maxMoves)
                continue;

            foreach (Cell c in pathCells)
                _cells[c.X, c.Y] = c with { Type = CellType.Path };

            foreach (Cell b in blockers)
            {
                if (InGrid(b.X, b.Y) && _cells[b.X, b.Y].Type == CellType.Empty)
                {
                    _cells[b.X, b.Y] = b with { Type = CellType.Obstacle };
                }
            }

            _cells[_start.X, _start.Y] = _start with { Type = CellType.Start };
            _cells[_end.X, _end.Y] = _end with { Type = CellType.End };

            AddRandomObstacles(pathCells, blockers);

            if (!EnforceUniqueness(blockers, pathCells))
                continue;

            int outW = _width + 2;
            int outH = _height + 2;
            Cell[,] withBorder = new Cell[outW, outH];

            for (int y = 0; y < outH; y++)
            {
                for (int x = 0; x < outW; x++)
                {
                    withBorder[x, y] = new Cell(x, y);
                }
            }

            for (int x = 0; x < outW; x++)
            {
                withBorder[x, 0] = new Cell(x, 0, CellType.Obstacle);                // haut
                withBorder[x, outH - 1] = new Cell(x, outH - 1, CellType.Obstacle);  // bas
            }
            
            for (int y = 0; y < outH; y++)
            {
                withBorder[0, y] = new Cell(0, y, CellType.Obstacle);                    // gauche
                withBorder[outW - 1, y] = new Cell(outW - 1, y, CellType.Obstacle);      // droite
            }

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    withBorder[x + 1, y + 1] = new Cell(x + 1, y + 1, _cells[x, y].Type);
                }
            }

            withBorder[_start.X + 1, _start.Y + 1] = new Cell(_start.X + 1, _start.Y + 1, CellType.Path);
            withBorder[_end.X + 1, _end.Y + 1] = new Cell(_end.X + 1, _end.Y + 1, CellType.Path);

            Cell startBorder;
            if (_start.X == 0) startBorder = new Cell(0, _start.Y + 1);                      // côté gauche → x=0
            else if (_start.X == _width - 1) startBorder = new Cell(outW - 1, _start.Y + 1); // côté droit → x=max
            else if (_start.Y == 0) startBorder = new Cell(_start.X + 1, 0);                 // côté haut → y=0
            else startBorder = new Cell(_start.X + 1, outH - 1);                             // côté bas → y=max

            Cell endBorder;
            if (_end.X == 0) endBorder = new Cell(0, _end.Y + 1);
            else if (_end.X == _width - 1) endBorder = new Cell(outW - 1, _end.Y + 1);
            else if (_end.Y == 0) endBorder = new Cell(_end.X + 1, 0);
            else endBorder = new Cell(_end.X + 1, outH - 1);

            withBorder[startBorder.X, startBorder.Y] = startBorder with { Type = CellType.Start };
            withBorder[endBorder.X, endBorder.Y] = endBorder with { Type = CellType.End };

            List<Direction> finalMoves = new(_movesForWin);

            if (finalMoves.Count == 0 || finalMoves[0] != InwardFromStart())
            {
                finalMoves.Insert(0, InwardFromStart());
            }
            
            if (finalMoves.Count == 0 || finalMoves[^1] != OutwardToEnd())
            {
                finalMoves.Add(OutwardToEnd());
            }

            if (!SimulateOn(withBorder, startBorder, endBorder, finalMoves, out List<Cell> tracedBorder))
                continue;

            for (int y = 0; y < outH; y++)
            {
                for (int x = 0; x < outW; x++)
                {
                    if (withBorder[x, y].Type == CellType.Path)
                    {
                        withBorder[x, y] = new Cell(x, y);
                    }
                }
            }
            
            foreach (Cell c in tracedBorder)
            {
                if ((c.X == startBorder.X && c.Y == startBorder.Y) || (c.X == endBorder.X && c.Y == endBorder.Y))
                    continue;
                
                if (withBorder[c.X, c.Y].Type == CellType.Empty)
                {
                    withBorder[c.X, c.Y] = c with { Type = CellType.Path };
                }
            }

            return new Grid(outW, outH, _seed, withBorder, startBorder, endBorder, finalMoves);

            Direction InwardFromStart()
            {
                if (_start.Y == 0) 
                    return Direction.Bottom;
                
                if (_start.Y == _height - 1) 
                    return Direction.Top;
                
                if (_start.X == 0) 
                    return Direction.Right;
                
                return Direction.Left; // _start.X == _width-1
            }

            Direction OutwardToEnd()
            {
                if (_end.Y == 0) 
                    return Direction.Top;
                
                if (_end.Y == _height - 1) 
                    return Direction.Bottom;
                
                if (_end.X == 0) 
                    return Direction.Left;
                
                return Direction.Right; // _end.X == _width-1
            }

            bool SimulateOn(Cell[,] grid, Cell s, Cell e, List<Direction> moves, out List<Cell> traced)
            {
                traced = [];
                int w = grid.GetLength(0), h = grid.GetLength(1);
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
                    
                    if (IsBlocked(pos.X + dx, pos.Y + dy)) 
                        return false;
                    
                    while (true)
                    {
                        int nx = pos.X + dx, ny = pos.Y + dy;
                        
                        if (IsBlocked(nx, ny)) 
                            break;
                        
                        pos = new Cell(nx, ny);
                        traced.Add(pos);
                    }
                }
                
                return pos == e;

                bool InBounds(int x, int y)
                {
                    return x >= 0 && y >= 0 && x < w && y < h;
                }

                bool IsBlocked(int x, int y)
                {
                    if (!InBounds(x, y)) 
                        return true;
                    
                    return grid[x, y].Type == CellType.Obstacle;
                }
            }
        }

        throw new InvalidOperationException("Grid generation failed after maximum attempts");
    }

    
    private (Cell Start, Cell End) CalculateStartAndEnd()
    {
        int startSide = _random.Next(4); // 0=top, 1=right, 2=bottom, 3=left
        int endSide = (startSide + 2) % 4; // opposite side

        Cell start = RandomCellOnSide(startSide);

        for (int tries = 0; tries < 100; tries++)
        {
            Cell cellOnSide = RandomCellOnSide(endSide);
            if (startSide % 2 == 0)
            {
                if (cellOnSide.X == _start.X) 
                    continue;
            }
            else
            {
                if (cellOnSide.Y == _start.Y) 
                    continue;
            }
            
            return (start, cellOnSide);
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
        
        return (start, forced);
    }

    private Cell RandomCellOnSide(int side)
    {
        return side switch
        {
            0 => new Cell(_random.Next(_width), 0),
            1 => new Cell(_width - 1, _random.Next(_height)),
            2 => new Cell(_random.Next(_width), _height - 1),
            _ => new Cell(0, _random.Next(_height))
        };
    }

    private bool TryBuildGuaranteedPath(out List<Cell> path, out HashSet<Cell> obstacles)
    {
        path = [];
        obstacles = [];

        int minMoves = Math.Min(_width, _height) <= 8 ? 6 : Math.Min(_width, _height) <= 16 ? 7 : 8;
        int maxMoves = Math.Min(_width, _height) <= 8 ? 9 : Math.Min(_width, _height) <= 16 ? 10 : 12;
        int targetMoves = _random.Next(minMoves, maxMoves + 1);

        List<Cell>? anchors = BuildZigZagAnchors(targetMoves);
        
        if (anchors is null || anchors.Count < 2) 
            return false;

        for (int i = 0; i < anchors.Count - 1; i++)
        {
            Cell a = anchors[i];
            Cell b = anchors[i + 1];
            
            if (!(a.X == b.X || a.Y == b.Y)) 
                return false;

            int dx = Math.Sign(b.X - a.X);
            int dy = Math.Sign(b.Y - a.Y);

            int sx = b.X + dx, sy = b.Y + dy;
            Cell aroundB = new(sx, sy);
            
            if (InGrid(sx, sy) && aroundB != _start && aroundB != _end)
                obstacles.Add(aroundB);
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
                
                if (!InGrid(x, y)) 
                    return false;
                
                linearCells.Add(new Cell(x, y));
            }
            
            cur = nxt;
        }

        _movesForWin.Clear();
        DeriveMoves(linearCells);
        
        if (_movesForWin.Count < minMoves || _movesForWin.Count > maxMoves)
            return false;

        if (!SimulateMovesAndTrace(_movesForWin, obstacles, out List<Cell> traced))
            return false;
        
        if (traced.Count == 0 || traced[^1] != _end)
            return false;

        path = traced;
        return true;
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
                int col = _random.Next(min, maxX + 1);
                int guard = 200;
                
                while (guard-- > 0 && (col == current.X || col == _end.X))
                {
                    col = _random.Next(min, maxX + 1);
                }
                
                Cell next = new(col, current.Y);
                anchors.Add(next);
                current = next;
            }
            else
            {
                int row = _random.Next(min, maxY + 1);
                int guard = 200;
                
                while (guard-- > 0 && (row == current.Y || row == _end.Y))
                {
                    row = _random.Next(min, maxY + 1);
                }
                
                Cell next = new(current.X, row);
                anchors.Add(next);
                current = next;
            }
            
            horizontal = !horizontal;
        }

        Cell last = anchors[^1];
        if (!(last.X == _end.X || last.Y == _end.Y))
        {
            if (horizontal)
            {
                anchors[^1] = new Cell(last.X, _end.Y);
            }
            else
            {
                anchors[^1] = new Cell(_end.X, last.Y);
            }
        }
        anchors.Add(_end);

        for (int i = 0; i < anchors.Count - 1; i++)
        {
            Cell a = anchors[i]; Cell b = anchors[i + 1];
            
            if (!(a.X == b.X || a.Y == b.Y)) 
                return null;

            int dx = Math.Sign(b.X - a.X), dy = Math.Sign(b.Y - a.Y);
            int sx = b.X + dx, sy = b.Y + dy;
            
            if (!InGrid(sx, sy))
            {
                if (b != _end) 
                    return null;
            }
            
            Cell aroundB = new(sx, sy);
            
            if (aroundB == _start || aroundB == _end) 
                return null;
        }

        return anchors;
    }

    private void DeriveMoves(List<Cell> path)
    {
        if (path.Count < 2) 
            return;
        
        int i = 1;
        while (i < path.Count)
        {
            Cell prev = path[i - 1];
            Cell cur = path[i];
            int dx = Math.Sign(cur.X - prev.X);
            int dy = Math.Sign(cur.Y - prev.Y);
            
            Direction dir = dx switch 
            { 
                > 0 => Direction.Right,
                < 0 => Direction.Left,
                _ => dy > 0 ? Direction.Bottom : Direction.Top
            };
            
            while (i < path.Count)
            {
                Cell p = path[i - 1]; Cell c = path[i];
                int dx2 = Math.Sign(c.X - p.X); int dy2 = Math.Sign(c.Y - p.Y);
                
                Direction dir2 = dx2 switch
                {
                    > 0 => Direction.Right,
                    < 0 => Direction.Left,
                    _ => dy2 > 0 ? Direction.Bottom : Direction.Top 
                };
                
                if (dir2 != dir) 
                    break;
                
                i++;
            }
            _movesForWin.Add(dir);
        }
    }
    
    private bool SimulateMovesAndTrace(List<Direction> moves, HashSet<Cell> blockers, out List<Cell> traced)
    {
        traced = [];
        
        if (moves.Count == 0) 
            return false;

        Cell pos = _start;
        traced.Add(pos);

        foreach (Direction move in moves)
        {
            int dx = 0, dy = 0;
            
            switch (move)
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

        bool Blocked(int x, int y)
        {
            return !InGrid(x, y) || blockers.Contains(new Cell(x, y));
        }
    }

    private void AddRandomObstacles(List<Cell> pathCells, HashSet<Cell> mandatoryBlockers)
    {
        if (_cells is null) 
            return;
        
        HashSet<Cell> forbidden = new(pathCells);
        
        foreach (Cell b in mandatoryBlockers) 
            forbidden.Add(b);
        
        forbidden.Add(_start);
        forbidden.Add(_end);

        double capPercent = Math.Min(_width, _height) <= 8 ? 0.07 : Math.Min(_width, _height) <= 16 ? 0.09 : 0.11;
        int maxTotalObstacles = (int)Math.Floor(_width * _height * capPercent);

        int decoysTarget = Math.Max(2, Math.Min(_width, _height) / 4);
        int decoysPlaced = 0;
        int guardDecoy = _width * _height;
        
        while (decoysPlaced < decoysTarget && guardDecoy-- > 0)
        {
            if (CountObstacles() >= maxTotalObstacles) 
                break;

            bool placeOnRow = _random.NextDouble() < 0.5;
            int x = placeOnRow ? _random.Next(1, _width - 1) : _random.NextDouble() < 0.5 ? 1 : _width - 2;
            int y = placeOnRow ? _random.NextDouble() < 0.5 ? 1 : _height - 2 : _random.Next(1, _height - 1);

            if (forbidden.Contains(new Cell(x, y))) 
                continue;

            bool nearCritical = pathCells.Any(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y) <= 1);
            if (nearCritical) 
                continue;
            
            if (Math.Abs(_start.X - x) + Math.Abs(_start.Y - y) <= 1) 
                continue;
            
            if (Math.Abs(_end.X - x) + Math.Abs(_end.Y - y) <= 1) 
                continue;

            if (!CanPlaceIsolated(x, y, forbidden)) 
                continue;
            
            _cells[x, y] = new Cell(x, y, CellType.Obstacle);
            forbidden.Add(new Cell(x, y));
            decoysPlaced++;
        }

        double baseDensity = Math.Min(_width, _height) <= 8 ? 0.02 : Math.Min(_width, _height) <= 16 ? 0.035 : 0.05;
        int desiredFiller = (int)Math.Round(_width * _height * baseDensity);

        int remaining = Math.Max(0, maxTotalObstacles - CountObstacles());
        int toPlace = Math.Min(desiredFiller, remaining);

        int placed = 0; int guard = _width * _height * 5;
        while (placed < toPlace && guard-- > 0)
        {
            if (CountObstacles() >= maxTotalObstacles) 
                break;

            int x = _random.Next(_width); 
            int y = _random.Next(_height);
            
            if (forbidden.Contains(new Cell(x, y))) 
                continue;

            bool nearCritical2 = pathCells.Any(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y) == 1);

            if (nearCritical2 && _random.NextDouble() < 0.7) 
                continue;

            if (!CanPlaceIsolated(x, y, forbidden)) 
                continue;
            
            _cells[x, y] = new Cell(x, y, CellType.Obstacle);
            placed++;
        }
    }

    private int CountObstacles()
    {
        if (_cells is null) 
            return 0;
        
        int obstacleCount = 0;
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (_cells[x, y].Type == CellType.Obstacle) obstacleCount++;
            }
        }
        
        return obstacleCount;
    }

    private bool CanPlaceIsolated(int x, int y, HashSet<Cell>? forbidden = null)
    {
        if (_cells is null) 
            return false;
        
        if (!InGrid(x, y)) 
            return false;
        
        if (x == 0 || y == 0 || x == _width - 1 || y == _height - 1) 
            return false;
        
        if (forbidden is not null && forbidden.Contains(new Cell(x, y))) 
            return false;
        
        if (_cells[x, y].Type != CellType.Empty) 
            return false;
        
        return !(
            InGrid(x + 1, y) && _cells[x + 1, y].Type == CellType.Obstacle 
            || InGrid(x - 1, y) && _cells[x - 1, y].Type == CellType.Obstacle
            || InGrid(x, y + 1) && _cells[x, y + 1].Type == CellType.Obstacle
            || InGrid(x, y - 1) && _cells[x, y - 1].Type == CellType.Obstacle
            );
    }
    
    private bool EnforceUniqueness(HashSet<Cell> mandatoryBlockers, List<Cell> primaryPath)
    {
        if (_cells is null) 
            return false;
        
        if (!SimulateMovesOnFullGrid(_movesForWin, out _)) 
            return false;

        HashSet<Cell> forbidden = new(primaryPath);
        
        foreach (Cell b in mandatoryBlockers) 
            forbidden.Add(b);
        
        forbidden.Add(_start); forbidden.Add(_end);

        for (int iter = 0; iter < _width * _height; iter++)
        {
            if (!FindAlternativeSolutionDistinctFromPrimary(out List<Cell> altStops))
                return true;

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
                    
                    if (!InGrid(nx, ny)) 
                        break;
                    
                    if (_cells[nx, ny].Type == CellType.Obstacle) 
                        break;
                    
                    if (nx == b.X && ny == b.Y) 
                        break;
                    
                    candidates.Add(new Cell(nx, ny));
                    x = nx; y = ny;
                }
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            bool placed = false;
            foreach (Cell c in candidates)
            {
                if (forbidden.Contains(c)) 
                    continue;
                
                if (_cells[c.X, c.Y].Type != CellType.Empty && _cells[c.X, c.Y].Type != CellType.Path) 
                    continue;

                CellType prev = _cells[c.X, c.Y].Type;

                if (!CanPlaceIsolated(c.X, c.Y, forbidden))
                    continue;

                _cells[c.X, c.Y] = c with { Type = CellType.Obstacle };

                if (SimulateMovesOnFullGrid(_movesForWin, out _))
                {
                    placed = true;
                    forbidden.Add(c);
                    break;
                }

                _cells[c.X, c.Y] = c with { Type = prev };
            }

            if (!placed)
                return false;
        }

        return false;
    }
    
    private bool SimulateMovesOnFullGrid(List<Direction> moves, out List<Cell> traced)
    {
        traced = [];
        
        if (_cells is null || moves.Count == 0) 
            return false;
        
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

            if (IsBlockedFull(pos.X + dx, pos.Y + dy)) 
                return false;
            
            while (true)
            {
                int nx = pos.X + dx, ny = pos.Y + dy;
                
                if (IsBlockedFull(nx, ny)) 
                    break;
                
                pos = new Cell(nx, ny);
                traced.Add(pos);
            }
        }

        return pos == _end;

        bool IsBlockedFull(int x, int y)
        {
            if (!InGrid(x, y)) 
                return true;
            
            return _cells![x, y].Type == CellType.Obstacle;
        }
    }

    private bool FindAlternativeSolutionDistinctFromPrimary(out List<Cell> altStops)
    {
        altStops = [];
        
        if (_cells is null) 
            return false;

        Dictionary<Cell, List<(Cell to, char m)>> transitions = new();
        Dictionary<Cell, int> dist = new();
        Dictionary<Cell, List<(Cell prev, char m)>> parents = new();
        Queue<Cell> q = new();
        dist[_start] = 0; 
        parents[_start] = []; 
        q.Enqueue(_start);

        while (q.Count > 0)
        {
            Cell u = q.Dequeue();
            int du = dist[u];
            
            foreach ((Cell v, char m) in GetTrans(u))
            {
                if (!dist.TryGetValue(v, out int value))
                {
                    dist[v] = du + 1;
                    parents[v] = [(u, m)];
                    q.Enqueue(v);
                }
                else if (value == du + 1)
                {
                    List<(Cell prev, char m)> list = parents[v];
                    bool already = list.Any(pr => pr.prev == u && pr.m == m);

                    if (!already)
                    {
                        list.Add((u, m));
                    }
                }
            }
        }

        if (!dist.ContainsKey(_end)) 
            return false;

        List<Direction> primary = _movesForWin;

        (List<Direction> seq, List<Cell> stops) one = ReconstructOne();
        
        if (one.seq.Count == primary.Count)
        {
            bool same = !one.seq.Where((t, i) => t != primary[i]).Any();

            if (!same)
            {
                altStops = one.stops; 
                return true;
            }
        }

        if (!TryReconstructDifferent(primary, out List<Cell> stops2)) 
            return false;

        altStops = stops2; 
        
        return true;

        List<(Cell to, char m)> GetTrans(Cell p)
        {
            if (transitions.TryGetValue(p, out List<(Cell to, char m)>? list)) 
                return list;
            
            list = [];
            foreach ((int dx, int dy, char ch) in new[] { (0,-1,'U'), (0,1,'D'), (-1,0,'L'), (1,0,'R') })
            {
                int x = p.X, y = p.Y;
                
                if (!InGrid(x + dx, y + dy) || _cells[x + dx, y + dy].Type == CellType.Obstacle) 
                    continue;
                
                while (InGrid(x + dx, y + dy) && _cells[x + dx, y + dy].Type != CellType.Obstacle)
                {
                    x += dx; y += dy;
                }
                
                Cell to = new(x, y);
                
                if (to != p)
                {
                    list.Add((to, ch));
                }
            }
            
            transitions[p] = list;
            return list;
        }

        (List<Direction> seq, List<Cell> stops) ReconstructOne()
        {
            List<char> seq = [];
            List<Cell> stops = [];
            Cell cur = _end; 
            stops.Add(cur);
            
            while (cur != _start)
            {
                (Cell prev, char m) pr = parents[cur][0];
                seq.Add(pr.m);
                cur = pr.prev; stops.Add(cur);
            }
            
            seq.Reverse(); 
            stops.Reverse();
            
            List<Direction> seqDir = seq.ConvertAll(c => 
                c switch
                {
                    'U' => Direction.Top,
                    'D' => Direction.Bottom,
                    'L' => Direction.Left,
                    'R' => Direction.Right, _ => Direction.Top 
                });
            
            return (seqDir, stops);
        }
        
        bool TryReconstructDifferent(List<Direction> primarySeq, out List<Cell> stops)
        {
            stops = [];
            Stack<(Cell node, int idx, List<char> seq)> stack = new();
            stack.Push((_end, 0, []));

            HashSet<(Cell, int)> visitedBranch = [];

            while (stack.Count > 0)
            {
                (Cell node, _, List<char> seq) = stack.Pop();
                
                if (node == _start)
                {
                    List<char> s = new(seq); s.Reverse();
                    
                    List<Direction> sDir = s.ConvertAll(c => c switch
                    {
                        'U' => Direction.Top,
                        'D' => Direction.Bottom,
                        'L' => Direction.Left,
                        'R' => Direction.Right,
                        _ => Direction.Top 
                    });
                    
                    if (sDir.Count == primarySeq.Count)
                    {
                        bool equal = !sDir.Where((t, i) => t != primarySeq[i]).Any();

                        if (!equal)
                        {
                            SimulateMovesOnFullGrid(sDir, out List<Cell> traced);
                            
                            stops = traced;
                            return true;
                        }
                    }
                    
                    continue;
                }

                if (!parents.TryGetValue(node, out List<(Cell prev, char m)>? parList)) 
                    continue;
                
                foreach ((Cell prev, char m) in parList)
                {
                    List<char> nextSeq = [..seq, m];
                    (Cell prev, int Count) key = (prev, nextSeq.Count);
                    
                    if (visitedBranch.Add(key))
                    {
                        stack.Push((prev, 0, nextSeq));
                    }
                }
            }

            stops = [];
            return false;
        }
    }
    
    private bool InGrid(int x, int y)
    {
        return x >= 0 && y >= 0 && x < _width && y < _height;
    }

    #endregion
}