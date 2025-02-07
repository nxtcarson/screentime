using System;
using System.Windows;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace ScreenTimeMonitor
{
    public partial class DashboardWindow : Window
    {
        private readonly DispatcherTimer _refreshTimer;
        private readonly ObservableCollection<ActivityRecord> _records;
        private readonly ICollectionView _recordsView;
        private ActivityRecord? _lastClickedRecord;

        public DashboardWindow()
        {
            InitializeComponent();
            _records = new ObservableCollection<ActivityRecord>();
            _recordsView = CollectionViewSource.GetDefaultView(_records);
            DataGrid.ItemsSource = _recordsView;

            // Add handler for checkbox clicks
            DataGrid.LoadingRow += (s, e) =>
            {
                var row = e.Row;
                row.MouseLeftButtonUp += DataGrid_RowMouseLeftButtonUp;
            };

            LoadData();

            // Set up auto-refresh timer (2 seconds)
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _refreshTimer.Tick += (s, e) => LoadData();
            _refreshTimer.Start();
        }

        private void ShowAnalysis(object sender, RoutedEventArgs e)
        {
            var analysis = new AnalysisWindow();
            analysis.Show();
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

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _recordsView.Filter = null;
            }
            else
            {
                _recordsView.Filter = obj =>
                {
                    if (obj is ActivityRecord record)
                    {
                        return record.Application.ToLower().Contains(searchText);
                    }
                    return false;
                };
            }
        }

        private string GetBaseApplicationName(string windowTitle)
        {
            // Extract base application name from window title
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
            
            // For other applications, take the first part of the title before any separator
            var parts = windowTitle.Split(new[] { " - ", " – ", " | " }, StringSplitOptions.None);
            return parts.Length > 1 ? parts[parts.Length - 1] : windowTitle;
        }

        private void DeleteSelected(object sender, RoutedEventArgs e)
        {
            var selectedRecords = _records.Where(r => r.IsSelected).ToList();
            if (!selectedRecords.Any())
            {
                System.Windows.MessageBox.Show("Please select at least one record to delete.", 
                    "No Selection", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete {selectedRecords.Count} selected record(s)?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using var connection = new SqliteConnection("Data Source=screen_time.db");
                    foreach (var record in selectedRecords)
                    {
                        connection.Execute(@"
                            DELETE FROM ScreenTime 
                            WHERE WindowTitle LIKE @WindowTitlePattern 
                            AND date(StartTime) = date('now', 'localtime')",
                            new { WindowTitlePattern = $"%{record.Application}%" });
                    }
                    LoadData(); // Refresh the data
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error deleting records: {ex.Message}", 
                        "Error", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
            }
        }

        private void RefreshData(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // Store current selections
                var selectedApplications = _records
                    .Where(r => r.IsSelected)
                    .Select(r => r.Application)
                    .ToHashSet();

                using var connection = new SqliteConnection("Data Source=screen_time.db");
                var records = connection.Query<ScreenTimeRecord>(@"
                    WITH TimeData AS (
                        SELECT 
                            WindowTitle,
                            CAST(ROUND((julianday(EndTime) - julianday(StartTime)) * 86400) AS INTEGER) as Seconds,
                            IsActive
                        FROM ScreenTime 
                        WHERE date(StartTime) = date('now', 'localtime')
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
                        ActiveSeconds = g.Sum(r => r.ActiveSeconds),
                        IdleSeconds = g.Sum(r => r.IdleSeconds)
                    })
                    .OrderByDescending(r => r.ActiveSeconds + r.IdleSeconds);

                _records.Clear();
                foreach (var record in groupedRecords)
                {
                    _records.Add(new ActivityRecord
                    {
                        Application = record.Application,
                        ActiveTime = FormatTimeSpan(TimeSpan.FromSeconds(record.ActiveSeconds)),
                        IdleTime = FormatTimeSpan(TimeSpan.FromSeconds(record.IdleSeconds)),
                        TotalTime = FormatTimeSpan(TimeSpan.FromSeconds(record.ActiveSeconds + record.IdleSeconds)),
                        IsSelected = selectedApplications.Contains(record.Application)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void DataGrid_RowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.Item is ActivityRecord clickedRecord)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    if (_lastClickedRecord != null)
                    {
                        int startIndex = _records.IndexOf(_lastClickedRecord);
                        int endIndex = _records.IndexOf(clickedRecord);

                        if (startIndex > -1 && endIndex > -1)
                        {
                            int minIndex = Math.Min(startIndex, endIndex);
                            int maxIndex = Math.Max(startIndex, endIndex);

                            bool setTo = clickedRecord.IsSelected;
                            for (int i = minIndex; i <= maxIndex; i++)
                            {
                                _records[i].IsSelected = setTo;
                            }
                        }
                    }
                }
                _lastClickedRecord = clickedRecord;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _refreshTimer.Stop();
            base.OnClosing(e);
        }
    }

    public class ScreenTimeRecord
    {
        public required string WindowTitle { get; set; }
        public long ActiveSeconds { get; set; }
        public long IdleSeconds { get; set; }
    }

    public class ActivityRecord : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Application { get; set; } = "";
        public string ActiveTime { get; set; } = "";
        public string IdleTime { get; set; } = "";
        public string TotalTime { get; set; } = "";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 