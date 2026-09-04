namespace StealthEyeGame.Levels
{
    /// <summary>Art einer Rasterzelle im Wandraster eines Levels.</summary>
    public enum WallType
    {
        /// <summary>Keine Wand - begehbar, blockiert keine Sicht.</summary>
        Empty,

        /// <summary>Normale, unzerstörbare Wand.</summary>
        Solid,
    }
}
