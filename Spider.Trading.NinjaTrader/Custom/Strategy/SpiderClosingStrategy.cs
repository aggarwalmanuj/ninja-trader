using System;
using System.ComponentModel;
using NinjaTrader.Cbi;

namespace NinjaTrader.Strategy
{
    [Description("Base Spider Closing Strategy")]
    public class SpiderClosingStrategy : SpiderBaseStrategy
    {

        protected int? InitialOpenQuantity { get; set; }

        protected int? InitialQuantityToBeClosed { get; set; }

        protected int FilledQuantity { get; set; }

        protected MarketPosition? OpenMarketPosition { get; set; }

        protected override void Initialize()
        {
            foreach (Account currentAccount in Cbi.Globals.Accounts)
            {
                if (string.Compare(currentAccount.Name, this.Account.Name, StringComparison.InvariantCultureIgnoreCase) == 0 &&
                    currentAccount.Positions != null)
                {
                    PositionCollection positions = currentAccount.Positions;
                    foreach (Position currentPosition in positions)
                    {
                        if (
                            string.Compare(currentPosition.Instrument.FullName, this.Instrument.FullName,
                                StringComparison.InvariantCultureIgnoreCase) == 0)
                        {

                            Log(string.Format("Found an open {0} position of {1} shares for {2} in account {3}", 
                                currentPosition.MarketPosition.ToString().ToUpper(),
                                currentPosition.Quantity,
                                this.Instrument.FullName,
                                this.Account.Name), LogLevel.Information);

                            OpenMarketPosition = currentPosition.MarketPosition;
                            InitialOpenQuantity = currentPosition.Quantity;
                            InitialQuantityToBeClosed =
                                Convert.ToInt32(Math.Floor(currentPosition.Quantity*PositionSizePercentage/100d));

                            break;
                        }
                    }
                }
            }

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
            if (InitialOpenQuantity == null || OpenMarketPosition == null ||
                OpenMarketPosition.Value == MarketPosition.Flat)
            {
                throw new InvalidOperationException(
                    string.Format("Could not retrieve an open position for {0} in account {1}", this.Instrument.FullName,
                        this.Account.Name));
            }

            if (OpenMarketPosition.Value == MarketPosition.Long)
            {
                return OrderAction.Sell;
            }
            else if (OpenMarketPosition.Value == MarketPosition.Short)
            {
                return OrderAction.BuyToCover;
            }

            return base.GetOrderAction();
        }

        protected override void HandlePartiallyFilledQuantity(int filledQty, double avgFillPrice)
        {
            FilledQuantity = filledQty;
        }

        protected override int GetBuyQuantity()
        {
            if (InitialQuantityToBeClosed == null || OpenMarketPosition == null ||
                OpenMarketPosition.Value == MarketPosition.Flat)
            {
                throw new InvalidOperationException(
                    string.Format("Could not retrieve an open position for {0} in account {1}", this.Instrument.FullName,
                        this.Account.Name));
            }

            return InitialQuantityToBeClosed.Value - FilledQuantity;
        }


        protected override int GetSellQuantity()
        {
            if (InitialQuantityToBeClosed == null || OpenMarketPosition == null ||
                OpenMarketPosition.Value == MarketPosition.Flat)
            {
                throw new InvalidOperationException(
                    string.Format("Could not retrieve an open position for {0} in account {1}", this.Instrument.FullName,
                        this.Account.Name));
            }

            return InitialQuantityToBeClosed.Value - FilledQuantity;
        }
    }
}