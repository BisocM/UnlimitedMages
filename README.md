# Unlimited Mages for Mage Arena

**Unlimited Mages** is a mod that unlocks the player limit, allowing for much larger teams and lobbies than the base game supports. It dynamically scales the in-game UI and networking logic to create a seamless experience for custom games with up to 16 players (8v8) or more.

---

## Features

* **Configurable Team Size:** Set the number of players per team via a simple configuration file.
* **Dynamic UI Scaling:** The lobby, scoreboard, and in-game kill feed are all dynamically resized to properly display all players without visual clutter.
* **Staggered Lobby Layout:** Player models in the lobby are arranged in a clean, multi-row grid to accommodate larger teams.
* **Network Compatibility:** Works for both the host and clients joining the lobby.
* **Announcer Fixes:** Prevents announcer audio errors when multi-kills exceed the default four-player limit.

---

## Requirements

* **[BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases)** (the `x64` version).

---

## Installation

If this is your first time using BepInEx, start with the **"First-Time BepInEx Setup"**. If you already have BepInEx installed, you can skip to **"Mod Installation"**.

### First-Time BepInEx Setup

1.  **Download BepInEx:** Go to the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases) and download the file named `BepInEx_x64_[version].zip`.
2.  **Find the Game Folder:** In your Steam Library, right-click **Mage Arena** -> `Manage` -> `Browse local files`. This will open the main game directory.
3.  **Extract BepInEx:** Open the downloaded zip file and drag all of its contents directly into your game's root folder (the one you opened in the previous step).
4.  **Run the Game:** Launch **Mage Arena** once. You will see a BepInEx console window appear for a moment. This will generate the necessary folders for your mods. After you reach the main menu, you can close the game.

### Mod Installation

5.  **Download the Mod:** Download the `UnlimitedMages.dll` file from the releases page.
6.  **Place the Mod File:** Navigate to the `BepInEx/plugins/` folder that was just created inside your game directory.
7.  **Drag and Drop:** Place the `UnlimitedMages.dll` file into the `plugins` folder.
8.  **Done!** Launch the game. The mod is now installed.

---

## Configuration

The first time you run the game with the mod installed, it will generate a configuration file.

* **File Location:** `BepInEx/config/com.magearena.unlimited_mages.cfg`

You can edit this file with any text editor to change the mod's settings. Please note that there is an upper limit of 16 per team.

```ini
## Settings file was created by plugin Unlimited Mages Mod v1.0.0
## Plugin GUID: com.magearena.unlimited_mages

[General]

## The number of players per team.
# Setting type: Int32
# Default value: 5
TeamSize = 5
```

## Issues & Contributing

Please feel free to create an issue if you have identified any, and propose appropriate changes via forks. I do not foresee myself maintaining this mod perilously in the future, and most likely won't track breaking updates and releases.

If you wish to contribute, you may need to fix the references to the packaged DLL files.

# FAQ
#### **Q: Does everyone need this mod, or just the host?**
**A:** Unfortunately, **everyone** in the lobby must have the mod installed. This is not optional. The game's code for UI, networking, and core logic is run on each player's machine. A player without the mod:
1.  **Cannot join:** Their game won't recognize a lobby with more than 8 players as valid due to the hardcoded game logic.
2.  **Cannot see:** Their UI doesn't have the code to draw the extra player slots in the lobby or on the scoreboard.
3.  **Will crash:** Their game will get an error when the host sends data for a 9th player because its internal arrays are too small.

#### **Q: How do I change the team/lobby size?**
**A:** After running the game once with the mod, a configuration file will be created at `BepInEx/config/com.magearena.unlimited_mages.cfg`. You can open this file with any text editor and change the `TeamSize` value.

**All players must set this number to the exact same value.** A lobby size of 16 would mean setting `TeamSize = 8`.

#### **Q: What is the maximum number of players you recommend?**
**A:** The mod is designed and tested to work comfortably with up to 16 players (8v8). While it might be possible to push the number higher in the config file, you may encounter performance degradation, UI scaling issues, or other unexpected bugs.

#### **Q: My friend and I both have the mod and the same config, but they still can't join. What's wrong?**
**A:** I heavily suggest a restart of the game. The game still has bugs that are not caused by mods (such as dissonance enumeration error when a player leaves).

If your friendd joins and then leaves, they cannot join back without completely breaking their visual presence in the lobby. However, a connection would still be healthily established and they should be able to join teams, from my experience.

#### **Q: What happens if the host leaves the game mid-match?**
**A:** The game does not have a host migration system. If the host (the player who created the lobby) disconnects, the server will shut down and the match will end for all players.

#### **Q: I have found a bug. What do I do?**
**A:** You may report it using the issue functionality on GitHub.