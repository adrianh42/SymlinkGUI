# Symlink GUI

A modern Windows 11 application and File Explorer extension for creating symbolic links easily without using the terminal (`mklink`).

Built with **C# / .NET 10**, **WinUI 3 (Windows App SDK)**, and **Fluent Design**.

---

## Features

- **Fluent Design WinUI 3 App**:
  - Mica backdrop and native Windows 11 dark/light theme integration.
  - Browse Source (files or folders) and Destination folders using modern Windows pickers.
  - Drag-and-drop support: drag files/folders directly into the Source or Destination cards.
  - Live link target preview (`Destination\LinkName → SourcePath`).
  - Automatic collision avoidance and name validation.
  - In-app Explorer integration toggle (Install / Uninstall context menu).
  - Developer Mode detection and direct link to Windows Developer settings.
- **File Explorer Context Menu Integration**:
  - **Right-click file/folder** ➔ **"Pick as Symlink Source"** (supports multi-selection).
  - **Right-click folder background / folder** ➔ **"Drop Symbolic Link Here"**.
  - **Right-click file/folder** ➔ **"Create Symlink To..."** (opens GUI with Source pre-filled).
  - Headless command-line router ensures right-click actions execute instantly without UI startup overhead.
- **Clean Architecture & Extensibility**:
  - Modular `ILinkCreator` engine designed to support Directory Junctions and Hard Links in future updates.
  - Designed for future Windows 11 compact context menu (`IExplorerCommand`) and winget distribution.

---

## Requirements

- Windows 10 (version 1809 / build 17763 or newer) or Windows 11.
- .NET 10 SDK (for building from source).
- Windows **Developer Mode** enabled (recommended to create symlinks without administrator prompts), or run as Administrator.

---

## How to Build & Run

### 1. Build the solution
```powershell
dotnet build
```

### 2. Run the unit tests
```powershell
dotnet test
```

### 3. Run the application
```powershell
dotnet run --project src/SymlinkGUI
```

Or run the built executable directly:
```powershell
.\src\SymlinkGUI\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\SymlinkGUI.exe
```

---

## Command Line Usage

| Command | Description |
|---|---|
| `SymlinkGUI.exe` | Launches the standalone WinUI 3 GUI. |
| `SymlinkGUI.exe --open "<path>"` | Opens the GUI with the specified path pre-selected as Source. |
| `SymlinkGUI.exe --pick "<path>"` | Adds file/folder to the current pick session (multi-select supported). |
| `SymlinkGUI.exe --drop symlink "<destFolder>"` | Creates symbolic links for all picked items inside `<destFolder>`. |
| `SymlinkGUI.exe --install` | Registers Explorer context menu verbs under `HKCU\Software\Classes`. |
| `SymlinkGUI.exe --uninstall` | Removes all context menu verbs. |
