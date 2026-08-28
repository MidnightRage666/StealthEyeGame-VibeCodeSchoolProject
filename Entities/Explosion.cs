using System.Numerics;

namespace StealthEyeGame.Entities
{
    /// <summary>
    /// Rein visuelle/zeitliche Repräsentation einer Explosion. Die eigentlichen
    /// Auswirkungen (Wände zerstören, Augen schädigen, Geräusch auslösen)
    /// werden vom GameManager einmalig bei Erzeugung angewendet - diese Klasse
    /// hält nur noch fest, wie lange die Explosion sichtbar bleibt.
    /// </summary>
    public class Explosion
    {
        public Vector2 Position { get; }
        public float Radius { get; }
        public float TimeRemaining { get; private set; }
        public float TotalDuration { get; }

        public Explosion(Vector2 position, float radius, float duration)
        {
            Position = position;
            Radius = radius;
            TotalDuration = duration;
            TimeRemaining = duration;
        }

        public void Update(float dt) => TimeRemaining -= dt;

        public bool IsFinished => TimeRemaining <= 0f;

        /// <summary>0 = gerade entstanden, 1 = gleich verschwunden. Für Fade-Out beim Rendern.</summary>
        public float Progress => TotalDuration <= 0f ? 1f : 1f - (TimeRemaining / TotalDuration);
    }
}
