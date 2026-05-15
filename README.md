# Devil Spire Extender (DSE)

Devil Spire Extender (DSE) is a modding framework for *Devil Spire Falls*, inspired by SKSE. It provides a launcher and a core API to allow modders to extend the game without modifying original files.

## Features (GDScript-First Update)
*   **GDScript-First API**: Write normal GDScript that interacts directly with game objects.
*   **Thin Core**: Minimal abstraction, providing direct access to the `Player` and `Global` systems.
*   **Dynamic Loading**: The launcher uses DLL injection to load mod packs without modifying game files.
*   **User-Friendly Launcher**: Manage mods, set load order, and launch the game with one click.
*   **Non-Destructive Modding**: Mods are loaded dynamically using Godot's resource pack system.

## Getting Started

### For Players
1.  Download the latest DSE Launcher.
2.  Ensure *Devil Spire Falls* is installed.
3.  Place mods in the `mods/` folder inside the game directory.
4.  Run the launcher and click **Launch Game**.

### For Modders
Check out the [MODDING_GUIDE.md](MODDING_GUIDE.md) for detailed instructions on how to create your first mod using the new GDScript-first API.

## Design & Architecture
DSE follows a "Loader" pattern:
1. **Launcher (C#)**: Manages mods and uses DLL injection to load the DSE Core and mod packs into the game process.
2. **Bootstrapper (GDScript)**: Included in `DSECore.pck`, it hijacks the game's global singleton to initialize the DSE Core and mount mods.
3. **Core API (GDScript)**: Injected as `dse.gd`, it provides the hook system and game accessors.
4. **Hook System (Advanced)**: For complex hooks that require modifying game scripts, developers must provide their own patched versions of game files in the `patches/` directory, as original game code is not redistributed with DSE.

## License
MIT
