# DataCenter-ModLoader

A [MelonLoader](https://melonwiki.xyz/) plugin for **Data Center** that injects Steam Workshop mods into the game at runtime.

## Overview

DataCenter-ModLoader acts as a bridge between the Steam Workshop and the game. It hooks into MelonLoader on startup and automatically loads any Workshop mods subscribed to via Steam, without requiring manual file management.

## Requirements

- [MelonLoader](https://melonwiki.xyz/#/?id=requirements) (latest stable)
- Steam with Data Center installed
- [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) (latest release)
- `steam_api64.dll` (found in your Data Center install at `Data Center_Data\Plugins\x86_64`)

## Installation

1. Install **MelonLoader** for Data Center if you haven't already.
2. Download the latest release of `DataCenter-SteamPlugin.dll` from the [Releases](https://github.com/ASavageSwan/-DataCenter-ModLoader/releases) page.
3. Download **Steamworks.NET** and copy its `.dll` into the `UserLibs\` folder in your Data Center game directory.
4. Copy `steam_api64.dll` from Game install location `\Data Center_Data\Plugins\x86_64\` into the same `UserLibs\` folder.
5. Place `DataCenter-SteamPlugin.dll` into the `Mods\` folder inside the Data Center game directory.
6. Launch the game — MelonLoader will load the plugin automatically on startup.

## manifest.json

The ModLoader requires a `manifest.json` file placed in the root of your `Mods\` folder to function. Without it, mods\plugins will not be loaded.

```json
{
    "Name": "My Mod Pack",
    "Mods": [
        "mods\\MyMod.dll"
    ],
    "Library": [
        "lib\\MyLib.dll"
    ],
    "Plugins": [
        "MyPlugin.dll"
    ]
}
```

| Field | Description |
|---|---|
| `Name` | The name of your mod collection / project |
| `Mods` | List of mod `.dll` paths relative to the `Mods/` folder |
| `Library` | List of library `.dll` paths relative to the `Mods/` folder |
| `Plugins` | List of plugin `.dll` paths relative to the `Mods/` folder |

## Usage

Once installed, the ModLoader runs silently in the background. Any Steam Workshop mods you are subscribed to will be injected into the game automatically when it starts.

No additional configuration is required.

## Building from Source

1. Clone this repository
2. Open `DataCenter-SteamPlugin.sln` in Visual Studio or Rider.
3. Restore NuGet packages and build in `Release` mode.
4. The compiled `.dll` will be in `DataCenter-SteamPlugin/bin/Release/`.
## License

This project is licensed under the [MIT License](LICENSE).

---

© 2026 ASavageSwan. All rights reserved.
