# IronPython Pad. Write code snippets here and F5 to run. If code is selected, only selection is run.
import os
import time
import socket
def generate_square_spiral_xy(spiral_radius):
    x_coords = [0]
    y_coords = [0]
    if spiral_radius == 0:
        return x_coords, y_coords
    # Directions: west, north, east, south
    directions = [
        (-1, 0),  # west
        (0, 1),   # north
        (1, 0),   # east
        (0, -1),  # south
    ]
    x, y = 0, 0
    step_length = 1
    direction_index = 0
    while True:
        # Each step length is used for two consecutive directions
        for _ in range(2):
            dx, dy = directions[direction_index % 4]
            for _ in range(step_length):
                x += dx
                y += dy
                x_coords.append(x)
                y_coords.append(y)
            # Stop after completing the outer right/east vertical edge
            if dx == 0 and dy == -1 and x == spiral_radius:
                return x_coords, y_coords
            direction_index += 1
        step_length += 1

# Define wrapper for MoveAxi-based slewing
def slewRA(duration: float = 1.0, rate: int = 1):
	SharpCap.Mounts.SelectedMount.MoveAxis(0,rate+1)
	time.sleep(duration)
	SharpCap.Mounts.SelectedMount.MoveAxis(0,1)
def slewDEC(duration: float = 1.0, rate: int = 1):
	SharpCap.Mounts.SelectedMount.MoveAxis(1,rate)
	time.sleep(duration)
	SharpCap.Mounts.SelectedMount.MoveAxis(1,0)

# Main function
step_in_dec_deg = 0.0036 # (25um spacing in 400EFL)
step_in_ra_hour = 2.4e-4 # (25um spacing in 400EFL)
spiral_radius = 32
TRIGGER_HOST = "127.0.0.1"
TRIGGER_PORT = 5555
RA_baseline = SharpCap.Mounts.SelectedMount.RA
DEC_baseline = SharpCap.Mounts.SelectedMount.Dec
SharpCap.Mounts.SelectedMount.Tracking = False
x_coords, y_coords = generate_square_spiral_xy(spiral_radius)
SharpCap.SelectedCamera.Controls.OutputFormat.Value = 'PNG files (*.png)'
pwd = os.path.dirname(os.path.abspath(__file__))
for ind_image in range(len(x_coords)):
	# Capture an image
	SharpCap.SelectedCamera.CaptureSingleFrameTo(pwd+'/results/solar_'+str(ind_image)+'.png')
	# Trigger spectrometer
	with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
	    s.connect((TRIGGER_HOST, TRIGGER_PORT))
	    s.sendall(b"TRIGGER")
	if ind_image < len(x_coords) -1:
		delta_x = x_coords[ind_image + 1] - x_coords[ind_image]
		delta_y = y_coords[ind_image + 1] - y_coords[ind_image]
		if delta_x is not 0:
			slewRA(rate = delta_x)
		if delta_y is not 0:
			slewDEC(rate = delta_y)
SharpCap.Mounts.SelectedMount.Tracking = True