# mods/BonusGold/init.gd
extends Node

# Get reference to DSE Core
@onready var DSE = get_node("/root/DSE")

func _ready():
    DSE.add_hook("enemy_died", _on_enemy_died)
    DSE.log_info("Bonus Gold (XP) Mod Loaded!")

func _on_enemy_died(enemy):
    var player = DSE.get_player()
    if player:
        # Giving 100 bonus XP for each kill
        player.get_xp(100)
        DSE.log_info("Bonus Gold Mod: Gave 100 XP for killing " + str(enemy.char_name))
