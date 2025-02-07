using System;
using System.Windows;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ScreenTimeMonitor
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;
        private const string MutexName = "Global\\ScreenTimeMonitor_SingleInstance_v2";
        private bool _mutexOwned = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Attempt to create or open the mutex
                _mutex = new Mutex(false, MutexName);
                
                try
                {
                    // Try to acquire the mutex with a timeout
                    _mutexOwned = _mutex.WaitOne(TimeSpan.FromSeconds(1), false);
                    
                    if (!_mutexOwned)
                    {
                        File.AppendAllText("screentime.log", $"{DateTime.Now}: Application already running. Exiting...\n");
                        System.Windows.MessageBox.Show("Screen Time Monitor is already running in the system tray.", 
                                      "Already Running", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Information);
                        Current.Shutdown();
                        return;
                    }
                }
                catch (AbandonedMutexException)
                {
                    // If the mutex was abandoned, we now own it
                    _mutexOwned = true;
                    File.AppendAllText("screentime.log", $"{DateTime.Now}: Recovered from abandoned mutex\n");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Mutex error - {ex.Message}\n");
                // Don't proceed if we can't establish mutex
                System.Windows.MessageBox.Show($"Error starting application: {ex.Message}", 
                              "Startup Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
                Current.Shutdown();
                return;
            }

            try
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Application OnStartup...\n");
                
                // Set up global exception handling
                this.DispatcherUnhandledException += App_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
                
                base.OnStartup(e);
                
                // Create and show the main window
                MainWindow = new MainWindow();
                MainWindow.Show();
                
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Application OnStartup completed\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: FATAL ERROR during startup - {ex.Message}\n{ex.StackTrace}\n");
                System.Windows.MessageBox.Show($"A fatal error occurred during startup: {ex.Message}", 
                              "Startup Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Application exiting...\n");
                if (_mutexOwned && _mutex != null)
                {
                    try 
                    { 
                        _mutex.ReleaseMutex(); 
                        _mutexOwned = false;
                    } 
                    catch (Exception ex) 
                    {
                        File.AppendAllText("screentime.log", $"{DateTime.Now}: Error releasing mutex - {ex.Message}\n");
                    }
                }
                if (_mutex != null)
                {
                    _mutex.Close();
                    _mutex = null;
                }
                base.OnExit(e);
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Application exit complete\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: ERROR during exit - {ex.Message}\n");
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            File.AppendAllText("screentime.log", $"{DateTime.Now}: Unhandled Exception - {e.Exception.Message}\n{e.Exception.StackTrace}\n");
            System.Windows.MessageBox.Show($"An error occurred: {e.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                File.AppendAllText("screentime.log", $"{DateTime.Now}: Fatal Exception - {ex.Message}\n{ex.StackTrace}\n");
                System.Windows.MessageBox.Show($"A fatal error occurred: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            File.AppendAllText("screentime.log", $"{DateTime.Now}: Task Exception - {e.Exception.Message}\n{e.Exception.StackTrace}\n");
            System.Windows.MessageBox.Show($"A task error occurred: {e.Exception.Message}", "Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.SetObserved();
        }
    }
} 