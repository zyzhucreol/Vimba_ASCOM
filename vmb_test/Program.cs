// Example from Vimba X dotnet API readme
// See https://aka.ms/new-console-template for more information
// Before running this example, make sure all camera attributes are set correctly in Vimba X Viewer.

using System.Runtime.InteropServices;
using VmbNET;
class Program
{
    static void Main()
    {
        using var vmb = IVmbSystem.Startup(); // API startup (loads transport layers)

        var cam = vmb.GetCameras()[0]; // Get the first available camera

        using var openCam = cam.Open(); // Open the camera 

        // Show a list of camera features
        foreach (var feature in openCam.Features)
        {
            try
            {
                Console.WriteLine($"Feature Name: {feature.Name}, Type: {feature.FeatureType}, Value: {feature.Value}");
            }
            catch
            {
                // Some features may not support reading the value directly
                Console.WriteLine($"Feature Name: {feature.Name}, Type: {feature.FeatureType}, Value: <unavailable>");
            }
        }

        int exposure_time = 5000;
        int gain = 0;
        // Set camera attributes
        openCam.Features.ExposureTime = exposure_time; // Set the exposure time value in us
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
            byte[] imageData = new byte[pixelCount * sizeof(ushort)];
            Marshal.Copy(imagePtr, imageData, 0, (int)pixelCount);
            // Convert byte[] (8-bit buffer holder) to ushort[] (16-bit image)
            ushort[] ushortData = new ushort[pixelCount];
            Buffer.BlockCopy(imageData, 0, ushortData, 0, imageData.Length);

            // Simple image processing
            float sum = ushortData.Sum(x => (float)x);
            Console.WriteLine($"Sum of all pixels: {sum}");
        }; // IDisposable: Frame is automatically requeued

        // Convenience function to start acquisition
        using var acquisition = openCam.StartFrameAcquisition();

        Thread.Sleep(100); // gap between frames in ms

    } // IDisposable: Stops acquisition, closes camera, shuts down Vimba X
}