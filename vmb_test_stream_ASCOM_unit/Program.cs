/*=============================================================================
  Copyright (C) 2024-2025 Allied Vision Technologies. All Rights Reserved.
  Subject to the BSD 3-Clause License.
=============================================================================*/

using System.Runtime.InteropServices;
using VmbNET;
using static VmbNET.ICapturingModule;

/// <summary>
/// Local mirror of the ASCOM CameraStates enum so this unit-test project
/// stays free of ASCOM dependencies while preserving the same state machine.
/// </summary>
internal enum CameraStates
{
    cameraIdle = 0,
    cameraWaiting = 1,
    cameraExposing = 2,
    cameraReading = 3,
    cameraDownload = 4,
    cameraError = 5,
}

/// <summary>
/// Object-oriented, ASCOM-free re-implementation of the lifecycle exposed by
/// <c>ASCOM.ZZVimbaX.Camera.CameraHardware</c>. Only the members exercised by
/// the unit test are provided: <see cref="SetConnected(bool)"/>,
/// <see cref="StartExposure(double, bool)"/>, <see cref="ImageReady"/> and
/// <see cref="ImageArray"/>, plus the auxiliary state needed to support them.
/// </summary>
internal sealed class VimbaCameraController : IDisposable
{
    // CCD geometry constants - mirror the values used in CameraHardware.
    private const int CcdWidth = 2464;
    private const int CcdHeight = 2056;
    private const double PixelSize = 3.45;

    // Vimba X handles managed by this controller.
    private IVmbSystem? _vmb;
    private ICamera? _cam;
    private IOpenCamera? _openCam;
    private IStream? _stream;
    private IStreamCapture? _preparedStream;
    private IFrame? _frame;

    // Sub-frame and exposure state.
    private int _cameraNumX = CcdWidth;
    private int _cameraNumY = CcdHeight;
    private int _cameraStartX;
    private int _cameraStartY;
    private DateTime _exposureStart = DateTime.MinValue;
    private double _cameraLastExposureDuration;
    private bool _cameraImageReady;
    private CameraStates _currentCameraState = CameraStates.cameraIdle;
    private int[,]? _cameraImageArray;

    private readonly string _cameraId;
    private readonly uint _bufferCount;

    public VimbaCameraController(string cameraId, uint bufferCount = 10)
    {
        _cameraId = cameraId;
        _bufferCount = bufferCount;
    }

    #region Connection lifecycle

    /// <summary>
    /// Synchronously connects to or disconnects from the camera hardware.
    /// Mirrors <c>CameraHardware.SetConnected</c> but without ASCOM unique-ID
    /// reference counting (a single client is assumed in the unit test).
    /// </summary>
    public void SetConnected(bool newState)
    {
        if (newState)
        {
            if (_openCam != null)
            {
                LogMessage("SetConnected", "Already connected; ignoring request.");
                return;
            }

            LogMessage("SetConnected", "Connecting to hardware.");
            _vmb = IVmbSystem.Startup();
            _cam = _vmb.GetCameraByID(_cameraId);
            _openCam = _cam.Open();

            // Apply the same defaults the ASCOM driver applies on connect.
            _openCam.Features.ExposureTimeAbs = 500;       // microseconds
            _openCam.Features.Gain = 0;                    // dB
            _openCam.Features.AcquisitionMode = "SingleFrame";
            _openCam.Stream.Features.GVSPAdjustPacketSize(TimeSpan.FromSeconds(1));

            _stream = _openCam.Stream;
            _preparedStream = _stream.PrepareCapture(AllocationModeValue.AnnounceFrame, _bufferCount);

            LogMessage("SetConnected", "Vimba X camera opened.");
        }
        else
        {
            if (_openCam == null)
            {
                LogMessage("SetConnected", "Already disconnected; ignoring request.");
                return;
            }

            LogMessage("SetConnected", "Disconnecting from hardware.");

            try { _frame?.Release(); } catch { }
            _frame = null;

            try { _preparedStream?.TearDown(); } catch { }
            _preparedStream = null;

            try { _stream?.Close(); } catch { }
            _stream = null;

            try { _openCam.Dispose(); } catch { }
            _openCam = null;
            _cam = null;

            try { _vmb?.Dispose(); } catch { }
            _vmb = null;

            LogMessage("SetConnected", "Vimba X camera closed and API shut down.");
        }
    }

    public bool IsConnected => _openCam != null;

    public void Dispose() => SetConnected(false);

    #endregion

    #region Camera properties

    public CameraStates CameraState => _currentCameraState;

    public int CameraXSize => CcdWidth;
    public int CameraYSize => CcdHeight;
    public double PixelSizeX => PixelSize;
    public double PixelSizeY => PixelSize;

    public int NumX
    {
        get => _cameraNumX;
        set => _cameraNumX = value;
    }

    public int NumY
    {
        get => _cameraNumY;
        set => _cameraNumY = value;
    }

    public int StartX
    {
        get => _cameraStartX;
        set => _cameraStartX = value;
    }

    public int StartY
    {
        get => _cameraStartY;
        set => _cameraStartY = value;
    }

    public short Gain
    {
        get
        {
            CheckConnected(nameof(Gain));
            return (short)_openCam!.Features.Gain;
        }
        set
        {
            CheckConnected(nameof(Gain));
            _openCam!.Features.Gain = value;
        }
    }

    public string AcquisitionMode
    {
        get
        {
            CheckConnected(nameof(AcquisitionMode));
            return _openCam!.Features.AcquisitionMode;
        }
        set
        {
            CheckConnected(nameof(AcquisitionMode));
            _openCam!.Features.AcquisitionMode = value;
        }
    }

    public double CCDTemperature
    {
        get
        {
            CheckConnected(nameof(CCDTemperature));
            return _openCam!.Features.DeviceTemperature;
        }
    }

    public double LastExposureDuration
    {
        get
        {
            if (!_cameraImageReady)
            {
                throw new InvalidOperationException(
                    "Call to LastExposureDuration before the first image has been taken!");
            }
            return _cameraLastExposureDuration;
        }
    }

    public string LastExposureStartTime
    {
        get
        {
            if (!_cameraImageReady)
            {
                throw new InvalidOperationException(
                    "Call to LastExposureStartTime before the first image has been taken!");
            }
            return _exposureStart.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss");
        }
    }

    #endregion

    #region Exposure control

    /// <summary>
    /// Starts an exposure of the specified duration (seconds).
    /// Mirrors <c>CameraHardware.StartExposure</c>.
    /// </summary>
    public void StartExposure(double duration, bool light)
    {
        CheckConnected(nameof(StartExposure));

        _cameraImageReady = false;

        if (duration < 0.0)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "0.0 upwards");
        if (_cameraNumX > CcdWidth)
            throw new ArgumentOutOfRangeException(nameof(NumX), _cameraNumX, $"<= {CcdWidth}");
        if (_cameraNumY > CcdHeight)
            throw new ArgumentOutOfRangeException(nameof(NumY), _cameraNumY, $"<= {CcdHeight}");
        if (_cameraStartX > CcdWidth)
            throw new ArgumentOutOfRangeException(nameof(StartX), _cameraStartX, $"<= {CcdWidth}");
        if (_cameraStartY > CcdHeight)
            throw new ArgumentOutOfRangeException(nameof(StartY), _cameraStartY, $"<= {CcdHeight}");

        _cameraLastExposureDuration = duration;
        _openCam!.Features.ExposureTimeAbs = duration * 1_000_000.0; // seconds -> microseconds

        _exposureStart = DateTime.Now;
        _currentCameraState = CameraStates.cameraExposing;
        LogMessage("StartExposure", $"{duration} {light}");
        _openCam.StartFrameAcquisition();
    }

    /// <summary>
    /// Returns true once a frame is available for download.
    /// Mirrors <c>CameraHardware.ImageReady</c> and updates the camera state.
    /// </summary>
    public bool ImageReady(double timeoutSeconds = 3.0)
    {
        if (_cameraLastExposureDuration <= 0)
        {
            LogMessage("ImageReady", "No exposure has been taken yet.");
            return false;
        }

        CheckConnected(nameof(ImageReady));

        // Release any previously cached frame before polling for a new one.
        if (_frame != null)
        {
            try { _frame.Release(); } catch { }
            _frame = null;
        }

        try
        {
            _frame = _preparedStream!.WaitForFrame(TimeSpan.FromSeconds(timeoutSeconds));
            _cameraImageReady = _frame != null;
        }
        catch (Exception ex)
        {
            LogMessage("ImageReady", $"WaitForFrame failed: {ex.Message}");
            _cameraImageReady = false;
        }

        _currentCameraState = _cameraImageReady
            ? CameraStates.cameraReading
            : CameraStates.cameraExposing;

        return _cameraImageReady;
    }

    /// <summary>
    /// Returns the pixel data from the most recent exposure as a 2D
    /// <see cref="int"/> array of size <see cref="NumX"/> by <see cref="NumY"/>.
    /// Mirrors <c>CameraHardware.ImageArray</c>.
    /// </summary>
    public int[,] ImageArray
    {
        get
        {
            if (!_cameraImageReady || _frame == null)
            {
                throw new InvalidOperationException(
                    "Call to ImageArray before the first image has been taken!");
            }

            _currentCameraState = CameraStates.cameraDownload;

            IntPtr imagePtr = (IntPtr)_frame.ImageData;
            int pixelCount = _cameraNumX * _cameraNumY;

            ushort[] imageBufferData = new ushort[pixelCount];
            Marshal.Copy(imagePtr, (short[])(object)imageBufferData, 0, pixelCount);

            int[,] result = new int[_cameraNumX, _cameraNumY];
            for (int y = 0; y < _cameraNumY; y++)
            {
                for (int x = 0; x < _cameraNumX; x++)
                {
                    result[x, y] = imageBufferData[y * _cameraNumX + x];
                }
            }

            _cameraImageArray = result;
            _currentCameraState = CameraStates.cameraIdle;
            return result;
        }
    }

    /// <summary>
    /// Returns the most recent frame's raw 16-bit pixel buffer as a flat
    /// <see cref="ushort"/> array. This is an auxiliary helper kept for the
    /// simple pixel-sum processing performed by the unit test.
    /// </summary>
    public ushort[] ImageBuffer
    {
        get
        {
            if (!_cameraImageReady || _frame == null)
            {
                throw new InvalidOperationException(
                    "Call to ImageBuffer before the first image has been taken!");
            }

            uint width = _frame.Width;
            uint height = _frame.Height;
            int pixelCount = (int)(width * height);

            IntPtr imagePtr = (IntPtr)_frame.ImageData;
            byte[] byteBuffer = new byte[pixelCount * sizeof(ushort)];
            Marshal.Copy(imagePtr, byteBuffer, 0, pixelCount);

            ushort[] pixels = new ushort[pixelCount];
            Buffer.BlockCopy(byteBuffer, 0, pixels, 0, byteBuffer.Length);
            return pixels;
        }
    }

    /// <summary>Width of the most recent frame in pixels.</summary>
    public uint FrameWidth => _frame?.Width ?? 0u;

    /// <summary>Height of the most recent frame in pixels.</summary>
    public uint FrameHeight => _frame?.Height ?? 0u;

    /// <summary>ID of the most recent frame.</summary>
    public ulong FrameId => _frame?.Id ?? 0ul;

    /// <summary>Releases the currently cached frame back to the stream.</summary>
    public void ReleaseFrame()
    {
        if (_frame != null)
        {
            try { _frame.Release(); } catch { }
            _frame = null;
        }
    }

    /// <summary>Stops a running exposure / acquisition.</summary>
    public void StopExposure()
    {
        CheckConnected(nameof(StopExposure));
        _openCam!.Features.AcquisitionStop();
        _currentCameraState = CameraStates.cameraIdle;
    }

    /// <summary>Aborts a running exposure / acquisition.</summary>
    public void AbortExposure()
    {
        CheckConnected(nameof(AbortExposure));
        _openCam!.Features.AcquisitionAbort();
        _currentCameraState = CameraStates.cameraIdle;
    }

    #endregion

    #region Helpers

    private void CheckConnected(string member)
    {
        if (_openCam == null)
        {
            throw new InvalidOperationException($"{member}: not connected to camera.");
        }
    }

    private static void LogMessage(string identifier, string message)
    {
        Console.WriteLine($"[{identifier}] {message}");
    }

    #endregion
}

/// <summary>
/// Drives <see cref="VimbaCameraController"/> through the same acquisition
/// sequence used by the original procedural Program.cs, but expressed via the
/// ASCOM-style object surface (SetConnected, StartExposure, ImageReady,
/// ImageArray).
/// </summary>
internal static class Program
{
    private static void Main()
    {
        const string cameraId = "DEV_000F314DA17F";
        const int nFrames = 1;
        const double exposureSeconds = 0.1;          // 100,000 us
        const short gain = 0;                        // dB
        const double frameTimeoutSeconds = 3.0;

        using var camera = new VimbaCameraController(cameraId);

        camera.SetConnected(true);

        // Override the SingleFrame default applied by SetConnected so the
        // unit test can stream a continuous sequence of frames.
        //camera.AcquisitionMode = "Continuous";
        camera.Gain = gain;

        camera.StartExposure(exposureSeconds, light: true);

        for (int i = 0; i < nFrames; ++i)
        {
            if (!camera.ImageReady(frameTimeoutSeconds))
            {
                Console.WriteLine($"Frame {i}: timed out waiting for frame.");
                continue;
            }

            Console.WriteLine($"Frame Received! ID={camera.FrameId}");

            uint width = camera.FrameWidth;
            uint height = camera.FrameHeight;
            uint pixelCount = width * height;

            ushort[] imageData = camera.ImageBuffer;

            float sum = imageData.Sum(x => (float)x);
            float exposureUs = (float)(exposureSeconds * 1_000_000.0);
            float opticalPower = sum / (pixelCount * exposureUs * (float)Math.Pow(10, (float)gain / 20));

            Console.WriteLine($" {width} X {height} Sum of all pixels: {sum}");
            Console.WriteLine($"Optical power (readout units/us): {opticalPower}");

            camera.ReleaseFrame();
        }

        camera.StopExposure();
        camera.SetConnected(false);
    }
}
