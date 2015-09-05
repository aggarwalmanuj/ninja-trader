using System;
using System.ComponentModel;
using NinjaTrader.Cbi;
using NinjaTrader.Data;

namespace NinjaTrader.Strategy
{
    [Description("Base Spider Opening Strategy")]
    public class SpiderBaseOpeningStrategy : SpiderBaseStrategy
    {
        private int _numberOfPortfolioPositions = 5;
        private double _totalPortfolioAmount = 10000;

        protected double? InitialPositionAmount { get; set; }

        protected double FilledAmount { get; set; }

        #region Position Sizing Management

        [Description("Total Portfolio Amount")]
        [GridCategory("Position Sizing Management")]
        public double TotalPortfolioAmount
        {
            get { return _totalPortfolioAmount; }
            set { _totalPortfolioAmount = Math.Max(1, value); }
        }

        [Description("Number Of Portfolio Positions In Each Sub Portfolio")]
        [GridCategory("Position Sizing Management")]
        public int NumberOfPortfolioPositions
        {
            get { return _numberOfPortfolioPositions; }
            set { _numberOfPortfolioPositions = Math.Max(1, value); }
        }

        #endregion

        protected override void Initialize()
        {
            InitialPositionAmount = ((TotalPortfolioAmount/NumberOfPortfolioPositions)*PositionSizePercentage/100d);

            Log(string.Format("Calculated OPENING position size amount={0:c} to {1} for {2}", InitialPositionAmount, GetOrderAction(), this.Instrument.FullName), LogLevel.Information);

            base.Initialize();
        }

        protected override void HandlePartiallyFilledQuantity(int filledQuantity, double avgFillPrice)
        {
            LogDebugFormat("ORDER PARTIALLY FILLED: Filled Qty={0}, Avg. Fill Price={1:c}", filledQuantity, avgFillPrice);
            FilledAmount =  (filledQuantity*avgFillPrice);
            if (FilledAmount > InitialPositionAmount.Value)
            {
                Log("ORDER OVERFILLED", LogLevel.Warning);
                LogDebug("--- ORDER OVER FILLED ---");
            }
        }

        protected override int GetBuyQuantity()
        {
            var amountRemaining = GetAmountRemaining();
            double qtyRequired = amountRemaining/GetBuyPrice();
            return Convert.ToInt32(Math.Floor(qtyRequired));
        }

        protected override int GetSellQuantity()
        {
            var amountRemaining = GetAmountRemaining();
            double qtyRequired = amountRemaining / GetSellPrice();
            return Convert.ToInt32(Math.Floor(qtyRequired));
        }

        protected double GetAmountRemaining()
        {
            if (InitialPositionAmount == null)
            {
                throw new InvalidOperationException(
                    string.Format("Could not retrieve an opening amount for {0} in account {1}", this.Instrument.FullName,
                        this.Account.Name));
            }

            double amountRemaining = InitialPositionAmount.Value - FilledAmount;
            LogDebugFormat("Amount remaining to open: {0:c}", amountRemaining);
            return amountRemaining;
        }
    }
}