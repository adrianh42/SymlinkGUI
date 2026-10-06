# Building & Running Symlink GUI

This guide provides instructions on how to build, test, run, and package **Symlink GUI** from source.

---

## Prerequisites

- **Operating System**: Windows 10 (version 1809 / build 17763 or newer) or Windows 11.
- **SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/) or newer.
- **Runtimes**: Standard [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/). No Windows App SDK is required.

---

## 1. Build the Solution

To restore dependencies and build the entire solution:

```powershell
dotnet build
```

---

## 2. Run the Unit Tests

To execute the automated unit test suite:

```powershell
dotnet test
```

---

## 3. Run the Application

### Via `dotnet run`
```powershell
dotnet run --project src/SymlinkGUI
```

### Direct Executable Execution
After building in Debug configuration:
```powershell
.\src\SymlinkGUI\bin\Debug\net10.0-windows\SymlinkGUI.exe
```

---

## 4. Build a Portable Release (Folder / ZIP)

To create a lightweight, portable release folder and compressed `.zip` archive:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-portable.ps1
```

### Outputs
The build script outputs the artifacts into the `dist/` directory:
- `dist\SymlinkGUI-Portable-win-x64\` — Portable directory ready to run via `SymlinkGUI.exe` (~0.23 MB).
- `dist\SymlinkGUI-Portable-win-x64.zip` — Compressed portable package (~100 KB).

> [!NOTE]
> Unlike the WinUI 3 edition, this Windows Forms edition does **not** require the Windows App SDK runtime. Users only need the standard [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/).
