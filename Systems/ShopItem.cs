using System;

namespace StealthEyeGame.Systems
{
    /// <summary>
    /// Beschreibt einen kaufbaren Shop-Gegenstand generisch, damit neue Items
    /// später hinzugefügt werden können, ohne den Shop selbst anzufassen -
    /// es muss nur ein neuer Eintrag im <see cref="ShopCatalog"/> ergänzt werden.
    /// </summary>
    public class ShopItem
    {
        public ShopItemType ItemType { get; }
        public string Name { get; }
        public string Description { get; }

        private readonly Func<PersistentProgress, int> _getPrice;
        private readonly Func<PersistentProgress, bool> _canPurchase;
        private readonly Action<PersistentProgress> _apply;
        private readonly Func<PersistentProgress, string> _getOwnedLabel;

        public ShopItem(ShopItemType itemType, string name, string description,
                         Func<PersistentProgress, int> getPrice,
                         Func<PersistentProgress, bool> canPurchase,
                         Action<PersistentProgress> apply,
                         Func<PersistentProgress, string> getOwnedLabel)
        {
            ItemType = itemType;
            Name = name;
            Description = description;
            _getPrice = getPrice;
            _canPurchase = canPurchase;
            _apply = apply;
            _getOwnedLabel = getOwnedLabel;
        }

        public int GetPrice(PersistentProgress progress) => _getPrice(progress);

        public bool CanPurchase(PersistentProgress progress) =>
            _canPurchase(progress) && progress.Coins >= GetPrice(progress);

        public string GetOwnedLabel(PersistentProgress progress) => _getOwnedLabel(progress);

        /// <summary>Zieht die Kosten ab und wendet den Effekt an. Ruft nur auf, wenn CanPurchase true war.</summary>
        public void Purchase(PersistentProgress progress)
        {
            int price = GetPrice(progress);
            if (!progress.TrySpend(price)) return;
            _apply(progress);
        }
    }
}
