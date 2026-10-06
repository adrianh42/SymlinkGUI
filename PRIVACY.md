# Privacy Policy for Symlink GUI

**Last Updated:** October 6, 2026

This Privacy Policy applies to the **Symlink GUI** application (including the graphical interface, command-line utility, and shell extensions).

---

## 1. Overview

Symlink GUI is designed to respect and prioritize your privacy. The application operates entirely offline and does not collect, store, transmit, or share any personal data, telemetry, or diagnostic information.

---

## 2. Information Collection and Use

- **No Personal Information**: We do not collect names, email addresses, IP addresses, device identifiers, or any other personally identifiable information (PII).
- **No Telemetry or Analytics**: The application contains no tracking software, crash reporting libraries, analytics SDKs, or background telemetry services.
- **No Advertising**: The application is free, open source, and does not serve advertisements.
- **No Internet Access**: Symlink GUI runs completely locally on your machine. It makes no outgoing network requests and does not communicate with external servers or cloud services.

---

## 3. Local Data and System Permissions

All operations performed by Symlink GUI take place strictly on your local computer:

- **Filesystem Access**: When you select source files/folders and destination directories, paths are read solely to create filesystem links (symbolic links) on your local NTFS drive. No file contents or path metadata are sent outside your local machine.
- **Temporary State**: When using the "Pick as Symlink Source" context menu feature, the selected file paths are temporarily saved to a local file in your user temporary directory (`%TEMP%\SymlinkGUI\picked.txt`) until you drop the links or pick new items.
- **Administrator Elevation**: When creating symbolic links without Developer Mode enabled, Windows requires elevated privileges (`SeCreateSymbolicLinkPrivilege`). The User Account Control (UAC) prompt is used exclusively by the local operating system to grant permission to call the Windows `CreateSymbolicLink` API.
- **Windows Registry**: When Explorer context menu integration is toggled on, registry entries are written under `HKEY_CURRENT_USER\Software\Classes` to register shell verbs for your user account. These can be uninstalled at any time from within the app settings or via the command line.

---

## 4. Third-Party Services

Symlink GUI does not integrate with or transfer information to any third-party services, APIs, or external data brokers.

---

## 5. Children's Privacy

Because Symlink GUI does not collect any personal information whatsoever, it complies with the Children's Online Privacy Protection Act (COPPA) and General Data Protection Regulation (GDPR) standards.

---

## 6. Changes to This Privacy Policy

If this Privacy Policy is updated in future versions of the software, the updated document will be published to the project's repository with a revised "Last Updated" date.

---

## 7. Contact & Support

If you have questions or concerns regarding this Privacy Policy, please open an issue in the official project repository:

- **Repository**: [https://github.com/adrianh42/SymlinkGUI](https://github.com/adrianh42/SymlinkGUI)
- **Issues**: [https://github.com/adrianh42/SymlinkGUI/issues](https://github.com/adrianh42/SymlinkGUI/issues)
