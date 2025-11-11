using System;
using System.Collections.Generic;
using GridGenerator;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace SlideQuest.Client.Pages;

public class PuzzlePresenter : ComponentBase
{
    [Inject] protected IGridGenerator _gridGenerator { get; set; } = null!;

    protected enum Difficulty { Easy, Normal, Hard, Expert }

    protected Difficulty _difficulty = Difficulty.Easy;

    // Per-difficulty caps for max dimension
    private readonly Dictionary<Difficulty, int> _caps = new()
    {
        { Difficulty.Easy, 8 },
        { Difficulty.Normal, 16 },
        { Difficulty.Hard, 24 },
        { Difficulty.Expert, 32 }
    };

    // User-configurable maximums for width/height
    protected int _maxWidth;
    protected int _maxHeight;

    // Actual generated grid size
    protected int _width;
    protected int _height;

    protected Grid? _grid;
    protected Cell _player;
    protected bool _won;

    // Seed input as raw text; if empty or invalid -> randomize
    protected string? _seedInput;

    private readonly Random _rng = new();
    private const int MinSize = 5; // absolute minimum practical size for a playable grid (Easy level lower bound)

    // Per-difficulty minimum caps for min dimension
    private readonly Dictionary<Difficulty, int> _mins = new()
    {
        { Difficulty.Easy, 5 },
        { Difficulty.Normal, 10 },
        { Difficulty.Hard, 16 },
        { Difficulty.Expert, 24 }
    };

    protected int Min => MinFor(_difficulty);

    protected override void OnInitialized()
    {
        int cap = CapFor(_difficulty);
        int minCap = MinFor(_difficulty);
        _maxWidth = cap;
        _maxHeight = cap;
        _width = minCap;
        _height = minCap;
    }

    protected void OnDifficultyChanged(ChangeEventArgs e)
    {
        if (e.Value is null) return;
        if (Enum.TryParse(typeof(Difficulty), e.Value.ToString(), out object? value) && value is Difficulty d)
        {
            _difficulty = d;
            int cap = CapFor(_difficulty);
            // Reset inputs to the default values for the selected difficulty
            _maxWidth = cap;
            _maxHeight = cap;
            StateHasChanged();
        }
    }

    protected int CapFor(Difficulty d) => _caps[d];
    protected int MinFor(Difficulty d) => _mins[d];

    protected void Generate()
    {
        int cap = CapFor(_difficulty);
        int minCap = MinFor(_difficulty);
        int maxW = Math.Clamp(_maxWidth, minCap, cap);
        int maxH = Math.Clamp(_maxHeight, minCap, cap);

        // Parse seed input (keeps the input field untouched)
        int? parsedSeed = null;
        if (!string.IsNullOrWhiteSpace(_seedInput) && int.TryParse(_seedInput, out int parsed))
            parsedSeed = parsed;

        // Dimensions demandées pour la génération
        int reqW;
        int reqH;
        if (parsedSeed.HasValue)
        {
            // Dimensions déterministes basées sur la seed fournie
            Random dimRng = new(parsedSeed.Value);
            reqW = dimRng.Next(minCap, maxW + 1);
            reqH = dimRng.Next(minCap, maxH + 1);
        }
        else
        {
            // Dimensions aléatoires dans la plage
            reqW = _rng.Next(minCap, maxW + 1);
            reqH = _rng.Next(minCap, maxH + 1);
        }

        // Determine seed: use provided if any; otherwise randomize one
        int seedToUse = parsedSeed ?? Random.Shared.Next(int.MinValue, int.MaxValue);

        // Réessayer automatiquement si une génération échoue (pas de chemin/obstacles)
        const int maxAttempts = 5;
        Grid? generated = null;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                generated = _gridGenerator.Generate(reqW, reqH, seedToUse);
                if (generated is null)
                    continue;

                // Validation basique: au moins 1 mouvement et Start/End placés
                bool hasMoves = generated.Moves is { Count: > 0 };
                bool hasPathOrObstacle = false;
                for (int y = 0; y < generated.Height && !hasPathOrObstacle; y++)
                for (int x = 0; x < generated.Width && !hasPathOrObstacle; x++)
                {
                    var c = generated.Cells[x, y];
                    if (c == CellType.Path || c == CellType.Obstacle) hasPathOrObstacle = true;
                }

                if (hasMoves && hasPathOrObstacle)
                    break; // OK

                generated = null; // force retry
            }
            catch (Exception)
            {
                // Échec de génération: on retente
                generated = null;
            }
        }

        if (generated is null)
        {
            // Dernier recours: relancer une demande
            try
            {
                if (parsedSeed.HasValue)
                {
                    // Garder la même seed et les mêmes dimensions déterministes pour garantir la reproductibilité
                    generated = _gridGenerator.Generate(reqW, reqH, seedToUse);
                }
                else
                {
                    // Pas de seed saisie: on retente avec d'autres dimensions dans la même plage et une nouvelle seed aléatoire
                    int altW = _rng.Next(minCap, maxW + 1);
                    int altH = _rng.Next(minCap, maxH + 1);
                    int altSeed = Random.Shared.Next(int.MinValue, int.MaxValue);
                    generated = _gridGenerator.Generate(altW, altH, altSeed);
                }
            }
            catch
            {
                // reste null, on laisse l'UI inchangée
            }
        }

        if (generated is null)
            return; // pas de grille exploitable, rien ne change

        _grid = generated;
        // Ne pas remplir/altérer le champ input seed; afficher la seed utilisée séparément via _grid.Seed
        _width = _grid.Width;
        _height = _grid.Height;
        _player = _grid.Start;
        _won = false;
        StateHasChanged();
    }

    // All generation logic has been extracted to GridGenerator. Only keep player interactions below.

    private bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;

    // --- Interactive sliding logic ---
    protected void OnKeyDown(KeyboardEventArgs e)
    {
        if (_grid is null || _won) 
            return;
        
        string? key = e.Key?.ToLowerInvariant();
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
        if (dx == 0 && dy == 0) 
            return;
        
        (int x, int y) = _player;

        // slide until next cell would be blocked or out of grid
        while (true)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!InGrid(nx, ny) || IsBlocked(nx, ny))
                break;
            x = nx; y = ny;
        }

        _player = new Cell(x, y);
        if (_player == _grid?.End) _won = true;
        StateHasChanged();
    }

    protected bool IsBlocked(int x, int y)
    {
        if (_grid is null) 
            return true;
        
        CellType cell = _grid.Cells[x, y];
        return cell == CellType.Obstacle;
    }
}
