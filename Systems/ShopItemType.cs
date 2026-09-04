namespace StealthEyeGame.Systems
{
    /// <summary>
    /// Alle Item-Typen, die das Shop-System kennt. Nicht jeder Typ hat bereits einen
    /// Eintrag im aktuellen <see cref="ShopCatalog"/> - die Enum-Werte für zukünftige
    /// Items sind bewusst schon vorbereitet, damit die Architektur ohne Bruch erweitert
    /// werden kann (siehe Aufgabenstellung: Medkit, Unsichtbarkeit, Noise Maker, EMP, Smoke Bomb).
    /// </summary>
    public enum ShopItemType
    {
        Dynamite,
        MaxHpUpgrade,
        StrongerDynamite,
        Medkit,

        // Für die Zukunft vorbereitet, aktuell noch nicht im Katalog gelistet:
        StrongerDynamiteMk2,
        Invisibility,
        NoiseMaker,
        Emp,
        SmokeBomb
    }
}
