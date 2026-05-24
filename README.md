# Elin Mods

A collection of [BepInEx](https://github.com/BepInEx/BepInEx) mods for the game **[Elin](https://store.steampowered.com/app/2135150/Elin/)**, built with [HarmonyLib](https://github.com/pardeike/Harmony) and targeting **.NET Standard 2.0**.

Repository: <https://github.com/Yuof/Elin-Mods>

## Mods

### 🗺️ AutoExplore — `Elin_AutoExplore`

[**Steam Workshop**](https://steamcommunity.com/sharedfiles/filedetails/?id=3365829584)

![AutoExplore screenshot](https://images.steamusercontent.com/ugc/16420027241307950/5A5F4F03A03135AB0E6B8EC22D2025B67BF50AB7/)

Automates exploration of the current map with a single keypress.

- Press **L** to start auto-exploring the current map.
- Press **L + Left Shift** to cycle modes: *Explore → Gather → Mine → Combined*.
- Handles auto-fight, trap disarming, looting, harvesting, mining, meditation, sleep, shrine interaction and food consumption.
- Task priority: fight visible enemies → disarm visible traps → explore & loot.
- Configurable exclusion lists for gathering and mining (in-game right-click menu with Shift, or via config file).
- In-game configuration menu — middle-click your character to open it.
- Keybinds available for moving up/down stairs.

**Plugin ID:** `yuof.elin.autoExplore.mod`
**Config:** `Elin/BepInEx/config/yuof.elin.autoExplore.mod.cfg`

#### Screenshots

<p align="center">
  <img src="https://images.steamusercontent.com/ugc/16420027241272152/5A5F4F03A03135AB0E6B8EC22D2025B67BF50AB7/" width="240" />
  <img src="https://images.steamusercontent.com/ugc/16421205175474211/567FC7C0BB5093A8264CEC2B3176A63863549BEB/" width="240" />
  <img src="https://images.steamusercontent.com/ugc/16420571196349772/14FC746AD033A907AD436A09F4D56A1615E944B2/" width="240" />
  <img src="https://images.steamusercontent.com/ugc/16422020601285599/0C6DAE963D837C5CF185AD3D991FE6659F90F4E6/" width="240" />
  <img src="https://images.steamusercontent.com/ugc/16422020601285592/6DD47D47CECD2D82F21BB944D55265B98EFE537B/" width="240" />
  <img src="https://images.steamusercontent.com/ugc/16422020601285609/049B486AFD19DA306CE9B2C4815F3E75BA5A28A9/" width="240" />
</p>

### 🎨 UI Extensions — `ElinUI`

[**Steam Workshop**](https://steamcommunity.com/sharedfiles/filedetails/?id=3369689937)

![UI Extensions screenshot](https://images.steamusercontent.com/ugc/16420571198010007/1F0D9D77F9D046F2CD6A5449B2B5ACDF5C4C6B05/)

Quality-of-life additions to the game's UI.

- Mana bar in the party window.
- Item value shown in tooltips.
- Tourism value shown for relevant items.
- Decay percentage displayed (100% = rotten).
- Combat log and loot list widgets, plus tooltip and roster button improvements.

**Plugin ID:** `yuof.elin.uiExtensions.mod`

#### Screenshots

<p align="center">
  <img src="https://images.steamusercontent.com/ugc/16420571197998131/C101C280B12C54E2BF02A8003F109A9D97A8DC92/" width="240" />
  <img src="https://images.steamusercontent.com/ugc/16420571197998120/36E666B75543CFAA35EA6939C2222710D2EF1C84/" width="240" />
  <img src="https://images.steamusercontent.com/ugc/16420571197998133/7E61C921D0237462383AEC97F034B6F2C2E5F185/" width="240" />
</p>

### 🛠️ Skill Helper — `Elin_SkillHelper`

Automates repetitive skill-training actions. Automatically stops on low stamina.

> Not published to the Workshop — build from source.

| Key | Action       |
| --- | ------------ |
| `O` | Shearing     |
| `P` | Performance  |
| `K` | Watering     |
| `U` | Lockpicking  |
| `I` | Stop / cancel |

**Plugin ID:** `yuof.elin.skillhelper.mod`

## Installation

1. Install [BepInEx](https://docs.bepinex.dev/) for Elin.
2. Build the solution (see below) or grab a prebuilt mod folder.
3. Copy each mod's output `.dll` together with its `package.xml` into:
   ```
   Elin/Package/Mod_<ModName>/
   ```
4. Launch the game and enable the mods from the in-game mod menu.

## Building

Requirements:
- Visual Studio 2022 / 2026 or the .NET SDK
- A local installation of Elin (game assemblies are referenced from the install path)

```powershell
dotnet build ElinMods.sln -c Release
```

Each project copies its output `.dll` to `Elin/Package/Mod_<ModName>/`. Update assembly reference paths in the `.csproj` files if your Elin installation is in a non-default location.

## Project Structure

| Project              | Description                          |
| -------------------- | ------------------------------------ |
| `Elin_AutoExplore`   | Auto-exploration / combat / gathering |
| `EIinUI` (`ElinUI`)  | UI quality-of-life extensions        |
| `Elin_SkillHelper`   | Skill-training automation helpers    |

## Author

**Yuof** — <https://github.com/Yuof>

## License

**Copyright © 2025 Yuof. All rights reserved.**

See [`LICENSE`](./LICENSE) for the full terms.