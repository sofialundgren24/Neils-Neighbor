using System;
using System.Collections.Generic;
using System.Linq;
using NG.Core;
using NG.Data;
using NG.Game;
using NG.State;
using NG.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace NG.UI
{
    /// <summary>
    /// Spelets UI i UI Toolkit. Ersätter platshållar-HUD:en (GameHud).
    ///
    /// Uppbyggnad:
    ///  - Utseendet står i Assets/_Game/UI/NG.uss. Koden sätter bara klassnamn ("panel", "btn-primary" ...).
    ///  - Delar byggs om när spelets tillstånd ändras (händelser från EventBus), inte varje bildruta.
    ///  - Det som ändras hela tiden (timers, mätare, om en knapp är klickbar) uppdateras varje bildruta
    ///    via små "bindningar" (Binder), så att knappar inte byts ut medan man trycker på dem.
    ///  - Ingen spellogik här: allt går via GameSession.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameUI : MonoBehaviour
    {
        enum Window { None, Shop, Storage, Orders, Debug }

        static readonly (string label, PlaceableCategory[] cats)[] ShopTabs =
        {
            ("Kvarter", new[] { PlaceableCategory.Production, PlaceableCategory.Community, PlaceableCategory.House }),
            ("Blommor", new[] { PlaceableCategory.Plant }),
            ("Träd & buskar", new[] { PlaceableCategory.Tree, PlaceableCategory.Hedge }),
            ("Dekor", new[] { PlaceableCategory.Decor, PlaceableCategory.Water }),
            ("Staket", new[] { PlaceableCategory.Fence }),
            ("Mark & sten", new[] { PlaceableCategory.Path })
        };

        static readonly Color[] PaintColors =
        {
            new Color(0.56f, 0.83f, 1f), new Color(1f, 0.71f, 0.64f), new Color(1f, 0.89f, 0.48f),
            new Color(0.64f, 0.88f, 0.6f), new Color(0.78f, 0.62f, 1f), new Color(1f, 1f, 1f)
        };

        [SerializeField] StyleSheet styleSheet;

        GameSession _s;
        WorldInteraction _ia;
        WorldView _world;
        GameBootstrap _boot;
        Camera _cam;
        VisualElement _root;

        // Lager och delar
        VisualElement _questPanel, _selectionPanel, _placingBar, _windowLayer, _modalLayer, _toastLayer, _bubbleLayer;
        Button _buildButton, _ordersButton, _storageButton;
        Label _pointer;

        // Bindningar per del
        readonly Binder _topBinder = new Binder();
        readonly Binder _questBinder = new Binder();
        readonly Binder _selectionBinder = new Binder();
        readonly Binder _placingBinder = new Binder();
        readonly Binder _windowBinder = new Binder();
        readonly Binder _modalBinder = new Binder();

        // Tillstånd i UI:t
        Window _window;
        int _shopTab;
        bool _showDaily;
        readonly Queue<int> _levelUps = new Queue<int>();
        bool _dirtyQuests, _dirtySelection, _dirtyWindow, _dirtyModal = true;
        string _shownSelectionId;
        MapExpansion _shownExpansion;
        WorldInteraction.Mode _shownMode;
        string _shownModalKey;
        readonly List<(Label label, float until)> _toasts = new List<(Label, float)>();
        readonly List<BubbleSlot> _bubbles = new List<BubbleSlot>();
        readonly List<Action> _unsubscribe = new List<Action>();

        class BubbleSlot
        {
            public Button button;
            public Action action;
        }

        /// <summary>Uppdaterare som körs varje bildruta för en del av UI:t.</summary>
        sealed class Binder
        {
            readonly List<Action> _actions = new List<Action>();
            public void Clear() => _actions.Clear();
            public void Add(Action a) => _actions.Add(a);
            public void Run()
            {
                foreach (var a in _actions) a();
            }
        }

        // ---------- Start ----------

        public void Bind(GameSession session, WorldInteraction interaction, WorldView world)
        {
            _s = session;
            _ia = interaction;
            _world = world;
            _boot = GetComponent<GameBootstrap>();
            _cam = Camera.main;

            var doc = GetComponent<UIDocument>();
            _root = doc.rootVisualElement;
            _root.Clear();
            if (styleSheet != null) _root.styleSheets.Add(styleSheet);
            _root.AddToClassList("root");
            _root.pickingMode = PickingMode.Ignore;

            _bubbleLayer = Layer("bubbles");
            BuildTopBar();
            _questPanel = Panel("quest-panel");
            _selectionPanel = Panel("selection-panel");
            _buildButton = Btn("Bygg", () => Toggle(Window.Shop), "btn", "btn-gold", "btn-big", "build-button");
            _root.Add(_buildButton);
            _placingBar = Panel("placing-bar");
            _windowLayer = Layer("windows");
            _toastLayer = Layer("toasts");
            _toastLayer.AddToClassList("toasts");
            _pointer = new Label("Här!") { pickingMode = PickingMode.Ignore };
            _pointer.AddToClassList("pointer");
            _root.Add(_pointer);
            _modalLayer = Layer("modals");

            InputGate.UiHitTest = IsOverUi;

            Sub<QuestsChanged>(_ => _dirtyQuests = true);
            Sub<ObjectChanged>(e => { if (e.InstanceId == _ia.SelectedId) _dirtySelection = true; });
            Sub<ObjectRemoved>(_ => _dirtySelection = true);
            Sub<InventoryChanged>(_ => { _dirtyWindow = true; _dirtySelection = true; });
            Sub<OrdersChanged>(_ => _dirtyWindow = true);
            Sub<ExpansionUnlocked>(_ => _dirtySelection = true);
            Sub<LevelUp>(e => { _levelUps.Enqueue(e.NewLevel); _dirtyModal = true; _dirtyWindow = true; });
            Sub<GameMessage>(e => Toast(e.Text));

            _dirtyQuests = _dirtySelection = _dirtyWindow = _dirtyModal = true;
        }

        void Sub<T>(Action<T> handler) where T : struct
        {
            EventBus.Subscribe(handler);
            _unsubscribe.Add(() => EventBus.Unsubscribe(handler));
        }

        void OnDestroy()
        {
            foreach (var u in _unsubscribe) u();
            if (InputGate.UiHitTest == (Func<Vector2, bool>)IsOverUi) InputGate.UiHitTest = null;
        }

        // ---------- Bildruta ----------

        void Update()
        {
            if (_s == null) return;
            if (_cam == null) _cam = Camera.main;

            bool placing = _ia.CurrentMode == WorldInteraction.Mode.Placing;
            if (_ia.SelectedId != _shownSelectionId || _ia.SelectedExpansion != _shownExpansion || _ia.CurrentMode != _shownMode)
                _dirtySelection = true;

            if (_dirtyQuests) { _dirtyQuests = false; RebuildQuests(); }
            if (_dirtySelection) { _dirtySelection = false; RebuildSelection(); }
            if (_dirtyWindow) { _dirtyWindow = false; RebuildWindow(); }

            string modalKey = CurrentModalKey();
            if (_dirtyModal || modalKey != _shownModalKey) { _dirtyModal = false; RebuildModal(modalKey); }

            _questPanel.style.display = !placing && _window == Window.None && _s.Quests.Active.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _buildButton.style.display = placing ? DisplayStyle.None : DisplayStyle.Flex;
            _placingBar.style.display = placing ? DisplayStyle.Flex : DisplayStyle.None;
            InputGate.ModalOpen = modalKey != null;

            _topBinder.Run();
            _questBinder.Run();
            _selectionBinder.Run();
            _placingBinder.Run();
            _windowBinder.Run();
            _modalBinder.Run();

            UpdateBubbles();
            UpdatePointer();
            UpdateToasts();
        }

        bool IsOverUi(Vector2 screenPos)
        {
            if (_root?.panel == null) return false;
            var panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = _root.panel.Pick(panelPos);
            return picked != null && picked != _root;
        }

        // ---------- Toppfält ----------

        void BuildTopBar()
        {
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("topbar");
            _root.Add(bar);

            var profileChip = Chip(bar);
            var avatar = new Label();
            avatar.AddToClassList("avatar");
            profileChip.Add(avatar);
            var playerName = Text("", "chip-label");
            profileChip.Add(playerName);
            _topBinder.Add(() =>
            {
                avatar.text = _s.PlayerName.Substring(0, 1);
                avatar.EnableInClassList("avatar-sofia", _s.State.character == "sofia");
                playerName.text = _s.PlayerName;
            });

            var levelChip = Chip(bar);
            var badge = new Label();
            badge.AddToClassList("level-badge");
            levelChip.Add(badge);
            var xpCol = new VisualElement();
            var xpText = Text("", "small");
            xpCol.Add(xpText);
            var xpBar = ProgressBar(() => _s.Progression.LevelProgress, _topBinder, "bar-xp");
            xpCol.Add(xpBar);
            levelChip.Add(xpCol);
            _topBinder.Add(() =>
            {
                badge.text = _s.Progression.Level.ToString();
                xpText.text = $"{_s.Progression.Xp} / {_s.Progression.XpToNext} XP";
            });

            var coinChip = Chip(bar);
            coinChip.Add(Dot("coin-dot"));
            var coins = Text("", "chip-label");
            coinChip.Add(coins);
            var gemChip = Chip(bar);
            gemChip.Add(Dot("gem-dot"));
            var gems = Text("", "chip-label");
            gemChip.Add(gems);
            var charmChip = Chip(bar);
            charmChip.Add(Dot("charm-dot"));
            var charm = Text("", "chip-label");
            charmChip.Add(charm);
            _topBinder.Add(() =>
            {
                coins.text = _s.Wallet.Get(CurrencyType.Coins).ToString("N0");
                gems.text = _s.Wallet.Get(CurrencyType.Gems).ToString("N0");
                charm.text = $"Charm {_s.World.Charm}";
            });

            bar.Add(new VisualElement { pickingMode = PickingMode.Ignore, name = "spacer" });
            bar.Q("spacer").AddToClassList("spacer");

            var daily = Btn("Dagens gåva", () => { _showDaily = true; _dirtyModal = true; }, "btn", "btn-gold");
            bar.Add(daily);
            _topBinder.Add(() => daily.style.display = _s.Daily.CanClaim ? DisplayStyle.Flex : DisplayStyle.None);

            _storageButton = Btn("Lager", () => Toggle(Window.Storage), "btn");
            bar.Add(_storageButton);
            _ordersButton = Btn("Grannskapstavlan", () => Toggle(Window.Orders), "btn");
            bar.Add(_ordersButton);
            _topBinder.Add(() =>
            {
                _ordersButton.text = $"Grannskapstavlan ({_s.Orders.Orders.Count})";
                _ordersButton.style.display = _s.Orders.IsUnlocked ? DisplayStyle.Flex : DisplayStyle.None;
            });
            bar.Add(Btn("Debug", () => Toggle(Window.Debug), "btn"));
        }

        void Toggle(Window w)
        {
            _window = _window == w ? Window.None : w;
            _dirtyWindow = true;
        }

        // ---------- Uppdrag ----------

        void RebuildQuests()
        {
            _questBinder.Clear();
            _questPanel.Clear();
            var scroll = new ScrollView();
            _questPanel.Add(scroll);
            foreach (var q in _s.Quests.Active)
            {
                var def = _s.Quests.Def(q);
                if (def == null) continue;
                var card = Card();
                if (def.neighbor != null)
                {
                    var nb = Text(def.neighbor.DisplayName, "neighbor-name");
                    nb.style.color = Color.Lerp(def.neighbor.color, Color.black, 0.35f);
                    card.Add(nb);
                }
                card.Add(Text(def.DisplayName, "subtitle"));
                for (int i = 0; i < def.objectives.Length; i++)
                {
                    int index = i;
                    var line = Text("", "small");
                    card.Add(line);
                    _questBinder.Add(() => line.text = "• " + _s.Quests.Describe(q, index));
                }
                string reward = $"{def.rewardCoins} mynt" + (def.rewardGems > 0 ? $", {def.rewardGems} diamanter" : "") + $", {def.rewardXp} XP";
                var claim = Btn($"Hämta belöning\n{reward}", () => Report(_s.Quests.Claim(q)), "btn", "btn-primary");
                card.Add(claim);
                _questBinder.Add(() => claim.style.display = _s.Quests.IsComplete(q) ? DisplayStyle.Flex : DisplayStyle.None);
                scroll.Add(card);
            }
        }

        // ---------- Markerat objekt ----------

        void RebuildSelection()
        {
            _shownSelectionId = _ia.SelectedId;
            _shownExpansion = _ia.SelectedExpansion;
            _shownMode = _ia.CurrentMode;
            _selectionBinder.Clear();
            _selectionPanel.Clear();
            RebuildPlacingBar();

            var o = _s.World.Get(_ia.SelectedId);
            bool idle = _ia.CurrentMode == WorldInteraction.Mode.Idle;
            if (idle && o != null) BuildObjectPanel(o);
            else if (idle && _ia.SelectedExpansion != null) BuildExpansionPanel(_ia.SelectedExpansion);
            _selectionPanel.style.display = _selectionPanel.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void BuildObjectPanel(PlacedObjectState o)
        {
            var def = _s.World.Def(o);
            if (def == null) return;
            var p = _selectionPanel;
            p.Add(Header(def.DisplayName, () => _ia.ClearSelection()));
            var b = _selectionBinder;

            if (o.build != null)
            {
                var t = Text("", "text");
                p.Add(t);
                p.Add(ProgressBar(() => Timers.Progress(o.build), b));
                b.Add(() => t.text = $"Bygger ... {Fmt.Duration(Timers.Remaining(o.build))} kvar");
                p.Add(SpeedUp(() => o.build != null ? TimeService.SpeedUpGemCost(Timers.Remaining(o.build)) : 0,
                    () => _s.World.SpeedUpBuild(o), b));
            }
            else if (_s.Renovation.IsRenovatable(o)) BuildHousePanel(o);
            else if (def is PlantDefinition plant) BuildPlantPanel(o, plant);
            else if (def is ProductionDefinition prod) BuildProductionPanel(o, prod);
            else if (def is CommunityDefinition community) BuildCommunityPanel(o, community);
            else if (o.decayed) p.Add(Text("Grått och slitet. Renovera huset intill så får det tillbaka färgen.", "small"));
            else p.Add(Text($"Skönhet +{def.beautyScore}", "small"));

            var row = Row();
            row.Add(Btn("Flytta", () => _ia.BeginMove(o.instanceId), "btn", "grow"));
            if (_s.World.CanSell(o))
                row.Add(Btn($"Sälj (+{_s.World.SellValue(o)})", () => Report(_s.World.Sell(o)), "btn", "grow"));
            p.Add(row);
        }

        void BuildHousePanel(PlacedObjectState o)
        {
            var p = _selectionPanel;
            var b = _selectionBinder;
            var reno = _s.Renovation;
            var nb = _s.Content.AllOf<NeighborDefinition>().FirstOrDefault(n => n.house != null && n.house.Id == o.definitionId);
            if (nb != null) p.Add(Text($"Här bor {nb.DisplayName}", "small"));

            if (reno.IsComplete(o))
            {
                p.Add(Text("Färdigrenoverat!", "subtitle"));
                p.Add(Text("Fasad", "small"));
                var original = Btn("Original", () => Report(reno.SetFacade(o, null)), "btn");
                if (string.IsNullOrEmpty(o.renovation.facadeId)) original.AddToClassList("tab-active");
                p.Add(original);
                foreach (var f in reno.Facades(o).OrderBy(f => f.unlockLevel))
                {
                    var facade = f;
                    bool unlocked = reno.IsFacadeUnlocked(facade);
                    string facadeCost = facade.coinCost > 0 ? $" · {facade.coinCost} mynt" : "";
                    var row = Row();
                    var sw = new VisualElement();
                    sw.AddToClassList("swatch");
                    sw.style.backgroundColor = facade.color;
                    row.Add(sw);
                    var btn = Btn(unlocked ? facade.DisplayName + facadeCost : $"{facade.DisplayName} – nivå {facade.unlockLevel}",
                        () => Report(reno.SetFacade(o, facade)), "btn", "grow");
                    if (o.renovation.facadeId == facade.Id) btn.AddToClassList("tab-active");
                    btn.SetEnabled(unlocked);
                    row.Add(btn);
                    p.Add(row);
                }
                return;
            }

            p.Add(Text($"Renovering: fas {o.renovation.phase} av {reno.PhaseCount(o)}", "subtitle"));
            if (reno.IsInProgress(o))
            {
                var t = Text("", "text");
                p.Add(ProgressBar(() => o.renovation.active != null ? Timers.Progress(o.renovation.active) : 1f, b));
                p.Add(t);
                b.Add(() => t.text = o.renovation.active != null ? $"{Fmt.Duration(Timers.Remaining(o.renovation.active))} kvar" : "");
                p.Add(SpeedUp(() => reno.SpeedUpCost(o), () => reno.SpeedUp(o), b));
                return;
            }

            var next = reno.NextPhase(o);
            if (next == null) return;
            p.Add(Text($"Nästa: {Fmt.PhaseName(next.variantName)}", "text"));
            string cost = next.cost.amount > 0 ? $"{next.cost.amount} {WalletService.Name(next.cost.currency)}" : "Gratis";
            string mats = InventoryService.Format(next.materials);
            p.Add(Text($"{cost}{(mats.Length > 0 ? ", " + mats : "")} · {Fmt.Duration(next.durationSec)} · +{next.xpReward} XP", "small"));
            var go = Btn("Renovera", () => Report(reno.StartNext(o)), "btn", "btn-primary", "btn-big");
            p.Add(go);
            var why = Text("", "warn");
            p.Add(why);
            b.Add(() =>
            {
                bool can = reno.CanStartNext(o, out var reason);
                go.SetEnabled(can);
                why.text = can ? "" : reason;
            });
        }

        void BuildPlantPanel(PlacedObjectState o, PlantDefinition plant)
        {
            var p = _selectionPanel;
            var b = _selectionBinder;
            var g = _s.Growth;
            if (o.decayed)
            {
                p.Add(Text("Vissen. Renovera huset intill så kommer den tillbaka.", "small"));
                return;
            }
            var status = Text("", "text");
            p.Add(status);
            var bar = ProgressBar(() => o.growth != null ? Timers.Progress(o.growth) : 1f, b);
            p.Add(bar);
            var speed = SpeedUp(() => g.SpeedUpCost(o), () => g.SpeedUp(o), b);
            p.Add(speed);
            Button harvest = null;
            if (plant.IsHarvestable)
            {
                p.Add(Text($"Ger {plant.harvest.amount} {plant.harvest.resource.DisplayName}", "small"));
                harvest = Btn("Skörda", () => Report(g.Harvest(o)), "btn", "btn-primary", "btn-big");
                p.Add(harvest);
            }
            b.Add(() =>
            {
                bool growing = g.IsGrowing(o);
                status.text = growing ? $"Växer ... {Fmt.Duration(Timers.Remaining(o.growth))} kvar" : plant.IsHarvestable ? "Redo att skördas!" : "I full blom.";
                bar.style.display = growing ? DisplayStyle.Flex : DisplayStyle.None;
                speed.style.display = growing ? DisplayStyle.Flex : DisplayStyle.None;
                if (harvest != null) harvest.SetEnabled(g.CanHarvest(o));
            });
        }

        void BuildProductionPanel(PlacedObjectState o, ProductionDefinition def)
        {
            var p = _selectionPanel;
            var b = _selectionBinder;
            var prod = _s.Production;

            var collect = Btn("", () => Report(prod.Collect(o)), "btn", "btn-primary", "btn-big");
            p.Add(collect);
            b.Add(() =>
            {
                int n = prod.ReadyCount(o);
                collect.text = $"Hämta {n} färdiga";
                collect.style.display = n > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            });

            var queue = Text("", "small");
            p.Add(queue);
            p.Add(ProgressBar(() => prod.CurrentProgress(o), b));
            b.Add(() =>
            {
                var names = o.production.queue.Select(id => prod.Recipe(id)?.DisplayName ?? id).ToList();
                queue.text = names.Count == 0
                    ? $"Kö 0/{def.queueSize}: inget pågår"
                    : $"Kö {names.Count}/{def.queueSize}: {names[0]} ({Fmt.Duration(prod.CurrentRemaining(o))})" +
                      (names.Count > 1 ? ", sedan " + string.Join(", ", names.Skip(1)) : "");
            });
            var speed = SpeedUp(() => prod.SpeedUpCost(o), () => prod.SpeedUp(o), b);
            p.Add(speed);
            b.Add(() => speed.style.display = prod.IsWorking(o) ? DisplayStyle.Flex : DisplayStyle.None);

            p.Add(Text("Recept", "subtitle"));
            var scroll = new ScrollView();
            scroll.style.maxHeight = 260;
            p.Add(scroll);
            foreach (var r in prod.Recipes(o))
            {
                var recipe = r;
                bool unlocked = _s.Progression.IsUnlocked(recipe);
                string text = unlocked
                    ? $"{recipe.DisplayName}\n{InventoryService.Format(recipe.inputs)} · {Fmt.Duration(recipe.durationSec)}"
                    : $"{recipe.DisplayName} – nivå {recipe.unlockLevel}";
                var btn = Btn(text, () => Report(prod.Enqueue(o, recipe)), "btn");
                scroll.Add(btn);
                b.Add(() => btn.SetEnabled(prod.CanEnqueue(o, recipe, out _)));
            }
        }

        void BuildCommunityPanel(PlacedObjectState o, CommunityDefinition def)
        {
            var p = _selectionPanel;
            var b = _selectionBinder;
            var c = _s.Community;
            p.Add(Text($"Ger {def.incomeCoins} mynt och {def.incomeXp} XP var {Fmt.Duration(def.incomeIntervalSec)}", "small"));
            var status = Text("", "text");
            p.Add(status);
            p.Add(ProgressBar(() => o.income != null ? Timers.Progress(o.income) : 0f, b));
            var collect = Btn("Hämta", () => Report(c.Collect(o)), "btn", "btn-primary", "btn-big");
            p.Add(collect);
            b.Add(() =>
            {
                bool ready = c.CanCollect(o);
                status.text = ready ? "Redo att hämta!" : $"Nästa om {Fmt.Duration(c.Remaining(o))}";
                collect.SetEnabled(ready);
            });
        }

        void BuildExpansionPanel(MapExpansion e)
        {
            var p = _selectionPanel;
            p.Add(Header(string.IsNullOrEmpty(e.name) ? "Nytt område" : e.name, () => _ia.ClearSelection()));
            p.Add(Text($"Kräver nivå {e.requiredLevel} · {e.coinCost:N0} mynt", "text"));
            var btn = Btn("Lås upp", () =>
            {
                var r = _s.World.UnlockExpansion(e);
                Report(r);
                if (r.Ok) _ia.ClearSelection();
            }, "btn", "btn-primary", "btn-big");
            p.Add(btn);
            _selectionBinder.Add(() => btn.SetEnabled(_s.Progression.Level >= e.requiredLevel && _s.Wallet.CanAfford(CurrencyType.Coins, e.coinCost)));
        }

        // ---------- Byggläge ----------

        void RebuildPlacingBar()
        {
            _placingBinder.Clear();
            _placingBar.Clear();
            var def = _ia.PlacingDef;
            if (_ia.CurrentMode != WorldInteraction.Mode.Placing || def == null) return;
            bool moving = _ia.MovingId != null;
            _placingBar.Add(Text(moving ? $"Flytta {def.DisplayName}" : $"{def.DisplayName} – {WalletService.Format(def.cost)}", "subtitle"));
            var hint = Text("", "small");
            _placingBar.Add(hint);
            var row = Row();
            var confirm = Btn("Bekräfta", () => _ia.Confirm(), "btn", "btn-primary", "grow");
            row.Add(confirm);
            row.Add(Btn("Rotera", () => _ia.Rotate(), "btn", "grow"));
            row.Add(Btn("Klar", () => _ia.CancelPlacing(), "btn", "grow"));
            _placingBar.Add(row);
            _placingBinder.Add(() =>
            {
                confirm.SetEnabled(_ia.PlacementValid);
                hint.text = _ia.PlacementValid ? "Dra objektet eller tryck där det ska stå." : _ia.PlacementReason;
            });
        }

        // ---------- Fönster ----------

        void RebuildWindow()
        {
            _windowBinder.Clear();
            _windowLayer.Clear();
            if (_window == Window.None) return;
            var w = Panel("window", _windowLayer);
            switch (_window)
            {
                case Window.Shop: BuildShop(w); break;
                case Window.Storage: BuildStorage(w); break;
                case Window.Orders: BuildOrders(w); break;
                case Window.Debug: BuildDebug(w); break;
            }
        }

        VisualElement WindowHeader(VisualElement w, string title)
        {
            var h = Header(title, () => { _window = Window.None; _dirtyWindow = true; });
            w.Add(h);
            return h;
        }

        void BuildShop(VisualElement w)
        {
            WindowHeader(w, "Bygg");
            var tabs = Row();
            for (int i = 0; i < ShopTabs.Length; i++)
            {
                int index = i;
                var tab = Btn(ShopTabs[i].label, () => { _shopTab = index; _dirtyWindow = true; }, "btn", "tab", "grow");
                if (i == _shopTab) tab.AddToClassList("tab-active");
                tabs.Add(tab);
            }
            w.Add(tabs);

            var scroll = new ScrollView();
            scroll.AddToClassList("grow");
            var grid = new VisualElement();
            grid.AddToClassList("wrap");
            scroll.Add(grid);
            w.Add(scroll);

            var cats = ShopTabs[_shopTab].cats;
            foreach (var d in _s.Content.AllOf<PlaceableDefinition>()
                         .Where(d => d.inShop && cats.Contains(d.category))
                         .OrderBy(d => d.unlockLevel).ThenBy(d => d.DisplayName))
            {
                var def = d;
                bool unlocked = _s.Progression.IsUnlocked(def);
                string text = unlocked
                    ? $"<b>{def.DisplayName}</b>\n{WalletService.Format(def.cost)}" + (def.buildTimeSec > 0 ? $"\n{Fmt.Duration(def.buildTimeSec)}" : "")
                    : $"<b>{def.DisplayName}</b>\nNivå {def.unlockLevel}";
                var item = Btn(text, () =>
                {
                    _window = Window.None;
                    _dirtyWindow = true;
                    _ia.BeginPlace(def);
                }, "btn", "shop-item");
                grid.Add(item);
                _windowBinder.Add(() => item.SetEnabled(_s.World.CanBuy(def, out _)));
            }
        }

        void BuildStorage(VisualElement w)
        {
            WindowHeader(w, "Lager");
            var items = _s.Inventory.All.Where(kv => kv.Value > 0).OrderBy(kv => _s.Inventory.NameOf(kv.Key)).ToList();
            if (items.Count == 0) w.Add(Text("Tomt. Odla och skörda för att fylla lagret.", "small"));
            var scroll = new ScrollView();
            scroll.AddToClassList("grow");
            var grid = new VisualElement();
            grid.AddToClassList("wrap");
            scroll.Add(grid);
            w.Add(scroll);
            foreach (var kv in items)
            {
                var card = Card();
                card.style.width = 150;
                card.style.marginRight = 8;
                if (_s.Content.TryGet<ResourceDefinition>(kv.Key, out var res))
                {
                    var dot = Dot("coin-dot");
                    dot.style.backgroundColor = res.color;
                    card.Add(dot);
                }
                card.Add(Text(_s.Inventory.NameOf(kv.Key), "subtitle"));
                card.Add(Text($"{kv.Value} st", "text"));
                grid.Add(card);
            }
        }

        void BuildOrders(VisualElement w)
        {
            WindowHeader(w, "Grannskapstavlan");
            var b = _windowBinder;
            if (_s.Orders.Orders.Count == 0)
            {
                var wait = Text("", "small");
                w.Add(wait);
                b.Add(() => wait.text = $"Nya beställningar kommer om {Fmt.Duration(Math.Max(0, _s.State.nextOrderUnix - TimeService.UnixNow))}.");
            }
            var scroll = new ScrollView();
            scroll.AddToClassList("grow");
            w.Add(scroll);
            foreach (var order in _s.Orders.Orders.ToList())
            {
                var o = order;
                var card = Card();
                var nb = _s.Orders.Neighbor(o);
                if (nb != null)
                {
                    var who = Text($"{nb.DisplayName} vill ha:", "subtitle");
                    who.style.color = Color.Lerp(nb.color, Color.black, 0.4f);
                    card.Add(who);
                }
                foreach (var item in o.items)
                {
                    var it = item;
                    var line = Text("", "text");
                    card.Add(line);
                    b.Add(() =>
                    {
                        int have = _s.Inventory.Count(it.resourceId);
                        line.text = $"{_s.Inventory.NameOf(it.resourceId)}  {have}/{it.amount}" + (have >= it.amount ? "  (klart)" : "");
                    });
                }
                card.Add(Text($"Belöning: {o.coins} mynt, {o.xp} XP", "small"));
                var row = Row();
                var deliver = Btn("Leverera", () => Report(_s.Orders.Fulfil(o)), "btn", "btn-primary", "grow");
                row.Add(deliver);
                row.Add(Btn("Släng", () => _s.Orders.Discard(o), "btn"));
                card.Add(row);
                b.Add(() => deliver.SetEnabled(_s.Orders.CanFulfil(o)));
                scroll.Add(card);
            }
        }

        void BuildDebug(VisualElement w)
        {
            WindowHeader(w, "Debug");
            var grid = new VisualElement();
            grid.AddToClassList("wrap");
            w.Add(grid);
            grid.Add(Btn("+1 000 mynt", () => _s.Wallet.Add(CurrencyType.Coins, 1000), "btn"));
            grid.Add(Btn("+100 diamanter", () => _s.Wallet.Add(CurrencyType.Gems, 100), "btn"));
            grid.Add(Btn("+250 XP", () => _s.Progression.AddXp(250), "btn"));
            grid.Add(Btn("+10 av varje vara", () =>
            {
                foreach (var res in _s.Content.AllOf<ResourceDefinition>()) _s.Inventory.Add(res.Id, 10);
            }, "btn"));
            grid.Add(Btn("Spola fram 10 min", () => { TimeService.DebugSkip(TimeSpan.FromMinutes(10)); _s.Tick(); }, "btn"));
            grid.Add(Btn("Spola fram 1 h", () => { TimeService.DebugSkip(TimeSpan.FromHours(1)); _s.Tick(); }, "btn"));
            grid.Add(Btn("Klara alla uppdrag", () =>
            {
                foreach (var q in _s.Quests.Active.ToList()) if (_s.Quests.IsComplete(q)) _s.Quests.Claim(q);
            }, "btn"));
            var fps = Text("", "small");
            w.Add(fps);
            _windowBinder.Add(() => fps.text = $"Grafiknivå: {DeviceTier.Current} · {1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):0} FPS");
            w.Add(Btn("Ny sparfil (börja om)", () => { if (_boot != null) _boot.ResetSaveAndReload(); }, "btn", "btn-close"));
        }

        // ---------- Dialogrutor ----------

        string CurrentModalKey()
        {
            if (_s.NeedsTerms) return "terms";
            if (_s.NeedsCharacter) return "character";
            if (_levelUps.Count > 0) return "level:" + _levelUps.Peek();
            var intro = _s.Quests.NextUnseenIntro();
            if (intro != null) return "intro:" + intro.questId;
            if (_showDaily) return "daily";
            return null;
        }

        void RebuildModal(string key)
        {
            _shownModalKey = key;
            _modalBinder.Clear();
            _modalLayer.Clear();
            if (key == null) return;
            if (key == "terms") BuildTerms();
            else if (key == "character") BuildCharacterSelect();
            else if (key.StartsWith("level:")) BuildLevelUp(_levelUps.Peek());
            else if (key.StartsWith("intro:")) BuildIntro(_s.Quests.NextUnseenIntro());
            else if (key == "daily") BuildDaily();
        }

        VisualElement Dim()
        {
            var dim = new VisualElement();
            dim.AddToClassList("dim");
            _modalLayer.Add(dim);
            return dim;
        }

        /// <summary>Första start: villkor och integritetspolicy måste godkännas (krav för App Store och Google Play).</summary>
        void BuildTerms()
        {
            var dim = Dim();
            dim.AddToClassList("dim-soft");
            var m = Panel("modal", dim);
            m.Add(Text("Välkommen till Neil's Neighbor!", "title"));
            m.Add(Text("Innan vi startar renoveringen av grannskapet behöver du godkänna våra användarvillkor och vår integritetspolicy.", "text"));
            var links = Row();
            links.Add(Btn("Läs villkoren", () => Application.OpenURL(_s.Config.termsUrl), "btn", "grow"));
            links.Add(Btn("Integritetspolicy", () => Application.OpenURL(_s.Config.privacyUrl), "btn", "grow"));
            m.Add(links);
            var accept = new Toggle("Jag är över 13 år och godkänner användarvillkoren.");
            accept.AddToClassList("check");
            m.Add(accept);
            var start = Btn("Börja spela!", () =>
            {
                if (!accept.value) return;
                _s.AcceptTerms();
                _boot?.Save();
                _dirtyModal = true;
            }, "btn", "btn-primary", "btn-big");
            m.Add(start);
            _modalBinder.Add(() => start.SetEnabled(accept.value));
        }

        void BuildCharacterSelect()
        {
            var dim = Dim();
            dim.AddToClassList("dim-soft");
            var m = Panel("modal", dim);
            m.Add(Text("Vem flyttar in på Lindgatan?", "title"));
            m.Add(Text("Välj din karaktär. Du kan inte byta senare.", "small"));
            var row = Row();
            row.Add(CharacterCard("neil", "Neil", "Händig och nyfiken. Har drömt om ett eget hus med veranda.", new Color(0.43f, 0.71f, 1f)));
            row.Add(CharacterCard("sofia", "Sofia", "Kreativ och full av idéer. Ser skönheten i varje grått hus.", new Color(1f, 0.56f, 0.72f)));
            m.Add(row);
        }

        VisualElement CharacterCard(string id, string name, string bio, Color color)
        {
            var card = Card();
            card.AddToClassList("grow");
            card.AddToClassList("character-card");
            var portrait = new Label(name.Substring(0, 1));
            portrait.AddToClassList("portrait");
            portrait.style.backgroundColor = color;
            card.Add(portrait);
            card.Add(Text(name, "title"));
            card.Add(Text(bio, "small"));
            card.Add(Btn($"Spela som {name}", () =>
            {
                _s.ChooseCharacter(id);
                _boot?.Save();
                _dirtyModal = true;
                _dirtyQuests = true;
            }, "btn", "btn-primary"));
            return card;
        }

        void BuildLevelUp(int level)
        {
            var dim = Dim();
            var m = Panel("modal", dim);
            m.Add(Text($"Nivå {level}!", "title"));
            var row = _s.Progression.RewardsFor(level);
            if (row != null) m.Add(Text($"Belöning: {row.coinReward} mynt, {row.gemReward} diamanter", "text"));
            var unlocks = _s.Progression.UnlockNamesAt(level).Take(10).ToList();
            if (unlocks.Count > 0)
            {
                m.Add(Text("Nytt:", "subtitle"));
                foreach (var n in unlocks) m.Add(Text("• " + n, "small"));
            }
            m.Add(Btn("Toppen!", () => { _levelUps.Dequeue(); _dirtyModal = true; }, "btn", "btn-primary", "btn-big"));
        }

        void BuildIntro(QuestProgress q)
        {
            if (q == null) return;
            var def = _s.Quests.Def(q);
            var nb = def.neighbor;
            var dim = Dim();
            var box = Panel("dialog", dim);
            var portrait = new Label(nb != null && nb.DisplayName.Length > 0 ? nb.DisplayName.Substring(0, 1) : "?");
            portrait.AddToClassList("portrait");
            portrait.style.backgroundColor = nb != null ? nb.color : Color.gray;
            box.Add(portrait);
            var col = new VisualElement();
            col.AddToClassList("grow");
            col.Add(Text(nb != null ? nb.DisplayName : "Granne", "title"));
            col.Add(Text(def.DisplayName, "subtitle"));
            col.Add(Text(_s.Personalize(def.intro), "text"));
            col.Add(Btn("Okej!", () => { _s.Quests.MarkIntroSeen(q); _dirtyModal = true; }, "btn", "btn-primary", "btn-big"));
            box.Add(col);
        }

        void BuildDaily()
        {
            var dim = Dim();
            var m = Panel("modal", dim);
            m.Add(Text("Dagens gåva", "title"));
            var days = new VisualElement();
            days.AddToClassList("wrap");
            int today = _s.Daily.NextDay;
            for (int day = 1; day <= 7; day++)
            {
                string gems = _s.Daily.GemsFor(day) > 0 ? $"\n+{_s.Daily.GemsFor(day)} dia" : "";
                var box = new Label($"Dag {day}\n{_s.Daily.CoinsFor(day)} mynt{gems}");
                box.AddToClassList("day-box");
                if (day == today) box.AddToClassList("day-today");
                else if (day < today) box.AddToClassList("day-done");
                days.Add(box);
            }
            m.Add(days);
            m.Add(Text("Kom tillbaka varje dag för större gåvor. Missar du en dag börjar det om.", "small"));
            var row = Row();
            var claim = Btn("Hämta", () => { Report(_s.Daily.Claim()); _showDaily = false; _dirtyModal = true; }, "btn", "btn-primary", "btn-big", "grow");
            row.Add(claim);
            row.Add(Btn("Senare", () => { _showDaily = false; _dirtyModal = true; }, "btn"));
            m.Add(row);
            _modalBinder.Add(() => claim.SetEnabled(_s.Daily.CanClaim));
        }

        // ---------- Bubblor, pekare, meddelanden ----------

        void UpdateBubbles()
        {
            int used = 0;
            var panel = _root.panel;
            bool hide = _ia.CurrentMode == WorldInteraction.Mode.Placing || _shownModalKey != null;
            if (_cam != null && panel != null && !hide)
            {
                foreach (var o in _s.World.Objects)
                {
                    string text = null;
                    Action action = null;
                    var obj = o;
                    if (o.build != null) text = $"Bygger {Fmt.Duration(Timers.Remaining(o.build))}";
                    else if (o.renovation?.active != null) text = $"Renoverar {Fmt.Duration(Timers.Remaining(o.renovation.active))}";
                    else if (_s.Production.ReadyCount(o) > 0) { text = $"Hämta ({_s.Production.ReadyCount(o)})"; action = () => Report(_s.Production.Collect(obj)); }
                    else if (_s.Growth.CanHarvest(o)) { text = "Skörda"; action = () => Report(_s.Growth.Harvest(obj)); }
                    else if (_s.Community.CanCollect(o)) { text = "Hämta"; action = () => Report(_s.Community.Collect(obj)); }
                    else if (_s.Production.IsWorking(o)) text = Fmt.Duration(_s.Production.CurrentRemaining(o));
                    else if (_s.Renovation.CanStartNext(o, out _)) { text = "Renovera!"; action = () => _ia.Select(obj.instanceId); }
                    if (text == null || !_world.TryGetTop(o.instanceId, out var top)) continue;
                    var world = top + Vector3.up * 0.3f;
                    if (_cam.WorldToScreenPoint(world).z < 0) continue;
                    var pos = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, _cam);

                    if (used >= _bubbles.Count) _bubbles.Add(CreateBubble());
                    var slot = _bubbles[used++];
                    slot.action = action;
                    slot.button.text = text;
                    slot.button.pickingMode = action != null ? PickingMode.Position : PickingMode.Ignore;
                    slot.button.EnableInClassList("bubble-action", action != null);
                    slot.button.style.display = DisplayStyle.Flex;
                    slot.button.style.left = pos.x;
                    slot.button.style.top = pos.y;
                    slot.button.style.translate = new Translate(Length.Percent(-50), Length.Percent(-100));
                }
            }
            for (int i = used; i < _bubbles.Count; i++) _bubbles[i].button.style.display = DisplayStyle.None;
        }

        BubbleSlot CreateBubble()
        {
            var slot = new BubbleSlot();
            slot.button = new Button(() => slot.action?.Invoke());
            slot.button.AddToClassList("bubble");
            _bubbleLayer.Add(slot.button);
            return slot;
        }

        /// <summary>Tutorial: "Här!" pekar på det som det första uppdraget handlar om.</summary>
        void UpdatePointer()
        {
            _pointer.style.display = DisplayStyle.None;
            if (_shownModalKey != null || _window != Window.None || _ia.CurrentMode != WorldInteraction.Mode.Idle) return;
            string focus = _s.Quests.Def(_s.Quests.CurrentFocus())?.focus;
            if (string.IsNullOrEmpty(focus)) return;

            Vector2 anchor;
            bool below = false;
            if (focus == "ui:build") anchor = new Vector2(_buildButton.worldBound.center.x, _buildButton.worldBound.yMin);
            else if (focus == "ui:orders") { anchor = new Vector2(_ordersButton.worldBound.center.x, _ordersButton.worldBound.yMax); below = true; }
            else if (focus == "ui:storage") { anchor = new Vector2(_storageButton.worldBound.center.x, _storageButton.worldBound.yMax); below = true; }
            else
            {
                var o = _s.World.Objects.FirstOrDefault(x => x.definitionId == focus);
                if (o == null || _ia.SelectedId == o.instanceId || _cam == null || _root.panel == null) return;
                if (!_world.TryGetTop(o.instanceId, out var top)) return;
                var world = top + Vector3.up * 1.3f;
                if (_cam.WorldToScreenPoint(world).z < 0) return;
                anchor = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, world, _cam);
            }
            float bob = Mathf.Sin(Time.unscaledTime * 6f) * 6f;
            _pointer.style.display = DisplayStyle.Flex;
            _pointer.style.left = anchor.x;
            _pointer.style.top = anchor.y + (below ? 8 + bob : -8 + bob);
            _pointer.style.translate = new Translate(Length.Percent(-50), Length.Percent(below ? 0 : -100));
        }

        void Toast(string text)
        {
            if (string.IsNullOrEmpty(text) || _toastLayer == null) return;
            var l = Text(text, "toast");
            l.pickingMode = PickingMode.Ignore;
            _toastLayer.Add(l);
            _toasts.Add((l, Time.unscaledTime + 2.8f));
        }

        void UpdateToasts()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                if (_toasts[i].until > Time.unscaledTime) continue;
                _toasts[i].label.RemoveFromHierarchy();
                _toasts.RemoveAt(i);
            }
        }

        void Report(Result r)
        {
            if (!r.Ok) Toast(r.Reason);
        }

        // ---------- Byggstenar ----------

        VisualElement Layer(string name)
        {
            var l = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.left = 0; l.style.right = 0; l.style.top = 0; l.style.bottom = 0;
            _root.Add(l);
            return l;
        }

        VisualElement Panel(string cls, VisualElement parent = null)
        {
            var p = new VisualElement();
            p.AddToClassList("panel");
            p.AddToClassList(cls);
            (parent ?? _root).Add(p);
            return p;
        }

        static VisualElement Card()
        {
            var c = new VisualElement();
            c.AddToClassList("card");
            return c;
        }

        static VisualElement Chip(VisualElement parent)
        {
            var c = new VisualElement();
            c.AddToClassList("chip");
            parent.Add(c);
            return c;
        }

        static VisualElement Dot(string cls)
        {
            var d = new VisualElement();
            d.AddToClassList(cls);
            return d;
        }

        static VisualElement Row()
        {
            var r = new VisualElement();
            r.AddToClassList("row");
            return r;
        }

        static Label Text(string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            return l;
        }

        static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        VisualElement Header(string title, Action onClose)
        {
            var row = Row();
            var t = Text(title, "title");
            t.AddToClassList("grow");
            row.Add(t);
            row.Add(Btn("X", onClose, "btn", "btn-close"));
            return row;
        }

        static VisualElement ProgressBar(Func<float> progress, Binder binder, string extraClass = null)
        {
            var bg = new VisualElement();
            bg.AddToClassList("bar");
            if (extraClass != null) bg.AddToClassList(extraClass);
            var fill = new VisualElement();
            fill.AddToClassList("bar-fill");
            bg.Add(fill);
            binder.Add(() => fill.style.width = Length.Percent(Mathf.Clamp01(progress()) * 100f));
            return bg;
        }

        Button SpeedUp(Func<int> gems, Func<Result> action, Binder binder)
        {
            var b = Btn("", () => Report(action()), "btn", "btn-gem");
            binder.Add(() =>
            {
                int g = gems();
                b.text = g <= 0 ? "Gör klart (gratis)" : $"Snabba på ({g} diamanter)";
            });
            return b;
        }
    }
}
