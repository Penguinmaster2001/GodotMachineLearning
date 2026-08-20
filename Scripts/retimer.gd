
extends Node



@export var max_fps := 30.0
@export var enabled := true

var step := 0
var time := 0.0
var inter_frames := 0


func _ready():
	if !enabled:
		return
	
	RenderingServer.render_loop_enabled = false
	Engine.physics_ticks_per_second = 10000000
	Engine.time_scale = Engine.physics_ticks_per_second / 60.0
	get_tree().paused = true
	while true:
		get_tree().paused = false
		await get_tree().process_frame



func _physics_process(dt):
	time += dt
	inter_frames += 1

	if time > 1.0 / max_fps:
		RenderingServer.force_draw()
		RenderingServer.force_sync()
		# print("inter frames: ", inter_frames)
		time = 0.0
		inter_frames = 0
	
	await get_tree().physics_frame
	get_tree().paused = true
	step += 1
