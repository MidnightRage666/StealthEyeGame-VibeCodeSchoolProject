using System.Numerics;

namespace StealthEyeGame.Entities
{
    /// <summary>
    /// Ein vom Spieler platziertes Dynamitstück. Zündet nach Ablauf der Zündschnur
    /// automatisch (siehe <see cref="ShouldExplode"/>) und löst dann in der
    /// Spiellogik (GameManager) eine <see cref="Explosion"/> aus.
    /// </summary>
    public class Dynamite
    {
        public Vector2 Position { get; }
        public float FuseTimeRemaining { get; private set; }
        public float FuseTimeTotal { get; }
        public bool HasExploded { get; private set; }

        public Dynamite(Vector2 position, float fuseSeconds)
        {
            Position = position;
            FuseTimeTotal = fuseSeconds;
            FuseTimeRemaining = fuseSeconds;
        }

        public void Update(float dt)
        {
            if (HasExploded) return;
            FuseTimeRemaining -= dt;
        }

        public bool ShouldExplode => !HasExploded && FuseTimeRemaining <= 0f;

        public void MarkExploded() => HasExploded = true;

        /// <summary>0 = gerade gezündet, 1 = kurz vor der Explosion. Für das Blink-Rendering.</summary>
        public float FuseProgress => FuseTimeTotal <= 0f ? 1f : 1f - (FuseTimeRemaining / FuseTimeTotal);
    }
}
