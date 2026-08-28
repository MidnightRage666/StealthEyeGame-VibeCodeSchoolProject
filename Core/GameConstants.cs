namespace StealthEyeGame.Core
{
    /// <summary>
    /// Zentrale, spielweite Konstanten. Alles an einem Ort, damit
    /// Balancing und Layout-Anpassungen einfach bleiben.
    /// </summary>
    public static class GameConstants
    {
        // --- Spielfeld / Raster ---
        public const int CellSize = 32;          // Pixelgröße einer Rasterzelle
        public const int Cols = 30;               // Rasterbreite in Zellen
        public const int Rows = 20;               // Rasterhöhe in Zellen

        public const int CanvasWidth = Cols * CellSize;   // 960
        public const int CanvasHeight = Rows * CellSize;  // 640

        // Zusätzlicher Rand für UI-Leiste oberhalb des Spielfelds
        public const int TopBarHeight = 40;

        public const int WindowWidth = CanvasWidth;
        public const int WindowHeight = CanvasHeight + TopBarHeight;

        // --- Spieler ---
        public const float PlayerRadius = 7f;
        public const float PlayerBaseSpeed = 240f; // Pixel pro Sekunde
        public const float PlayerMaxHP = 100f;

        // --- Sichtkegel-Rendering ---
        public const int VisionRayCount = 28;      // Anzahl Strahlen für das Sichtkegel-Polygon
        public const float VisionRayStep = 4f;      // Schrittweite beim Raymarching in Pixel

        // --- Augen-KI: Bewegung ---
        public const float EyeRadius = 8f;             // Kollisionsradius, wenn ein Auge sich bewegt
        public const float EyeMoveSpeed = 95f;         // Pixel/Sekunde beim Verfolgen/Zurückkehren
        public const float EyeArriveThreshold = 8f;    // ab wann "Ziel erreicht" gilt

        // --- Augen-KI: Suche ---
        public const float SearchMinDuration = 3.0f;
        public const float SearchMaxDuration = 5.0f;
        public const float SearchSegmentMinDuration = 0.7f;
        public const float SearchSegmentMaxDuration = 1.4f;

        // --- Augen-KI: Idle-Blickverhalten ---
        public const float IdleLookMinDuration = 1.2f;
        public const float IdleLookMaxDuration = 3.0f;

        // --- Augen: Gesundheit ---
        public const float EyeMaxHP = 40f;

        // --- Dynamit / Explosion ---
        public const float DynamiteFuseSeconds = 3f;
        public const float ExplosionBaseRadius = 70f;
        public const float ExplosionBaseDamage = 60f;
        public const float ExplosionDirectHitRadiusFactor = 0.55f; // Anteil von Radius für "direkt getroffen"
        public const float ExplosionNoiseRadiusFactor = 2.4f;      // Anteil von Radius für "hört Explosion"
        public const float ExplosionVisualDuration = 0.6f;

        // --- Währung / Belohnungen ---
        public const int CoinsPerLevelComplete = 25;
        public const int CoinsPerEyeDestroyed = 10;
        public const float PlayerBaseMaxHP = PlayerMaxHP;

        // --- Simulation ---
        public const float FixedDeltaTime = 1f / 60f;
        public const int TimerIntervalMs = 16; // ~60 FPS
    }
}
