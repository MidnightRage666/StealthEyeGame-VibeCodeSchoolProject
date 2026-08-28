using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using StealthEyeGame.Core;
using StealthEyeGame.Entities;

namespace StealthEyeGame.Levels
{
    /// <summary>
    /// Erzeugt zufällige, aber garantiert lösbare Level.
    ///
    /// Strategie:
    ///  1. Zufälliges Wandraster gemäß Schwierigkeitsgrad erzeugen (Rand ist immer Solid).
    ///  2. Start- und Ausgangszelle mit ausreichendem Abstand wählen und freiräumen.
    ///  3. Einen garantierten, leicht verschlungenen Korridor von Start zu Ausgang
    ///     "einschneiden" (freiräumen) - das stellt Lösbarkeit unabhängig vom
    ///     Zufallsraster sicher, ganz ohne teure Regenerations-Schleifen. Da dieser
    ///     Korridor ausschließlich aus Empty-Zellen besteht, bleibt das Level auch
    ///     dann lösbar, wenn KEINE einzige angeknackste Wand zerstört wird - die
    ///     Anforderung aus Abschnitt 24 ("notwendige Wege dürfen nicht ausschließlich
    ///     durch zerstörbare Wände blockiert sein") ist damit automatisch erfüllt.
    ///  4. Mit einer BFS zusätzlich verifizieren, dass Start und Ausgang verbunden sind
    ///     (reine Absicherung/Assertion für zukünftige Änderungen).
    ///  5. Einen Teil der verbleibenden massiven Wände in "angeknackste" Wände
    ///     umwandeln - das sind rein optionale, mit Dynamit freisprengbare Abkürzungen.
    ///  6. Augen auf freien Zellen platzieren, mit Mindestabstand zueinander und
    ///     zum Startpunkt (damit der Spieler nicht sofort gesehen wird).
    /// </summary>
    public static class LevelGenerator
    {
        public static Level Generate(int levelNumber, Random rng)
        {
            var settings = DifficultySettings.ForLevel(levelNumber);
            var grid = new WallType[GameConstants.Cols, GameConstants.Rows];

            // --- 1) Rand + zufällige innere Wände ---
            for (int c = 0; c < GameConstants.Cols; c++)
            {
                for (int r = 0; r < GameConstants.Rows; r++)
                {
                    bool isBorder = c == 0 || r == 0 || c == GameConstants.Cols - 1 || r == GameConstants.Rows - 1;
                    if (isBorder)
                    {
                        grid[c, r] = WallType.Solid;
                    }
                    else
                    {
                        grid[c, r] = rng.NextDouble() < settings.WallDensity ? WallType.Solid : WallType.Empty;
                    }
                }
            }

            // --- 2) Start und Ausgang wählen ---
            (int c, int r) startCell = (2, rng.Next(2, GameConstants.Rows - 2));
            (int c, int r) exitCell = ChooseExitCell(rng, startCell);

            int startSafeRadius = levelNumber <= 1 ? 3 : (levelNumber <= 3 ? 2 : 1);
            ClearArea(grid, startCell, startSafeRadius);
            ClearArea(grid, exitCell, 1);

            // --- 3) Garantierten Korridor von Start zu Ausgang einschneiden ---
            CarveGuaranteedPath(grid, rng, startCell, exitCell);

            // --- 4) Verbindungsprüfung (Absicherung) ---
            System.Diagnostics.Debug.Assert(IsReachable(grid, startCell, exitCell),
                "Level-Generator hat kein lösbares Level erzeugt - das sollte nie passieren.");

            // --- 5) Angeknackste (zerstörbare) Wände als optionale Abkürzungen ---
            AddCrackedWalls(grid, rng, startCell, exitCell, levelNumber);

            // --- 6) Augen platzieren ---
            var eyes = PlaceEyes(grid, rng, startCell, exitCell, settings);

            Vector2 playerStart = CellCenter(startCell.c, startCell.r);
            RectangleF exitRect = new RectangleF(
                exitCell.c * GameConstants.CellSize + GameConstants.CellSize * 0.15f,
                exitCell.r * GameConstants.CellSize + GameConstants.CellSize * 0.15f,
                GameConstants.CellSize * 0.7f,
                GameConstants.CellSize * 0.7f);

            return new Level(levelNumber, grid, playerStart, exitRect, eyes);
        }

        private static (int c, int r) ChooseExitCell(Random rng, (int c, int r) startCell)
        {
            int minExitCol = GameConstants.Cols - 6;
            int maxExitCol = GameConstants.Cols - 2;
            for (int attempt = 0; attempt < 25; attempt++)
            {
                int c = rng.Next(minExitCol, maxExitCol);
                int r = rng.Next(2, GameConstants.Rows - 2);
                float dist = MathF.Sqrt((c - startCell.c) * (c - startCell.c) + (r - startCell.r) * (r - startCell.r));
                if (dist >= GameConstants.Cols * 0.45f)
                    return (c, r);
            }
            return (GameConstants.Cols - 3, (startCell.r + GameConstants.Rows / 2) % (GameConstants.Rows - 4) + 2);
        }

        private static void ClearArea(WallType[,] grid, (int c, int r) center, int radius)
        {
            for (int c = center.c - radius; c <= center.c + radius; c++)
            {
                for (int r = center.r - radius; r <= center.r + radius; r++)
                {
                    if (c <= 0 || r <= 0 || c >= GameConstants.Cols - 1 || r >= GameConstants.Rows - 1) continue;
                    grid[c, r] = WallType.Empty;
                }
            }
        }

        private static void CarveGuaranteedPath(WallType[,] grid, Random rng, (int c, int r) from, (int c, int r) to)
        {
            int cc = from.c, rr = from.r;
            grid[cc, rr] = WallType.Empty;

            int maxSteps = (GameConstants.Cols + GameConstants.Rows) * 4;
            int steps = 0;

            while ((cc != to.c || rr != to.r) && steps < maxSteps)
            {
                steps++;
                int dx = Math.Sign(to.c - cc);
                int dy = Math.Sign(to.r - rr);

                bool wiggle = rng.NextDouble() < 0.20;

                if (wiggle)
                {
                    bool horizontalWiggle = dy == 0 || rng.NextDouble() < 0.5;
                    int wdx = horizontalWiggle ? (rng.Next(2) == 0 ? -1 : 1) : 0;
                    int wdy = !horizontalWiggle ? (rng.Next(2) == 0 ? -1 : 1) : 0;
                    cc = Clamp(cc + wdx, 1, GameConstants.Cols - 2);
                    rr = Clamp(rr + wdy, 1, GameConstants.Rows - 2);
                }
                else
                {
                    bool moveHorizontal = dy == 0 ? true : (dx == 0 ? false : rng.NextDouble() < 0.5);
                    if (moveHorizontal && dx != 0) cc = Clamp(cc + dx, 1, GameConstants.Cols - 2);
                    else if (dy != 0) rr = Clamp(rr + dy, 1, GameConstants.Rows - 2);
                    else if (dx != 0) cc = Clamp(cc + dx, 1, GameConstants.Cols - 2);
                }

                grid[cc, rr] = WallType.Empty;
                if (rng.NextDouble() < 0.35)
                {
                    int extraC = Clamp(cc + (rng.Next(2) == 0 ? -1 : 1), 1, GameConstants.Cols - 2);
                    grid[extraC, rr] = WallType.Empty;
                }
            }

            if (cc != to.c || rr != to.r)
            {
                foreach (var cell in BresenhamLine(cc, rr, to.c, to.r))
                {
                    if (cell.c > 0 && cell.r > 0 && cell.c < GameConstants.Cols - 1 && cell.r < GameConstants.Rows - 1)
                        grid[cell.c, cell.r] = WallType.Empty;
                }
            }
        }

        /// <summary>
        /// Wandelt einen kleinen, mit dem Level skalierenden Anteil der übrig gebliebenen
        /// massiven Wände in zerstörbare "angeknackste" Wände um. Diese liegen NIE auf dem
        /// bereits garantierten Hauptweg (der ist schon Empty), sondern ausschließlich auf
        /// Zellen, die ansonsten dauerhaft Solid geblieben wären - sie sind also immer nur
        /// optionale Abkürzungen, niemals notwendig für die Lösbarkeit.
        /// </summary>
        private static void AddCrackedWalls(WallType[,] grid, Random rng, (int c, int r) startCell, (int c, int r) exitCell, int levelNumber)
        {
            float crackedChance = Math.Min(0.05f + levelNumber * 0.01f, 0.12f);
            int maxCracked = Math.Min(2 + levelNumber / 2, 8);
            int placed = 0;

            var candidates = new List<(int c, int r)>();
            for (int c = 1; c < GameConstants.Cols - 1; c++)
            {
                for (int r = 1; r < GameConstants.Rows - 1; r++)
                {
                    if (grid[c, r] != WallType.Solid) continue;
                    if ((c, r) == startCell || (c, r) == exitCell) continue;

                    // Nur Wände, die mindestens an einer Seite an eine freie Zelle grenzen,
                    // ergeben als Abkürzung überhaupt Sinn (sonst führt die Sprengung ins Nichts).
                    bool adjacentToOpen = grid[c - 1, r] != WallType.Solid || grid[c + 1, r] != WallType.Solid
                                          || grid[c, r - 1] != WallType.Solid || grid[c, r + 1] != WallType.Solid;
                    if (adjacentToOpen) candidates.Add((c, r));
                }
            }

            Shuffle(candidates, rng);

            foreach (var cell in candidates)
            {
                if (placed >= maxCracked) break;
                if (rng.NextDouble() > crackedChance) continue;
                grid[cell.c, cell.r] = WallType.Cracked;
                placed++;
            }
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static IEnumerable<(int c, int r)> BresenhamLine(int c0, int r0, int c1, int r1)
        {
            int dc = Math.Abs(c1 - c0), dr = Math.Abs(r1 - r0);
            int sc = c0 < c1 ? 1 : -1, sr = r0 < r1 ? 1 : -1;
            int err = dc - dr;
            int c = c0, r = r0;
            while (true)
            {
                yield return (c, r);
                if (c == c1 && r == r1) yield break;
                int e2 = 2 * err;
                if (e2 > -dr) { err -= dr; c += sc; }
                if (e2 < dc) { err += dc; r += sr; }
            }
        }

        private static bool IsReachable(WallType[,] grid, (int c, int r) from, (int c, int r) to)
        {
            var visited = new bool[GameConstants.Cols, GameConstants.Rows];
            var queue = new Queue<(int c, int r)>();
            queue.Enqueue(from);
            visited[from.c, from.r] = true;

            int[] dc = { 1, -1, 0, 0 };
            int[] dr = { 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == to) return true;

                for (int i = 0; i < 4; i++)
                {
                    int nc = cur.c + dc[i], nr = cur.r + dr[i];
                    if (nc < 0 || nr < 0 || nc >= GameConstants.Cols || nr >= GameConstants.Rows) continue;
                    if (visited[nc, nr] || grid[nc, nr] != WallType.Empty) continue;
                    visited[nc, nr] = true;
                    queue.Enqueue((nc, nr));
                }
            }
            return false;
        }

        private static List<Eye> PlaceEyes(WallType[,] grid, Random rng, (int c, int r) startCell, (int c, int r) exitCell,
                                            DifficultySettings.Params settings)
        {
            var eyes = new List<Eye>();
            var usedCells = new List<(int c, int r)>();

            int minDistFromStartSq = 5 * 5;
            int minDistBetweenEyesSq = 4 * 4;

            int attemptsBudget = settings.EyeCount * 60;
            int placed = 0;

            while (placed < settings.EyeCount && attemptsBudget-- > 0)
            {
                int c = rng.Next(1, GameConstants.Cols - 1);
                int r = rng.Next(1, GameConstants.Rows - 1);

                if (grid[c, r] != WallType.Empty) continue;
                if ((c, r) == startCell || (c, r) == exitCell) continue;

                int dStart = (c - startCell.c) * (c - startCell.c) + (r - startCell.r) * (r - startCell.r);
                if (dStart < minDistFromStartSq) continue;

                bool tooCloseToOther = false;
                foreach (var used in usedCells)
                {
                    int dd = (c - used.c) * (c - used.c) + (r - used.r) * (r - used.r);
                    if (dd < minDistBetweenEyesSq) { tooCloseToOther = true; break; }
                }
                if (tooCloseToOther) continue;

                usedCells.Add((c, r));
                placed++;

                float facingAngle = (float)(rng.Next(8) * (Math.PI / 4.0));
                float sweepAmplitude = MathF.PI / 5f + (float)rng.NextDouble() * (MathF.PI / 8f);
                int seed = rng.Next();

                eyes.Add(new Eye(
                    position: CellCenter(c, r),
                    facingAngle: facingAngle,
                    visionRange: settings.VisionRange,
                    visionHalfAngle: settings.VisionHalfAngle,
                    damagePerSecond: settings.DamagePerSecond,
                    slowMultiplierOnPlayer: settings.SlowMultiplier,
                    sweepAmplitude: sweepAmplitude,
                    seed: seed));
            }

            return eyes;
        }

        private static Vector2 CellCenter(int c, int r) =>
            new Vector2(c * GameConstants.CellSize + GameConstants.CellSize / 2f,
                        r * GameConstants.CellSize + GameConstants.CellSize / 2f);

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
