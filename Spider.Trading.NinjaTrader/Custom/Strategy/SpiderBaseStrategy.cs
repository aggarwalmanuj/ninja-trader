using System.Linq;
using System.Text.RegularExpressions;

#region Using declarations

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Indicator;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Strategy;
#endregion

// This namespace holds all strategies and is required. Do not change it.
namespace NinjaTrader.Strategy
{
    /// <summary>
    /// Enter long poisitons
    /// </summary>
    [Description("Base Spider Strategy")]
    public class SpiderBaseStrategy : Strategy
    {
        #region Variables
        // Wizard generated variables
        private int _fastMaPeriod = 2; // Default setting for FastMaPeriod
        private int _slowMaPeriod = 5; // Default setting for SlowMaPeriod
        private int _executionTimerInterval = 2;
		private int _atrPeriod = 14;
		private double _minAllowedSlippage = -0.05;
		private double _maxAllowedSlippage = 0.01;
        private double _oversoldStochValue = 15;
        private double _overboughtStochValue = 85;
        private int _timeSliceIntervalInMinutes = 15;
        private DateTime? _sessionBeginTime;
        private DateTime? _sessionEndTime;
        private int _stochasticsDPeriod = 3;
        private int _stochasticsKPeriod = 14;
        private int _stochasticsSmoothPeriod = 3;
        private int _minimumIntervalInMinBetweenOrderRetries = 3;
        private int _validityTriggerMinute = 30;
        private int _validityTriggerHour = 6;
        private DateTime _validityTriggerDate = DateTime.Today;
        private double _positionSizePercentage = 100;


        // User defined variables (add any user defined variables below)
        #endregion

        /// <summary>
        /// This method is used to configure the strategy and is called once before any strategy method is called.
        /// </summary>
        protected override void Initialize()
        {

            TraceOrders = true; 

            CurrentValidityDateTime = ValidityTriggerDate.Date
                .AddHours(ValidityTriggerHour)
                .AddMinutes(ValidityTriggerMinute);

            Add(PeriodType.Minute, 1);
            Add(PeriodType.Minute, ExecutionTimePeriod);

			Add(ATR(AtrPeriod));
            ATR(AtrPeriod).Plots[0].Pen.Color = Color.DarkBlue;

            SMA(FastMaPeriod).Plots[0].Pen.Color = Color.Orange;
            SMA(SlowMaPeriod).Plots[0].Pen.Color = Color.Green;

            Add(EMA(SlowMaPeriod));
            Add(EMA(FastMaPeriod));

            Add(Stochastics(StochasticsDPeriod, StochasticsKPeriod, StochasticsSmoothPeriod));

            StrategyBeginTime = DateTime.Now;

            CalculateOnBarClose = true;
        }

     

        /// <summary>
        /// Called on each bar update event (incoming tick)
        /// </summary>
        protected override void OnBarUpdate()
        {
            if (CurrentBars[0] < BarsRequired ||
                CurrentBars[1] < BarsRequired ||
                CurrentBars[2] < BarsRequired)
            {
                return;
            }

            // If everything is filled - then exit:
            if (IsStrategyOrderFilled)
                return;

            if (Bars == null || Bars.Session == null)
                return;
            

            
           
            if (BarsInProgress == 0)
            {
                if (CurrentBar > AtrPeriod)
                {
                    CurrentAtrPrice = ATR(AtrPeriod)[0];
                    LogDebugFormat("Setting ATR to {0:c} which is {1:p} of the last close price {2:c}", CurrentAtrPrice, CurrentAtrPrice/Close[0], Close[0]);
                    //LogDebugFormat("Found a last day closing price of {0:c}", LastDayClosingPrice);
                    //LogDebugFormat("Found current day opening price of {0:c}", CurrentDayOperningPrice);
                }
            }
            else
            {
                // Daily closing bar
                LastDayClosingPrice = PriorDayOHLC().Close[0];

                CurrentDayOperningPrice = CurrentDayOHL().Open[0];
            }

            if (BarsInProgress != 2)
            {
                //LogDebug("This is not the execution bar series - so stopping further processing of this bar");
                // These are the execution bars we are interested in
                return;
            }
            else
            {
                // Ensure that intraday indicators are evaluated
                GetIsNormalizedStochInBullishMode();
                GetIsNormalizedStochInBearishMode();
                GetIsEmaInBearishMode();
                GetIsEmaInBullishMode();
            }

            if (CurrentAtrPrice <= 0)
            {
                //LogDebug("Do not have the ATR price yet - so stopping further processing of this bar");
                // need the ATR to process further
                return;
            }

            if (Time[0] < CurrentValidityDateTime)
            {
                return;
            }

           

            // 1. If the order is not triggered yet - then check when is the right time to trigger the order
            if (null == CurrentOrder)
            {
                if (GetHasOrderBeenTriggered())
                {
                    QueueOrder();
                }
            }
            else 
            {
                if (IsStrategyOrderInFailedState)
                {
                    throw new InvalidOperationException("Order is in an invalid state");
                }
                else
                {
                    // 4. Partially filled? then queue up the remaining order
                    // We need to retry by adjusting the prices 
                    QueueOrder();
                }
            }
           
          



			
        }

      
        protected override void OnOrderUpdate(IOrder order)
        {
            if (order.OrderState == OrderState.Filled)
            {
                IsStrategyOrderFilled = true;
            }
            else if (order.OrderState == OrderState.PartFilled)
            {
                HandlePartiallyFilledQuantity(order.Filled, order.AvgFillPrice);

                IsStrategyOrderPartiallyFilled = true;
            }
            else if (order.OrderState == OrderState.Rejected)
            {
                Log("Order in rejected state." + GetEntrySignalName(), LogLevel.Error);
                IsStrategyOrderInFailedState = true;
            }
            else if (order.OrderState == OrderState.Unknown)
            {
                Log("Order in unknown state." + GetEntrySignalName(), LogLevel.Error);
                IsStrategyOrderInFailedState = true;
            }

            string message = string.Format("ORDER UPDATE: {0}, Status={1}", order.FromEntrySignal, order.OrderState);

            LogDebug("-----------------------");
            LogDebug(message);
            LogDebug("-----------------------");
            Log(message, LogLevel.Information);
        }

        
        #region Private/Protected Methods

        protected void LogDebugFormat(string formattedString, params object[] args)
        {
            LogDebug(string.Format(formattedString, args));
        }

        protected void LogDebug(string message)
        {
            string barTypeName = "01D";
            if (BarsInProgress == 1)
            {
                barTypeName = "01M";
            }
            if (BarsInProgress == 2)
            {
                barTypeName = string.Format("{0}M", ExecutionTimePeriod.ToString("00"));
            }
            string prefix = string.Format("[{0} | {1}]", this.Account.Name, this.Instrument.FullName);
            string datePart = string.Format("[{0:dd-MMM-yyyy h:mm:ss.fff tt}] ({1})", Time[0], barTypeName);
            const string formattedString = "{0} -- {1} -- {2}";
            Print(string.Format(formattedString, prefix, datePart, message));
        }

        /// <summary>
        /// 
        /// </summary>
        protected void QueueOrder()
        {
            if (!GetWhetherItIsOkToQueueOrderBasedOnLastEntryTimestamp())
            {
                return;
            }

            int quantity = 0;
            double price = 0;
            OrderAction ordAction = GetOrderAction();

            if (ordAction == OrderAction.Buy || ordAction == OrderAction.BuyToCover)
            {
                price = GetBuyPrice();
                quantity = GetBuyQuantity();
            }
            else
            {
                price = GetSellPrice();
                quantity = GetSellQuantity();
            }

            if (GetOrderAction() == OrderAction.Buy)
            {
                CurrentOrder = EnterLongLimit(BarsInProgress, true, quantity, price,
                    GetEntrySignalName());
            }
            else if (GetOrderAction() == OrderAction.BuyToCover)
            {
                CurrentOrder = ExitShortLimit(BarsInProgress, true, quantity, price,
                    GetExitSignalName(),
                    GetEntrySignalName());
            }
            else if (GetOrderAction() == OrderAction.SellShort)
            {
                CurrentOrder = EnterShortLimit(BarsInProgress, true, quantity, price,
                    GetEntrySignalName());
            }
            else if (GetOrderAction() == OrderAction.Sell)
            {
                CurrentOrder = ExitLongLimit(BarsInProgress, true, quantity, price,
                    GetExitSignalName(),
                    GetEntrySignalName());
            }

            OrderQueuedTime = Time[0];
            
            LogDebug("-----------------------------------");
            LogDebugFormat("ORDER QUEUED: {0} {1} shares @ {2:c}", ordAction, quantity, price);
            LogDebug("-----------------------------------");
        }

        protected bool GetWhetherItIsOkToQueueOrderBasedOnLastEntryTimestamp()
        {
            double minutesPastSinceLastOrderTry = Time[0].Subtract(OrderQueuedTime).TotalMinutes;
            double periodAllowedBetweenRetries = MinimumIntervalInMinutesBetweenOrderRetries*
                                                 GetRemainderSessionTimeFraction();

            double[] periods = new double[] {1.0d, periodAllowedBetweenRetries};
            double effectivePeriod = periods.Max();
            return (minutesPastSinceLastOrderTry > effectivePeriod);
        }


        protected virtual void HandlePartiallyFilledQuantity(int filledQuantity, double avgFillPrice)
        {

        }

        protected double GetSellPrice()
        {
            double bid = GetCurrentBid();
            double slippage = GetAllowededSlippageAmount();
            double adjusted = bid - slippage;
            LogDebugFormat("Current Bid={0:c}, Slippage Adjusted={1:c}, Diff={2:p}", bid, adjusted, slippage / bid);
            double[] all = new double[] {bid, adjusted};
            return all.Max();
        }

        protected virtual int GetSellQuantity()
        {
            return 0;
        }


        protected double GetBuyPrice()
        {
            double ask = GetCurrentAsk();
            double slippage = GetAllowededSlippageAmount();
            double adjusted = ask + slippage;
            LogDebugFormat("Current Ask={0:c}, Slippage Adjusted={1:c}, Diff={2:p}", ask, adjusted, slippage/ask);
            double[] all = new double[] { ask, adjusted };
            return all.Min();
        }

        protected virtual int GetBuyQuantity()
        {
            return 0;
        }


        protected string GetEntrySignalName()
        {
            return string.Format("S.OPEN.{1}.{0}", GetOrderAction(), this.Instrument.FullName);
        }

        protected string GetExitSignalName()
        {
            return string.Format("S.CLOSE.{1}.{0}", GetOrderAction(), this.Instrument.FullName);
        }

        protected double GetAllowededSlippageAmount()
        {
            if (MinAllowedSlippage > MaxAllowedSlippage)
                throw new ArithmeticException(
                    string.Format("Min Allowed Slippage {0} cannot be more than Max Allowed Slippage {1}",
                        MinAllowedSlippage, MaxAllowedSlippage));

            if (CurrentAtrPrice <= 0)
                throw new ArgumentOutOfRangeException("CurrentAtrPrice", "Atr price must be a positive number");

            double totalSlippageGapAllowedToBeConsumedInTheSession = GetTotalSlippageGapAllowedToBeConsumedInTheSession();
            double slippageLeftToBeConsumed = totalSlippageGapAllowedToBeConsumedInTheSession*
                                              GetRemainderSessionTimeFraction();
            double allowedSlippageFraction = MaxAllowedSlippage - slippageLeftToBeConsumed;
            double slippageAllowedAmount = allowedSlippageFraction*CurrentAtrPrice;

            LogDebugFormat("Slippage: Total Allowed={0}, Left To Consumed={1}, Slippage Fraction Available={2}, Amount Available For Slippage={3:c}",
                totalSlippageGapAllowedToBeConsumedInTheSession,
                slippageLeftToBeConsumed,
                allowedSlippageFraction,
                slippageAllowedAmount);

            return slippageAllowedAmount;
        }

        protected double GetTotalSlippageGapAllowedToBeConsumedInTheSession()
        {
            return MaxAllowedSlippage - MinAllowedSlippage;
        }

        protected bool GetHasOrderBeenTriggered()
        {
            if (IsOrderTriggered.HasValue && IsOrderTriggered.Value)
            {
                // Once the order has been triggered - these is no going back
                return IsOrderTriggered.Value;
            }

            bool isOrderTriggeredBasedOnIndicators = false;
            bool isOrderTriggeredBasedOnTime = false;
            OrderAction orderAction = GetOrderAction();

            if (orderAction == OrderAction.Buy || orderAction == OrderAction.BuyToCover)
            {
                // We are buying - check for bullish signs
                isOrderTriggeredBasedOnIndicators = GetIsEmaInBullishMode() && GetIsNormalizedStochInBullishMode();
            }
            else
            {
                // We are selling - check for bullish signs
                isOrderTriggeredBasedOnIndicators = GetIsEmaInBearishMode() && GetIsNormalizedStochInBearishMode();
            }
            
            isOrderTriggeredBasedOnTime = HasTimeTriggerFired();

            if (isOrderTriggeredBasedOnIndicators)
            {
                LogDebug("Order triggers have been fired based on INDICATORS.");
            }

            if (isOrderTriggeredBasedOnTime)
            {
                LogDebug("Order triggers have been fired based on TIME.");
            }

            IsOrderTriggered = isOrderTriggeredBasedOnIndicators || isOrderTriggeredBasedOnTime;

            return IsOrderTriggered.Value;
        }

        /// <summary>
        /// Very important to override this method in 
        /// inherited classes to ensure proper order type is executed
        /// </summary>
        /// <returns></returns>
        protected virtual OrderAction GetOrderAction()
        {
            return OrderAction.Buy;
        }

        protected bool GetIsNormalizedStochInBearishMode()
        {
            double currentKValue =
                Stochastics(BarsArray[2], StochasticsDPeriod, StochasticsKPeriod, StochasticsSmoothPeriod).K[0];
            double currentDValue =
                Stochastics(BarsArray[2], StochasticsDPeriod, StochasticsKPeriod, StochasticsSmoothPeriod).D[0];

            LogDebugFormat("Current stochastics values: K={0}, D={1}", currentKValue, currentDValue);

            if (currentKValue <= 5)
            {
                return true;
            }

            return GetIsStochInBearishMode();
        }

        protected bool GetIsNormalizedStochInBullishMode()
        {
            // If it is a high stochastics - then it is bullish regarless
            double currentKValue =
                Stochastics(BarsArray[2], StochasticsDPeriod, StochasticsKPeriod, StochasticsSmoothPeriod).K[0];
            double currentDValue =
                Stochastics(BarsArray[2], StochasticsDPeriod, StochasticsKPeriod, StochasticsSmoothPeriod).D[0];

            LogDebugFormat("Current stochastics values: K={0}, D={1}", currentKValue, currentDValue);

            if (currentKValue >= 95)
            {
                return true;
            }

            return GetIsStochInBullishMode();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected bool GetIsStochInBullishMode()
        {
            if (!IsStochCrossUp && 
                CrossAbove(
                Stochastics(BarsArray[2], StochasticsDPeriod, StochasticsKPeriod, StochasticsSmoothPeriod).K,
                Stochastics(BarsArray[2], StochasticsDPeriod, StochasticsKPeriod, StochasticsSmoothPeriod).D, 1)
                )
            {

                LogDebugFormat("********* STOCHASTICS CROSSED UP *********");

                IsStochCrossUp = true;
                IsStochCrossDown = false;
            }

            return IsStochCrossUp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected bool GetIsStochInBearishMode()
        {
            if (!IsStochCrossDown && 
                CrossBelow(
                Stochastics(BarsArray[2], StochasticsDPeriod, StochasticsKPeriod, StochasticsSmoothPeriod).K,
                Stochastics(BarsArray[2], StochasticsDPeriod, StochasticsKPeriod, StochasticsSmoothPeriod).D, 1)
                )
            {
                LogDebugFormat("********* STOCHASTICS CROSSED DOWN *********");

                IsStochCrossDown = true;
                IsStochCrossUp = false;
            }

            return IsStochCrossDown;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected bool GetIsEmaInBullishMode()
        {
            if (!IsEmaCrossUp && 
                CrossAbove(
                EMA(BarsArray[2], FastMaPeriod), 
                EMA(BarsArray[2], SlowMaPeriod), 1)
                )
            {
                LogDebugFormat("********* EMA CROSSED UP *********");

                IsEmaCrossUp = true;
                IsEmaCrossDown = false;
            }

            return IsEmaCrossUp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected bool GetIsEmaInBearishMode()
        {
            if (!IsEmaCrossDown &&
                CrossBelow(
                EMA(BarsArray[2], FastMaPeriod), 
                EMA(BarsArray[2], SlowMaPeriod), 1)
                )
            {
                LogDebugFormat("********* EMA CROSSED DOWN *********");

                IsEmaCrossDown = true;
                IsEmaCrossUp = false;
            }

            return IsEmaCrossDown;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected bool HasTimeTriggerFired()
        {
            // If there ar elast 30-40 minutes left in the session - then fire the 
            // time based trigger
            return GetRemainderSessionTimeFraction() <= 0.1d;
        }


        /// <summary>
        /// This is used for several calculations where certain thresholds
        /// need to be loosened up as time passes in the session
        /// </summary>
        /// <returns></returns>
        protected double GetRemainderSessionTimeFraction()
        {
            if (_sessionBeginTime == null || _sessionEndTime == null)
            {
                DateTime tempBegin;
                DateTime tempEnd;
                Bars.Session.GetNextBeginEnd(BarsArray[0], 0, out  tempBegin, out tempEnd);
                _sessionBeginTime = tempBegin;
                _sessionEndTime = tempEnd;
            }

            // Assuming we want to execute at least 30 minutes before the close 
            double totalNumberOfMinutes = Math.Abs(_sessionEndTime.Value.Subtract(_sessionBeginTime.Value).TotalMinutes) - 30;
            TotalNumberOfSlicesAvailableInSession = totalNumberOfMinutes / TimeSliceIntervalInMinutes;

            DateTime sessionDate = Time[0].Date;
            DateTime startOfTodaySession =
                sessionDate.AddHours(_sessionBeginTime.Value.Hour).AddMinutes(_sessionBeginTime.Value.Minute);

            double numberOfMinutesElapsedSinceBeginningOfSession =
                Math.Abs(Time[0].Subtract(startOfTodaySession).TotalMinutes);

            double remainder = totalNumberOfMinutes - numberOfMinutesElapsedSinceBeginningOfSession;
            double fractionRemainder = remainder / totalNumberOfMinutes;

            LogDebugFormat("{0:F2} minutes passed in the session with {1:F2} minutes ({2:p}) remaining.",
                numberOfMinutesElapsedSinceBeginningOfSession, remainder, fractionRemainder);

            return fractionRemainder;
        } 
        #endregion

        #region Properties

        #region Private/Protected Properties

        protected DateTime CurrentValidityDateTime { get; set; }

        protected double CurrentAtrPrice { get; set; }


        protected double LastDayClosingPrice { get; set; }


        protected double CurrentDayOperningPrice { get; set; }

        protected bool IsStrategyOrderPartiallyFilled { get; set; }

        protected bool IsStrategyOrderFilled { get; set; }

        protected bool IsStrategyOrderInFailedState { get; set; }

        protected DateTime OrderQueuedTime { get; set; }

        protected DateTime StrategyBeginTime { get; set; }

        protected double TotalNumberOfSlicesAvailableInSession { get; set; }

        protected bool IsStochCrossUp { get; set; }

        protected bool IsStochCrossDown { get; set; }

        protected bool IsEmaCrossUp { get; set; }

        protected bool IsEmaCrossDown { get; set; }

        protected bool? IsOrderTriggered { get; set; }

        protected IOrder CurrentOrder { get; set; }

        #endregion


        #region Position Sizing Management

        [Description("Position Size Percentage")]
        [GridCategory("Position Sizing Management")]
        public double PositionSizePercentage
        {
            get { return _positionSizePercentage; }
            set { _positionSizePercentage = Math.Max(0, value); }
        }

        #endregion

        #region Trade Management

        [Description("Execution Time Period")]
        [GridCategory("Trade Management")]
        public int ExecutionTimePeriod
        {
            get { return _executionTimerInterval; }
            set { _executionTimerInterval = Math.Max(1, value); }
        }


        [Description("Minimum Interval In Minutes Between Order Retries")]
        [GridCategory("Trade Management")]
        public int MinimumIntervalInMinutesBetweenOrderRetries
        {
            get { return _minimumIntervalInMinBetweenOrderRetries; }
            set { _minimumIntervalInMinBetweenOrderRetries = Math.Max(1, value); }
        }

        [Description("Validity Trigger Date")]
        [GridCategory("Trade Management")]
        public DateTime ValidityTriggerDate
        {
            get { return _validityTriggerDate; }
            set { _validityTriggerDate = value; }
        }

        [Description("Validity Trigger Hour")]
        [GridCategory("Trade Management")]
        public int ValidityTriggerHour
        {
            get { return _validityTriggerHour; }
            set { _validityTriggerHour = Math.Max(1, value); }
        }

        [Description("Validity Trigger Minute")]
        [GridCategory("Trade Management")]
        public int ValidityTriggerMinute
        {
            get { return _validityTriggerMinute; }
            set { _validityTriggerMinute = Math.Max(1, value); }
        }

        #endregion

        #region Time Slice Management

        [Description("Time Slice Interval In Minutes")]
        [GridCategory("Time Slice Management")]
        public int TimeSliceIntervalInMinutes
        {
            get { return _timeSliceIntervalInMinutes; }
            set { _timeSliceIntervalInMinutes = Math.Max(1, value); }
        }

        #endregion

        #region Indicator Settings

        [Description("Stochastics D Period On Execution Time Frame")]
        [GridCategory("Indicator Settings")]
        public int StochasticsDPeriod
        {
            get { return _stochasticsDPeriod; }
            set { _stochasticsDPeriod = Math.Max(1, value); }
        }

        [Description("Stochastics K Period On Execution Time Frame")]
        [GridCategory("Indicator Settings")]
        public int StochasticsKPeriod
        {
            get { return _stochasticsKPeriod; }
            set { _stochasticsKPeriod  = Math.Max(1, value); }
        }

        [Description("Stochastics Smooth Period On Execution Time Frame")]
        [GridCategory("Indicator Settings")]
        public int StochasticsSmoothPeriod
        {
            get { return _stochasticsSmoothPeriod; }
            set { _stochasticsSmoothPeriod  = Math.Max(1, value);}
        }

        [Description("Fast Ema Period On Execution Time Frame")]
        [GridCategory("Indicator Settings")]
        public int FastMaPeriod
        {
            get { return _fastMaPeriod; }
            set { _fastMaPeriod = Math.Max(1, value); }
        }

        [Description("Slow Ema Period On Execution Time Frame")]
        [GridCategory("Indicator Settings")]
        public int SlowMaPeriod
        {
            get { return _slowMaPeriod; }
            set { _slowMaPeriod = Math.Max(1, value); }
        }

        [Description("Oversold RSI Threshold On Execution Time Frame")]
        [GridCategory("Indicator Settings")]
        public double OversoldRsiThreshold
        {
            get { return _oversoldStochValue; }
            set { _oversoldStochValue = Math.Max(1, value); }
        }

        [Description("Overbought RSI Threshold On Execution Time Frame")]
        [GridCategory("Indicator Settings")]
        public double OverboughtRsiThreshold
        {
            get { return _overboughtStochValue; }
            set { _overboughtStochValue = Math.Max(1, value); }
        }

        #endregion

        #region Slippage Management

        [Description("Atr Period")]
		[GridCategory("Slippage Management")]
        public int AtrPeriod
		{
            get { return _atrPeriod; }
            set { _atrPeriod = Math.Max(1, value); }
        }
		
        [Description("Min Allowed Slippage")]
		[GridCategory("Slippage Management")]
        public double MinAllowedSlippage
		{
            get { return _minAllowedSlippage; }
            set { _minAllowedSlippage = Math.Max(-1, value); }
        }
		
        [Description("Max Allowed Slippage")]
		[GridCategory("Slippage Management")]
        public double MaxAllowedSlippage
		{
            get { return _maxAllowedSlippage; }
            set { _maxAllowedSlippage = Math.Max(-1, value); }
        }

        #endregion
		
        #endregion
		
		
		
    }
}
