using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Windows.Threading;
using System.Windows.Controls;

namespace ScreenTimeMonitor
{
    public partial class AnalysisWindow : Window
    {
        private readonly ObservableCollection<AppUsageData> _usageData;
        private readonly DispatcherTimer _updateTimer;
        private TimePeriod _currentPeriod = TimePeriod.Daily;
        private DateTime _currentDate = DateTime.Now.Date;

        public AnalysisWindow()
        {
            InitializeComponent();
            _usageData = new ObservableCollection<AppUsageData>();
            BarChart.ItemsSource = _usageData;

            LoadData();

            // Update every hour
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromHours(1)
            };
            _updateTimer.Tick += (s, e) => LoadData();
            _updateTimer.Start();
        }

        private void LoadData()
        {
            try
            {
                using var connection = new SqliteConnection("Data Source=screen_time.db");
                var dateFilter = GetDateFilter();
                var records = connection.Query<ScreenTimeRecord>($@"
                    WITH TimeData AS (
                        SELECT 
                            WindowTitle,
                            CAST(ROUND((julianday(EndTime) - julianday(StartTime)) * 86400) AS INTEGER) as Seconds,
                            IsActive
                        FROM ScreenTime 
                        WHERE {dateFilter}
                    )
                    SELECT 
                        WindowTitle,
                        SUM(CASE WHEN IsActive = 1 THEN Seconds ELSE 0 END) as ActiveSeconds,
                        SUM(CASE WHEN IsActive = 0 THEN Seconds ELSE 0 END) as IdleSeconds
                    FROM TimeData
                    GROUP BY WindowTitle
                    HAVING SUM(Seconds) > 0
                    ORDER BY SUM(Seconds) DESC");

                var groupedRecords = records
                    .GroupBy(r => GetBaseApplicationName(r.WindowTitle))
                    .Select(g => new
                    {
                        Application = g.Key,
                        TotalSeconds = g.Sum(r => r.ActiveSeconds + r.IdleSeconds)
                    })
                    .OrderByDescending(r => r.TotalSeconds)
                    .ToList();

                var totalTime = groupedRecords.Sum(r => r.TotalSeconds);

                _usageData.Clear();
                foreach (var record in groupedRecords)
                {
                    _usageData.Add(new AppUsageData
                    {
                        Application = record.Application,
                        TimeSpent = FormatTimeSpan(TimeSpan.FromSeconds(record.TotalSeconds)),
                        Percentage = (double)record.TotalSeconds / totalTime * 100
                    });
                }

                UpdatePeriodDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading analysis data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetDateFilter()
        {
            return _currentPeriod switch
            {
                TimePeriod.Daily => $"date(StartTime) = date('{_currentDate:yyyy-MM-dd}')",
                TimePeriod.Weekly => $@"
                    date(StartTime) >= date('{_currentDate.AddDays(-(int)_currentDate.DayOfWeek):yyyy-MM-dd}') AND
                    date(StartTime) <= date('{_currentDate.AddDays(6 - (int)_currentDate.DayOfWeek):yyyy-MM-dd}')",
                TimePeriod.Monthly => $@"
                    strftime('%Y-%m', StartTime) = '{_currentDate:yyyy-MM}'",
                _ => throw new ArgumentException("Invalid time period")
            };
        }

        private void UpdatePeriodDisplay()
        {
            PeriodDisplay.Text = _currentPeriod switch
            {
                TimePeriod.Daily => _currentDate.Date == DateTime.Now.Date ? "Today" : 
                                  _currentDate.Date == DateTime.Now.Date.AddDays(-1) ? "Yesterday" :
                                  _currentDate.ToString("MMMM d, yyyy"),
                TimePeriod.Weekly => $"Week of {_currentDate.AddDays(-(int)_currentDate.DayOfWeek):MMMM d, yyyy}",
                TimePeriod.Monthly => _currentDate.ToString("MMMM yyyy"),
                _ => throw new ArgumentException("Invalid time period")
            };
        }

        private void OnPeriodChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton radioButton)
            {
                _currentPeriod = radioButton.Content.ToString() switch
                {
                    "Daily" => TimePeriod.Daily,
                    "Weekly" => TimePeriod.Weekly,
                    "Monthly" => TimePeriod.Monthly,
                    _ => TimePeriod.Daily
                };
                _currentDate = DateTime.Now.Date;
                LoadData();
            }
        }

        private void NavigatePrevious(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentPeriod switch
            {
                TimePeriod.Daily => _currentDate.AddDays(-1),
                TimePeriod.Weekly => _currentDate.AddDays(-7),
                TimePeriod.Monthly => _currentDate.AddMonths(-1),
                _ => _currentDate
            };
            LoadData();
        }

        private void NavigateNext(object sender, RoutedEventArgs e)
        {
            var nextDate = _currentPeriod switch
            {
                TimePeriod.Daily => _currentDate.AddDays(1),
                TimePeriod.Weekly => _currentDate.AddDays(7),
                TimePeriod.Monthly => _currentDate.AddMonths(1),
                _ => _currentDate
            };

            if (nextDate.Date <= DateTime.Now.Date)
            {
                _currentDate = nextDate;
                LoadData();
            }
        }

        private string GetBaseApplicationName(string windowTitle)
        {
            if (windowTitle.Contains(" - Mozilla Firefox"))
                return "Mozilla Firefox";
            if (windowTitle.Contains(" - Google Chrome"))
                return "Google Chrome";
            if (windowTitle.Contains(" - Microsoft Edge"))
                return "Microsoft Edge";
            if (windowTitle.Contains(" - Visual Studio Code"))
                return "Visual Studio Code";
            if (windowTitle.Contains(" - Notepad"))
                return "Notepad";
            
            var parts = windowTitle.Split(new[] { " - ", " – ", " | " }, StringSplitOptions.None);
            return parts.Length > 1 ? parts[parts.Length - 1] : windowTitle;
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
            }
            if (timeSpan.TotalMinutes >= 1)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            return $"{timeSpan.Seconds}s";
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _updateTimer.Stop();
            base.OnClosing(e);
        }
    }

    public class AppUsageData
    {
        public string Application { get; set; } = "";
        public string TimeSpent { get; set; } = "";
        public double Percentage { get; set; }
    }

    public enum TimePeriod
    {
        Daily,
        Weekly,
        Monthly
    }

    public class PercentageToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !double.TryParse(values[0].ToString(), out double percentage) || !double.TryParse(values[1].ToString(), out double totalWidth))
                return 0.0;

            return Math.Max(0, Math.Min(totalWidth * (percentage / 100), totalWidth - 20));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 