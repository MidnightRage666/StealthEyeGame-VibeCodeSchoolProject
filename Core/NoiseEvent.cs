using System.Numerics;

namespace StealthEyeGame.Core
{
    /// <summary>
    /// Beschreibt eine einmalige Geräuschquelle in der Spielwelt (aktuell: Explosionen,
    /// zukünftig z. B. ein "Noise Maker"-Item). Wird von <see cref="GameManager.EmitNoise"/>
    /// verwendet, um alle Augen im Radius zu benachrichtigen - so bleibt die Logik,
    /// "wer hört was", an einer zentralen Stelle und ist leicht um weitere Geräuschquellen
    /// erweiterbar, ohne dass jede Quelle selbst wissen muss, wie Augen reagieren.
    /// </summary>
    public readonly struct NoiseEvent
    {
        public Vector2 Position { get; }
        public float Radius { get; }

        public NoiseEvent(Vector2 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }
}
