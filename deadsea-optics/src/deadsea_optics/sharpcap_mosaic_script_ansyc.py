# IronPython Pad. Write code snippets here and F5 to run. If code is selected, only selection is run.
import os
import time
import socket

# Define wrapper for MoveAxi-based slewing
def slewRA(duration: float = 1.0, rate: int = 1):
	SharpCap.Mounts.SelectedMount.MoveAxis(0,rate+1)
	time.sleep(duration)
	SharpCap.Mounts.SelectedMount.MoveAxis(0,1)
def slewDEC(duration: float = 1.0, rate: int = 1):
	SharpCap.Mounts.SelectedMount.MoveAxis(1,rate)
	time.sleep(duration)
	SharpCap.Mounts.SelectedMount.MoveAxis(1,0)

# Main function (Start capture from the bottom left of the FOV)
row_duration = 32 # 126s row duration will scan across the full solar disk
num_rows = 64
snapshot_interval = 0.4
TRIGGER_HOST = "127.0.0.1"
TRIGGER_PORT = 5555
RA_baseline = SharpCap.Mounts.SelectedMount.RA
DEC_baseline = SharpCap.Mounts.SelectedMount.Dec
SharpCap.Mounts.SelectedMount.Tracking = False
SharpCap.SelectedCamera.Controls.OutputFormat.Value = 'PNG files (*.png)'
pwd = os.path.dirname(os.path.abspath(__file__))
ind_image = 0
for ind_row in range(num_rows):
	# start row timer
	time_start = time.time()
	# start row scan
	if ind_row % 2 == 0:
		SharpCap.Mounts.SelectedMount.MoveAxis(0,2)
	else
		SharpCap.Mounts.SelectedMount.MoveAxis(0,0)
	while time.time() - time_start < row_duration:
		# Capture an image
		SharpCap.SelectedCamera.CaptureSingleFrameTo(pwd+'/results/solar_'+str(ind_image)+'.png')
		# Trigger spectrometer
		with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
		    s.connect((TRIGGER_HOST, TRIGGER_PORT))
		    s.sendall(b"TRIGGER")
		ind_image = ind_image + 1
		time.sleep(snapshot_interval)
	slewDEC()
SharpCap.Mounts.SelectedMount.Tracking = True