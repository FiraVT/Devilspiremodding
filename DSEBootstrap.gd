extends "res://System/global_orig.gd"

# DSE Surgical Injection Bootstrap

var dse_log_file: FileAccess = null
var dse_core_node = null

func _init():
	# 1. Setup Logging
	var exe_dir = OS.get_executable_path().get_base_dir()
	var log_path = exe_dir.path_join("dse_bootstrap.log")
	dse_log_file = FileAccess.open(log_path, FileAccess.WRITE)
	
	_dse_log("[DSE] ========================================")
	_dse_log("[DSE] DSE Surgical Bootstrap Started")
	_dse_log("[DSE] Time: " + Time.get_datetime_string_from_system())
	_dse_log("[DSE] Global singleton hijacked successfully.")
	
	var base_script = get_script().get_base_script()
	if base_script:
		_dse_log("[DSE] Base class: " + base_script.get_path())
	else:
		_dse_log("[DSE] CRITICAL ERROR: Base script (global_orig.gd) NOT FOUND!")
		_dse_log("[DSE] Extension will fail. Ensure DSECore.pck contains System/global_orig.gd")
		
	_dse_log("[DSE] ========================================")

	# 2. Check for DSE Core
	if not ResourceLoader.exists("res://System/dse.gd"):
		_dse_log("[DSE] CRITICAL ERROR: dse.gd not found in PCK index! (Path: res://System/dse.gd)")
	else:
		_dse_log("[DSE] DSE Core API script found.")

func _ready():
	# Call the original Global._ready()
	if super.has_method("_ready"):
		_dse_log("[DSE] Calling original Global._ready()")
		super._ready()
	
	# 3. Initialize DSE Core
	_dse_log("[DSE] Initializing Core API...")
	var core_script = load("res://System/dse.gd")
	if core_script:
		dse_core_node = core_script.new()
		dse_core_node.name = "DSE"
		# Add to root so it's a sibling of Global (which is 'self' here)
		get_tree().root.add_child.call_deferred(dse_core_node)
		_dse_log("[DSE] Core API initialized and scheduled for injection.")
		
		# 4. Load Mods
		_dse_load_mods.call_deferred()
	else:
		_dse_log("[DSE] ERROR: Failed to load dse_core.gd script resource")

func _dse_log(message: String):
	print(message)
	if dse_log_file:
		dse_log_file.store_line(message)
		dse_log_file.flush()

func _dse_load_mods():
	if not dse_core_node:
		_dse_log("[DSE] ERROR: Cannot load mods, DSE Core node is null")
		return

	var exe_dir = OS.get_executable_path().get_base_dir()
	var mods_dir = exe_dir.path_join("mods")
	var load_order_path = exe_dir.path_join("load_order.txt")

	if not DirAccess.dir_exists_absolute(mods_dir):
		_dse_log("[DSE] Mods directory not found: " + mods_dir)
		return

	var mod_list = []
	if FileAccess.file_exists(load_order_path):
		_dse_log("[DSE] Reading load order from: " + load_order_path)
		var file = FileAccess.open(load_order_path, FileAccess.READ)
		while not file.eof_reached():
			var mod_name = file.get_line().strip_edges()
			if mod_name != "" and not mod_name.begins_with("#"):
				mod_list.append(mod_name)
	else:
		_dse_log("[DSE] load_order.txt not found. Scanning mods directory...")
		mod_list = _dse_scan_mod_list(mods_dir)

	# Resolve dependencies and sort
	var sorted_mods = _dse_resolve_dependencies(mods_dir, mod_list)

	for mod_name in sorted_mods:
		_dse_load_mod(mods_dir, mod_name)

func _dse_load_mod(mods_dir: String, mod_name: String):
	var mod_path = mods_dir.path_join(mod_name)
	_dse_log("[DSE] Loading mod: " + mod_name)
	
	var mounted = false
	if mod_name.ends_with(".pck"):
		mounted = ProjectSettings.load_resource_pack(mod_path)
	else:
		mounted = DirAccess.dir_exists_absolute(mod_path)
		
	if not mounted:
		_dse_log("[DSE]   ERROR: Failed to mount/find mod: " + mod_path)
		return

	# --- mod.json Reading ---
	var json_path = "res://mods/" + mod_name + "/mod.json"
	if mod_name.ends_with(".pck") and not FileAccess.file_exists(json_path):
		json_path = "res://mod.json" # Fallback for flat PCKs

	if FileAccess.file_exists(json_path):
		var f = FileAccess.open(json_path, FileAccess.READ)
		if f:
			var json_text = f.get_as_text()
			var meta = JSON.parse_string(json_text)
			if meta is Dictionary:
				dse_core_node.mod_metadata[mod_name] = meta
				_dse_log("[DSE]   Loaded metadata: " + str(meta.get("id", mod_name)))

	# --- JSON Data Auto-loading ---
	var data_path = "res://mods/" + mod_name + "/data/"
	if not DirAccess.dir_exists_absolute(data_path):
		data_path = "res://data/" # Fallback for flat PCKs

	if DirAccess.dir_exists_absolute(data_path):
		var dir = DirAccess.open(data_path)
		if dir:
			dir.list_dir_begin()
			var file_name = dir.get_next()
			while file_name != "":
				if file_name.ends_with(".json") and file_name != "mod.json":
					var type = file_name.get_basename()
					_dse_load_json_data(data_path + file_name, type)
				file_name = dir.get_next()

	# --- init.gd Loading ---
	var internal_init = "res://mods/" + mod_name + "/init.gd"
	if not ResourceLoader.exists(internal_init) and mod_name.ends_with(".pck"):
		internal_init = "res://init.gd" # Fallback for flat PCKs
		
	if ResourceLoader.exists(internal_init):
		var script = load(internal_init)
		if script:
			var mod_instance = script.new()
			mod_instance.name = "Mod_" + mod_name.replace(".", "_")
			dse_core_node.add_child(mod_instance)
			dse_core_node.active_mods.append(mod_name)
			_dse_log("[DSE]   SUCCESS: Mod initialized")
		else:
			_dse_log("[DSE]   ERROR: Failed to load init script: " + internal_init)
	else:
		_dse_log("[DSE]   NOTE: No init.gd found for mod")
		dse_core_node.active_mods.append(mod_name)

func _dse_load_json_data(path: String, type: String):
	if FileAccess.file_exists(path):
		var f = FileAccess.open(path, FileAccess.READ)
		if f:
			var json_text = f.get_as_text()
			var data_array = JSON.parse_string(json_text)
			if data_array is Array:
				for entry in data_array:
					if entry is Dictionary and entry.has("id"):
						dse_core_node.register_data(type, entry["id"], entry)
				_dse_log("[DSE]   Loaded " + str(data_array.size()) + " entries into " + type)
			else:
				_dse_log("[DSE]   ERROR: JSON data in " + path + " is not an array")

func _dse_scan_mod_list(mods_dir: String) -> Array:
	var mods = []
	var dir = DirAccess.open(mods_dir)
	if dir:
		dir.list_dir_begin()
		var file_name = dir.get_next()
		while file_name != "":
			if not file_name.begins_with(".") and (file_name.ends_with(".pck") or dir.current_is_dir()):
				mods.append(file_name)
			file_name = dir.get_next()
	else:
		_dse_log("[DSE] ERROR: Could not open mods directory for scanning")
	return mods

func _dse_resolve_dependencies(mods_dir: String, mod_list: Array) -> Array:
	# Read all mod.json files to get dependencies
	var mod_metadata = {}
	for mod_name in mod_list:
		var meta = _dse_read_mod_metadata(mods_dir, mod_name)
		if meta:
			mod_metadata[mod_name] = meta

	# Topological sort based on dependencies
	var sorted = []
	var visited = {}
	var visiting = {}

	func visit(mod_name: String):
		if visited.has(mod_name):
			return
		if visiting.has(mod_name):
			_dse_log("[DSE] WARNING: Circular dependency detected involving: " + mod_name)
			return

		visiting[mod_name] = true

		if mod_metadata.has(mod_name) and mod_metadata[mod_name].has("dependencies"):
			for dep in mod_metadata[mod_name]["dependencies"]:
				if mod_list.has(dep):
					visit(dep)
				else:
					_dse_log("[DSE] WARNING: Missing dependency '" + dep + "' for mod '" + mod_name + "'")

		visiting.erase(mod_name)
		visited[mod_name] = true
		sorted.append(mod_name)

	for mod_name in mod_list:
		visit(mod_name)

	_dse_log("[DSE] Load order after dependency resolution: " + str(sorted))
	return sorted

func _dse_read_mod_metadata(mods_dir: String, mod_name: String) -> Dictionary:
	var mod_path = mods_dir.path_join(mod_name)
	var json_path = ""

	if mod_name.ends_with(".pck"):
		# Try to peek into PCK (complex), for now assume standard location
		return {}
	else:
		json_path = mod_path + "/mod.json"

	if FileAccess.file_exists(json_path):
		var f = FileAccess.open(json_path, FileAccess.READ)
		if f:
			var meta = JSON.parse_string(f.get_as_text())
			if meta is Dictionary:
				return meta
	return {}
