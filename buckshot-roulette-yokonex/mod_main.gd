extends Node

const LOG_NAME = "buckshot-gamehub"

func _init() -> void:
	var mod_root = get_script().resource_path.get_base_dir()
	Engine.set_meta("br_api_bus", load(mod_root + "/extensions/event_bus.gd"))
	
	if OS.has_feature("editor"):
		_preprocess_vanilla_scripts()
	
	var base = mod_root + "/extensions/"
	ModLoaderMod.install_script_hooks("res://scripts/RoundManager.gd", base + "round_manager_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/ShotgunShooting.gd", base + "shotgun_shooting_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/DealerIntelligence.gd", base + "dealer_intelligence_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/ItemManager.gd", base + "item_manager_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/ItemInteraction.gd", base + "item_interaction_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/HandcuffManager.gd", base + "handcuff_manager_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/HealthCounter.gd", base + "health_counter_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/ShellSpawner.gd", base + "shell_spawner_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/DeathManager.gd", base + "death_manager_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/DefibCutter.gd", base + "defib_cutter_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/MedicineManager.gd", base + "medicine_manager_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/SignatureManager.gd", base + "signature_manager_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/BurnerPhone.gd", base + "burner_phone_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/HandManager.gd", base + "hand_manager_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/ShellEjectManager.gd", base + "shell_eject_manager_ext.gd")
	ModLoaderMod.install_script_hooks("res://scripts/MenuManager.gd", base + "menu_manager_ext.gd")

func _ready() -> void:
	var mod_root = get_script().resource_path.get_base_dir()
	var bridge = load(mod_root + "/extensions/gamehub_bridge.gd").new()
	add_child(bridge)
	ModLoaderLog.info("Buckshot Roulette GameHub Mod loaded", LOG_NAME)

func _process(_delta: float) -> void:
	var s = get_tree().current_scene
	if not s: return
	var path = s.scene_file_path
	var last = str(Engine.get_meta("_br_last_scene", ""))
	if path != last:
		Engine.set_meta("_br_last_scene", path)
		if path.ends_with("menu.tscn"):
			var b = Engine.get_meta("br_api_bus", null)
			if b:
				b.emit(b.EVT_GAME_LAUNCH, {})
func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		var b = Engine.get_meta("br_api_bus", null)
		if b:
			b.emit(b.EVT_GAME_CLOSE, {})

func _preprocess_vanilla_scripts() -> void:
	var processor = load("res://addons/mod_loader/internal/mod_hook_preprocessor.gd").new()
	var vanilla_paths = [
		"res://scripts/RoundManager.gd",
		"res://scripts/ShotgunShooting.gd",
		"res://scripts/DealerIntelligence.gd",
		"res://scripts/ItemManager.gd",
		"res://scripts/ItemInteraction.gd",
		"res://scripts/HandcuffManager.gd",
		"res://scripts/HealthCounter.gd",
		"res://scripts/ShellSpawner.gd",
		"res://scripts/DeathManager.gd",
		"res://scripts/DefibCutter.gd",
		"res://scripts/MedicineManager.gd",
		"res://scripts/SignatureManager.gd",
		"res://scripts/BurnerPhone.gd",
		"res://scripts/HandManager.gd",
		"res://scripts/ShellEjectManager.gd",
		"res://scripts/MenuManager.gd",
	]
	for path in vanilla_paths:
		var script = load(path) as GDScript
		if not script:
			continue
		if script.source_code.contains("ModLoader Hooks"):
			continue
		var processed = processor.process_script(path, true)
		if processed == script.source_code:
			continue
		script.source_code = processed
		script.reload()
		var file = FileAccess.open(path, FileAccess.WRITE)
		if file:
			file.store_string(processed)
			file.close()
			ModLoaderLog.info("Preprocessed hook script: " + path, LOG_NAME)
