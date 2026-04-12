# Tips for building the ASCOM driver for AVT cameras via VimbaX
1. Look at sample standalone projects vm_test_loop for N-frame acquisition and vm_test for single frame acquisition.
2. Build ASCOM driver project (vmb_ascom7) in x64 configuration (Platform target: x64).
3. Add C:\Program Files\Allied Vision\Vimba X\api\bin to the PATH environment variable.
4. Open a command prompt and navigate to the ASCOM driver output folder (e.g. vmb_ascom7\bin\x64\Debug). Execute the generated .exe file with "-register" option in the command line to register the driver in ASCOM Profile Explorer. For example: ".\ASCOM.ZZVimbaX.exe -register".