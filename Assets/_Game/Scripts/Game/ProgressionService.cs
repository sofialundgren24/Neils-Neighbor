using System.Collections.Generic;
using System.Linq;
using NG.Core;
using NG.Data;

namespace NG.Game
{
    /// <summary>
    /// XP och nivåer. Nivåtabellen (levels.csv -> LevelTable) styr XP-krav och belöningar;
    /// upplåsningar styrs av unlockLevel på varje definition, så nya nivåer kräver ingen kod.
    /// </summary>
    public sealed class ProgressionService
    {
        readonly GameSession _s;
        public ProgressionService(GameSession s) { _s = s; }

        public int Level => _s.State.level;
        public long Xp => _s.State.xp;

        public int XpToNext => _s.Levels != null ? _s.Levels.XpToNext(Level) : XpFormula.XpToNext(Level);

        public float LevelProgress => XpToNext <= 0 ? 1f : (float)Xp / XpToNext;

        public bool IsUnlocked(PlaceableDefinition def) => def != null && def.unlockLevel <= Level;
        public bool IsUnlocked(RecipeDefinition def) => def != null && def.unlockLevel <= Level;

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            _s.State.xp += amount;
            while (_s.State.xp >= XpToNext)
            {
                _s.State.xp -= XpToNext;
                _s.State.level++;
                var row = _s.Levels != null ? _s.Levels.Get(_s.State.level) : null;
                if (row != null)
                {
                    _s.Wallet.Add(CurrencyType.Coins, row.coinReward);
                    _s.Wallet.Add(CurrencyType.Gems, row.gemReward);
                }
                EventBus.Publish(new LevelUp(_s.State.level));
                Analytics.Event("level_up", "level", _s.State.level);
            }
            EventBus.Publish(new XpChanged(_s.State.xp, _s.State.level));
        }

        /// <summary>Belöning för en nivå, för nivå-upp-rutan.</summary>
        public LevelRow RewardsFor(int level) => _s.Levels != null ? _s.Levels.Get(level) : null;

        /// <summary>Allt som blir tillgängligt på en viss nivå (byggbart, recept, områden), för nivå-upp-rutan.</summary>
        public IEnumerable<string> UnlockNamesAt(int level)
        {
            foreach (var p in _s.Content.AllOf<PlaceableDefinition>().Where(p => p.unlockLevel == level).OrderBy(p => p.category))
                yield return p is RenovatableDefinition ? $"Renovera: {p.DisplayName}" : p.DisplayName;
            foreach (var r in _s.Content.AllOf<RecipeDefinition>().Where(r => r.unlockLevel == level))
                yield return $"Recept: {r.DisplayName}";
            foreach (var f in _s.Content.AllOf<FacadeDefinition>().Where(f => f.unlockLevel == level))
                yield return $"{f.house?.DisplayName}: {f.DisplayName}";
            foreach (var e in _s.Config.expansions.Where(e => e.requiredLevel == level && e.requiredLevel > 0))
                yield return $"Nytt område: {e.name}";
            if (level == _s.Config.ordersUnlockLevel) yield return "Grannskapstavlan";
        }
    }
}
