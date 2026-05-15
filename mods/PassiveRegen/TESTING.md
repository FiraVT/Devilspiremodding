# PassiveRegen Mod - Testing Guide

## Prerequisites

1. ✅ DSE launcher is built and working
2. ✅ `dse_core.gd` is in the launcher's PCK
3. ✅ `DSEBootstrap.gd` is injecting properly
4. ✅ Game launches successfully

## Installation for Testing

### Step 1: Copy Mod to Launcher Directory

```bash
# Copy the PassiveRegen folder to your game's mods directory
cp -r mods/PassiveRegen <game_directory>/mods/PassiveRegen
```

Where `<game_directory>` is where your `devil_spire_falls.exe` lives.

### Step 2: Create load_order.txt (if not exists)

```bash
# In your game directory, create:
echo PassiveRegen > load_order.txt
```

Or manually create `load_order.txt` with:
```
PassiveRegen
```

### Step 3: Launch Game with DSE

Run the DSE launcher (your C# application) which should:
1. Inject the DSE PCK
2. Load the PassiveRegen mod
3. Start the game

## Verification Steps

### Test 1: Mod Loading
**Expected:** Mod loads without errors

1. Launch game
2. Check `dse_bootstrap.log` in game directory
3. Look for:
   ```
   [DSE] Loading mod: PassiveRegen
   [DSE]   Loaded metadata: passive_regen
   [DSE]   SUCCESS: Mod initialized
   [DSE INFO] [PassiveRegen] Mod loaded successfully!
   ```

**✅ PASS:** Log shows mod loaded
**❌ FAIL:** Error messages or mod not found

### Test 2: HP Regeneration (Out of Combat)
**Expected:** HP slowly regenerates when not in combat

1. Start a new game or load save
2. Take some damage (fall, get hit by enemy)
3. Run away from enemies (out of combat)
4. Wait and watch HP bar
5. HP should regenerate approximately 0.70% per second

**Example:** With 100 max HP:
- After 10 seconds: ~7 HP restored
- After 30 seconds: ~21 HP restored

**✅ PASS:** HP regenerates when out of combat
**❌ FAIL:** HP does not change

### Test 3: HP Regeneration (In Combat)
**Expected:** HP regenerates slower during combat

1. Engage an enemy (enter combat state)
2. Take damage but don't die
3. Observe HP regeneration while still in combat
4. Should regenerate ~0.49% per second (slower)

**✅ PASS:** HP regenerates slower in combat
**❌ FAIL:** No difference between combat/non-combat

### Test 4: MP Regeneration (Out of Combat)
**Expected:** MP regenerates quickly when not in combat

1. Cast spells to deplete MP
2. Move away from enemies
3. Watch MP bar regenerate
4. Should restore ~3% per second

**Example:** With 100 max MP:
- After 10 seconds: ~30 MP restored
- After 30 seconds: ~90 MP restored (capped at max)

**✅ PASS:** MP regenerates rapidly
**❌ FAIL:** MP does not change

### Test 5: MP Regeneration (In Combat)
**Expected:** MP regenerates slower during combat

1. Enter combat
2. Use spells
3. Observe MP regeneration
4. Should restore ~0.99% per second (slower)

**✅ PASS:** MP regenerates slower in combat
**❌ FAIL:** No difference between combat/non-combat

### Test 6: Death Prevention
**Expected:** No regeneration while dead

1. Let character die
2. Observe that HP does NOT regenerate
3. HP should stay at 0

**✅ PASS:** No regeneration while dead
**❌ FAIL:** HP regenerates when dead (bug!)

### Test 7: Max HP/MP Clamping
**Expected:** Cannot exceed maximum values

1. Wait until HP/MP are full
2. Continue waiting
3. HP and MP should stay at max (not exceed)

**✅ PASS:** Values clamped to max
**❌ FAIL:** Values exceed maximum

## Debugging

### Issue: Mod Not Loading

**Check:**
1. `mods/PassiveRegen/` folder exists in game directory
2. `mod.json` file exists and is valid JSON
3. `init.gd` file exists
4. `load_order.txt` includes "PassiveRegen"

**Solutions:**
- Verify folder structure matches exactly
- Check file permissions
- Review `dse_bootstrap.log` for errors

### Issue: DSE Not Found Warning

**Symptom:** Log shows `[PassiveRegen] WARNING: DSE not found!`

**Cause:** DSE Core singleton is not loaded

**Solutions:**
1. Verify `dse_core.gd` is in the launcher's PCK
2. Check that `DSEBootstrap.gd` creates DSE node
3. Verify DSE node is added to `/root/` scene tree

### Issue: No Regeneration

**Check:**
1. Is player spawned? (`Global.player` exists?)
2. Is body accessible? (`player.body` exists?)
3. Is character alive? (not dead or knocked out?)
4. Enable debug logging in `init.gd`:

```gdscript
# Uncomment these lines at end of apply_regeneration():
if old_hp < body.maxhp or old_mp < body.maxmp:
    var dse = get_node_or_null("/root/DSE")
    if dse:
        var combat_str = "IN COMBAT" if in_combat else "out of combat"
        dse.log_info("[PassiveRegen] " + combat_str + " - HP: +" + str(snappedf(hp_to_add, 0.01)) + " | MP: +" + str(snappedf(mp_to_add, 0.01)))
```

This will log every regeneration tick to `dse_bootstrap.log`.

### Issue: Regeneration Too Fast/Slow

**Adjust constants in `init.gd`:**
```gdscript
const HP_REGEN_OUT_OF_COMBAT = 0.0070  # Increase for faster
const HP_REGEN_IN_COMBAT = 0.0049
const MP_REGEN_OUT_OF_COMBAT = 0.0300
const MP_REGEN_IN_COMBAT = 0.0099
```

## Performance Testing

**Monitor:**
- FPS (should be unaffected)
- CPU usage (negligible increase)
- Memory (no leaks)

**Expected:**
- <1ms per frame overhead
- No noticeable performance impact

## Success Criteria

✅ **Mod loads successfully**
✅ **HP regenerates out of combat (0.70%/sec)**
✅ **HP regenerates in combat (0.49%/sec)**
✅ **MP regenerates out of combat (3.00%/sec)**
✅ **MP regenerates in combat (0.99%/sec)**
✅ **No regeneration when dead**
✅ **Values clamped to maximum**
✅ **No crashes or errors**
✅ **No performance issues**

## What This Tests Proves

If all tests pass, it demonstrates:

1. ✅ **DSE mod loading works**
2. ✅ **GDScript mods can be instantiated**
3. ✅ **Mods can access game singletons** (`/root/Global`)
4. ✅ **Mods can access game objects** (`player.body`)
5. ✅ **Mods can read game properties** (`body.hp`, `body.combat`)
6. ✅ **Mods can modify game state** (setting `body.hp`)
7. ✅ **Timer-based systems work** (`_process(delta)`)
8. ✅ **Logging system works**

## Known Limitations (Without Hooks)

This mod works **without any hooks** by directly accessing game objects. However, it has limitations:

1. **No event-driven updates** - Uses polling instead of reacting to events
2. **Fragile** - Breaks if game structure changes
3. **No mod communication** - Can't coordinate with other mods
4. **Performance** - Checks every frame even when nothing changes

## Future: With Hooks Implementation

Once hooks are implemented, this mod could use:

```gdscript
# Instead of polling in _process():
DSE.add_hook("on_combat_state_change", _on_combat_changed)
DSE.add_hook("on_time_tick", _apply_regen)  # Called once per second by game
DSE.add_hook("on_damage_taken", _on_damage)  # React to damage

# More efficient, event-driven, resilient to game changes
```

## Report Template

After testing, report results:

```
## PassiveRegen Test Results

**Date:** YYYY-MM-DD
**Game Version:** Devil Spire Falls vX.X.X
**DSE Version:** vX.X.X

### Test Results
- [ ] Test 1: Mod Loading - PASS/FAIL
- [ ] Test 2: HP Regen (Out) - PASS/FAIL
- [ ] Test 3: HP Regen (In) - PASS/FAIL
- [ ] Test 4: MP Regen (Out) - PASS/FAIL
- [ ] Test 5: MP Regen (In) - PASS/FAIL
- [ ] Test 6: Death Prevention - PASS/FAIL
- [ ] Test 7: Max Clamping - PASS/FAIL

### Notes:
[Any observations, issues, or suggestions]

### Conclusion:
✅ Ready for release / ❌ Needs fixes
```
