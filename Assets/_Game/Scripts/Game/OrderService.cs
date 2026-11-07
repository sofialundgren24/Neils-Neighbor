using System;
using System.Collections.Generic;
using System.Linq;
using NG.Core;
using NG.Data;
using NG.State;
using UnityEngine;

namespace NG.Game
{
    /// <summary>
    /// Beställningstavlan: grannarna vill ha varor och betalar mynt + XP. Det är det som ger skörd och
    /// produktion ett syfte. Beställningar slumpas bland varor som är upplåsta på spelarens nivå.
    /// </summary>
    public sealed class OrderService
    {
        readonly GameSession _s;
        readonly System.Random _rng = new System.Random();

        public OrderService(GameSession s) { _s = s; }

        public IReadOnlyList<OrderState> Orders => _s.State.orders;

        public bool CanFulfil(OrderState order) =>
            order.items.All(i => _s.Inventory.Count(i.resourceId) >= i.amount);

        public Result Fulfil(OrderState order)
        {
            if (!CanFulfil(order)) return Result.Fail("Du har inte alla varor än.");
            foreach (var i in order.items) _s.Inventory.Add(i.resourceId, -i.amount);
            _s.Wallet.Add(CurrencyType.Coins, order.coins);
            _s.Progression.AddXp(order.xp);
            Remove(order);
            EventBus.Publish(new OrderDelivered());
            return Result.Success;
        }

        public void Discard(OrderState order) => Remove(order);

        void Remove(OrderState order)
        {
            _s.State.orders.Remove(order);
            _s.State.nextOrderUnix = TimeService.UnixNow + _s.Config.orderRefillSec;
            EventBus.Publish(new OrdersChanged());
        }

        public void Tick()
        {
            if (Orders.Count >= _s.Config.orderSlots || TimeService.UnixNow < _s.State.nextOrderUnix) return;
            var order = Generate();
            _s.State.nextOrderUnix = TimeService.UnixNow + _s.Config.orderRefillSec;
            if (order == null) return;
            _s.State.orders.Add(order);
            EventBus.Publish(new OrdersChanged());
        }

        OrderState Generate()
        {
            int level = _s.Progression.Level;
            var pool = _s.Content.AllOf<ResourceDefinition>().Where(r => r.unlockLevel <= level).ToList();
            if (pool.Count == 0) return null;

            int kinds = Mathf.Min(pool.Count, 1 + _rng.Next(Mathf.Min(3, 1 + level / 3)));
            var order = new OrderState();
            int value = 0;
            foreach (var res in pool.OrderBy(_ => _rng.Next()).Take(kinds))
            {
                int amount = 1 + _rng.Next(Mathf.Max(1, 2 + level / 4));
                order.items.Add(new ResourceStack(res.Id, amount));
                value += res.sellPrice * amount;
            }
            order.coins = Mathf.Max(1, Mathf.RoundToInt(value * _s.Config.orderRewardMultiplier));
            order.xp = Mathf.Max(1, Mathf.CeilToInt(value * 0.5f));
            return order;
        }
    }
}
