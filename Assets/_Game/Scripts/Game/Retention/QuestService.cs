using System;
using System.Collections.Generic;
using System.Linq;
using NG.Core;
using NG.Data;
using NG.State;

namespace NG.Game
{
    /// <summary>
    /// Grannarnas uppdrag. Uppdrag blir aktiva när kravet (förra uppdraget i kedjan + nivå) är uppfyllt,
    /// max GameConfig.maxActiveQuests åt gången. Mål läses antingen av från spelets tillstånd
    /// (renovering, ägda objekt, nivå, lager) eller räknas med händelser (skörd, produktion, leveranser).
    /// De första uppdragen är tutorialen: deras focus säger vad UI:t ska peka på.
    /// Uppdragen skrivs i Assets/_Game/Data/quests.csv.
    /// </summary>
    public sealed class QuestService : IDisposable
    {
        readonly GameSession _s;
        readonly Dictionary<string, QuestDefinition> _defs = new Dictionary<string, QuestDefinition>();

        public QuestService(GameSession s)
        {
            _s = s;
            foreach (var q in s.Content.AllOf<QuestDefinition>()) _defs[q.Id] = q;
            EventBus.Subscribe<ItemsHarvested>(OnHarvested);
            EventBus.Subscribe<ItemsProduced>(OnProduced);
            EventBus.Subscribe<OrderDelivered>(OnDelivered);
            EventBus.Subscribe<ClutterCleared>(OnCleared);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<ItemsHarvested>(OnHarvested);
            EventBus.Unsubscribe<ItemsProduced>(OnProduced);
            EventBus.Unsubscribe<OrderDelivered>(OnDelivered);
            EventBus.Unsubscribe<ClutterCleared>(OnCleared);
        }

        public IReadOnlyList<QuestProgress> Active => _s.State.quests;

        public QuestDefinition Def(QuestProgress q) => q != null && _defs.TryGetValue(q.questId, out var d) ? d : null;

        public NeighborDefinition Neighbor(QuestProgress q) => Def(q)?.neighbor;

        /// <summary>Uppdraget vars introduktion inte har visats än (för dialogrutan).</summary>
        public QuestProgress NextUnseenIntro() =>
            Active.FirstOrDefault(q => !q.introSeen && !string.IsNullOrEmpty(Def(q)?.intro));

        public void MarkIntroSeen(QuestProgress q)
        {
            q.introSeen = true;
            EventBus.Publish(new QuestsChanged());
        }

        /// <summary>Första aktiva uppdraget med ett tutorialmål att peka på.</summary>
        public QuestProgress CurrentFocus() =>
            Active.FirstOrDefault(q => !IsComplete(q) && !string.IsNullOrEmpty(Def(q)?.focus));

        // ---------- Framsteg ----------

        public int Progress(QuestProgress q, int index)
        {
            var def = Def(q);
            if (def == null || index < 0 || index >= def.objectives.Length) return 0;
            var o = def.objectives[index];
            int value = o.type switch
            {
                ObjectiveType.Renovate => RenovatedPhases(o.targetId),
                ObjectiveType.Own => _s.World.Objects.Count(x => x.definitionId == o.targetId && x.build == null),
                ObjectiveType.ReachLevel => _s.Progression.Level,
                ObjectiveType.Collect => _s.Inventory.Count(o.targetId),
                _ => index < q.counters.Count ? q.counters[index] : 0
            };
            return Math.Min(value, o.amount);
        }

        public bool IsComplete(QuestProgress q)
        {
            var def = Def(q);
            if (def == null) return false;
            for (int i = 0; i < def.objectives.Length; i++)
                if (Progress(q, i) < def.objectives[i].amount) return false;
            return true;
        }

        int RenovatedPhases(string houseId)
        {
            int best = 0;
            foreach (var o in _s.World.Objects)
                if (o.definitionId == houseId && o.renovation != null)
                    best = Math.Max(best, o.renovation.complete ? _s.Renovation.PhaseCount(o) : o.renovation.phase);
            return best;
        }

        /// <summary>Text för ett mål, t.ex. "Skörda Vete 3/6".</summary>
        public string Describe(QuestProgress q, int index)
        {
            var o = Def(q).objectives[index];
            string target = NameOf(o.targetId);
            string text = o.type switch
            {
                ObjectiveType.Renovate => $"Renovera {target} (fas {o.amount})",
                ObjectiveType.Own => $"Bygg {target}",
                ObjectiveType.Harvest => o.targetId == "*" ? "Skörda" : $"Skörda {target}",
                ObjectiveType.Produce => $"Tillverka {target}",
                ObjectiveType.Deliver => "Leverera beställningar",
                ObjectiveType.ReachLevel => $"Nå nivå {o.amount}",
                ObjectiveType.Collect => $"Samla {target}",
                ObjectiveType.Clear => o.targetId == "*" ? "Städa tomterna" : ClearVerb(o.targetId) + " " + target.ToLowerInvariant(),
                _ => o.type.ToString()
            };
            return o.type == ObjectiveType.ReachLevel || o.type == ObjectiveType.Renovate
                ? $"{text}{(Progress(q, index) >= o.amount ? "  (klart)" : "")}"
                : $"{text}  {Progress(q, index)}/{o.amount}";
        }

        string NameOf(string id)
        {
            if (string.IsNullOrEmpty(id) || id == "*") return "";
            if (_s.Content.TryGet<ResourceDefinition>(id, out var res)) return res.DisplayName;
            if (_s.Content.TryGet<PlaceableDefinition>(id, out var def)) return def.DisplayName;
            return id;
        }

        // ---------- Belöning ----------

        public Result Claim(QuestProgress q)
        {
            if (!IsComplete(q)) return Result.Fail("Uppdraget är inte klart än.");
            var def = Def(q);
            _s.State.quests.Remove(q);
            _s.State.completedQuests.Add(q.questId);
            Analytics.Event("quest_complete", "quest", q.questId, "level", _s.Progression.Level);
            if (def.rewardCoins > 0) _s.Wallet.Add(CurrencyType.Coins, def.rewardCoins);
            if (def.rewardGems > 0) _s.Wallet.Add(CurrencyType.Gems, def.rewardGems);
            _s.Progression.AddXp(def.rewardXp);
            EventBus.Publish(new QuestsChanged());
            if (!string.IsNullOrEmpty(def.outro))
                GameSession.Message($"{def.neighbor?.DisplayName ?? "Grannen"}: {_s.Personalize(def.outro)}");
            Activate();
            return Result.Success;
        }

        // ---------- Uppdatering ----------

        public void Tick() => Activate();

        /// <summary>Gör nya uppdrag aktiva när det finns plats och kraven är uppfyllda.</summary>
        void Activate()
        {
            if (Active.Count >= _s.Config.maxActiveQuests) return;
            var done = new HashSet<string>(_s.State.completedQuests);
            var active = new HashSet<string>(Active.Select(q => q.questId));
            bool changed = false;
            foreach (var def in _defs.Values.OrderBy(d => d.order).ThenBy(d => d.Id))
            {
                if (Active.Count >= _s.Config.maxActiveQuests) break;
                if (done.Contains(def.Id) || active.Contains(def.Id)) continue;
                if (def.unlockLevel > _s.Progression.Level) continue;
                if (!string.IsNullOrEmpty(def.requiresQuest) && !done.Contains(def.requiresQuest)) continue;
                var q = new QuestProgress { questId = def.Id };
                for (int i = 0; i < def.objectives.Length; i++) q.counters.Add(0);
                _s.State.quests.Add(q);
                active.Add(def.Id);
                changed = true;
            }
            if (changed) EventBus.Publish(new QuestsChanged());
        }

        void Count(ObjectiveType type, string resourceId, int amount)
        {
            foreach (var q in Active)
            {
                var def = Def(q);
                if (def == null) continue;
                while (q.counters.Count < def.objectives.Length) q.counters.Add(0);
                for (int i = 0; i < def.objectives.Length; i++)
                {
                    var o = def.objectives[i];
                    if (o.type != type) continue;
                    if (type != ObjectiveType.Deliver && o.targetId != "*" && o.targetId != resourceId) continue;
                    q.counters[i] = Math.Min(o.amount, q.counters[i] + amount);
                }
            }
            EventBus.Publish(new QuestsChanged());
        }

        void OnHarvested(ItemsHarvested e) => Count(ObjectiveType.Harvest, e.ResourceId, e.Amount);
        void OnProduced(ItemsProduced e) => Count(ObjectiveType.Produce, e.ResourceId, e.Amount);
        void OnDelivered(OrderDelivered e) => Count(ObjectiveType.Deliver, null, 1);
        void OnCleared(ClutterCleared e) => Count(ObjectiveType.Clear, e.DefinitionId, 1);

        string ClearVerb(string defId) =>
            _s.Content.TryGet<ClutterDefinition>(defId, out var c) && c.action == ClutterAction.Water ? "Vattna" : "Rensa";
    }
}
