# Unlimited Mages for Mage Arena

[![View on Thunderstore](https://img.shields.io/badge/Thunderstore-UnlimitedMages-23395B?style=for-the-badge&logo=thunderstore)](https://thunderstore.io/c/mage-arena/p/BisocM/UnlimitedMages/) 
[![Latest Release](https://img.shields.io/github/v/release/BisocM/UnlimitedMages?style=for-the-badge)](https://github.com/BisocM/UnlimitedMages/releases/latest)

-----
**Unlimited Mages** is a BepInEx mod for Mage Arena that removes the hardcoded player limit, allowing for massive custom games. Upper limit is 128v128 players.

## Features

This mod is quite basic in its nature - it allows the expansion of the base game's 4v4 limit up to 128v128, with an attempt to include adaptive UI scaling, basic networking for ease of configuration, and others, to improve the user experience. 

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
    * In your Steam Library, right-click **Mage Arena** -> `Manage` -> `Browse local files`.
    * Extract the contents of the BepInEx zip file directly into your game's root folder.
    * Run the game once to let BepInEx generate its necessary folders, then close the game.
2.  **Install the Mod:**
    * Download this mod from the Thunderstore page by clicking **Manual Download**.
    * Extract the contents of the downloaded zip.
    * Place the `UnlimitedMages.dll` file into the `BepInEx/plugins/` folder inside your game directory.
3.  **Done!** Launch the game.

-----

## Usage

Configuration is handled entirely in-game by the lobby host.

1.  Ensure all players who wish to join have the mod installed.
2.  The player who wants to be the host navigates to `Play` -> `Create Lobby`.
3.  On the "Create Lobby" screen, a new **Team Size** slider will appear above the buttons.
4.  The host uses this slider to set the desired players per team (e.g., setting it to 8 creates an 8v8 lobby).
5.  The host creates the lobby. Other players can now find and join it from the server browser. The team size is automatically synced to them.

-----

## FAQ

#### **Q: Does everyone in the lobby need this mod?**

**A:** Yes, **everyone** must have the mod installed. This is not optional. A player without the mod will crash or be unable to join, because:

1.  Their game's code will not recognize a lobby with more than 8 players as valid.
2.  Their UI doesn't have the code to draw the extra player slots.
3.  Their game will throw an error when the host sends data for a 9th player because its internal data structures (arrays) are too small to handle it.

#### **Q: How do I change the team/lobby size?**

**A:** The **lobby host** sets the team size using the new slider on the "Create Lobby" screen. Clients who join the lobby will automatically have their game adjusted to match the host's settings.

#### **Q: What is the maximum number of players?**

**A:** The mod supports a `TeamSize` of up to 128 (for a 128v128, 256-player match). However, for the best performance and stability, **8v8 (16 players) or 16v16 (32 players) is the recommended maximum.** Pushing beyond that may lead to severe performance issues or unexpected bugs.

#### **Q: My friend and I both have the mod, but they still can't join! What's wrong?**

**A:**

1.  **Restart the game.** This mod patches a lot of things on startup. A clean restart for all players is the best first step.
2.  **Verify Mod Version.** Make sure everyone is running the same version of Unlimited Mages. Mismatched versions can cause conflicts.
3.  **Note the "re-join" bug.** The base game has issues if a player leaves a lobby and tries to rejoin. Their player model might be invisible or broken. The custom network cleanup in this mod helps, but the issue can still occur. A full game restart is the only guaranteed fix.

#### **Q: What happens if the host leaves the game mid-match?**

**A:** The base game does not have host migration. If the host (the player who created the lobby) disconnects, the server will shut down and the match will end for everyone.

#### **Q: I found a bug. What do I do?**

**A:** Please report any bugs using the "Issues" tab on the GitHub repository.

-----

## Contributing

This project is open source. If you wish to contribute, feel free to fork the repository and submit a pull request. You will likely need to update the `.csproj` file to point to the correct locations for the game's DLL files on your machine.