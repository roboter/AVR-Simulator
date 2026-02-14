# AVR Simulator

A simple AVR simulator written in C# using WPF. This project allows you to emulate AVR microcontrollers and run programs compiled into Intel HEX format.

## Features

- Emulates AVR core instructions.
- Supports Intel HEX file parsing.
- Simple WPF interface for visualization.
- Background worker for emulation execution.

## Supported AVR Chips

Currently, the simulator has specific support for:

- **ATmega328**

The base `AVRInterpreter` class provides a foundation for implementing other AVR chips.

## How to Build

### Prerequisites

- Windows OS
- .NET Framework 4.0 or later
- Visual Studio (2010 or newer) or MSBuild

### Building with Visual Studio

1. Open `AVR Simulator.sln` in Visual Studio.
2. Select the `Release` configuration.
3. Build the solution (`Build > Build Solution`).
4. The executable will be located in `bin/Release/AVR Simulator.exe`.

### Building with MSBuild

1. Open the Developer Command Prompt for Visual Studio.
2. Navigate to the project root directory.
3. Run the following command:
   ```bash
   msbuild "AVR Simulator.sln" /p:Configuration=Release /p:Platform=x86
   ```
4. The executable will be located in `bin/Release/AVR Simulator.exe`.

## Usage

The simulator currently loads a file named `Blink.hex` from the execution directory by default. You can modify `MainWindow.xaml.cs` to change the loaded file or add a file picker.

## License

This project is licensed under the [LICENSE](LICENSE) file.
