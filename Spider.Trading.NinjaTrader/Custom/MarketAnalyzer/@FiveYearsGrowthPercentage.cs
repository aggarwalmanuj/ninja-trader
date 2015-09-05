// 
// Copyright (C) 2006, NinjaTrader LLC <www.ninjatrader.com>.
// NinjaTrader reserves the right to modify or overwrite this NinjaScript component with each release.
//

#region Using declarations
using System;
using System.ComponentModel;
using System.Drawing;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.MarketAnalyzer;
#endregion

// This namespace holds all market analyzer column definitions and is required. Do not change it.
namespace NinjaTrader.MarketAnalyzer
{
	/// <summary>
	/// </summary>
	public class FiveYearsGrowthPercentage : NinjaTrader.MarketAnalyzer.Column
	{
		/// <summary>
		/// This method is used to configure the market analyzer column and is called once before any event mathod is called.
		/// </summary>
		protected override void Initialize()
		{
			CalculateOnBarCloseConfigurable	= false;
			RequiresBars					= false;
		}

		/// <summary>
		/// Called on each incoming market data tick.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnFundamentalData(FundamentalDataEventArgs e)
		{
			if (e.FundamentalDataType != FundamentalDataType.FiveYearsGrowthPercentage)
				return;

			Value = e.DoubleValue;
		}
	}
}
