#  Secretary Sera — Native Windows AI Task Management

  

<div align="center">

  <img src="https://raw.githubusercontent.com/ShigrafS/Sera/main/Sera/Assets/Logo.png" alt="Sera Logo" width="120">

  <h3>Your intelligent, native companion for effortless task orchestration.</h3>

  <p>

    Built with modern <b>WinUI 3</b> and <b>.NET 8</b> • Powered by <b>NVIDIA NIM</b>

  </p>

</div>

  

---

  

## 🌟 Overview

  

**Sera** is a native Windows application designed for high-performance task management driven by natural language. No more complex menus or rigid forms—simply tell Sera what you need to do, and she handles the rest.

  

From quick reminders to complex recurring projects, Sera leverages high-end LLMs (like Llama 3) via the NVIDIA inference stack to parse your intent with deterministic precision.

  

## ✨ Key Features

  

- **🗣️ Natural Language Extraction**: Add, complete, or reschedule tasks using plain English.

- **🔒 Secure by Design**: Credentials and API keys are stored in the **Windows Credential Locker** (PasswordVault), never in plain text.

- **⚡ High Performance**: Native WinUI 3 interface with silky-smooth animations and glassmorphism.

- **📅 Smart Recurrence**: Support for daily/weekly recurring tasks with automatic rollover.

- **🤖 Deterministic Logic**: State-of-the-art parsing ensures your intents are mapped to exact database actions without ambiguity.

- **📦 Reliable Data**: Fully persistent storage powered by a local **SQLite** engine.

  

## 🛠️ Technical Stack

  

- **Framework**: .NET 8 / WinUI 3 (Windows App SDK)

- **AI Engine**: NVIDIA NIM (Llama 3.1 70B/405B)

- **Database**: Entity Framework Core with SQLite

- **Architecture**: Strict 4-Layer Separation (Core, Services, Data, UI)

- **Scheduling**: Quartz.NET for task persistence and rollovers

- **Style**: Modern Windows 11 Design System

  

## 🚀 Getting Started

  

### Prerequisites

  

- **Windows 10/11**

- **.NET 8 SDK**

- An **[NVIDIA API Key](https://build.nvidia.com/)** (for AI parsing)

  

### Installation

  

1. **Clone the repository:**

   ```powershell

   git clone https://github.com/ShigrafS/Sera.git

   cd Sera

   ```

  

2. **Run the application:**

   ```powershell

   dotnet run --project Sera/Sera.csproj

   ```

  

3. **Initial Setup:**

   On the first launch, Sera will guide you to the **Settings** page. Enter your NVIDIA API key and test the connection.

  

## 📂 Project Structure

  

```text

Sera/

├── Sera.Core/       # Interfaces, Shared Models, Constants

├── Sera.Services/   # AI Logic, Execution Engine, Quartz Jobs

├── Sera.Data/       # EF Core DbContext, Entities, Migrations

├── Sera.UI/         # WinUI 3 Pages, ViewModels, Resources

└── Sera.Tests/      # Focused logic and service validation

```

  

## 📜 Notes

  

- **Deterministic UI**: The database is the single source of truth; the UI is just a reactive window into it.

- **Stateless AI**: The LLM acts as a pure parser; logic resides in the Services layer.

- **Security First**: No secrets in source code; use the `PasswordVault`.

  

---

  

<p align="center">

  Developed with ❤️ by <a href="https://github.com/ShigrafS">ShigrafS</a>

</p>
