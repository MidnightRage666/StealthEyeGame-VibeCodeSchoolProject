using System;
using System.Numerics;

namespace StealthEyeGame.Core
{
    /// <summary>
    /// Kleine mathematische Hilfsfunktionen, die im Standard-.NET fehlen
    /// (Winkel-Normalisierung, Winkel-Interpolation mit Drehgeschwindigkeit, Clamp).
    /// </summary>
    public static class MathUtil
    {
        /// <summary>Normalisiert einen Winkel (Radiant) auf den Bereich (-PI, PI].</summary>
        public static float NormalizeAngle(float angle)
        {
            while (angle > MathF.PI) angle -= 2f * MathF.PI;
            while (angle <= -MathF.PI) angle += 2f * MathF.PI;
            return angle;
        }

        /// <summary>Kürzeste Winkeldifferenz von a nach b, im Bereich (-PI, PI].</summary>
        public static float AngleDifference(float a, float b) => NormalizeAngle(b - a);

        /// <summary>
        /// Dreht 'current' in Richtung 'target' mit maximal 'maxDelta' Radiant,
        /// immer auf dem kürzesten Weg. Ergibt flüssige, organische Pupillenbewegung.
        /// </summary>
        public static float RotateTowards(float current, float target, float maxDelta)
        {
            float diff = AngleDifference(current, target);
            if (MathF.Abs(diff) <= maxDelta) return NormalizeAngle(target);
            return NormalizeAngle(current + MathF.Sign(diff) * maxDelta);
        }

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        private static float Clamp01(float t) => t < 0 ? 0 : (t > 1 ? 1 : t);

        public static float DistanceSquared(Vector2 a, Vector2 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
