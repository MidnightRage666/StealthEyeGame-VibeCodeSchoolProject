using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using StealthEyeGame.Core;
using StealthEyeGame.Entities;

namespace StealthEyeGame.Levels
{
    /// <summary>
    /// Repräsentiert ein einzelnes, fertig generiertes Level: das Wand-Raster
    /// (inklusive zerstörbarer "angeknackster" Wände), die Gegner, Start- und
    /// Ausgangsposition. Stellt außerdem die geometrischen Hilfsfunktionen bereit,
    /// die Spieler-/Augenkollision und Sichtlinien-Prüfung benötigen (beide
    /// arbeiten direkt auf dem Raster - das ist deutlich schneller als
    /// Rechteck-Listen abzulaufen, und bleibt auch nach Wandzerstörung korrekt,
    /// weil es keinen separaten, zwischengespeicherten Rechteck-Cache gibt).
    /// </summary>
    public class Level
    {
        public int LevelNumber { get; }
        public WallType[,] WallGrid { get; }   // [col, row]
        public Vector2 PlayerStart { get; }
        public RectangleF ExitRect { get; }
        public List<Eye> Eyes { get; }

        public Level(int levelNumber, WallType[,] wallGrid, Vector2 playerStart, RectangleF exitRect, List<Eye> eyes)
        {
            LevelNumber = levelNumber;
            WallGrid = wallGrid;
            PlayerStart = playerStart;
            ExitRect = exitRect;
            Eyes = eyes;
        }

        public WallType GetWallType(int col, int row)
        {
            if (col < 0 || row < 0 || col >= GameConstants.Cols || row >= GameConstants.Rows) return WallType.Solid;
            return WallGrid[col, row];
        }

        public bool IsWallAtCell(int col, int row) => GetWallType(col, row) != WallType.Empty;

        /// <summary>Zerstört eine zuvor angeknackste Wand (z. B. durch eine Explosion). Kein Effekt bei Solid/Empty.</summary>
        public void DestroyWallAt(int col, int row)
        {
            if (col <= 0 || row <= 0 || col >= GameConstants.Cols - 1 || row >= GameConstants.Rows - 1) return;
            if (WallGrid[col, row] == WallType.Cracked)
            {
                WallGrid[col, row] = WallType.Empty;
            }
        }

        /// <summary>Ist der gegebene Weltpunkt innerhalb einer Wandzelle (Solid oder Cracked)?</summary>
        public bool IsWallAtWorld(Vector2 worldPos)
        {
            int col = (int)MathF.Floor(worldPos.X / GameConstants.CellSize);
            int row = (int)MathF.Floor(worldPos.Y / GameConstants.CellSize);
            return IsWallAtCell(col, row);
        }

        /// <summary>
        /// Prüft, ob ein achsparalleles Quadrat (Spieler- oder Augen-Bounding-Box) mit einer Wand kollidiert.
        /// </summary>
        public bool CollidesWithWall(Vector2 center, float radius)
        {
            int minCol = (int)MathF.Floor((center.X - radius) / GameConstants.CellSize);
            int maxCol = (int)MathF.Floor((center.X + radius) / GameConstants.CellSize);
            int minRow = (int)MathF.Floor((center.Y - radius) / GameConstants.CellSize);
            int maxRow = (int)MathF.Floor((center.Y + radius) / GameConstants.CellSize);

            for (int c = minCol; c <= maxCol; c++)
            {
                for (int r = minRow; r <= maxRow; r++)
                {
                    if (IsWallAtCell(c, r)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Raymarching-Sichtlinienprüfung zwischen zwei Weltpunkten. Wird sowohl von der
        /// Augen-Erkennungslogik als auch beim Zeichnen des (durch Wände begrenzten)
        /// Sichtkegel-Polygons verwendet.
        /// </summary>
        public bool HasWallBetween(Vector2 from, Vector2 to)
        {
            Vector2 diff = to - from;
            float distance = diff.Length();
            if (distance < 0.001f) return false;

            Vector2 dir = diff / distance;
            float traveled = 0f;
            while (traveled < distance)
            {
                Vector2 point = from + dir * traveled;
                if (IsWallAtWorld(point)) return true;
                traveled += GameConstants.VisionRayStep;
            }
            return false;
        }

        /// <summary>
        /// Wie weit man von 'from' in Richtung 'angle' laufen kann, bevor eine Wand
        /// oder die maximale Reichweite erreicht wird. Wird für das Sichtkegel-Polygon benutzt.
        /// </summary>
        public float RaycastDistance(Vector2 from, float angle, float maxRange)
        {
            Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            float traveled = 0f;
            while (traveled < maxRange)
            {
                Vector2 point = from + dir * traveled;
                if (IsWallAtWorld(point)) return traveled;
                traveled += GameConstants.VisionRayStep;
            }
            return maxRange;
        }

        public bool PlayerReachedExit(Vector2 playerPos, float playerRadius)
        {
            RectangleF inflated = ExitRect;
            inflated.Inflate(playerRadius * 0.5f, playerRadius * 0.5f);
            return inflated.Contains(playerPos.X, playerPos.Y);
        }

        /// <summary>Liefert alle nicht-leeren Zellen als Rechtecke fürs Rendering (inkl. Typ).</summary>
        public IEnumerable<(RectangleF Rect, WallType Type)> EnumerateWallsForRender()
        {
            for (int c = 0; c < GameConstants.Cols; c++)
            {
                for (int r = 0; r < GameConstants.Rows; r++)
                {
                    var type = WallGrid[c, r];
                    if (type == WallType.Empty) continue;
                    yield return (new RectangleF(c * GameConstants.CellSize, r * GameConstants.CellSize,
                                                  GameConstants.CellSize, GameConstants.CellSize), type);
                }
            }
        }

        /// <summary>Alle Rasterzellen (Mittelpunkte) innerhalb eines Kreises um center - für Explosionseffekte.</summary>
        public IEnumerable<(int Col, int Row)> CellsWithinRadius(Vector2 center, float radius)
        {
            int minCol = Math.Max(0, (int)MathF.Floor((center.X - radius) / GameConstants.CellSize));
            int maxCol = Math.Min(GameConstants.Cols - 1, (int)MathF.Floor((center.X + radius) / GameConstants.CellSize));
            int minRow = Math.Max(0, (int)MathF.Floor((center.Y - radius) / GameConstants.CellSize));
            int maxRow = Math.Min(GameConstants.Rows - 1, (int)MathF.Floor((center.Y + radius) / GameConstants.CellSize));

            for (int c = minCol; c <= maxCol; c++)
            {
                for (int r = minRow; r <= maxRow; r++)
                {
                    float cx = c * GameConstants.CellSize + GameConstants.CellSize / 2f;
                    float cy = r * GameConstants.CellSize + GameConstants.CellSize / 2f;
                    float dist = Vector2.Distance(new Vector2(cx, cy), center);
                    if (dist <= radius) yield return (c, r);
                }
            }
        }
    }
}
