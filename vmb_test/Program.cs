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

        var cam = vmb.GetCameraByID("DEV_000F314DA17F"); // Get the first available camera

        using var openCam = cam.Open(); // Open the camera 

        // Show a list of camera features
        foreach (var feature in openCam.Features)
        {
            try
            {
                Console.WriteLine($"Feature Name: {feature.Name}, Value: {feature.Value}");
            }
            catch
            {
                // Some features may not support reading the value directly
                Console.WriteLine($"Feature Name: {feature.Name}, Value: <unavailable>");
            }
        }

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
        using var acquisition = openCam.StartFrameAcquisition();

        Thread.Sleep(100);

    } // IDisposable: Stops acquisition, closes camera, shuts down Vimba X
}