"""Send a software trigger to a running usb2000zz.py instance."""

import socket

TRIGGER_HOST = "127.0.0.1"
TRIGGER_PORT = 5555

with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
    s.connect((TRIGGER_HOST, TRIGGER_PORT))
    s.sendall(b"TRIGGER")
    # print("Trigger sent.")