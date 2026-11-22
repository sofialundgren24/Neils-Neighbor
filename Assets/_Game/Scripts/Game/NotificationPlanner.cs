using System;
using System.Collections.Generic;
using System.Linq;
using NG.Core;
using NG.Data;
using NG.Notifications;
using NG.State;
using UnityEngine;

namespace NG.Game
{
    /// <summary>
    /// Bestämmer vilka påminnelser spelaren får när appen stängs, utifrån sparfilen:
    ///   - när den första blomman/grödan är redo att skördas
    ///   - när en affär har färdiga varor
    ///   - när en renovering eller ett bygge är klart
    ///   - när lekparken/skolan har intäkter
    ///   - dagens gåva (nästa förmiddag)
    ///   - "grannarna saknar dig" efter tre dagar
    /// Inga notiser mellan 22 och 08 (de flyttas till kl 9). Spelaren kan stänga av dem i Inställningar.
    /// </summary>
    public static class NotificationPlanner
    {
        const string EnabledKey = "ng_notifications";
        const string AskedKey = "ng_notifications_asked";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value) LocalNotifications.CancelAll();
            }
        }

        /// <summary>Fråga om lov en gång, efter tutorialen (inte direkt vid första start).</summary>
        public static bool ShouldAskPermission(GameSession s) =>
            Enabled && LocalNotifications.Supported && s.Progression.Level >= 1 && PlayerPrefs.GetInt(AskedKey, 0) == 0;

        public static void MarkAsked()
        {
            PlayerPrefs.SetInt(AskedKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>När spelaren kommer tillbaka: inga gamla notiser kvar.</summary>
        public static void OnResume() => LocalNotifications.CancelAll();

        /// <summary>När appen stängs eller läggs i bakgrunden: schemalägg påminnelserna.</summary>
        public static void OnPause(GameSession s)
        {
            LocalNotifications.CancelAll();
            if (!Enabled || s == null) return;
            foreach (var (id, title, body, seconds) in Plan(s))
                LocalNotifications.Schedule(id, title, body, TimeSpan.FromSeconds(ShiftOutOfNight(seconds)));
        }

        public static IEnumerable<(string id, string title, string body, double seconds)> Plan(GameSession s)
        {
            var objects = s.World.Objects;

            double plants = Earliest(objects.Where(o => o.growth != null && !o.decayed && s.Growth.Def(o)?.IsHarvestable == true),
                o => Timers.Remaining(o.growth));
            if (plants > 0) yield return ("harvest", Loc.T("Dags att skörda!"), Loc.T("Blommorna i din trädgård står i full blom."), plants);

            double goods = Earliest(objects.Where(o => s.Production.IsWorking(o)), o => s.Production.CurrentRemaining(o));
            if (goods > 0) yield return ("goods", Loc.T("Varorna är klara"), Loc.T("Grannarna väntar på dina buketter och varor."), goods);

            var reno = objects.Where(o => o.renovation?.active != null).OrderBy(o => Timers.Remaining(o.renovation.active)).FirstOrDefault();
            if (reno != null)
                yield return ("renovation", Loc.T("Renoveringen är klar!"),
                    Loc.F("{0} är redo för nästa steg.", s.World.Def(reno)?.DisplayName), Timers.Remaining(reno.renovation.active));

            double build = Earliest(objects.Where(o => o.build != null), o => Timers.Remaining(o.build));
            if (build > 0) yield return ("build", Loc.T("Bygget är klart!"), Loc.T("Kom och se hur det blev."), build);

            double income = Earliest(objects.Where(o => o.income != null && s.Community.IsCommunity(o)), o => s.Community.Remaining(o));
            if (income > 0) yield return ("income", Loc.T("Lekparken är full av liv"), Loc.T("Det finns mynt att hämta i kvarteret."), income);

            // Dagens gåva: nästa förmiddag kl 10 lokal tid.
            var now = DateTime.Now;
            var tenTomorrow = now.Date.AddDays(1).AddHours(10);
            yield return ("daily", Loc.T("Dagens gåva väntar"), Loc.T("Hämta din gåva och håll igång serien!"), (tenTomorrow - now).TotalSeconds);

            yield return ("miss_you", Loc.T("Grannarna saknar dig"), Loc.F("Lindgatan väntar på dig, {0}!", s.PlayerName), TimeSpan.FromDays(3).TotalSeconds);
        }

        static double Earliest(IEnumerable<PlacedObjectState> items, Func<PlacedObjectState, double> remaining)
        {
            double best = 0;
            foreach (var o in items)
            {
                double r = remaining(o);
                if (r > 60 && (best == 0 || r < best)) best = r;
            }
            return best;
        }

        /// <summary>Notiser som skulle komma mitt i natten flyttas till kl 9 på morgonen.</summary>
        static double ShiftOutOfNight(double seconds)
        {
            var fire = DateTime.Now.AddSeconds(seconds);
            if (fire.Hour >= 22) fire = fire.Date.AddDays(1).AddHours(9);
            else if (fire.Hour < 8) fire = fire.Date.AddHours(9);
            return (fire - DateTime.Now).TotalSeconds;
        }
    }
}
