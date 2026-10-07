# Symlink GUI

A modern Windows 11 application and File Explorer extension for creating symbolic links, directory junctions, and hard links without touching the command line (`mklink`).

Built with **C# / .NET 10**, **WinUI 3 (Windows App SDK)**, and **Fluent Design**.

---

## Requirements

- **Operating System**: Windows 10 (version 1809 / build 17763 or newer) or Windows 11.
- **Runtimes** *(for running portable releases)*:
  - [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/)
  - [Windows App SDK Runtime](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)
- **Permissions**:
  - **Directory Junctions** & **Hard Links**: Work out of the box with standard user privileges—no elevation or Developer Mode required.
  - **Symbolic Links**: Windows requires either **Developer Mode** enabled (`Settings` ➔ `System` ➔ `For developers`) or administrator privileges. If Developer Mode is disabled, the app automatically prompts for elevation (UAC) when creating symbolic links.

---

## Understanding Link Types

| Link Type | Targets | Cross-Volume? | Privileges Needed | Behavior |
|---|---|---|---|---|
| **Symbolic Link** | Files & Folders | Yes (Local & Remote SMB) | Developer Mode or Admin | Transparent filesystem pointer. Can cross drives and network shares. |
| **Directory Junction** | Folders only | No (Local NTFS only) | Standard User | Links one local directory to another. Works without admin rights. |
| **Hard Link** | Files only | No (Same NTFS volume only) | Standard User | Creates an additional directory entry pointing to the same file data on disk without duplicating storage. |

### How do they differ from a standard shortcut (`.lnk`)?
- **Standard Shortcut (`.lnk`)**: Only recognized by Windows File Explorer. When other applications, games, or command-line scripts try to read a `.lnk` file, they see a tiny shortcut file—not the actual content.
- **Filesystem Links**: Handled directly at the NTFS filesystem level. Windows and any running programs treat the link as if the real file or folder is physically located at the target destination.

### Why use Filesystem Links?
- **Save Disk Space**: Move large folders (games, virtual machines, cache directories) to a secondary hard drive while tricking the application into thinking they are still on your main C: drive.
- **Bypass Admin Restrictions**: Use **Directory Junctions** for folders when you cannot enable Windows Developer Mode or do not have administrator rights.
- **Cloud Syncing**: Sync folders with OneDrive, Dropbox, or Google Drive without changing their original folder structure.
- **Development & Modding**: Link shared libraries, assets, or configuration files across multiple game mods or codebases without duplicating files.

---

## Features

- **Fluent Design WinUI 3 App**:
  - Mica backdrop and native Windows 11 dark/light theme integration.
  - **Link Type Selection**: Choose between **Symbolic Link**, **Directory Junction**, and **Hard Link** with real-time descriptions.
  - **Dynamic Input Validation**: Incompatible browse buttons automatically disable based on the selected link type (e.g. file browse disabled for Junctions, folder browse disabled for Hard Links).
  - Browse Source (files or folders) and Destination folders using modern Windows pickers.
  - Drag-and-drop support: drag files or folders directly into the Source or Destination cards.
  - Live link target preview (`Destination\LinkName → SourcePath`).
  - Automatic collision avoidance and name validation.
  - In-app Explorer integration toggle (Install / Uninstall context menu).
- **File Explorer Context Menu Integration**:
  - **Right-click file/folder** ➔ **"Pick as Link Source"** (supports multi-selection).
  - **Right-click folder background / folder** ➔ **"Drop Link Here"** cascading menu:
    - **Symbolic Link**
    - **Directory Junction**
    - **Hard Link**
  - **Right-click file/folder** ➔ **"Create Link To..."** (opens GUI with Source pre-filled).
  - Headless command-line router ensures right-click actions execute instantly without UI startup overhead.

---

## How to Use

### Method 1: Using the Graphical App
1. Launch **Symlink GUI**.
2. Select your desired **Link type** (**Symbolic Link**, **Directory Junction**, or **Hard Link**).
3. Select your **Source**: Click **File** / **Folder**, or drag and drop any file or folder directly onto the Source card.
4. Select your **Destination**: Click **Browse** or drag and drop your target directory into the Destination card.
5. Verify the link name and preview in the **Link Target Preview** card.
6. Click **Create Link**.

### Method 2: Directly from File Explorer
1. Enable the context menu from within the Symlink GUI app settings (or via CLI `--install`).
2. In File Explorer, **right-click** any file or folder (or multiple items) and click **"Pick as Link Source"**.
3. Navigate to the folder where you want the link created.
4. **Right-click** an empty space inside the folder (or right-click the folder itself), highlight **"Drop Link Here"**, and choose the link type:
   - **Symbolic Link**
   - **Directory Junction**
   - **Hard Link**
5. Your link is created instantly!

---

## Building & Development

For instructions on building the project from source, running tests, and creating release packages, see [BUILD.md](BUILD.md).

---

## Command Line Usage

Symlink GUI includes a built-in CLI router for automation and shell integration:

| Command | Description |
|---|---|
| `SymlinkGUI.exe` | Launches the standalone WinUI 3 GUI. |
| `SymlinkGUI.exe --open "<path>"` | Opens the GUI with the specified path pre-selected as Source. |
| `SymlinkGUI.exe --pick "<path>"` | Adds file/folder to the current pick session (multi-select supported). |
| `SymlinkGUI.exe --drop <type> "<destFolder>"` | Creates links of `<type>` (`symlink`, `junction`, or `hardlink`) for all picked items inside `<destFolder>`. |
| `SymlinkGUI.exe --install` | Registers Explorer context menu verbs under `HKCU\Software\Classes`. |
| `SymlinkGUI.exe --uninstall` | Removes all context menu verbs. |

---

## Privacy Policy

Symlink GUI operates entirely offline and does not collect or transmit any telemetry or personal data. See [PRIVACY.md](PRIVACY.md) for full details.

---

## License

This project is licensed under the **GNU General Public License v3.0** (GPLv3). See the [LICENSE](LICENSE) file for details.
