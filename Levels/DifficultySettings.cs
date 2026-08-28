using System;

namespace StealthEyeGame.Levels
{
    /// <summary>
    /// Berechnet alle schwierigkeitsabhängigen Parameter für ein gegebenes Levellevel.
    /// Alle Werte sind gedeckelt ("Cap"), damit das Spiel mit steigendem Level zwar
    /// härter, aber weiterhin fair und lösbar bleibt.
    /// </summary>
    public static class DifficultySettings
    {
        public struct Params
        {
            public int EyeCount;
            public float WallDensity;       // 0..1, Anteil zufällig gesetzter innerer Wandzellen
            public float VisionRange;       // Pixel
            public float VisionHalfAngle;   // Radiant
            public float DamagePerSecond;
            public float SlowMultiplier;    // z.B. 0.5 = halbe Geschwindigkeit im Sichtkegel
        }

        public static Params ForLevel(int level)
        {
            level = Math.Max(1, level);

            int eyeCount = Math.Min(1 + (level - 1) / 1, 7);          // 1,2,3,4,5,6,7 (Cap 7)
            if (level == 1) eyeCount = 1;
            else if (level == 2) eyeCount = 2;
            else eyeCount = Math.Min(3 + (level - 3) / 2, 7);

            float wallDensity = Math.Min(0.08f + level * 0.018f, 0.30f);
            float visionRange = Math.Min(130f + level * 14f, 300f);
            float visionHalfAngleDeg = Math.Min(20f + level * 2.2f, 42f);
            float damagePerSecond = Math.Min(12f + level * 1.6f, 38f);
            float slowMultiplier = Math.Max(0.55f - level * 0.02f, 0.25f);

            return new Params
            {
                EyeCount = eyeCount,
                WallDensity = wallDensity,
                VisionRange = visionRange,
                VisionHalfAngle = visionHalfAngleDeg * MathF.PI / 180f,
                DamagePerSecond = damagePerSecond,
                SlowMultiplier = slowMultiplier
            };
        }
    }
}
