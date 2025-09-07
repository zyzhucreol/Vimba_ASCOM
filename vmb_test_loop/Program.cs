// vmb_test image acuqisition in an infinite loop
// See https://aka.ms/new-console-template for more information
// Before running this example, make sure all camera attributes are set correctly in Vimba X Viewer.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using VmbNET;
class Program
{
    static void Main()
    {
        using var vmb = IVmbSystem.Startup(); // API startup (loads transport layers)

        var cam = vmb.GetCameras()[0]; // Get the first available camera

        using var openCam = cam.Open(); // Open the camera

        int N_frames = 10;
        int exposure_time = 5000;
        int gain = 0;
        // Set camera attributes
        openCam.Features.ExposureTimeAbs = exposure_time; // Set the exposure time value in us
        openCam.Features.Gain = gain; // Set the gain value in dB

        // Register an event handler for incoming frames
        openCam.FrameReceived += (s, e) =>
        {
            using var frame = e.Frame;
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
        }; // IDisposable: Frame is automatically requeued

        // Convenience function to start acquisition
        for (int i = 0; i < N_frames; i++)
        {
            using var acquisition = openCam.StartFrameAcquisition();
            Thread.Sleep(100); // gap between acquisitions to allow for data processing and printing
        }

    } // IDisposable: Stops acquisition, closes camera, shuts down Vimba X
}