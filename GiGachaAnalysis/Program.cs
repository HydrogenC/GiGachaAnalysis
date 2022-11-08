using System;
using System.Globalization;
using System.Reflection.Emit;
using ScottPlot;
using static ScottPlot.Plottable.PopulationPlot;

namespace GiGachaAnalysis;

/// <summary>
/// 一抽
/// </summary>
/// <param name="IsSpecial">是否为限定</param>
/// <param name="WonFifty">是否小保底没歪</param>
/// <param name="Pities">从上个五星起的累计抽数</param>
/// <param name="PitiesSpecial">从上个限定五星起的累计抽数</param>
/// <param name="Time">时间</param>
record struct Pull(bool IsSpecial, bool WonFifty,
    int Pities, int PitiesSpecial, DateTime Time);

/// <summary>
/// 统计数据
/// </summary>
/// <param name="PityAvg">平均五星出货抽数</param>
/// <param name="PityAvgSpecial">平均限定五星出货抽数</param>
/// <param name="WinChance">小保底不歪的概率</param>
/// <param name="SpecialCount">限定五星总数</param>
/// <param name="Count">五星总数</param>
record struct PullStats(double PityAvg, double PityAvgSpecial,
    double WinChance, int SpecialCount, int Count);

internal class Program
{
    static readonly string[] nonSpecial = new string[]
    {
        "刻晴", "迪卢克", "七七", "莫娜", "琴", "提纳里"
    };

    const int DAY_SLICES = 48;
    const int SLICE_MINUTES = 24 * 60 / DAY_SLICES;

    static void CreateTimePlot(PullStats[] timeStats)
    {
        var timeAvgPlot = new Plot(1000, 600);
        var dataRange = Enumerable.Range(0, DAY_SLICES);

        var avgData = timeAvgPlot.AddBar(
            dataRange.Select((x) => timeStats[x].PityAvgSpecial).ToArray(),
            dataRange.Select((x) => (x + 0.5) * SLICE_MINUTES / 60.0).ToArray()
            );
        avgData.XAxisIndex = 0;
        avgData.YAxisIndex = 0;
        avgData.BarWidth = 0.8 * SLICE_MINUTES / 60.0;

        var chanceData = timeAvgPlot.AddScatter(
            dataRange.Select((x) => x * SLICE_MINUTES / 60.0).ToArray(),
            dataRange.Select((x) => timeStats[x].WinChance).ToArray()
            );
        chanceData.XAxisIndex = 0;
        chanceData.YAxisIndex = 1;

        timeAvgPlot.Title("Genshin Pulls Analysis");
        timeAvgPlot.XAxis.Label("Time");
        timeAvgPlot.YAxis.Label("Avg Pulls");
        timeAvgPlot.YAxis.Color(avgData.Color);
        timeAvgPlot.YAxis2.Ticks(true);
        timeAvgPlot.SetAxisLimits(yMin: 80, yMax: 95, yAxisIndex: 0);
        timeAvgPlot.SetAxisLimits(yMin: 0.4, yMax: 0.6, yAxisIndex: 1);
        timeAvgPlot.YAxis2.Label("Winning Chance");
        timeAvgPlot.YAxis2.Color(chanceData.Color);

        timeAvgPlot.SaveFig("D:\\TimePlot.png");
    }

    static void CreateDatePlot(PullStats[] dateStats, DateTime[] dates)
    {
        var dateTotalPlot = new Plot(1000, 600);
        var dataRange = Enumerable.Range(0, dates.Length);

        var countData = dateTotalPlot.AddScatter(
            dataRange.Select((x) => dates[x].ToOADate()).ToArray(),
            dataRange.Select((x) => (double)dateStats[x].SpecialCount).ToArray()
            );
        countData.XAxisIndex = 0;
        countData.YAxisIndex = 0;

        var peaks = new List<int>();
        for (int i = 0; i < dateStats.Length; i++)
        {
            if (dateStats[i].SpecialCount >= 1000)
            {
                peaks.Add(i);
            }
        }

        dateTotalPlot.XAxis.ManualTickPositions(
            peaks.Select((x) => dates[x].ToOADate()).ToArray(),
            peaks.Select((x) => dates[x].ToString("d")).ToArray()
            );

        dateTotalPlot.Title("Genshin Pulls Analysis");
        dateTotalPlot.XAxis.TickLabelStyle(rotation: 45);
        dateTotalPlot.XAxis.Label("Time");
        dateTotalPlot.XAxis.DateTimeFormat(true);
        dateTotalPlot.YAxis.Label("Total Special");
        dateTotalPlot.YAxis.Color(countData.Color);

        dateTotalPlot.SaveFig("D:\\DatePlot.png");
    }

    static void AddToAvg(ref PullStats stats, Pull pull)
    {
        stats.PityAvg = (stats.PityAvg * stats.Count + pull.Pities) / (stats.Count + 1.0);
        stats.Count++;

        if (pull.IsSpecial)
        {
            stats.WinChance = (stats.WinChance * stats.SpecialCount + (pull.WonFifty ? 1 : 0)) / (stats.SpecialCount + 1.0);
            stats.PityAvgSpecial = (stats.PityAvgSpecial * stats.SpecialCount + pull.PitiesSpecial) / (stats.SpecialCount + 1.0);
            stats.SpecialCount++;
        }
    }
    static void Main(string[] args)
    {
        CultureInfo provider = CultureInfo.InvariantCulture;
        List<Pull> pulls = new();

        // 存放 csv 数据的文件夹
        string dataDir = "D:\\Code\\player_data";

        // 枚举目录中所有 csv 文件
        foreach (var i in Directory.EnumerateFiles(dataDir))
        {
            Console.WriteLine($"Reading file {Path.GetFileName(i)}");

            var lines = File.ReadLines(i);
            var pities = 0;
            var winFiftyFlag = true;
            // 第一行为表头，跳过第一行
            foreach (var line in lines.Skip(1))
            {
                // 以逗号为分割，将字符串切开
                string[] segments = line.Split(',', StringSplitOptions.TrimEntries);
                switch (segments[1])
                {
                    // 非限定池，跳过
                    case "100":
                    case "200":
                    case "302":
                        continue;
                }

                pities++;
                switch (segments[3])
                {
                    // 非五星，跳过
                    case "3":
                    case "4":
                        continue;
                }

                var isSpecial = !nonSpecial.Contains(segments[0]);
                pulls.Add(new Pull(
                    isSpecial, winFiftyFlag, pities,
                    pities + (winFiftyFlag ? 0 : pulls.Last().Pities),
                    DateTime.ParseExact(segments[5], "yyyy-MM-dd HH:mm:ss", provider)
                    ));

                winFiftyFlag = isSpecial;
                pities = 0;
            }
        }

        var timeStats = new PullStats[DAY_SLICES];
        for (int i = 0; i < DAY_SLICES; i++)
        {
            timeStats[i] = new PullStats(0, 0, 0, 0, 0);
        }

        foreach (var i in pulls)
        {
            var time = i.Time.TimeOfDay;
            var index = (time.Hours * 60 + time.Minutes) / SLICE_MINUTES;
            AddToAvg(ref timeStats[index], i);
        }

        CreateTimePlot(timeStats);

        var minDate = new DateTime(pulls.Min((x) => x.Time.Ticks)).Date;
        var maxDate = new DateTime(pulls.Max((x) => x.Time.Ticks)).Date;

        Console.WriteLine($"Date vary from {minDate} to {maxDate}");
        var k = (maxDate - minDate).Days + 1;
        var dates = new DateTime[k];
        var dateStats = new PullStats[k];
        for (int i = 0; i < k; i++)
        {
            dates[i] = minDate.AddDays(i).Date;
        }

        foreach (var i in pulls)
        {
            var index = (i.Time.Date - minDate).Days;
            AddToAvg(ref dateStats[index], i);
        }

        CreateDatePlot(dateStats, dates);
    }
}