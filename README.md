<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>Experimental - PluginsAutoUpdate</strong></h2>
  <h3>Checks the github releases and compare the versions for update. If update available will update.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/aga/PluginsAutoUpdate/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/aga/PluginsAutoUpdate?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/aga/PluginsAutoUpdate" alt="License">
</p>

## Getting Started (delete me)

1. **Edit `PluginMetadata` Attribute**  
   - Set your plugin's `Id`, `Name`, `Version`, `Author` and `Description`.
2. **Edit `PluginsAutoUpdate.csproj`**  
   - Set the `<AssemblyName>` property to match your plugin's main class name.
   - Add any additional dependencies as needed.
3. **Implement your plugin logic** in C#.
   - Place your main plugin class in the root of the project.
   - Use the SwiftlyS2 managed API to interact with the game and core.
4. **Add resources**  
   - Place any required files in the `gamedata`, `templates`, or `translations` folders as needed.

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.

## Experimental notice

This is an **experimental auto-updater**, not a fully supported production plugin.  
It makes HTTP requests to GitHub and overwrites plugin files on disk. **Use at your own risk and keep backups.**  
It also relies on Swiftly's hot-reload behaviour, which is **not ideal** for many plugins – for consistent behaviour you should treat updates as requiring a **full server restart**.

## How it works (short)

- Reads config from `configs/plugins/PluginsAutoUpdate/config.toml`.
- Every `CheckIntervalMinutes` minutes it:
  - For each entry in `[PluginsAutoUpdate.Repositories]` (key = plugin folder name, value = `Owner/Repo` or GitHub URL), calls `/releases/latest`.
  - Compares the latest tag (e.g. `v1.0.4`) with the local version (DLL / `.version` sidecar).
  - If remote > local:
    - **DLL asset** → replace main DLL (backup `.bak` when possible).
    - **ZIP asset** → extract and copy the whole plugin folder into `<swiftly_root>/plugins/<PluginName>/`, but do **not** overwrite existing files under `configs/`.

## Quick config

File (auto-created on first run):

```text
<swiftly_root>/configs/plugins/PluginsAutoUpdate/config.toml
```

Minimal example:

```toml
[PluginsAutoUpdate]
CheckIntervalMinutes = 30

[PluginsAutoUpdate.Repositories]
PluginFolder = "Owner/Repo"
```

Steps:

1. Install the plugin normally in `plugins/`.
2. Start the server once so `config.toml` is created.
3. Edit `[PluginsAutoUpdate.Repositories]` and add your `PluginFolder = "Owner/Repo"`.
4. Restart server or reload the plugin.