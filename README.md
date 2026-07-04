# DynamicTeleportAreas

DynamicTeleportAreas is a performance utility mod for Valheim that fixes the long portal loading screens caused by using the [Render Limits](https://github.com/JereKuusela/valheim-render_limits) mod with increased loaded areas and/or generated areas.

## The Problem and The Solution

### The Problem

When using [Render Limits](https://github.com/JereKuusela/valheim-render_limits) to see further out into the world, traveling through portals can take a very long time. This happens because Valheim forces the game to completely load all those extra-large distances while you are still inside the loading screen, causing the portal transition to drag on.

### The Solution

DynamicTeleportAreas solves this by automatically managing your view distance behind the scenes:

1. Entering a Portal: The moment you step into a portal, the mod instantly drops your view distance back down to the normal game defaults.
2. Fast Loading: Because the game only has to load a normal, small area, you pass through the loading screen quickly.
3. Arriving: Once you step out of the portal and are already moving around, the mod waits a brief moment and then smoothly slides your view distance back up to your preferred high settings.

Because this extra detail is loaded while you are already playing, the game handles it in the background, completely eliminating the long loading screen wait times.

## Features

* Fast Portals: Keeps portal loading screens short, even if you normally play with extreme view distances.
* Background Loading: Forces the game to load distant scenery while you are actively moving around, rather than freezing you on a loading screen.
* Smooth Arrival: Includes a slight delay after you exit a portal so the game can focus on loading your immediate surroundings before it starts drawing the distant horizon.

## Configuration Settings

The mod creates a configuration file named com.arifogel.dynamicteleportareas.cfg inside your BepInEx\config\ folder the first time you run the game. You can edit this file with a text editor or a mod manager like r2modman.

1. Global Settings
   * *Enabled* (Enabled/Disabled): Turns the mod on or off.
   * *MessageHudNotifications* (Enabled/Disabled): If turned on, a small text alert will appear in the top-left corner of your screen whenever the mod shifts your view distances.

2. Standard Gameplay Environment
   * *NormalLoadedArea* (Number): Your preferred high visibility distance for objects and structures while running around.
   * *NormalGeneratedArea* (Number): Your preferred high visibility distance for the terrain and mountains in the far distance.

3. Portal Transition Environment
   * *PortalLoadedArea* (Number): The temporary, lower object/structure visibility distance used during the portal loading screen (set to the standard Valheim default of 2).
   * *PortalGeneratedArea* (Number): The temporary, lower terrain rendering distance used during the portal loading screen (set to the standard Valheim default of 4).

4. Engine Timing Controls
   * *FrameDelayCount* (Number): How many animation frames the mod waits after you exit a portal before it starts expanding your view distance back to your high settings (default is 30 frames).

## Requirements

* BepInEx Pack for Valheim (Version 5.4.x) [[Nexus](https://www.nexusmods.com/site/mods/115)] [[Thunderstore](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)]

* [Render Limits](https://github.com/JereKuusela/valheim-render_limits) [[Nexus](https://www.nexusmods.com/valheim/mods/1842)] [[Thunderstore](https://thunderstore.io/c/valheim/p/JereKuusela/Render_Limits)] by Jere Kuusela

## Installation

1. Ensure both BepInEx and Render Limits are installed and working.

2. Download DynamicTeleportAreas.dll.

3. Place the .dll file into your game's plugins folder:

   * Manual Installation: Steam\steamapps\common\Valheim\BepInEx\plugins\

   * r2modman Users: Select your profile, click Help, click Browse profile folder, and navigate to BepInEx/plugins/.

4. Start the game to automatically generate and apply the default settings.
