# ⚡ System Live Dashboard

A modern, sleek, and real-time system monitoring dashboard built with **C# (.NET 8)**, **SignalR**, and **Chart.js**. 

This project reads hardware metrics directly from the system and pushes them to a web browser in real-time without requiring page reloads.

![Dashboard Preview](https://github.com/ashkanpeymanofficial/system-live-dashboard/blob/main/screenshots/preview%20(2).png?raw=true) <!-- (می‌تونی بعدا عکس خودت رو اینجا بذاری) -->

## ✨ Features

- **Real-time Monitoring:** Live updates of CPU load (per thread), Memory usage, Network speed, and Uptime via **SignalR**.
- **Top Processes:** Displays the top 5 processes consuming the most RAM.
- **Disk Usage:** Real-time tracking of local disk capacities.
- **History & Analytics:** Stores metrics in a local **SQLite** database (using EF Core) and displays a historical trend chart for the last 1 or 24 hours.
- **CSV Export:** Download historical data for external analysis.
- **Smart Alerts:** Visual pulse alerts when CPU exceeds 80% or RAM exceeds 90%.
- **Modern UI:** Sleek dark neon theme with glassmorphism effects using vanilla CSS.

## 🛠️ Tech Stack

- **Backend:** C# .NET 8, ASP.NET Core Minimal APIs, SignalR
- **Database:** SQLite + Entity Framework Core
- **Frontend:** HTML5, CSS3, JavaScript (Chart.js)
- **Hardware Info:** LibreHardwareMonitorLib & System.Management

## 🚀 How to Run

1. Clone the repository:
   ```bash
   git clone https://github.com/ashkanpeymanofficial/system-live-dashboard.git
