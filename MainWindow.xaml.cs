using System;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Dapper;
using System.ComponentModel;
using System.Windows.Forms;
using System.IO;
using System.Drawing;

namespace ScreenTimeMonitor
{
    public partial class MainWindow : Window
    {
        private const int IdleThresholdSeconds = 60; // 1 minute idle
        private DateTime _lastInputTime;
        private string _currentWindow = string.Empty;
        private bool _isIdle;
        public ICommand ShowDashboardCommand { get; }
        private Icon? _appIcon;
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);
        
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public MainWindow()
        {
            try
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Application starting...\n");
                
                InitializeComponent();
                DataContext = this;
                ShowDashboardCommand = new RelayCommand(_ => ShowDashboard());
                
                // Initialize NotifyIcon
                try
                {
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                    if (File.Exists(iconPath))
                    {
                        _appIcon = new Icon(iconPath);
                        NotifyIcon.Icon = _appIcon;
                    }
                    else
                    {
                        NotifyIcon.Icon = System.Drawing.SystemIcons.Application;
                        File.AppendAllText("screentime.log", $"{DateTime.Now}: Custom icon not found, using default icon\n");
                    }
                }
                catch (Exception ex)
                {
                    File.AppendAllText("screentime.log", $"{DateTime.Now}: Error loading icon - {ex.Message}\n");
                    NotifyIcon.Icon = System.Drawing.SystemIcons.Application;
                }
                
                NotifyIcon.Visibility = Visibility.Visible;
                
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Initializing database...\n");
                InitializeDatabase();
                
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Starting monitoring...\n");
                StartMonitoring();
                
                // Show notification
                try
                {
                    NotifyIcon.ShowBalloonTip(
                        "Screen Time Monitor",
                        "Application is now monitoring your screen time. Double-click the tray icon to view statistics.",
                        Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                }
                catch (Exception ex)
                {
                    File.AppendAllText("screentime.log", $"{DateTime.Now}: Failed to show balloon tip - {ex.Message}\n");
                }
                
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Application started successfully\n");
                
                // Hide the main window but keep the application running
                this.Hide();
                this.ShowInTaskbar = false;
            }
            catch (Exception ex)
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: ERROR - {ex.Message}\n{ex.StackTrace}\n");
                System.Windows.MessageBox.Show($"Error starting application: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Application closing...\n");
                if (NotifyIcon != null)
                {
                    NotifyIcon.Dispose();
                }
                _appIcon?.Dispose();
                base.OnClosing(e);
            }
            catch (Exception ex)
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: ERROR during closing - {ex.Message}\n");
            }
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection("Data Source=screen_time.db");
            connection.Open();
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS ScreenTime (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    WindowTitle TEXT,
                    StartTime DATETIME,
                    EndTime DATETIME,
                    IsActive INTEGER
                )");
        }

        private void ShowDashboard(object? sender = null, RoutedEventArgs? e = null)
        {
            try
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Opening dashboard...\n");
                var dashboard = new DashboardWindow();
                dashboard.Show();
                dashboard.Activate(); // Ensure the window comes to front
            }
            catch (Exception ex)
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: ERROR opening dashboard - {ex.Message}\n");
                System.Windows.MessageBox.Show("Error opening dashboard: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExitApplication(object? sender = null, RoutedEventArgs? e = null)
        {
            try
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Application exit requested\n");
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: ERROR during exit - {ex.Message}\n");
            }
        }

        private void StartMonitoring()
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += (_, _) => CheckActivity();
            timer.Start();
            _lastInputTime = DateTime.Now;
            File.AppendAllText("screentime.log", $"{DateTime.Now}: Monitoring timer started\n");
        }

        private void CheckActivity()
        {
            try
            {
                var activeWindow = GetActiveWindowTitle();
                var isIdle = IsUserIdle();

                if (_isIdle != isIdle || _currentWindow != activeWindow)
                {
                    LogCurrentSession();
                    _currentWindow = activeWindow;
                    _isIdle = isIdle;
                    File.AppendAllText("screentime.log", $"{DateTime.Now}: Activity changed - Window: {activeWindow}, Idle: {isIdle}\n");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: ERROR in CheckActivity - {ex.Message}\n");
            }
        }

        private void LogCurrentSession()
        {
            var endTime = DateTime.Now;
            using var connection = new SqliteConnection("Data Source=screen_time.db");
            connection.Execute(
                "INSERT INTO ScreenTime (WindowTitle, StartTime, EndTime, IsActive) " +
                "VALUES (@WindowTitle, @StartTime, @EndTime, @IsActive)",
                new
                {
                    WindowTitle = _currentWindow,
                    StartTime = _lastInputTime,
                    EndTime = endTime,
                    IsActive = !_isIdle
                });
            _lastInputTime = endTime;
        }

        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            var buff = new System.Text.StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();
            return GetWindowText(handle, buff, nChars) > 0 ? buff.ToString() : "Unknown";
        }

        private bool IsUserIdle()
        {
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            GetLastInputInfo(ref lastInputInfo);
            
            var lastInput = Environment.TickCount - lastInputInfo.dwTime;
            return lastInput > IdleThresholdSeconds * 1000;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }
} 