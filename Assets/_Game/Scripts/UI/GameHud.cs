using System;
using System.Collections.Generic;
using System.Linq;
using NG.Core;
using NG.Data;
using NG.Game;
using NG.State;
using NG.World;
using UnityEngine;

namespace NG.UI
{
    /// <summary>
    /// PLATSHÅLLAR-UI (Unity IMGUI) så att hela spelloopen går att spela redan nu.
    /// Det ska bytas mot riktigt UI senare (UI Toolkit med egen grafik), men det nya UI:t anropar
    /// exakt samma metoder i GameSession – ingen spellogik bor här.
    ///
    /// Innehåll: toppfält (nivå, XP, mynt, diamanter), byggmeny, panel för markerat objekt/område,
    /// bubblor över objekt (timers, Skörda, Hämta), lager, beställningar, nivå-upp-ruta, meddelanden och debug.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        enum Window { None, Shop, Storage, Orders, Debug }

        static readonly (string label, PlaceableCategory[] cats)[] ShopTabs =
        {
            ("Byggnader", new[] { PlaceableCategory.Production, PlaceableCategory.House }),
            ("Växter", new[] { PlaceableCategory.Plant }),
            ("Träd & buskar", new[] { PlaceableCategory.Tree, PlaceableCategory.Hedge }),
            ("Dekor", new[] { PlaceableCategory.Decor, PlaceableCategory.Water }),
            ("Staket", new[] { PlaceableCategory.Fence }),
            ("Mark", new[] { PlaceableCategory.Path })
        };

        static readonly Color[] PaintColors =
        {
            new Color(0.56f, 0.83f, 1f), new Color(1f, 0.71f, 0.64f), new Color(1f, 0.89f, 0.48f),
            new Color(0.64f, 0.88f, 0.6f), new Color(0.78f, 0.62f, 1f), new Color(1f, 1f, 1f)
        };

        GameSession _s;
        WorldInteraction _interaction;
        WorldView _world;
        GameBootstrap _bootstrap;
        Camera _cam;

        Window _window;
        int _shopTab;
        Vector2 _scroll;
        readonly List<(string text, float until)> _toasts = new List<(string, float)>();
        readonly Queue<int> _levelUps = new Queue<int>();

        float _scale = 1f;
        GUIStyle _box, _label, _small, _title, _button, _bigButton, _bubble;
        Texture2D _white;

        public void Bind(GameSession session, WorldInteraction interaction, WorldView world)
        {
            _s = session;
            _interaction = interaction;
            _world = world;
            _bootstrap = GetComponent<GameBootstrap>();
            _cam = Camera.main;
            EventBus.Subscribe<GameMessage>(OnMessage);
            EventBus.Subscribe<LevelUp>(OnLevelUp);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<GameMessage>(OnMessage);
            EventBus.Unsubscribe<LevelUp>(OnLevelUp);
        }

        void OnMessage(GameMessage m) => Toast(m.Text);
        void OnLevelUp(LevelUp e) => _levelUps.Enqueue(e.NewLevel);

        void Toast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _toasts.Add((text, Time.unscaledTime + 2.5f));
        }

        void Report(Result r)
        {
            if (!r.Ok) Toast(r.Reason);
        }

        // ---------- Rita ----------

        void OnGUI()
        {
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) InputGate.BeginUiFrame();

            EnsureStyles();
            _scale = Mathf.Clamp(Screen.height / 720f, 0.75f, 3f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            float w = Screen.width / _scale;
            float h = Screen.height / _scale;

            if (_s == null)
            {
                GUI.Label(new Rect(w * 0.5f - 150, h * 0.5f - 20, 300, 40), "Laddar ...", _title);
                if (repaint) InputGate.EndUiFrame();
                return;
            }

            InputGate.ModalOpen = _levelUps.Count > 0;

            DrawBubbles();
            DrawTopBar(w);

            if (_interaction.CurrentMode == WorldInteraction.Mode.Placing) DrawPlacingBar(w, h);
            else
            {
                DrawBottomButtons(h);
                if (_interaction.SelectedId != null) DrawSelection(w);
                else if (_interaction.SelectedExpansion != null) DrawExpansion(w, _interaction.SelectedExpansion);
            }

            switch (_window)
            {
                case Window.Shop: DrawShop(w, h); break;
                case Window.Storage: DrawStorage(w, h); break;
                case Window.Orders: DrawOrders(w, h); break;
                case Window.Debug: DrawDebug(w, h); break;
            }

            DrawToasts(w);
            if (_levelUps.Count > 0) DrawLevelUp(w, h);

            if (repaint) InputGate.EndUiFrame();
        }

        /// <summary>Rutor som blockerar tryck mot världen. Rect i UI-koordinater (före skalning).</summary>
        void Block(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            InputGate.AddGuiRect(new Rect(r.x * _scale, r.y * _scale, r.width * _scale, r.height * _scale));
        }

        void Panel(Rect r)
        {
            GUI.Box(r, GUIContent.none, _box);
            Block(r);
        }

        void DrawTopBar(float w)
        {
            var bar = new Rect(8, 8, w - 16, 52);
            Panel(bar);
            var p = _s.Progression;
            GUI.Label(new Rect(20, 12, 90, 44), $"Nivå {p.Level}", _title);
            var xpRect = new Rect(110, 24, 180, 20);
            DrawBar(xpRect, p.LevelProgress, new Color(0.45f, 0.8f, 1f));
            GUI.Label(xpRect, $"{p.Xp} / {p.XpToNext} XP", _small);

            GUI.Label(new Rect(310, 12, 160, 44), $"Mynt: {_s.Wallet.Get(CurrencyType.Coins):N0}", _label);
            GUI.Label(new Rect(470, 12, 170, 44), $"Diamanter: {_s.Wallet.Get(CurrencyType.Gems):N0}", _label);

            float x = w - 16;
            x -= 90; if (GUI.Button(new Rect(x, 14, 82, 40), "Debug", _button)) Toggle(Window.Debug);
            x -= 150; if (GUI.Button(new Rect(x, 14, 142, 40), $"Beställningar ({_s.Orders.Orders.Count})", _button)) Toggle(Window.Orders);
            x -= 90; if (GUI.Button(new Rect(x, 14, 82, 40), "Lager", _button)) Toggle(Window.Storage);
        }

        void Toggle(Window win) => _window = _window == win ? Window.None : win;

        void DrawBottomButtons(float h)
        {
            var r = new Rect(12, h - 84, 150, 72);
            Block(r);
            if (GUI.Button(r, "Bygg", _bigButton)) Toggle(Window.Shop);
        }

        // ---------- Byggmeny ----------

        void DrawShop(float w, float h)
        {
            var r = new Rect(12, h - 470, Mathf.Min(720, w - 24), 376);
            Panel(r);
            GUI.Label(new Rect(r.x + 14, r.y + 6, 300, 30), "Bygg", _title);
            if (GUI.Button(new Rect(r.xMax - 46, r.y + 8, 36, 30), "X", _button)) _window = Window.None;

            float tabW = (r.width - 28) / ShopTabs.Length;
            for (int i = 0; i < ShopTabs.Length; i++)
            {
                var tr = new Rect(r.x + 14 + i * tabW, r.y + 42, tabW - 4, 32);
                GUI.color = i == _shopTab ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                if (GUI.Button(tr, ShopTabs[i].label, _button)) { _shopTab = i; _scroll = Vector2.zero; }
            }
            GUI.color = Color.white;

            var cats = ShopTabs[_shopTab].cats;
            var items = _s.Content.AllOf<PlaceableDefinition>()
                .Where(d => d.inShop && cats.Contains(d.category))
                .OrderBy(d => d.unlockLevel).ThenBy(d => d.DisplayName)
                .ToList();

            const float itemW = 160, itemH = 96;
            var view = new Rect(r.x + 10, r.y + 82, r.width - 20, r.height - 92);
            int perRow = Mathf.Max(1, Mathf.FloorToInt((view.width - 20) / (itemW + 8)));
            int rows = Mathf.CeilToInt(items.Count / (float)perRow);
            var content = new Rect(0, 0, view.width - 20, rows * (itemH + 8));
            _scroll = GUI.BeginScrollView(view, _scroll, content);
            for (int i = 0; i < items.Count; i++)
            {
                var d = items[i];
                var ir = new Rect((i % perRow) * (itemW + 8), (i / perRow) * (itemH + 8), itemW, itemH);
                bool unlocked = _s.Progression.IsUnlocked(d);
                bool canBuy = _s.World.CanBuy(d, out var reason);
                string text = unlocked
                    ? $"<b>{d.DisplayName}</b>\n{WalletService.Format(d.cost)}" + (d.buildTimeSec > 0 ? $"\n{Fmt.Duration(d.buildTimeSec)}" : "")
                    : $"<b>{d.DisplayName}</b>\nNivå {d.unlockLevel}";
                GUI.enabled = canBuy;
                if (GUI.Button(ir, text, _button))
                {
                    _window = Window.None;
                    _interaction.BeginPlace(d);
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }

        void DrawPlacingBar(float w, float h)
        {
            var def = _interaction.PlacingDef;
            var r = new Rect(w * 0.5f - 300, h - 120, 600, 108);
            Panel(r);
            string what = _interaction.MovingId != null ? $"Flytta {def.DisplayName}" : $"{def.DisplayName} – {WalletService.Format(def.cost)}";
            GUI.Label(new Rect(r.x + 14, r.y + 6, r.width - 28, 28), what, _label);
            string hint = _interaction.PlacementValid ? "Dra objektet eller tryck där det ska stå." : _interaction.PlacementReason;
            GUI.Label(new Rect(r.x + 14, r.y + 32, r.width - 28, 24), hint, _small);

            float bw = (r.width - 40) / 3f;
            GUI.enabled = _interaction.PlacementValid;
            if (GUI.Button(new Rect(r.x + 10, r.y + 58, bw, 42), "Bekräfta", _button)) _interaction.Confirm();
            GUI.enabled = true;
            if (GUI.Button(new Rect(r.x + 20 + bw, r.y + 58, bw, 42), "Rotera", _button)) _interaction.Rotate();
            if (GUI.Button(new Rect(r.x + 30 + bw * 2, r.y + 58, bw, 42), "Klar", _button)) _interaction.CancelPlacing();
        }

        // ---------- Markerat objekt ----------

        void DrawSelection(float w)
        {
            var o = _s.World.Get(_interaction.SelectedId);
            var def = _s.World.Def(o);
            if (o == null || def == null) { _interaction.ClearSelection(); return; }

            var r = new Rect(w - 352, 70, 340, 470);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 8, r.width - 24, r.height - 16));
            GUILayout.BeginHorizontal();
            GUILayout.Label(def.DisplayName, _title);
            if (GUILayout.Button("X", _button, GUILayout.Width(36))) _interaction.ClearSelection();
            GUILayout.EndHorizontal();

            if (o.build != null)
            {
                GUILayout.Label($"Bygger ... {Fmt.Duration(Timers.Remaining(o.build))} kvar", _label);
                SpeedUpButton(TimeService.SpeedUpGemCost(Timers.Remaining(o.build)), () => _s.World.SpeedUpBuild(o));
            }
            else if (_s.Renovation.IsRenovatable(o)) DrawHouse(o);
            else if (def is PlantDefinition plant) DrawPlant(o, plant);
            else if (def is ProductionDefinition) DrawProduction(o);
            else if (o.decayed) GUILayout.Label("Grått och slitet. Renovera huset intill så får det tillbaka färgen.", _small);
            else GUILayout.Label($"Skönhet +{def.beautyScore}", _small);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Flytta", _button)) _interaction.BeginMove(o.instanceId);
            if (_s.World.CanSell(o) && GUILayout.Button($"Sälj (+{_s.World.SellValue(o)})", _button)) Report(_s.World.Sell(o));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawHouse(PlacedObjectState o)
        {
            var reno = _s.Renovation;
            int total = reno.PhaseCount(o);
            if (reno.IsComplete(o))
            {
                GUILayout.Label("Färdigrenoverat!", _label);
                GUILayout.Label("Målarfärg:", _small);
                GUILayout.BeginHorizontal();
                foreach (var c in PaintColors)
                {
                    GUI.backgroundColor = c;
                    if (GUILayout.Button(" ", _button, GUILayout.Width(40), GUILayout.Height(34))) Report(reno.SetPaint(o, c));
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Originalfärg", _button)) Report(reno.SetPaint(o, null));
                return;
            }

            GUILayout.Label($"Renovering: fas {o.renovation.phase} av {total}", _label);
            if (reno.IsInProgress(o))
            {
                DrawBarLayout(Timers.Progress(o.renovation.active));
                GUILayout.Label($"{Fmt.Duration(Timers.Remaining(o.renovation.active))} kvar", _small);
                SpeedUpButton(reno.SpeedUpCost(o), () => reno.SpeedUp(o));
                return;
            }

            var next = reno.NextPhase(o);
            if (next == null) return;
            GUILayout.Label($"Nästa: {Fmt.PhaseName(next.variantName)}", _small);
            string cost = next.cost.amount > 0 ? $"{next.cost.amount} {WalletService.Name(next.cost.currency)}" : "Gratis";
            string mats = InventoryService.Format(next.materials);
            GUILayout.Label($"Kostar {cost}{(mats.Length > 0 ? ", " + mats : "")} · {Fmt.Duration(next.durationSec)} · +{next.xpReward} XP", _small);
            bool can = reno.CanStartNext(o, out var why);
            GUI.enabled = can;
            if (GUILayout.Button("Renovera", _bigButton, GUILayout.Height(54))) Report(reno.StartNext(o));
            GUI.enabled = true;
            if (!can) GUILayout.Label(why, _small);
        }

        void DrawPlant(PlacedObjectState o, PlantDefinition plant)
        {
            var g = _s.Growth;
            if (o.decayed) { GUILayout.Label("Vissen. Renovera huset intill så kommer den tillbaka.", _small); return; }
            if (g.IsGrowing(o))
            {
                GUILayout.Label("Växer ...", _label);
                DrawBarLayout(Timers.Progress(o.growth));
                GUILayout.Label($"{Fmt.Duration(Timers.Remaining(o.growth))} kvar", _small);
                SpeedUpButton(g.SpeedUpCost(o), () => g.SpeedUp(o));
            }
            else if (plant.IsHarvestable)
            {
                GUILayout.Label($"Ger {plant.harvest.amount} {plant.harvest.resource.DisplayName}", _small);
                if (GUILayout.Button("Skörda", _bigButton, GUILayout.Height(54))) Report(g.Harvest(o));
            }
            else GUILayout.Label("I full blom.", _label);
        }

        void DrawProduction(PlacedObjectState o)
        {
            var p = _s.Production;
            var def = p.Def(o);
            if (p.ReadyCount(o) > 0 && GUILayout.Button($"Hämta {p.ReadyCount(o)} färdiga", _bigButton, GUILayout.Height(48)))
                Report(p.Collect(o));

            GUILayout.Label($"Kö {o.production.queue.Count}/{def.queueSize}", _small);
            for (int i = 0; i < o.production.queue.Count; i++)
            {
                var r = p.Recipe(o.production.queue[i]);
                string name = r != null ? r.DisplayName : o.production.queue[i];
                GUILayout.Label(i == 0 ? $"  {name}: {Fmt.Duration(p.CurrentRemaining(o))}" : $"  {name}", _small);
            }
            if (p.IsWorking(o)) SpeedUpButton(p.SpeedUpCost(o), () => p.SpeedUp(o));

            GUILayout.Label("Recept:", _label);
            foreach (var r in p.Recipes(o))
            {
                bool unlocked = _s.Progression.IsUnlocked(r);
                bool can = p.CanEnqueue(o, r, out _);
                string text = unlocked
                    ? $"{r.DisplayName}  ({InventoryService.Format(r.inputs)}) · {Fmt.Duration(r.durationSec)}"
                    : $"{r.DisplayName} – nivå {r.unlockLevel}";
                GUI.enabled = can;
                if (GUILayout.Button(text, _button)) Report(p.Enqueue(o, r));
                GUI.enabled = true;
            }
        }

        void SpeedUpButton(int gems, Func<Result> action)
        {
            string text = gems <= 0 ? "Gör klart (gratis)" : $"Snabba på ({gems} diamanter)";
            if (GUILayout.Button(text, _button)) Report(action());
        }

        void DrawExpansion(float w, MapExpansion e)
        {
            var r = new Rect(w - 352, 70, 340, 200);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 8, r.width - 24, r.height - 16));
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.IsNullOrEmpty(e.name) ? "Nytt område" : e.name, _title);
            if (GUILayout.Button("X", _button, GUILayout.Width(36))) _interaction.ClearSelection();
            GUILayout.EndHorizontal();
            GUILayout.Label($"Kräver nivå {e.requiredLevel} · {e.coinCost:N0} mynt", _label);
            GUI.enabled = _s.Progression.Level >= e.requiredLevel && _s.Wallet.CanAfford(CurrencyType.Coins, e.coinCost);
            if (GUILayout.Button("Lås upp", _bigButton, GUILayout.Height(54)))
            {
                var res = _s.World.UnlockExpansion(e);
                Report(res);
                if (res.Ok) _interaction.ClearSelection();
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        // ---------- Bubblor över objekt ----------

        void DrawBubbles()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            foreach (var o in _s.World.Objects)
            {
                string text = null;
                Action action = null;

                if (o.build != null) text = $"Bygger {Fmt.Duration(Timers.Remaining(o.build))}";
                else if (o.renovation?.active != null) text = $"Renoverar {Fmt.Duration(Timers.Remaining(o.renovation.active))}";
                else if (_s.Production.ReadyCount(o) > 0) { text = $"Hämta ({_s.Production.ReadyCount(o)})"; action = () => Report(_s.Production.Collect(o)); }
                else if (_s.Growth.CanHarvest(o)) { text = "Skörda"; action = () => Report(_s.Growth.Harvest(o)); }
                else if (_s.Production.IsWorking(o)) text = Fmt.Duration(_s.Production.CurrentRemaining(o));
                else if (_s.Renovation.CanStartNext(o, out _)) { text = "Renovera!"; action = () => _interaction.Select(o.instanceId); }
                if (text == null || !_world.TryGetTop(o.instanceId, out var top)) continue;

                var sp = _cam.WorldToScreenPoint(top + Vector3.up * 0.3f);
                if (sp.z < 0) continue;
                var gp = new Vector2(sp.x / _scale, (Screen.height - sp.y) / _scale);
                var size = _bubble.CalcSize(new GUIContent(text)) + new Vector2(16, 8);
                var r = new Rect(gp.x - size.x * 0.5f, gp.y - size.y, size.x, size.y);
                if (action != null)
                {
                    Block(r);
                    if (GUI.Button(r, text, _bubble)) action();
                }
                else GUI.Label(r, text, _bubble);
            }
        }

        // ---------- Fönster ----------

        void DrawStorage(float w, float h)
        {
            var r = CenterRect(w, h, 420, 460);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 8, r.width - 28, r.height - 16));
            Header("Lager");
            var items = _s.Inventory.All.Where(kv => kv.Value > 0).OrderBy(kv => _s.Inventory.NameOf(kv.Key)).ToList();
            if (items.Count == 0) GUILayout.Label("Tomt. Odla och skörda för att fylla lagret.", _small);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var kv in items)
                GUILayout.Label($"{_s.Inventory.NameOf(kv.Key)}:  {kv.Value}", _label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawOrders(float w, float h)
        {
            var r = CenterRect(w, h, 520, 480);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 8, r.width - 28, r.height - 16));
            Header("Beställningar");
            if (_s.Orders.Orders.Count == 0)
                GUILayout.Label($"Nya beställningar kommer om {Fmt.Duration(Math.Max(0, _s.State.nextOrderUnix - TimeService.UnixNow))}.", _small);
            foreach (var order in _s.Orders.Orders.ToList())
            {
                GUILayout.BeginVertical(_box);
                foreach (var item in order.items)
                {
                    int have = _s.Inventory.Count(item.resourceId);
                    GUI.color = have >= item.amount ? new Color(0.7f, 1f, 0.7f) : Color.white;
                    GUILayout.Label($"{_s.Inventory.NameOf(item.resourceId)}  {have}/{item.amount}", _label);
                }
                GUI.color = Color.white;
                GUILayout.Label($"Belöning: {order.coins} mynt, {order.xp} XP", _small);
                GUILayout.BeginHorizontal();
                GUI.enabled = _s.Orders.CanFulfil(order);
                if (GUILayout.Button("Leverera", _button)) Report(_s.Orders.Fulfil(order));
                GUI.enabled = true;
                if (GUILayout.Button("Släng", _button, GUILayout.Width(90))) _s.Orders.Discard(order);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndArea();
        }

        void DrawDebug(float w, float h)
        {
            var r = CenterRect(w, h, 360, 430);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 8, r.width - 28, r.height - 16));
            Header("Debug");
            if (GUILayout.Button("+1 000 mynt", _button)) _s.Wallet.Add(CurrencyType.Coins, 1000);
            if (GUILayout.Button("+100 diamanter", _button)) _s.Wallet.Add(CurrencyType.Gems, 100);
            if (GUILayout.Button("+250 XP", _button)) _s.Progression.AddXp(250);
            if (GUILayout.Button("+10 av varje vara", _button))
                foreach (var res in _s.Content.AllOf<ResourceDefinition>()) _s.Inventory.Add(res.Id, 10);
            if (GUILayout.Button("Spola fram 10 min", _button)) { TimeService.DebugSkip(TimeSpan.FromMinutes(10)); _s.Tick(); }
            if (GUILayout.Button("Spola fram 1 h", _button)) { TimeService.DebugSkip(TimeSpan.FromHours(1)); _s.Tick(); }
            GUILayout.Space(8);
            GUILayout.Label($"Grafiknivå: {DeviceTier.Current} · {1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):0} FPS", _small);
            GUILayout.Space(8);
            if (GUILayout.Button("Ny sparfil (börja om)", _button) && _bootstrap != null) _bootstrap.ResetSaveAndReload();
            GUILayout.EndArea();
        }

        void DrawLevelUp(float w, float h)
        {
            int level = _levelUps.Peek();
            var dim = new Rect(0, 0, w, h);
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(dim, _white);
            GUI.color = Color.white;
            Block(dim);

            var r = CenterRect(w, h, 440, 420);
            GUI.Box(r, GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 12, r.width - 32, r.height - 24));
            GUILayout.Label($"Nivå {level}!", _title);
            var row = _s.Progression.RewardsFor(level);
            if (row != null) GUILayout.Label($"Belöning: {row.coinReward} mynt, {row.gemReward} diamanter", _label);
            GUILayout.Space(6);
            GUILayout.Label("Nytt:", _label);
            foreach (var n in _s.Progression.UnlockNamesAt(level).Take(10)) GUILayout.Label("• " + n, _small);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Toppen!", _bigButton, GUILayout.Height(52))) _levelUps.Dequeue();
            GUILayout.EndArea();
        }

        void DrawToasts(float w)
        {
            _toasts.RemoveAll(t => t.until < Time.unscaledTime);
            float y = 70;
            foreach (var t in _toasts)
            {
                var size = _bubble.CalcSize(new GUIContent(t.text)) + new Vector2(28, 14);
                GUI.Label(new Rect(w * 0.5f - size.x * 0.5f, y, size.x, size.y), t.text, _bubble);
                y += size.y + 4;
            }
        }

        // ---------- Hjälpare ----------

        void Header(string title)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, _title);
            if (GUILayout.Button("X", _button, GUILayout.Width(36))) _window = Window.None;
            GUILayout.EndHorizontal();
        }

        static Rect CenterRect(float w, float h, float rw, float rh)
        {
            rw = Mathf.Min(rw, w - 24);
            rh = Mathf.Min(rh, h - 90);
            return new Rect((w - rw) * 0.5f, Mathf.Max(70, (h - rh) * 0.5f), rw, rh);
        }

        void DrawBar(Rect r, float progress, Color color)
        {
            GUI.color = new Color(0, 0, 0, 0.35f);
            GUI.DrawTexture(r, _white);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(progress), r.height), _white);
            GUI.color = Color.white;
        }

        void DrawBarLayout(float progress)
        {
            var r = GUILayoutUtility.GetRect(100, 14, GUILayout.ExpandWidth(true));
            DrawBar(r, progress, new Color(0.5f, 0.9f, 0.5f));
        }

        void EnsureStyles()
        {
            if (_box != null) return;
            _white = Texture2D.whiteTexture;

            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.13f, 0.16f, 0.2f, 0.88f));
            bg.Apply();
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 8, 8) };
            _box.normal.background = bg;

            _label = new GUIStyle(GUI.skin.label) { fontSize = 17, richText = true, wordWrap = true };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(_label) { fontSize = 14 };
            _small.normal.textColor = new Color(0.85f, 0.9f, 0.95f);
            _title = new GUIStyle(_label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = false };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 15, richText = true, wordWrap = true };
            _bigButton = new GUIStyle(_button) { fontSize = 22, fontStyle = FontStyle.Bold };

            var bubbleBg = new Texture2D(1, 1);
            bubbleBg.SetPixel(0, 0, new Color(1f, 0.98f, 0.92f, 0.95f));
            bubbleBg.Apply();
            _bubble = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _bubble.normal.background = bubbleBg;
            _bubble.hover.background = bubbleBg;
            _bubble.active.background = bubbleBg;
            _bubble.normal.textColor = new Color(0.2f, 0.2f, 0.25f);
            _bubble.hover.textColor = new Color(0.1f, 0.4f, 0.2f);
            _bubble.active.textColor = new Color(0.1f, 0.4f, 0.2f);
        }
    }

    /// <summary>Formatering av tider och namn för UI:t.</summary>
    public static class Fmt
    {
        public static string Duration(double seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
            if (s >= 3600) return $"{s / 3600} h {s % 3600 / 60:00} min";
            return $"{s / 60}:{s % 60:00}";
        }

        public static string PhaseName(string variant) => variant switch
        {
            "Phase1_Repaired" => "Laga",
            "Phase2_Painted" => "Måla",
            "Phase3_Decorated" => "Dekorera",
            _ => variant
        };
    }
}
