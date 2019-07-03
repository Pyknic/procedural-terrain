extends KinematicBody

export(int, "Visible", "Hidden", "Caputered, Confined") var mouse_mode = 2
export(float) var camera_angle = 0
export(float) var mouse_sensitivity = 0.3
var camera_change = Vector2()

var velocity = Vector3()
var direction = Vector3()

#fly variables
export(float) var FLY_SPEED = 20
export(float) var FLY_ACCEL = 4
var flying = false

#walk variables
var gravity = -9.8 * 3
export(float) var MAX_SPEED = 20
export(float) var MAX_RUNNING_SPEED = 30
export(float) var ACCEL = 2
export(float) var DEACCEL = 6

#jumping
export(float) var jump_height = 15
var in_air = 0
var has_contact = false

#slope variables
export(float) var MAX_SLOPE_ANGLE = 35

#stair variables
export(float) var MAX_STAIR_SLOPE = 20
export(float) var STAIR_JUMP_HEIGHT = 6

func _ready():
	Input.set_mouse_mode(mouse_mode)
	pass

func _physics_process(delta):
	aim()
	if flying:
		fly(delta)
	else:
		walk(delta)

func _input(event):
	if event is InputEventKey:
		if event.is_action_pressed("ui_quit"):
			get_tree().quit()
			
	if event is InputEventMouseMotion:
		camera_change = event.relative
		
func walk(delta):
	# reset the direction of the player
	direction = Vector3()
	
	# get the rotation of the camera
	var aim = $Head/Camera.get_global_transform().basis
	# check input and change direction
	if Input.is_action_pressed("move_forward"):
		direction -= aim.z
	if Input.is_action_pressed("move_backward"):
		direction += aim.z
	if Input.is_action_pressed("stride_left"):
		direction -= aim.x
	if Input.is_action_pressed("stride_right"):
		direction += aim.x
	direction.y = 0
	direction = direction.normalized()
	
	if (is_on_floor()):
		has_contact = true
		var n = $Tail.get_collision_normal()
		var floor_angle = rad2deg(acos(n.dot(Vector3(0, 1, 0))))
		if floor_angle > MAX_SLOPE_ANGLE:
			velocity.y += gravity * delta
		
	else:
		if !$Tail.is_colliding():
			has_contact = false
		velocity.y += gravity * delta

	if (has_contact and !is_on_floor()):
		move_and_collide(Vector3(0, -1, 0))
	
	if (direction.length() > 0 and $StairCatcher.is_colliding()):
		var stair_normal = $StairCatcher.get_collision_normal()
		var stair_angle = rad2deg(acos(stair_normal.dot(Vector3(0, 1, 0))))
		if stair_angle < MAX_STAIR_SLOPE:
			velocity.y = STAIR_JUMP_HEIGHT
			has_contact = false
	
	
	var temp_velocity = velocity
	temp_velocity.y = 0
	
	var speed
	if Input.is_action_pressed("move_sprint"):
		speed = MAX_RUNNING_SPEED
	else:
		speed = MAX_SPEED
	
	
	# where would the player go at max speed
	var target = direction * speed
	
	var acceleration
	if direction.dot(temp_velocity) > 0:
		acceleration = ACCEL
	else:
		acceleration = DEACCEL
	
	# calculate a portion of the distance to go
	temp_velocity = temp_velocity.linear_interpolate(target, acceleration * delta)
	
	velocity.x = temp_velocity.x
	velocity.z = temp_velocity.z
	
	if has_contact and Input.is_action_just_pressed("jump"):
		velocity.y = jump_height
		has_contact = false
	
	# move
	velocity = move_and_slide(velocity, Vector3(0, 1, 0))
	
	if !has_contact:
		in_air += 1
		
	$StairCatcher.translation.x = direction.x
	$StairCatcher.translation.z = direction.z
	
func fly(delta):
	# reset the direction of the player
	direction = Vector3()
	
	# get the rotation of the camera
	var aim = $Head/Camera.get_global_transform().basis
	
	# check input and change direction
	if Input.is_action_pressed("move_forward"):
		direction -= aim.z
	if Input.is_action_pressed("move_backward"):
		direction += aim.z
	if Input.is_action_pressed("stride_left"):
		direction -= aim.x
	if Input.is_action_pressed("stride_right"):
		direction += aim.x
	
	direction = direction.normalized()
	
	# where would the player go at max speed
	var target = direction * FLY_SPEED
	
	# calculate a portion of the distance to go
	velocity = velocity.linear_interpolate(target, FLY_ACCEL * delta)
	
	# move
	move_and_slide(velocity)
	
func aim():
	if camera_change.length() > 0:
		$Head.rotate_y(deg2rad(-camera_change.x * mouse_sensitivity))

		var change = -camera_change.y * mouse_sensitivity
		if change + camera_angle < 90 and change + camera_angle > -90:
			$Head/Camera.rotate_x(deg2rad(change))
			camera_angle += change
		camera_change = Vector2()


func _on_Area_body_entered( body ):
	if body.name == "Gary":
		flying = true


func _on_Area_body_exited( body ):
	if body.name == "Gary":
		flying = false