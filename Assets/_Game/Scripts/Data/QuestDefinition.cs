using System;
using UnityEngine;

namespace NG.Data
{
    /// <summary>Vad ett uppdragsmål räknar.</summary>
    public enum ObjectiveType
    {
        /// <summary>Huset (targetId) har minst amount klara renoveringsfaser.</summary>
        Renovate,
        /// <summary>Minst amount färdigbyggda objekt av sorten targetId står på kartan.</summary>
        Own,
        /// <summary>Skörda amount av varan targetId ("*" = vad som helst) efter att uppdraget startat.</summary>
        Harvest,
        /// <summary>Hämta amount av varan targetId från en produktionsbyggnad efter att uppdraget startat.</summary>
        Produce,
        /// <summary>Leverera amount beställningar efter att uppdraget startat.</summary>
        Deliver,
        /// <summary>Nå nivå amount.</summary>
        ReachLevel,
        /// <summary>Ha minst amount av varan targetId i lagret.</summary>
        Collect
    }

    [Serializable]
    public class QuestObjective
    {
        public ObjectiveType type;
        public string targetId;
        [Min(1)] public int amount = 1;

        /// <summary>Mål som räknas med händelser (inte läses av från spelets tillstånd).</summary>
        public bool IsCounted => type == ObjectiveType.Harvest || type == ObjectiveType.Produce || type == ObjectiveType.Deliver;
    }

    /// <summary>
    /// Ett uppdrag från en granne. Uppdrag bildar kedjor via requiresQuest; de första fungerar som tutorial
    /// (focus pekar ut vad spelaren ska trycka på). Skapas från Assets/_Game/Data/quests.csv.
    /// </summary>
    [CreateAssetMenu(menuName = "NG/Quest", fileName = "Q_New")]
    public class QuestDefinition : DefinitionBase
    {
        public NeighborDefinition neighbor;
        [Tooltip("Sortering när flera uppdrag blir tillgängliga samtidigt.")]
        public int order;
        [Min(1)] public int unlockLevel = 1;
        [Tooltip("Id på uppdraget som måste vara klart först. Tomt = inget krav.")]
        public string requiresQuest;

        [TextArea] public string intro;
        [TextArea] public string outro;

        public QuestObjective[] objectives = Array.Empty<QuestObjective>();

        [Min(0)] public int rewardCoins;
        [Min(0)] public int rewardGems;
        [Min(0)] public int rewardXp;

        [Tooltip("Tutorial: definitions-id att peka på (t.ex. house_cottage), eller ui:build / ui:orders / ui:storage.")]
        public string focus;
    }
}
