# Screen Time Monitor

A modern, elegant Windows application that tracks and analyzes your application usage time. Built with WPF and .NET 8, this tool helps you understand how you spend time on your computer by monitoring active windows and providing detailed analytics.

## Features

- **Real-time Tracking**: Monitors active window usage in real time
- **Idle Detection**: Distinguishes between active and idle time
- **Modern UI**: Clean, dark-themed interface with smooth animations
- **System Tray Integration**: Runs quietly in the background
- **Detailed Analytics**: View daily, weekly, and monthly usage patterns
- **Search & Filter**: Easily find specific applications
- **Bulk Actions**: Select multiple items with shift-click support
- **Data Management**: Delete unwanted records

## Requirements

- Windows 10/11
- .NET 8.0 SDK or Runtime
- SQLite

## Setup & Running

1. Clone the repository:
```bash
git clone https://github.com/nxtcarson/screentime.git
cd screentime
```

2. Build the project:
```bash
dotnet build
```

3. Run the application:
```bash
dotnet run
```

The application will start minimized in the system tray. Double-click the tray icon to open the dashboard.

## Usage

- **View Dashboard**: Double-click the system tray icon
- **Select Items**: Click checkboxes or use shift-click for multiple selection
- **Search**: Use the search box to filter applications
- **Delete Records**: Select items and click "Delete Selected"
- **View Analytics**: Click "Show Analysis" for detailed usage patterns
- **Exit**: Right-click the tray icon and select "Exit"

## Development

The project uses:
- WPF for the UI
- SQLite for data storage
- Dapper for database access
- MVVM pattern for architecture

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details. 