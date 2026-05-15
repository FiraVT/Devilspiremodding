# Passive Regeneration Mod

**Version:** 1.0.0
**Author:** DSE Team
**For:** Devil Spire Falls

## Description

Adds Skyrim-style passive health and mana regeneration to Devil Spire Falls. Your character will now slowly regenerate HP and MP over time, with different rates for combat vs. non-combat situations.

## Features

### Health Regeneration
- **Out of Combat:** 0.70% of maximum HP per second
- **In Combat:** 0.49% of maximum HP per second

### Mana Regeneration
- **Out of Combat:** 3.00% of maximum MP per second
- **In Combat:** 0.99% of maximum MP per second

### Smart System
- Automatically detects combat state
- Does not regenerate while dead or knocked out
- Works seamlessly with existing game mechanics

## Installation

1. Place the `PassiveRegen` folder in your `mods/` directory
2. Launch the game with the DSE launcher
3. Regeneration will start automatically

## Configuration

To adjust regeneration rates, edit `init.gd` and modify these values:

```gdscript
const HP_REGEN_OUT_OF_COMBAT = 0.0070  # 0.70% per second
const HP_REGEN_IN_COMBAT = 0.0049      # 0.49% per second
const MP_REGEN_OUT_OF_COMBAT = 0.0300  # 3.00% per second
const MP_REGEN_IN_COMBAT = 0.0099      # 0.99% per second
```

## Technical Notes

- Uses Godot's `_process(delta)` to apply regeneration every second
- Accesses player via `/root/Global.player.body`
- Respects game's combat state system
- No hooks required - uses direct property access

## Compatibility

- **Compatible with:** All mods (no hooks or patches required)
- **Performance:** Negligible impact (single calculation per second)
- **Save Compatible:** Yes (regeneration is not saved, recalculates each tick)

## Testing

To verify the mod is working:
1. Start a new game or load a save
2. Check the DSE log (`dse_bootstrap.log`) for:
   ```
   [DSE INFO] [PassiveRegen] Mod loaded successfully!
   ```
3. Take damage and observe HP regenerating over time
4. Use magic and observe MP regenerating over time
5. Enter combat - regeneration should slow down

## Why This Mod Matters

This is the **first test mod** for the DSE modding API. It demonstrates:
- ✅ Mod loading system works
- ✅ Direct game access works
- ✅ Timer-based modifications work
- ✅ Combat state detection works

## Future Improvements (requiring hooks)

With full hook support, this mod could:
- Hook into damage events for instant feedback
- Hook into combat state changes for smoother transitions
- Add configurable UI elements
- Add sound effects on regeneration ticks
- Add visual effects (healing aura)

## Known Limitations

**Current Implementation:**
- Does not use hooks (they're not implemented yet)
- Directly accesses game objects
- May break if game structure changes
- No configuration UI (must edit code)

**With Hooks (Future):**
- Would be more resilient to game updates
- Could add UI configuration
- Could integrate with other mods via events
- Could add sound/visual effects

## License

MIT License - Free to modify and redistribute

## Support

For issues or questions, see the DSE documentation or create an issue on the DSE GitHub repository.
