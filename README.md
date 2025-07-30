# Unlimited Mages for Mage Arena

**Unlimited Mages** is a BepInEx mod for Mage Arena that removes the hardcoded player limit, allowing for much larger teams and lobbies. It comprehensively patches the game's UI, networking, and core logic to create a seamless and stable experience for custom games with up to 32 players (16v16).

-----

## Features

This mod goes beyond a simple number change and patches multiple game systems to ensure stability and a good user experience.

* **Configurable Team Size:** Use the config file to set the number of players per team, from the default of 4 up to 16.
* **Dynamic UI Scaling:** The UI is dynamically rebuilt to handle more players:
    * **Lobby:** Player name lists and character models are resized and rearranged to fit everyone.
    * **Scoreboard:** The end-of-game scoreboard automatically adds new slots for all players.
    * **Kill Feed:** The in-game death message feed is rescaled to show all players without overlap.
* **Wrapping Lobby Layout:** Player models in the lobby are arranged in a clean, multi-row staggered grid, preventing them from overflowing the screen.
* **Robust Network Cleanup:** Replaces the game's original, unstable lobby exit logic with a custom, ordered shutdown sequence. This fixes crashes and instability when leaving large lobbies.
* **Core Logic Patches:**
    * Adjusts server-side logic for team assignments to recognize the new player counts.
    * Fixes lobby Browse so players can correctly see and join custom-sized lobbies.
    * Corrects announcer audio for multi-kills, preventing errors when more than four players are killed in a row.
    * Fixes a base-game bug where players who left were not correctly removed from the kick-player list.

-----

## Installation

It is highly recommended to use a mod manager for the best experience.

### With a Mod Manager (Recommended)

1.  Click the **Install with Mod Manager** button on the Thunderstore page.
2.  The mod manager will install the mod and all its dependencies for you.
3.  Launch the game by clicking **Start modded** in the manager.

### Manually

If you prefer to install manually, you must install the BepInEx dependency first.

1.  **Install BepInEx:**
    * Download **BepInEx 5.x (x64)** from the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases).
    * In your Steam Library, right-click **Mage Arena** -\> `Manage` -\> `Browse local files`.
    * Extract the contents of the BepInEx zip file directly into your game's root folder.
    * Run the game once to let BepInEx generate its necessary folders, then close the game.
2.  **Install the Mod:**
    * Download this mod from the Thunderstore page by clicking **Manual Download**.
    * Extract the contents of the downloaded zip.
    * Place the `UnlimitedMages.dll` file into the `BepInEx/plugins/` folder inside your game directory.
3.  **Done\!** Launch the game.

-----

## Configuration

The first time you run the game with the mod installed, a configuration file will be generated. You can edit this file to change the team size.

* **File Location:** `BepInEx/config/com.magearena.unlimited_mages.cfg`

**IMPORTANT:** All players in the lobby **MUST** have the exact same value for `TeamSize`.

```ini
## Settings file was created by plugin Unlimited Mages v1.0.0
## Plugin GUID: com.magearena.unlimited_mages

[General]

## The maximum number of players allowed per team. The game is 2 teams, so total lobby size will be (TeamSize * 2).
# Setting type: Int32
# Default value: 5
# Acceptable value range: From 4 to 16
TeamSize = 5
```

-----

## FAQ

#### **Q: Does everyone in the lobby need this mod?**

**A:** Yes, **everyone** must have the mod installed and configured to the **exact same team size**. This is not optional. A player without the mod (or with a different config) will crash or be unable to join, because:

1.  Their game's code will not recognize a lobby with more than 8 players as valid.
2.  Their UI doesn't have the code to draw the extra player slots.
3.  Their game will throw an error when the host sends data for a 9th player because its internal data structures (arrays) are too small to handle it.

#### **Q: How do I change the team/lobby size?**

**A:** After running the game once with the mod, a config file is created at `BepInEx/config/com.magearena.unlimited_mages.cfg`. Open this file and change the `TeamSize` value. A `TeamSize` of 8 will create an 8v8 lobby for 16 total players. **Ensure every player in the lobby edits their file to have the same number.**

#### **Q: What is the maximum number of players?**

**A:** The mod supports a `TeamSize` of up to 16 (for a 16v16, 32-player match). However, for the best performance and stability, **8v8 (16 players) is the recommended maximum.** Pushing beyond that may lead to performance issues or unexpected bugs.

#### **Q: My friend and I both have the mod and the same config, but they still can't join\! What's wrong?**

**A:**

1.  **Restart the game.** This mod patches a lot of things on startup. A clean restart for all players is the best first step.
2.  **Verify the config file.** Have one person send their `.cfg` file to everyone else to guarantee they are identical. A single number difference will break the lobby.
3.  **Note the "re-join" bug.** The base game has issues if a player leaves a lobby and tries to rejoin. Their player model might be invisible or broken. The custom network cleanup in this mod helps, but the issue can still occur. A full game restart is the only guaranteed fix.

#### **Q: What happens if the host leaves the game mid-match?**

**A:** The base game does not have host migration. If the host (the player who created the lobby) disconnects, the server will shut down and the match will end for everyone.

#### **Q: I found a bug. What do I do?**

**A:** Please report any bugs using the "Issues" tab on the GitHub repository. As noted, this mod may not be actively maintained to keep up with all future game updates, but bug reports are still valuable.

-----

## Contributing

This project is open source. If you wish to contribute, feel free to fork the repository and submit a pull request. You will likely need to update the `.csproj` file to point to the correct locations for the game's DLL files on your machine.