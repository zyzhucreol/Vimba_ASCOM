/*=============================================================================
  Copyright (C) 2024-2025 Allied Vision Technologies. All Rights Reserved.
  Subject to the BSD 3-Clause License.
=============================================================================*/

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using VmbNET;

namespace AsynchronousGrabOpenCV
{
    /// <summary>
    /// Example program using openCV to display images asynchronously grabbed with VmbNET.
    /// </summary>
    /// 
    class AsynchronousGrabOpenCV
    {
        // code returned by OpenCV's WaitKey() when <enter> is pressed
        const int EnterKeyCode = 13;

        static async Task Main(string[] args)
        {
            // Set camera attributes
            int exposure_time = 5000;
            int gain = 0;

            Console.WriteLine("////////////////////////////////////////////////////");
            Console.WriteLine("/// VmbNET Asynchronous Grab with OpenCV Example ///");
            Console.WriteLine("////////////////////////////////////////////////////\n");

            string cameraId = "";
            var allocationMode = ICapturingModule.AllocationModeValue.AnnounceFrame;
            if (args.Length == 0)
            {
                // no arguments are OK.
            }
            else if ((args.Length == 1 || args.Length == 2) && args[0] != "-h")
            {
                if (args[0] == "-x")
                {
                    allocationMode = ICapturingModule.AllocationModeValue.AllocAndAnnounceFrame;
                    if (args.Length == 2)
                    {
                        cameraId = args[1];
                    }
                }
                else
                {
                    cameraId = args[0];
                }
            }
            else
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  AsynchronousGrabOpenCV.exe [ -x ] [cameraId]");
                Console.WriteLine("  AsynchronousGrabOpenCV.exe [ -h ]\n");
                Console.WriteLine("Parameters:");
                Console.WriteLine("  [ -x ]      If present, frame buffers are allocated by the transport layer)");
                Console.WriteLine("  cameraId    ID or extended ID of the camera to use (using the first camera found if not specified)");
                Console.WriteLine("  [ -h ]      Display this usage information)\n");
                return;
            }

            using var vmb = IVmbSystem.Startup(); // API startup (loads transport layers)

            var camera = vmb.GetCameras()[0]; // Get the first available camera

            // open the camera
            using var openCamera = camera.Open();

            var features = openCamera.Features;
            // Set camera attributes
            features.ExposureTimeAbs = exposure_time; // Set the exposure time value in us
            features.Gain = gain; // Set the gain value in dB

            // prepare stream 0 for capturing
            using var streamCapture = openCamera.PrepareCapture(allocationMode, 5);

            // create disposable concurrent queue for processing the arguments of each frame received event
            using var frameQueue = new BlockingCollection<IFrame>(5);

            // event handler for received frames
            openCamera.FrameReceived += (_, frameReceivedEventArgs) =>
            {
                // get the frame without the "using" keyword because it will be disposed on the main thread
                IFrame frame = frameReceivedEventArgs.Frame;

                // add the event arguments to the queue
                frameQueue.Add(frame);
            };

            // start the acquisition in the camera
            features.AcquisitionStart();

            // pop each event argument from the queue and process the frame, until the user presses <enter>
            foreach (var frame in frameQueue.GetConsumingEnumerable())
            {
                // display frame if completed
                if (frame.FrameStatus == IFrame.FrameStatusValue.Completed)
                {
                    // Access image data
                    uint width = frame.Width;
                    uint height = frame.Height;
                    uint pixelCount = width * height;
                    IntPtr imagePtr = (IntPtr)frame.ImageData;
                    byte[] imageBufferData = new byte[pixelCount * sizeof(ushort)];
                    Marshal.Copy(imagePtr, imageBufferData, 0, (int)pixelCount);
                    // Convert byte[] (8-bit buffer holder) to ushort[] (16-bit image)
                    ushort[] image_data = new ushort[pixelCount];
                    Buffer.BlockCopy(imageBufferData, 0, image_data, 0, imageBufferData.Length);

                    // Simple image processing
                    float sum = image_data.Sum(x => (float)x);
                    float optical_power = sum / ((float)pixelCount);
                    Console.WriteLine($" {width} X {height} Sum of all pixels: {sum}");
                    Console.WriteLine($"Average pixel value: {optical_power}");
                }

                // display the frame's status
                Console.WriteLine($"Frame status: {frame.FrameStatus.ToString()}");

                // manual dispose since frame was obtained on another thread
                frame.Dispose();
            }

            // stop the acquisition, terminate the capturing, close the camera and shutdown Vimba X
            features.AcquisitionStop();
        }
    }
}
