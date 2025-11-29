<div align="center">

# [SwiftlyS2] PluginsAutoUpdate

[![GitHub Release](https://img.shields.io/github/v/release/agasking1337/PluginsAutoUpdate?color=FFFFFF&style=flat-square)](https://github.com/agasking1337/PluginsAutoUpdate/releases/latest)
[![GitHub Issues](https://img.shields.io/github/issues/agasking1337/PluginsAutoUpdate?color=FF0000&style=flat-square)](https://github.com/agasking1337/PluginsAutoUpdate/issues)
[![GitHub Downloads](https://img.shields.io/github/downloads/agasking1337/PluginsAutoUpdate/total?color=blue&style=flat-square)](https://github.com/agasking1337/PluginsAutoUpdate/releases)
[![GitHub Stars](https://img.shields.io/github/stars/agasking1337/PluginsAutoUpdate?style=social)](https://github.com/agasking1337/PluginsAutoUpdate/stargazers)<br/>
  <sub>Made by <a href="https://github.com/agasking1337" rel="noopener noreferrer" target="_blank">aga</a></sub>
  <br/>
</div>

## Overview

**PluginsAutoUpdate** is an experimental auto-updater plugin for SwiftlyS2 that automatically checks GitHub releases and updates your plugins when new versions are available. It monitors configured repositories, compares versions, and seamlessly updates plugins by downloading and replacing DLL files or extracting ZIP packages while preserving your configuration files.

> [!CAUTION]
> This is an **experimental plugin**. It makes HTTP requests to GitHub and overwrites plugin files on disk. **Use at your own risk and keep backups.** While it works with Swiftly's hot-reload, for consistent behavior you should treat updates as requiring a **full server restart**.

## Download Shortcuts
<ul>
  <li>
    <code>📦</code>
    <strong>&nbspDownload Latest Plugin Version</strong> ⇢
    <a href="https://github.com/agasking1337/PluginsAutoUpdate/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
  <li>
    <code>⚙️</code>
    <strong>&nbspDownload Latest SwiftlyS2 Version</strong> ⇢
    <a href="https://github.com/swiftly-solution/swiftlys2/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
</ul>

## Features
- **Automatic Updates**: Periodically checks GitHub releases for configured plugins and updates them automatically.
- **Version Tracking**: Smart version comparison using DLL metadata and `.version` sidecar files with hash verification.
- **Multiple Format Support**: Handles both single DLL files and ZIP packages containing full plugin folders.
- **Config Protection**: Preserves existing configuration files during ZIP-based updates to prevent data loss.
- **Manual Checks**: Use the `checkupdate` console command to trigger updates on demand.
- **Backup System**: Creates `.bak` backup files before replacing DLLs for easy rollback.
- **Flexible Repository Format**: Supports both `Owner/Repo` format and full GitHub URLs.

## Screenshots
> No screenshots needed for this plugin.

## Plugin Setup
> [!WARNING]
> Make sure you **have installed SwiftlyS2 Framework** before proceeding.

1. Download and extract the latest plugin version into your `swiftlys2/plugins/PluginsAutoUpdate` folder.
2. Start your server to allow the plugin to generate the default configuration file.
3. The configuration file will be created at: `swiftlys2/configs/plugins/PluginsAutoUpdate/config.toml`
4. Edit the configuration file to add the plugins you want to auto-update (see Configuration Guide below).
5. Restart the server or reload the plugin to apply changes.
6. The plugin will now automatically check for updates based on your configured interval.

## Configuration Guide

The plugin uses a TOML configuration file located at `configs/plugins/PluginsAutoUpdate/config.toml`.

**Basic Configuration:**

```toml
[PluginsAutoUpdate]
CheckIntervalMinutes = 30

[PluginsAutoUpdate.Repositories]
# Format: PluginFolderName = "Owner/Repo"
# or: PluginFolderName = "https://github.com/Owner/Repo"
ExamplePlugin = "someuser/example-plugin"
AnotherPlugin = "https://github.com/anotheruser/another-plugin"
```

**Configuration Options:**

- `CheckIntervalMinutes` (default: 30): How often the plugin checks for updates (minimum 1 minute).
- `Repositories`: Key-value pairs where:
  - **Key**: The exact plugin folder name in `swiftlys2/plugins/`
  - **Value**: GitHub repository in format `Owner/Repo` or full URL

**Example with Multiple Plugins:**

```toml
[PluginsAutoUpdate]
CheckIntervalMinutes = 60

[PluginsAutoUpdate.Repositories]
GameManager = "criskkky/sws2-gamemanager"
MapConfigs = "https://github.com/criskkky/sws2-mapconfigs"
```

## Backend Logic (How It Works)

1. **Initialization**: On plugin load, the service provider is created with dependency injection, initializing all services (Configuration, GitHub API, Version Management, Update).

2. **Scheduled Checks**: Every `CheckIntervalMinutes`, the plugin:
   - Reloads the configuration to pick up any changes
   - Iterates through all configured repositories
   - For each repository, calls GitHub's `/releases/latest` API endpoint

3. **Version Comparison**:
   - Extracts the version tag from the latest GitHub release (e.g., `v1.0.4`)
   - Checks local version using this priority:
     1. `.version` sidecar file with SHA256 hash verification
     2. DLL assembly version metadata
     3. DLL file version info
     4. Live loaded plugin version from memory

4. **Update Process** (when remote version > local version):
   - **For DLL assets**: Downloads the file, creates a `.bak` backup, and replaces the plugin DLL
   - **For ZIP assets**: Downloads and extracts to a temporary folder, finds the main DLL, copies all files to the plugin folder while **preserving existing config files**

5. **Version Persistence**: After successful updates, creates/updates a `.version` sidecar file containing the version and file hash for future comparisons.

6. **Manual Trigger**: The `checkupdate` console command runs the same update check logic immediately without waiting for the scheduled interval.

## Commands

- `checkupdate` - Manually trigger an update check for all configured plugins (console only)

## Support and Feedback
Feel free to [open an issue](https://github.com/agasking1337/PluginsAutoUpdate/issues/new) for any bugs or feature requests. If it's all working fine, consider starring the repository to show your support!

## Contribution Guidelines
Contributions are welcome! This plugin has been refactored with clean architecture principles including:
- Separation of responsibilities (Services pattern)
- Dependency injection with IServiceProvider
- Interface-based design for testability

For major changes, please open an issue first to discuss what you would like to change.

## Credits
- Developed by [aga](https://github.com/agasking1337)
- Readme template by [criskkky](https://github.com/criskkky)
- All contributors listed in the [Contributors Section](https://github.com/agasking1337/PluginsAutoUpdate/graphs/contributors)