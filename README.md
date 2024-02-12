# ModbusSlaveSimulation
Standalone Windows app supporting Modbus RTU, TCP, UDP and ASCIIoverRTU protocols for simulation. Also included are its Mono versions for Linux and Mac OS X (these are VB Net versions so for Linux you will have to install `mono-complete` and `mono-vbnc` packages while Mac might be different depending on the OS X version).

It is designed to allow running multiple instances of the app at the same time, just use a different port, example:
- one instance on IP 127.0.0.1 TCP Port 501 and another instance on IP 127.0.0.1 TCP Port 502

Based on modified [nModbus](https://code.google.com/p/nmodbus/) .NET 3.5 libraries, Copyright (c) 2006 Scott Alexander.
These are included as a resource for Windows version but are separate for Mono version.

Intended to be used as a quick testing tool. Can be tested with its counterpart [ModbusMaster](https://github.com/GitHubDragonFly/ModbusMaster).

# Screenshot

![Start Page](screenshots/Modbus%20Slave%20Simulation.png?raw=true)

# Functionality
- All values can be set before the connection is established - Coils, Discrete Inputs, Input Registers, Holding Registers.
- The Data Grid View is set to initially show 20 rows but can be changed within the `Row Count` to show more or all rows if necessary.
- In the Data Grid View, `I/O Address Range`, double-click the value to change it:
  - Boolean values will flip between 0 and 1
  - Uint16 values require unsigned integer value between 0 and 65535
- Discrete Inputs and Input Registers provide `read-only` access to the Master application and should be manipulated by the user directly in the simulator.
- Coils and Holding Registers provide `read/write` access to the Master application and can also be manipulated by the user directly in the simulator.
- For `RTU` `ASCIIoverRTU` protocols, on a single PC, this simulator can use the help of the [com0com](https://pete.akeo.ie/search/label/com0com) Windows program to provide virtual serial port pairs.
- Additional TextBox allows manual input of the serial port:
  - Mainly intended for Linux so [tty0tty](https://github.com/freemed/tty0tty) virtual port pairs, like `/dev/tnt0` <=> `/dev/tnt1`, could be accessed
  - This box was removed in Mac Mono version
- The library also supports Masked Bit Write, function code 22 (0x16H).

# Build
All it takes is to:
## -> For Windows
- Either use Windows executable files from the `exe` folder or follow the instructions below to build it yourself.
- Download and install Visual Studio community edition (ideally 2019).
- Download and extract the zip file of this project.
- Open this as an existing project in Visual Studio and, on the menu, do:
  - Build/Build Solution (or press Ctrl-Shift-B).
  - Debug/Start Debugging (or press F5) to run the app.
- Locate created EXE file in the `/bin/Debug` folder and copy it over to your preferred folder or Desktop
- For testing RTU protocols use [com0com](https://pete.akeo.ie/search/label/com0com) to create virtual serial ports
## -> For Mono
- Make sure that Mono is installed on your computer, both `mono-complete` and `mono-vbnc` for Linux while for Mac you might need to experiment (maybe `mono` and `mono-basic`).
- Download and extract the zip file of this project and locate Mono archive in the `Mono` folder.
- Extract 4 files and potentially rename the newly created folder and/or exe file to something shorter if you wish (just to make terminal navigation quicker).
- Open the terminal, navigate to the folder and type: `sudo mono ModbusSlaveSimulation.exe` (on Mac you might need to switch to superuser `su` account)
- For testing RTU protocols, on Linux you can possibly install and use [tty0tty](https://github.com/freemed/tty0tty) virtual ports while on Mac the later OS X versions seem to have pseudo terminals - pairs of devices such as `/dev/ptyp3` and `/dev/ttyp3`.

Note for Mac users: this was tested on an old iMac G5 PowerPC computer with Mono v2.10.2. Some odd behaviour was present in a sense that the app was loosing focus thus disrupting continuous TCP communication. There is a text box with red X that you can click to try to maintain focus (if you do something else afterwards then click it again). Since I cannot test it in any other way then it is left for you to experiment.

# License
Licensed under MIT license - see also the README.txt file inside the Resources folder.

# Trademarks
Any and all trademarks, either directly or indirectly mentioned in this project, belong to their respective owners.

# Useful Resources
The AdvancedHMI website [forum](https://www.advancedhmi.com/forum/), which is another open source project that has this app, its Mono version as well as its VB version.
