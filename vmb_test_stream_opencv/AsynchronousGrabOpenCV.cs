/*=============================================================================
  Copyright (C) 2024-2025 Allied Vision Technologies. All Rights Reserved.
  Subject to the BSD 3-Clause License.
=============================================================================*/

using Logging;
using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VmbNET;

namespace AsynchronousGrabOpenCV
{
    /// <summary>
    /// Example program using openCV to display images asynchronously grabbed with VmbNET.
    /// </summary>
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

            IVmbSystem.Logger = LoggerCreator.CreateLogger();

            // startup Vimba X
            using var vmbSystem = IVmbSystem.Startup();

            ICamera camera;
            if (cameraId.Length > 0)
            {
                try
                {
                    // get camera with user-supplied ID
                    camera = vmbSystem.GetCameraByID(cameraId);
                }
                catch (VmbNETException e)
                {
                    Console.WriteLine($"Received VmbNET error: \"{e.Message}\" when trying to get camera.");
                    return;
                }
            }
            else
            {
                // get first detected camera
                var cameras = vmbSystem.GetCameras();
                if (cameras.Count > 0)
                {
                    camera = cameras[0];
                }
                else
                {
                    Console.WriteLine("No cameras found.");
                    return;
                }
            }

            // open the camera
            using var openCamera = camera.Open();

            var features = openCamera.Features;
            // Set camera attributes
            features.ExposureTimeAbs = exposure_time; // Set the exposure time value in us
            features.Gain = gain; // Set the gain value in dB

            // for GigE cameras only, adjust the packet size if possible
            if (openCamera.TransportLayer.Type == TransportLayerType.GEV && openCamera.Stream.Features.GVSPAdjustPacketSize.Exists)
            {
                Console.WriteLine($"trying to adjust the package size");
                await (Task)openCamera.Stream.Features.GVSPAdjustPacketSize(TimeSpan.FromSeconds(1));
            }

            features.PixelFormat = features.PixelFormat.EnumEntriesByName.ContainsKey("RGB8") ? "RGB8" : "Mono8";
            MatType matType = features.PixelFormat == "RGB8" ? MatType.CV_8UC3 : MatType.CV_8UC1;

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

            // set window name
            var windowName = $"Stream from {camera.Serial}. Press <enter> to stop streaming.";

            int frameWidth = 1024;
            int? frameHeight = null;

            // pop each event argument from the queue and process the frame, until the user presses <enter>
            foreach (var frame in frameQueue.GetConsumingEnumerable())
            {
                // display frame if completed
                if (frame.FrameStatus == IFrame.FrameStatusValue.Completed)
                {
                    // convert the received frame to an OpenCV matrix and resize it
                    using var imageMat = Mat.FromPixelData((int)frame.Height, (int)frame.Width, matType, frame.ImageData);

                    frameHeight ??= (int)((double)(1024 * frame.Height) / frame.Width);
                    Cv2.Resize(imageMat, imageMat, new Size(frameWidth, frameHeight.Value));

                    Cv2.CvtColor(imageMat, imageMat, ColorConversionCodes.BGR2RGB);

                    // display the image in the window
                    Cv2.ImShow(windowName, imageMat);

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
                    float optical_power = sum / ((float)pixelCount * (float)exposure_time * (float)Math.Pow(10, (float)gain / 20));
                    Console.WriteLine($" {width} X {height} Sum of all pixels: {sum}");
                    Console.WriteLine($"Average pixel value: {optical_power}");
                }

                // display the frame's status
                Console.WriteLine($"Frame status: {frame.FrameStatus.ToString()}");

                // manual dispose since frame was obtained on another thread
                frame.Dispose();

                // get pressed key, if any
                var key = Cv2.WaitKey(1);
                if (key == EnterKeyCode)
                {
                    // user pressed <enter>, close the window
                    Cv2.DestroyWindow(windowName);

                    // remove event handler lambda function
                    openCamera.RemoveAllFrameEventHandlers();

                    // exit this loop
                    break;
                }
            }

            // stop the acquisition, terminate the capturing, close the camera and shutdown Vimba X
            features.AcquisitionStop();
        }
    }
}
