
//
//+------------------------------------------------------------------+
//|                                                  Smart Grid      |
//|                                      Copyright 2014, MD SAIF     |
//|                                   http://www.facebook.com/cls.fx |
//+------------------------------------------------------------------+
//-Grid trader cBot based on Bar-Time & Trend. For range market & 15 minute TimeFrame is best.

using System;
using cAlgo.API;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SmartGrid_MAX : Robot
    {
        private bool _accountIsOutOfMoney;
        private int _openTradeResult;

        private readonly string Label = "SmartGridMax";
        private DateTime _lastBuyTradeTime;
        private DateTime _lastSellTradeTime;

        private bool _stopBuy = false, _stopSell = false;
        private int _initialPipStep;
        private int _buyPipStep, _sellPipStep;
        private bool _evaluteOnBar;
        private bool _justForSofferenceSide;
        private TradeType _sideInSofference;

        [Parameter("Buy", DefaultValue = true)]
        public bool Buy { get; set; }

        [Parameter("Sell", DefaultValue = true)]
        public bool Sell { get; set; }

        [Parameter("Pip Step", DefaultValue = 10, MinValue = 1)]
        public int PipStep { get; set; }

        [Parameter("First Volume", DefaultValue = 1000, MinValue = 1, Step = 1)]
        public int FirstVolume { get; set; }

        [Parameter("Volume Exponent", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0)]
        public double VolumeExponent { get; set; }

        [Parameter("Max Spread", DefaultValue = 3.0)]
        public double MaxSpread { get; set; }

        [Parameter("Max Position", DefaultValue = 10)]
        public int MaxPosition { get; set; }

        [Parameter("Average TP", DefaultValue = 3, MinValue = 1)]
        public int AverageTakeProfit { get; set; }

        [Parameter("Evalute PipsStep On New Bar", DefaultValue = false, Group = "Dynamic Pip Step")]
        public bool EvalutePipsStepOnNewBar { get; set; }

        [Parameter("In Sofference Side Only", DefaultValue = false, Group = "Dynamic Pip Step")]
        public bool JustForSofferenceSide { get; set; }

        [Parameter("Run In Optimization", DefaultValue = false, Group = "System")]
        public bool RunInOptimization { get; set; }

        private double CurrentSpread
        {
            get { return (Symbol.Ask - Symbol.Bid) / Symbol.PipSize; }
        }
        private static Color GetColorWithOpacity(Color baseColor, decimal opacity)
        {
            var alpha = (int)Math.Round(byte.MaxValue * opacity, MidpointRounding.AwayFromZero);
            return Color.FromArgb(alpha, baseColor);
        }
        public static Style CreatePanelBackgroundStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#303060"), 0.25m), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, GetColorWithOpacity(Color.FromHex("#000030"), 0.25m), ControlState.LightTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#3C3C3C"), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#C3C3C3"), ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, new Thickness(1));

            return style;
        }
        protected override void OnStart()
        {

            if (!RunInOptimization)
            {

                CheckBox StopBuyNextTp = new CheckBox 
                {
                    Text = "STOP BUY NEXT TP",
                    IsChecked = !Buy,
                    Margin = "5 5 5 5",
                    ForegroundColor = Color.LightGreen,
                    BackgroundColor = Color.Black
                };
                StopBuyNextTp.Click += OnCheckChange;
                CheckBox StopSellNextTp = new CheckBox 
                {
                    Text = "STOP SELL NEXT TP",
                    IsChecked = !Sell,
                    Margin = "5 5 5 5",
                    ForegroundColor = Color.Red,
                    BackgroundColor = Color.Black
                };
                StopSellNextTp.Click += OnCheckChange;

                CheckBox StopBuy = new CheckBox 
                {
                    Text = "STOP BUY",
                    IsChecked = !Buy,
                    Margin = "5 5 5 5",
                    ForegroundColor = Color.LightGreen,
                    BackgroundColor = Color.Black
                };
                StopBuy.Click += OnCheckChange;
                CheckBox StopSell = new CheckBox 
                {
                    Text = "STOP SELL",
                    IsChecked = !Sell,
                    Margin = "5 5 5 5",
                    ForegroundColor = Color.Red,
                    BackgroundColor = Color.Black
                };
                StopSell.Click += OnCheckChange;


                CheckBox EvaluteOnNewBar = new CheckBox 
                {
                    Text = "NEW POS ON BAR",
                    IsChecked = EvalutePipsStepOnNewBar,
                    Margin = "5 5 5 5",
                    ForegroundColor = Color.Yellow,
                    BackgroundColor = Color.Black
                };
                EvaluteOnNewBar.Click += OnCheckChange;

                CheckBox ChkJustForSofferenceSide = new CheckBox 
                {
                    Text = "OPTIMIZE RETRACEMENT",
                    IsChecked = EvalutePipsStepOnNewBar,
                    Margin = "5 5 5 5",
                    ForegroundColor = Color.Yellow,
                    BackgroundColor = Color.Black
                };
                ChkJustForSofferenceSide.Click += OnCheckChange;


                StackPanel option = new StackPanel 
                {
                    Orientation = Orientation.Vertical,
                    Margin = "10 10 10 10",
                    BackgroundColor = Color.Transparent,
                    Style = CreatePanelBackgroundStyle()
                };
                Border OptionBorder = new Border 
                {
                    Width = 200,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Child = option,
                    Style = CreatePanelBackgroundStyle(),
                    Margin = "20 20 20 20"

                };
                option.AddChild(StopBuy);
                option.AddChild(StopSell);
                option.AddChild(StopBuyNextTp);
                option.AddChild(StopSellNextTp);
                option.AddChild(EvaluteOnNewBar);
                option.AddChild(ChkJustForSofferenceSide);

                Chart.AddControl(OptionBorder);

                Positions.Closed += OnPositionClosed;
            }
            _initialPipStep = PipStep;
            _buyPipStep = PipStep;
            _sellPipStep = PipStep;
            _evaluteOnBar = EvalutePipsStepOnNewBar;
            _justForSofferenceSide = JustForSofferenceSide;
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == Label && args.Position.SymbolName == Symbol.Name && args.Reason == PositionCloseReason.TakeProfit)
            {
                if (args.Position.TradeType == TradeType.Buy)
                {
                    _buyPipStep = _initialPipStep;
                    _sideInSofference = TradeType.Sell;
                }
                else if (args.Position.TradeType == TradeType.Sell)
                {
                    _sellPipStep = _initialPipStep;
                    _sideInSofference = TradeType.Buy;
                }
            }
        }
        private void OnCheckChange(CheckBoxEventArgs e)
        {

            if (e.CheckBox.Text == "STOP BUY NEXT TP")
            {

                if (e.CheckBox.IsChecked == true)
                {
                    Buy = false;
                }
                else
                {
                    Buy = true;
                }
            }
            else if (e.CheckBox.Text == "STOP SELL NEXT TP")
            {
                if (e.CheckBox.IsChecked == true)
                {
                    Sell = false;
                }
                else
                {
                    Sell = true;
                }
            }
            else if (e.CheckBox.Text == "STOP BUY")
            {
                if (e.CheckBox.IsChecked == true)
                {
                    _stopBuy = true;
                }
                else
                {
                    _stopBuy = false;
                }
            }
            else if (e.CheckBox.Text == "STOP SELL")
            {
                if (e.CheckBox.IsChecked == true)
                {
                    _stopSell = true;
                }
                else
                {
                    _stopSell = false;
                }
            }
            else if (e.CheckBox.Text == "NEW POS ON BAR")
            {
                if (e.CheckBox.IsChecked == true)
                {
                    _evaluteOnBar = true;
                }
                else
                {
                    _evaluteOnBar = false;
                }
            }
            else if (e.CheckBox.Text == "OPTIMIZE RETRACEMENT")
            {
                if (e.CheckBox.IsChecked == true)
                {
                    _justForSofferenceSide = true;
                }
                else
                {
                    _justForSofferenceSide = false;
                }
            }
        }
        protected override void OnError(Error error)
        {
            if (error.Code == ErrorCode.NoMoney)
            {
                _accountIsOutOfMoney = true;
                Print("opening stopped because: not enough money");
            }
        }

        protected override void OnTick()
        {
            if (CountOfTradesOfType(TradeType.Buy) > 0)
                AdjustBuyPositionTakeProfits(CalculateAveragePositionPrice(TradeType.Buy), AverageTakeProfit);
            if (CountOfTradesOfType(TradeType.Sell) > 0)
                AdjustSellPositionTakeProfits(CalculateAveragePositionPrice(TradeType.Sell), AverageTakeProfit);

            if (CurrentSpread <= MaxSpread && !_accountIsOutOfMoney)
            {
                FirstOpening();

                //if (!_evaluteOnBar) {
                ProcessTrades(true);
                //}

            }

            if (!RunInOptimization)
            {
                DisplayStatusOnChart();
            }

        }

        protected override void OnBar()
        {

            if (CurrentSpread <= MaxSpread && !_accountIsOutOfMoney && _evaluteOnBar)
            {
                ProcessTrades();
            }


            RefreshData();
        }

        protected override void OnStop()
        {
            if (!RunInOptimization)
            {
                Chart.RemoveAllObjects();
            }
        }

        private void FirstOpening()
        {
            if (!_stopBuy && Buy && CountOfTradesOfType(TradeType.Buy) == 0 && MarketSeries.Close.Last(1) > MarketSeries.Close.Last(2))
            {
                _openTradeResult = OrderSend(TradeType.Buy, LimitVolume(FirstVolume));
                if (_openTradeResult > 0)
                    _lastBuyTradeTime = MarketSeries.OpenTime.Last(0);
                else
                    Print("First BUY openning error at: ", Symbol.Ask, "Error Type: ", LastResult.Error);
            }
            if (!_stopSell && Sell && CountOfTradesOfType(TradeType.Sell) == 0 && MarketSeries.Close.Last(2) > MarketSeries.Close.Last(1))
            {
                _openTradeResult = OrderSend(TradeType.Sell, LimitVolume(FirstVolume));
                if (_openTradeResult > 0)
                    _lastSellTradeTime = MarketSeries.OpenTime.Last(0);
                else
                    Print("First SELL opening error at: ", Symbol.Bid, "Error Type: ", LastResult.Error);
            }
        }
        private void ProcessTrades(bool fromTick = false)
        {
            if (_evaluteOnBar && fromTick && !_justForSofferenceSide)
                return;
            if (CountOfTradesOfType(TradeType.Buy) > 0)
            {
                if (!_stopBuy && Math.Round(Symbol.Ask, Symbol.Digits) < Math.Round(FindLowestPositionPrice(TradeType.Buy) - _buyPipStep * Symbol.PipSize, Symbol.Digits) && _lastBuyTradeTime != MarketSeries.OpenTime.Last(0))
                {

                    if (_evaluteOnBar && fromTick && _justForSofferenceSide && _sideInSofference == TradeType.Buy)
                        return;


                    double delta = Math.Round(FindLowestPositionPrice(TradeType.Buy), Symbol.Digits) - Math.Round(Symbol.Ask, Symbol.Digits);
                    int pipsdelta = Convert.ToInt32(delta / Symbol.PipSize);
                    if ((_evaluteOnBar && !_justForSofferenceSide) || (_evaluteOnBar && _justForSofferenceSide && _sideInSofference == TradeType.Buy))
                    {
                        _buyPipStep = pipsdelta;
                    }

                    var calculatedVolume = CalculateVolume(TradeType.Buy);
                    _openTradeResult = OrderSend(TradeType.Buy, LimitVolume(calculatedVolume));
                    if (_openTradeResult > 0)
                        _lastBuyTradeTime = MarketSeries.OpenTime.Last(0);
                    else
                        Print("Next BUY opening error at: ", Symbol.Ask, "Error Type: ", LastResult.Error);
                }
            }
            if (CountOfTradesOfType(TradeType.Sell) > 0)
            {
                if (!_stopSell && Math.Round(Symbol.Bid, Symbol.Digits) > Math.Round(FindHighestPositionPrice(TradeType.Sell) + _sellPipStep * Symbol.PipSize, Symbol.Digits) && _lastSellTradeTime != MarketSeries.OpenTime.Last(0))
                {

                    if (_evaluteOnBar && fromTick && _justForSofferenceSide && _sideInSofference == TradeType.Sell)
                        return;

                    double delta = Math.Round(Symbol.Ask, Symbol.Digits) - Math.Round(FindHighestPositionPrice(TradeType.Sell), Symbol.Digits);
                    int pipsdelta = Convert.ToInt32(delta / Symbol.PipSize);
                    //if (_evaluteOnBar)
                    if ((_evaluteOnBar && !_justForSofferenceSide) || (_evaluteOnBar && _justForSofferenceSide && _sideInSofference == TradeType.Sell))
                    {
                        _sellPipStep = pipsdelta;
                    }

                    var calculatedVolume = CalculateVolume(TradeType.Sell);
                    _openTradeResult = OrderSend(TradeType.Sell, LimitVolume(calculatedVolume));
                    if (_openTradeResult > 0)
                        _lastSellTradeTime = MarketSeries.OpenTime.Last(0);
                    else
                        Print("Next SELL opening error at: ", Symbol.Bid, "Error Type: ", LastResult.Error);
                }
            }
        }
        private int OrderSend(TradeType tradeType, double volumeToUse)
        {
            var returnResult = 0;
            if (volumeToUse > 0)
            {
                if ((CountOfTradesOfType(tradeType) >= MaxPosition))
                    return returnResult;

                var result = ExecuteMarketOrder(tradeType, Symbol, volumeToUse, Label, 0, 0, 0, "smart_grid");

                if (result.IsSuccessful)
                {
                    Print(tradeType, "Opened at: ", result.Position.EntryPrice);
                    returnResult = 1;
                }
                else
                    Print(tradeType, "Openning Error: ", result.Error);
            }
            else
                Print("Volume calculation error: Calculated Volume is: ", volumeToUse);
            return returnResult;
        }

        private void AdjustBuyPositionTakeProfits(double averageBuyPositionPrice, int averageTakeProfit)
        {
            foreach (var buyPosition in Positions)
            {
                if (buyPosition.Label == Label && buyPosition.SymbolCode == Symbol.Code)
                {
                    if (buyPosition.TradeType == TradeType.Buy)
                    {
                        double? calculatedTakeProfit = Math.Round(averageBuyPositionPrice + averageTakeProfit * Symbol.PipSize, Symbol.Digits);
                        if (buyPosition.TakeProfit != calculatedTakeProfit)
                            ModifyPosition(buyPosition, buyPosition.StopLoss, calculatedTakeProfit);
                    }
                }
            }
        }

        private void AdjustSellPositionTakeProfits(double averageSellPositionPrice, int averageTakeProfit)
        {
            foreach (var sellPosition in Positions)
            {
                if (sellPosition.Label == Label && sellPosition.SymbolCode == Symbol.Code)
                {
                    if (sellPosition.TradeType == TradeType.Sell)
                    {
                        double? calculatedTakeProfit = Math.Round(averageSellPositionPrice - averageTakeProfit * Symbol.PipSize, Symbol.Digits);
                        if (sellPosition.TakeProfit != calculatedTakeProfit)
                            ModifyPosition(sellPosition, sellPosition.StopLoss, calculatedTakeProfit);
                    }
                }
            }
        }

        private void DisplayStatusOnChart()
        {
            if (CountOfTradesOfType(TradeType.Buy) > 1)
            {
                var y = CalculateAveragePositionPrice(TradeType.Buy);
                // ChartObjects.DrawHorizontalLine("bpoint", y, Colors.Yellow, 2, LineStyle.Dots);
                Chart.DrawHorizontalLine("bpoint", y, Color.Yellow, 2, LineStyle.Dots);
            }
            else
                //ChartObjects.RemoveObject("bpoint");
                Chart.RemoveObject("bpoint");
            if (CountOfTradesOfType(TradeType.Sell) > 1)
            {
                var z = CalculateAveragePositionPrice(TradeType.Sell);
                //ChartObjects.DrawHorizontalLine("spoint", z, Colors.HotPink, 2, LineStyle.Dots);
                Chart.DrawHorizontalLine("spoint", z, Color.HotPink, 2, LineStyle.Dots);
            }
            else
                //ChartObjects.RemoveObject("spoint");
                Chart.RemoveObject("spoint");
            //ChartObjects.DrawText("pan", GenerateStatusText(), StaticPosition.TopLeft, Colors.Tomato);
            Chart.DrawStaticText("pan", GenerateStatusText(), VerticalAlignment.Top, HorizontalAlignment.Left, Color.Tomato);
        }

        private string GenerateStatusText()
        {
            var statusText = "";
            var buyPositions = "";
            var sellPositions = "";
            var buyPipsStep = "";
            var sellPipsStep = "";
            var spread = "";
            var buyDistance = "";
            var sellDistance = "";
            var sofferenceSide = "";
            spread = "\nSpread = " + Math.Round(CurrentSpread, 1);
            sofferenceSide = "\nSofference Side = " + _sideInSofference.ToString();
            buyPositions = "\nBuy Positions = " + CountOfTradesOfType(TradeType.Buy);
            sellPositions = "\nSell Positions = " + CountOfTradesOfType(TradeType.Sell);
            buyPipsStep = "\nBuy Pips Step = " + _buyPipStep.ToString();
            sellPipsStep = "\nSell Pips Step = " + _sellPipStep.ToString();

            if (CountOfTradesOfType(TradeType.Buy) > 0)
            {
                var averageBuyFromCurrent = Math.Round((CalculateAveragePositionPrice(TradeType.Buy) - Symbol.Bid) / Symbol.PipSize, 1);
                buyDistance = "\nBuy Target Away = " + averageBuyFromCurrent;
            }
            if (CountOfTradesOfType(TradeType.Sell) > 0)
            {
                var averageSellFromCurrent = Math.Round((Symbol.Ask - CalculateAveragePositionPrice(TradeType.Sell)) / Symbol.PipSize, 1);
                sellDistance = "\nSell Target Away = " + averageSellFromCurrent;
            }
            if (CurrentSpread > MaxSpread)
                statusText = "MAX SPREAD EXCEED";
            else
                statusText = "Smart Grid" + spread + buyPositions + sellPositions + buyPipsStep + sellPipsStep + sofferenceSide;
            return (statusText);
        }



        private int CountOfTradesOfType(TradeType tradeType)
        {
            var tradeCount = 0;

            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == tradeType)
                        tradeCount++;
                }
            }

            return tradeCount;
        }

        private double CalculateAveragePositionPrice(TradeType tradeType)
        {
            double result = 0;
            double averagePrice = 0;
            double count = 0;


            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == tradeType)
                    {
                        averagePrice += position.EntryPrice * position.VolumeInUnits;
                        count += position.VolumeInUnits;
                    }
                }

            }

            if (averagePrice > 0 && count > 0)
                result = Math.Round(averagePrice / count, Symbol.Digits);
            return result;
        }

        private double FindLowestPositionPrice(TradeType tradeType)
        {
            double lowestPrice = 0;

            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == tradeType)
                    {
                        if (lowestPrice == 0)
                        {
                            lowestPrice = position.EntryPrice;
                            continue;
                        }
                        if (position.EntryPrice < lowestPrice)
                            lowestPrice = position.EntryPrice;
                    }
                }
            }

            return lowestPrice;
        }

        private double FindHighestPositionPrice(TradeType tradeType)
        {
            double highestPrice = 0;

            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == tradeType)
                    {
                        if (highestPrice == 0)
                        {
                            highestPrice = position.EntryPrice;
                            continue;
                        }
                        if (position.EntryPrice > highestPrice)
                            highestPrice = position.EntryPrice;
                    }
                }
            }

            return highestPrice;
        }

        private double FindPriceOfMostRecentPositionId(TradeType tradeType)
        {
            double price = 0;
            var highestPositionId = 0;

            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == tradeType)
                    {
                        if (highestPositionId == 0 || highestPositionId > position.Id)
                        {
                            price = position.EntryPrice;
                            highestPositionId = position.Id;
                        }
                    }
                }
            }

            return price;
        }

        private double GetMostRecentPositionVolume(TradeType tradeType)
        {
            double mostRecentVolume = 0;
            var highestPositionId = 0;

            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == tradeType)
                    {
                        if (highestPositionId == 0 || highestPositionId > position.Id)
                        {
                            mostRecentVolume = position.VolumeInUnits;
                            highestPositionId = position.Id;
                        }
                    }
                }
            }

            return mostRecentVolume;
        }

        private int CountNumberOfPositionsOfType(TradeType tradeType)
        {
            var mostRecentPrice = FindPriceOfMostRecentPositionId(tradeType);
            var numberOfPositionsOfType = 0;

            foreach (var position in Positions)
            {
                if (position.Label == Label && position.SymbolCode == Symbol.Code)
                {
                    if (position.TradeType == tradeType && tradeType == TradeType.Buy)
                    {
                        if (Math.Round(position.EntryPrice, Symbol.Digits) <= Math.Round(mostRecentPrice, Symbol.Digits))
                            numberOfPositionsOfType++;
                    }
                    if (position.TradeType == tradeType && tradeType == TradeType.Sell)
                    {
                        if (Math.Round(position.EntryPrice, Symbol.Digits) >= Math.Round(mostRecentPrice, Symbol.Digits))
                            numberOfPositionsOfType++;
                    }
                }
            }

            return (numberOfPositionsOfType);
        }

        private double CalculateVolume(TradeType tradeType)
        {
            var numberOfPositions = CountNumberOfPositionsOfType(tradeType);
            var mostRecentVolume = GetMostRecentPositionVolume(tradeType);
            var calculatedVolume = Symbol.NormalizeVolumeInUnits(mostRecentVolume * Math.Pow(VolumeExponent, numberOfPositions));
            return (calculatedVolume);
        }

        private double LimitVolume(double volumeIn)
        {
            var symbolVolumeMin = Symbol.VolumeInUnitsMin;
            var symbolVolumeMax = Symbol.VolumeInUnitsMax;
            var result = volumeIn;
            if (result < symbolVolumeMin)
                result = symbolVolumeMin;
            if (result > symbolVolumeMax)
                result = symbolVolumeMax;
            return (result);
        }
    }
}

