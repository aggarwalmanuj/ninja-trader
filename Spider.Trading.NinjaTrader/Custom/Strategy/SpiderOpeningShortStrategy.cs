using System.ComponentModel;
using NinjaTrader.Cbi;

namespace NinjaTrader.Strategy
{
    [Description("Spider Opening Short Strategy")]
    public class SpiderOpeningShortStrategy : SpiderBaseOpeningStrategy
    {
        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void OnBarUpdate()
        {
            base.OnBarUpdate();
        }

        protected override void OnOrderUpdate(IOrder order)
        {
            base.OnOrderUpdate(order);
        }

        protected override OrderAction GetOrderAction()
        {
            return OrderAction.SellShort;
        }
    }
}