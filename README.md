# UCB - UncomplicatedCustomBots

A server plugin for **SCP: Secret Laboratory** that fills your server with semi-smart AI bots. Works with both **[LabApi](https://github.com/northwood-studios/LabAPI)** and **[EXILED](https://github.com/ExMod-Team/EXILED)**.

Bots spawn automatically at the start of each round, pick a team, move through the facility on their own, open doors, use elevators, and fight like real players.

- **Author:** Mr. Baguetter
- **Original by:** SpGerg

---

## What the bots can do

- **Fill empty servers** - bots join when there are few real players and leave to make room when more players connect.
- **Move realistically** - they walk through doors, elevators, and corridors.
- **Play as a team** - MTF, Chaos and Guards form small squads and stick together.
- **Fight, flee and search** - humans shoot, reload, take cover and heal; SCP bots have their own behaviors (049, 049-2, 106, 173, 939, 3114).
- **Pick up items** - they can grab keycards, guns, medkits and other items you allow in the config.

---

## Requirements

You only need **one** of these frameworks on your server:

- **LabApi** (version 1.1.6.1 or newer)
- **EXILED** (version 9.14.2 or newer)

---

## Installation

1. **Download the right file** for your server:
   - For LabApi servers → `UncomplicatedCustomBots-LabApi.dll`
   - For EXILED servers → `UncomplicatedCustomBots-Exiled.dll`

2. **Move the file** into your plugins folder:
    - Windows:
        - EXILED: `%APPDATA%\EXILED\Plugins`
        - LabApi: `%APPDATA%\SCP Secret Laboratory\LabAPI\plugins\global`
    - Linux:
        - EXILED: `~.config/EXILED/Plugins`
        - LabApi: `~.config/SCP Secret Laboratory/LabAPI/plugins/global`

3. **Restart your server.**

---

## Configuration

After the first start you will find a config file here:

- EXILED: `EXILED/Configs/UncomplicatedCustomBots.yml`
- LabApi: `LabAPI/configs/global/UncomplicatedCustomBots.yml`

You can edit it with any text editor. Here are the most important options:

| Option | Default | What it does |
|--------|---------|--------------|
| `MaxBots` | `10` | How many bots can spawn at round start. Higher than 10 may cause lag. |
| `MaxPlayers` | `5` | If more real players than this are online, no bots will spawn. |
| `NewPlayersReplaceBots` | `true` | When a real player joins, a bot leaves to make room. |
| `AllowScps` | `false` | Allow bots to spawn as SCPs. |
| `AttackTutorials` | `false` | Allow bots to attack players on the Tutorial team. |
| `MtfSquadSize` / `ChaosSquadSize` / `GuardSquadSize` | `2` | How many bots per squad (2 or 4). Squad members stay together. |
| `SquadRegroupDistance` | `20` | How far squad members can spread before they regroup. |
| `BlacklistedRooms` | 5 rooms | Rooms where bots will never go (e.g. Pocket Dimension). |
| `AllowedPickupItems` | all items | Which items bots are allowed to pick up. Remove items you don't want them to use. |
| `Names` | ~50 names | Random names bots will use. You can add your own. |
| `EnableCreditTags` | `true` | Show a small credit tag for the plugin developers. |
| `Debug` | `false` | Only turn on if you need help troubleshooting. |

<details>
<summary>Advanced options (click to expand)</summary>

| Option | Default | What it does |
|--------|---------|--------------|
| `DebugBatchInterval` | `2` | How often debug messages are printed (in seconds). |
| `AllowPreReleases` | `false` | Let the autoupdater install beta versions. |
| `ShowSilentLogs` | `false` | Show extra internal logs. |
| `WaypointHeightLimits` | per zone | Fixes bot height in each zone so they don't float or sink. You normally don't need to change this. |
| `MaxWaypointDistance` | `1` | Splits long paths into shorter steps so bots turn more smoothly. |
| `GithubToken` | `""` | Optional GitHub token to avoid update check limits. |
| `NavMeshAvoidanceQuality` | `Medium` | How carefully bots avoid bumping into each other. |
| `PathQueueConcurrency` | `2` | How many bots can calculate a path at the same time. Lower = smoother server with many bots. |

</details>

---

## Commands

You need the right permission to use each command.

### For admins (in Remote Admin)

All start with `ucb`:

| Command | Example | Permission | What it does |
|---------|---------|------------|--------------|
| `ucb spawn` | `ucb spawn` or `ucb spawn John` | `ucb.spawn` | Spawns a new bot. |
| `ucb goto` | `ucb goto 5 HCZ_079` | `ucb.goto` | Sends a bot to a specific room. |
| `ucb start` | `ucb start 5` | `ucb.start` | Starts the AI for a bot that is standing still. |

Tip: you can also use short versions like `ucb s` for spawn.

### For players (in game console, press `~`)

| Command | Permission | What it does |
|---------|------------|--------------|
| `follow` | `ucb.follow` | Bots near you (same team, within ~30m) will start following you. |

### Server console only

| Command | What it does |
|---------|--------------|
| `ucbupdate` | Downloads the latest version and restarts the round. |
| `ucbupdatecheck` | Checks if a new version is available. |

### Debug commands

These are for testing and troubleshooting:

| Command | What it does |
|---------|--------------|
| `ucb bot` | Show info about a bot. |
| `ucb botui` | Open a small info panel about a bot. |
| `ucb drawpath` | Draw the path bots are currently walking. |
| `ucb roombounds` | Show the borders of your current room. |
| `ucb roomchildren` | List objects inside your current room. |
| `ucb raycast` | Show what you are looking at. |

---

## Need help?

- **Discord:** https://discord.gg/5StRGu8EJV

---

## Credits

- Original by **SpGerg**
- Rewritten and maintained by **Mr. Baguetter**
