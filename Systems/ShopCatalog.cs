using System.Collections.Generic;

namespace StealthEyeGame.Systems
{
    /// <summary>
    /// Die aktuell im Shop verfügbaren Gegenstände. Neue Items werden hier als
    /// zusätzlicher <see cref="ShopItem"/>-Eintrag ergänzt - der Shop selbst
    /// (Rendering, Klick-Handling, GameManager.BuyItem) bleibt dabei unverändert.
    /// </summary>
    public static class ShopCatalog
    {
        public static readonly IReadOnlyList<ShopItem> Items = new List<ShopItem>
        {
            new ShopItem(
                itemType: ShopItemType.Dynamite,
                name: "Dynamit",
                description: "Platzierbare Sprengladung mit Zündtimer.",
                getPrice: _ => 25,
                canPurchase: _ => true,
                apply: p => p.DynamiteOwned += 1,
                getOwnedLabel: p => $"Besitz: {p.DynamiteOwned}"
            ),
            new ShopItem(
                itemType: ShopItemType.MaxHpUpgrade,
                name: "Mehr HP",
                description: "Erhöht die maximale Lebensenergie dauerhaft um 20.",
                getPrice: p => 100 + (int)(p.BonusMaxHP / 20) * 40, // wird mit jedem Kauf teurer
                canPurchase: _ => true,
                apply: p => p.BonusMaxHP += 20f,
                getOwnedLabel: p => $"+{(int)p.BonusMaxHP} HP"
            ),
            new ShopItem(
                itemType: ShopItemType.StrongerDynamite,
                name: "Stärkeres Dynamit",
                description: "Explosionsradius und -schaden um 50% erhöht.",
                getPrice: _ => 150,
                canPurchase: p => !p.HasStrongerDynamite,
                apply: p => p.HasStrongerDynamite = true,
                getOwnedLabel: p => p.HasStrongerDynamite ? "Gekauft" : ""
            ),
            new ShopItem(
                itemType: ShopItemType.Medkit,
                name: "Medkit",
                description: "Heilt den Spieler um 25 HP.",
                getPrice: _ => 75,
                canPurchase: _ => true,
                apply: p => p.MedkitsOwned += 1,
                getOwnedLabel: p => $"Besitz: {p.MedkitsOwned}"
            )
        };
    }
}
