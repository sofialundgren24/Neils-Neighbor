using System.Linq;
using NG.Core;
using NG.Data;
using NG.State;

namespace NG.Game
{
    /// <summary>
    /// Navet i spellogiken. Håller spelarens data (PlayerState), innehållet (ContentDatabase + GameConfig)
    /// och en tjänst per regelområde. UI och vyer anropar tjänsterna och lyssnar på händelser via EventBus;
    /// de ändrar aldrig PlayerState direkt.
    ///
    ///   Wallet       mynt och diamanter
    ///   Inventory    varor i lagret
    ///   Progression  XP, nivåer, upplåsningar
    ///   World        placera, flytta, sälja, bygga, låsa upp områden
    ///   Renovation   husens faser och den permanenta färgen
    ///   Growth       växter, tillväxt och skörd
    ///   Production   affär och trädgårdsbutik: köer av recept
    ///   Orders       beställningstavlan
    ///
    /// Ren C# utan MonoBehaviour, så logiken kan testas utan scen. GameBootstrap skapar den och anropar Tick().
    /// </summary>
    public sealed class GameSession
    {
        public ContentDatabase Content { get; }
        public GameConfig Config { get; }
        public PlayerState State { get; }
        public LevelTable Levels => Config.levelTable;

        public WalletService Wallet { get; }
        public InventoryService Inventory { get; }
        public ProgressionService Progression { get; }
        public WorldService World { get; }
        public RenovationService Renovation { get; }
        public GrowthService Growth { get; }
        public ProductionService Production { get; }
        public OrderService Orders { get; }

        public GameSession(ContentDatabase content, GameConfig config, PlayerState state)
        {
            Content = content;
            Config = config;
            State = state;

            Wallet = new WalletService(this);
            Inventory = new InventoryService(this);
            Progression = new ProgressionService(this);
            World = new WorldService(this);
            Renovation = new RenovationService(this);
            Growth = new GrowthService(this);
            Production = new ProductionService(this);
            Orders = new OrderService(this);
        }

        /// <summary>Första start: plånbok, startområde och den nedgångna gatan från GameConfig.</summary>
        public void StartNewGameIfNeeded()
        {
            if (State.initialized)
            {
                World.RebuildGrid();
                return;
            }

            State.wallet[CurrencyType.Coins] = Config.startCoins;
            State.wallet[CurrencyType.Gems] = Config.startGems;
            foreach (var e in Config.expansions.Where(e => e.requiredLevel <= 1 && e.coinCost == 0))
                State.unlockedExpansions.Add(e.id);

            World.RebuildGrid();
            foreach (var p in Config.startLayout)
                World.AddFromLayout(p);

            State.initialized = true;
        }

        /// <summary>Anropas några gånger per sekund. Alla timers är absoluta tider, så det spelar ingen roll hur ofta.</summary>
        public void Tick()
        {
            World.Tick();
            Renovation.Tick();
            Growth.Tick();
            Production.Tick();
            Orders.Tick();
        }

        public static void Message(string text) => EventBus.Publish(new GameMessage(text));
    }

    /// <summary>Svar från en handling: lyckades den, och annars varför inte (visas för spelaren).</summary>
    public readonly struct Result
    {
        public readonly bool Ok;
        public readonly string Reason;

        Result(bool ok, string reason) { Ok = ok; Reason = reason; }

        public static Result Success => new Result(true, null);
        public static Result Fail(string reason) => new Result(false, reason);
    }

    /// <summary>Hjälpare för TimerState (absolut start + längd).</summary>
    public static class Timers
    {
        public static TimerState StartNow(int durationSec) => new TimerState(TimeService.UnixNow, durationSec);

        public static bool IsDone(TimerState t) => t == null || TimeService.UnixNow >= t.startUnix + t.durationSec;

        public static double Remaining(TimerState t) => t == null ? 0 : TimeService.Remaining(t.startUnix, t.durationSec);

        public static float Progress(TimerState t) => t == null ? 1f : TimeService.Progress(t.startUnix, t.durationSec);

        /// <summary>Gör timern klar direkt (snabbspolning).</summary>
        public static void Finish(TimerState t)
        {
            if (t != null) t.startUnix = TimeService.UnixNow - t.durationSec;
        }
    }
}
