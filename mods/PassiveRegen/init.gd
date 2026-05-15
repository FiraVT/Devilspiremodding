extends Node

# Passive Regeneration Mod - Skyrim-style HP/MP regen
# Health: 0.70% per second out of combat, 0.49% in combat
# Mana: 3.00% per second out of combat, 0.99% in combat

# Regeneration rates (% of max per second)
const HP_REGEN_OUT_OF_COMBAT = 0.0070  # 0.70%
const HP_REGEN_IN_COMBAT = 0.0049      # 0.49%
const MP_REGEN_OUT_OF_COMBAT = 0.0300  # 3.00%
const MP_REGEN_IN_COMBAT = 0.0099      # 0.99%

var regen_timer = 0.0
const REGEN_TICK = 1.0  # Apply regen every 1 second

func _ready():
	var dse = get_node_or_null("/root/DSE")
	if dse:
		dse.log_info("[PassiveRegen] Mod loaded successfully!")
		dse.log_info("[PassiveRegen] HP Regen: " + str(HP_REGEN_OUT_OF_COMBAT * 100) + "% (out) / " + str(HP_REGEN_IN_COMBAT * 100) + "% (in combat)")
		dse.log_info("[PassiveRegen] MP Regen: " + str(MP_REGEN_OUT_OF_COMBAT * 100) + "% (out) / " + str(MP_REGEN_IN_COMBAT * 100) + "% (in combat)")
	else:
		print("[PassiveRegen] WARNING: DSE not found!")

func _process(delta):
	regen_timer += delta

	# Apply regeneration every second
	if regen_timer >= REGEN_TICK:
		regen_timer -= REGEN_TICK
		apply_regeneration()

func apply_regeneration():
	var global = get_node_or_null("/root/Global")
	if not global:
		return

	var player = global.player
	if not player:
		return

	# Access player's body (where HP/MP live)
	var body = player.body
	if not body:
		return

	# Don't regenerate if dead or knocked out
	if body.state == body.DEAD or body.ko > 0:
		return

	# Determine combat state
	var in_combat = body.combat

	# Calculate regen amounts
	var hp_regen_rate = HP_REGEN_IN_COMBAT if in_combat else HP_REGEN_OUT_OF_COMBAT
	var mp_regen_rate = MP_REGEN_IN_COMBAT if in_combat else MP_REGEN_OUT_OF_COMBAT

	var hp_to_add = body.maxhp * hp_regen_rate
	var mp_to_add = body.maxmp * mp_regen_rate

	# Apply regeneration (clamped to max values)
	var old_hp = body.hp
	var old_mp = body.mp

	body.hp = min(body.hp + hp_to_add, body.maxhp)
	body.mp = min(body.mp + mp_to_add, body.maxmp)

	# Optional: Log regeneration for debugging
	if old_hp < body.hp or old_mp < body.mp:
		var dse = get_node_or_null("/root/DSE")
		if dse:
			var combat_str = "IN COMBAT" if in_combat else "out of combat"
			dse.log_info("[PassiveRegen] " + combat_str + " - HP: +" + str(snappedf(body.hp - old_hp, 0.01)) + " | MP: +" + str(snappedf(body.mp - old_mp, 0.01)))
