using System.ComponentModel;
using NinjaTrader.Cbi;

namespace NinjaTrader.Strategy
{
    [Description("Spider Opening Long Strategy")]
    public class SpiderOpeningLongStrategy : SpiderBaseOpeningStrategy
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
            return OrderAction.Buy;
        }
    }
}