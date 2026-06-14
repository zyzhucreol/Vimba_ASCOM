/*=============================================================================
  Copyright (C) 2024-2025 Allied Vision Technologies. All Rights Reserved.
  Subject to the BSD 3-Clause License.
=============================================================================*/

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using VmbNET;
using static VmbNET.ICapturingModule;

class Program
{
    static void Main()
    {
        using var vmb = IVmbSystem.Startup(); // API startup (loads transport layers)

        var cam = vmb.GetCameraByID("DEV_000F314DA17F"); // Get the first available camera

        using var openCam = cam.Open(); // Open the camera

        int N_frames = 100;
        int exposure_time = 100000;
        int gain = 0;
        // Set camera attributes
        openCam.Features.ExposureTimeAbs = exposure_time; // Set the exposure time value in us
        openCam.Features.Gain = gain; // Set the gain value in dB
        openCam.Features.AcquisitionMode = "Continuous"; // Set acquisition mode to continuous
        IStream stream = openCam.Stream;
        IStreamCapture preparedStream = stream.PrepareCapture(AllocationModeValue.AnnounceFrame, 10);
        openCam.Features.AcquisitionStart();
        // Do something with incoming frames
        for (int i = 0; i < N_frames; ++i)
        {
            IFrame frame = preparedStream.WaitForFrame(TimeSpan.FromMicroseconds(exposure_time*1.4));
            
            // Do something with frame
            Console.WriteLine($"Frame Received! ID={frame.Id}");
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
            Console.WriteLine($"Optical power (readout units/us): {optical_power}");
            
            frame.Release();
        }
        openCam.Features.AcquisitionStop();
        preparedStream.TearDown();
        stream.Close();

    } // IDisposable: Stops acquisition, closes camera, shuts down Vimba X
}