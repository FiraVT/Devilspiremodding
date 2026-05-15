extends Node

# DSE Core - Refactored for GDScript-First Modding
# Philosophy: Expose the game, don't abstract it.

var active_mods = []
var mod_metadata = {}
var services = {}
var hooks = {}

var registries = {
	"items": {},
	"monsters": {},
	"npcs": {},
	"spells": {},
	"recipes": {},
	"props": {},
	"buildings": {}
}

func _ready():
	log_info("DSE Core API Initialized")

# --- Game Accessors ---

func get_player() -> Node:
	var global = get_node_or_null("/root/Global")
	if global:
		return global.player
	return null

func get_game_global() -> Node:
	return get_node_or_null("/root/Global")

# --- Hook System ---

func add_hook(hook_name: String, callback: Callable, priority: int = 100):
	if not hooks.has(hook_name):
		hooks[hook_name] = []
	hooks[hook_name].append({"callback": callback, "priority": priority})
	hooks[hook_name].sort_custom(func(a, b): return a.priority < b.priority)
	log_info("Hook added: " + hook_name + " (priority: " + str(priority) + ")")

func trigger_hook(hook_name: String, args: Array = []):
	if hooks.has(hook_name):
		for hook_data in hooks[hook_name]:
			var callback = hook_data.callback
			if callback.is_valid():
				callback.callv(args)
			else:
				log_error("Invalid callback for hook: " + hook_name)

func remove_hook(hook_name: String, callback: Callable):
	if hooks.has(hook_name):
		hooks[hook_name] = hooks[hook_name].filter(func(h): return h.callback != callback)

# --- Service Registry ---

func add_service(service_name: String, service_instance: Object):
	services[service_name] = service_instance
	log_info("Service registered: " + service_name)

func get_service(service_name: String) -> Object:
	return services.get(service_name)

# --- Data Registration & Spawning ---

func register_data(type: String, id: String, data: Dictionary, merge: bool = false):
	if registries.has(type):
		if registries[type].has(id):
			if merge:
				# Merge new data into existing (new values overwrite)
				_deep_merge(registries[type][id], data)
				log_info("Merged data into " + type + ": " + id)
			else:
				log_warning("Overwriting existing " + type + ": " + id)
				registries[type][id] = data
		else:
			registries[type][id] = data
			log_info("Registered " + type + ": " + id)
	else:
		log_error("Unknown registry type: " + type)

func _deep_merge(base: Dictionary, patch: Dictionary):
	for key in patch:
		if base.has(key) and base[key] is Dictionary and patch[key] is Dictionary:
			_deep_merge(base[key], patch[key])
		elif base.has(key) and base[key] is Array and patch[key] is Array:
			# Append arrays instead of replacing
			for item in patch[key]:
				if not item in base[key]:
					base[key].append(item)
		else:
			base[key] = patch[key]

func get_registered_data(type: String, id: String) -> Dictionary:
	if registries.has(type):
		return registries[type].get(id, {})
	return {}

func spawn_item(id: String, position: Vector3) -> Node:
	var data = get_registered_data("items", id)
	if data.is_empty():
		log_error("Item not found in registry: " + id)
		return null
	
	var item_scene = load("res://Objects/Items/item.tscn")
	if not item_scene:
		log_error("Could not load item.tscn")
		return null
		
	var item = item_scene.instantiate()
	item.set_data(data.duplicate(true))
	item.global_position = position
	get_game_global().map.add_child(item)
	return item

func spawn_monster(id: String, position: Vector3) -> Node:
	var data = get_registered_data("monsters", id)
	if data.is_empty():
		log_error("Monster not found in registry: " + id)
		return null
		
	var monster_scene = load("res://Objects/Characters/Monster/monster.tscn")
	if not monster_scene:
		log_error("Could not load monster.tscn")
		return null
		
	var monster = monster_scene.instantiate()
	monster.set_data(data.duplicate(true))
	monster.global_position = position
	get_game_global().map.add_child(monster)
	return monster

# --- Logging ---

func log_info(message: String):
	print("[DSE INFO] " + str(message))

func log_error(message: String):
	printerr("[DSE ERROR] " + str(message))

func log_warning(message: String):
	print("[DSE WARN] " + str(message))

# --- Chunk Generation Pipeline ---
# Mods can register generation stages for terrain modification

var chunk_generators = {
	"terrain": [],      # Priority 0-99: Base terrain modification
	"vegetation": [],   # Priority 100-199: Trees, grass, rocks
	"structures": [],   # Priority 200-299: Buildings, settlements
	"population": [],   # Priority 300-399: NPCs, monsters
	"decoration": []    # Priority 400-499: Final touches
}

func register_chunk_generator(stage: String, generator: Callable, priority: int = 100):
	if chunk_generators.has(stage):
		chunk_generators[stage].append({"generator": generator, "priority": priority})
		chunk_generators[stage].sort_custom(func(a, b): return a.priority < b.priority)
		log_info("Registered chunk generator for stage '" + stage + "' (priority: " + str(priority) + ")")
	else:
		log_error("Unknown chunk generation stage: " + stage)

func run_chunk_generators(stage: String, chunk: Node, chunk_data: Dictionary):
	if chunk_generators.has(stage):
		for gen_data in chunk_generators[stage]:
			var generator = gen_data.generator
			if generator.is_valid():
				generator.call(chunk, chunk_data)
			else:
				log_error("Invalid generator for stage: " + stage)
