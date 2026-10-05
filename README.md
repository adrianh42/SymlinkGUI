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

### 4. Build a portable release (Folder / ZIP)
To build a lightweight portable folder and `.zip` archive:
```powershell
powershell -ExecutionPolicy Bypass -File .\build-portable.ps1
```
The output will be placed in:
- `dist\SymlinkGUI-Portable-win-x64\` (portable distribution folder ready to run via `SymlinkGUI.exe`)
- `dist\SymlinkGUI-Portable-win-x64.zip` (compressed archive, ~9.7 MB)

*Note: Since the .NET runtime and Windows App SDK are unbundled to keep the package lightweight, the target PC will require the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/) and [Windows App SDK Runtime](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads).*

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
