# Building & Running Symlink GUI

This guide provides instructions on how to build, test, run, and package **Symlink GUI** from source.

---

## Prerequisites

- **Operating System**: Windows 10 (version 1809 / build 17763 or newer) or Windows 11.
- **SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/) or newer.
- **Workloads / SDKs**: [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) components (included automatically via NuGet packages in the project).

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
.\src\SymlinkGUI\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\SymlinkGUI.exe
```

---

## 4. Build a Portable Release (Folder / ZIP)

To create a lightweight, portable release folder and compressed `.zip` archive:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-portable.ps1
```

You can also specify a custom runtime or version explicitly:
```powershell
powershell -ExecutionPolicy Bypass -File .\build-portable.ps1 -Runtime win-x64 -Version 1.0.1
```

### Outputs
The build script outputs the artifacts into the `dist/` directory (version number is resolved automatically from `Directory.Build.props` or the `-Version` argument):
- `dist\SymlinkGUI-Portable-win-x64-1.0.0\` — Portable directory ready to run via `SymlinkGUI.exe`.
- `dist\SymlinkGUI-Portable-win-x64-1.0.0.zip` — Compressed portable package (~9.7 MB).

> [!NOTE]
> To keep the download package lightweight, the .NET runtime and Windows App SDK are unbundled. Users running the portable build will need:
> - [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/)
> - [Windows App SDK Runtime](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)
