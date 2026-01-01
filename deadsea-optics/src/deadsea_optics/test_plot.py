# -*- coding: utf-8 -*-
"""
Created on Sun Dec 28 12:51:57 2025

@author: OISL
"""

import matplotlib.pyplot as plt
import numpy as np
import time

# Enable interactive mode to update plots dynamically
plt.ion()

# Initialize figure and axis
fig, ax = plt.subplots()
line, = ax.plot([], [], 'b-', label='Dynamic Data')  # empty line
ax.set_xlim(0, 100)
ax.set_ylim(0, 10)
ax.set_title("Dynamic Plot in Infinite Loop")
ax.set_xlabel("Time")
ax.set_ylabel("Value")
ax.legend()

# Data storage
x_data = []
y_data = []

i = 0
try:
    while True:
        # Generate new data
        x_data.append(i)
        y_data.append(np.random.random() * 10)

        # Update plot data
        line.set_data(x_data, y_data)
        
        # Adjust x-axis to scroll over time
        if i > 100:
            ax.set_xlim(i-100, i)

        # Redraw the figure
        fig.canvas.draw()
        fig.canvas.flush_events()

        i += 1
        time.sleep(0.1)  # Control update speed

except KeyboardInterrupt:
    print("Plotting stopped by user.")
finally:
    plt.ioff()  # Turn off interactive mode
    plt.show()