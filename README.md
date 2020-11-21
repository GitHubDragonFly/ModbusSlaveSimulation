# ModbusSlaveSimulation
Windows app supporting Modbus RTU, TCP, UDP and ASCIIoverRTU protocols for simulation.

Based on modified nModbus .NET 3.5 libraries, Copyright (c) 2006 Scott Alexander ( https://code.google.com/p/nmodbus/ ).

# Functionality
- The values can be set before the connection is established.
- For RTU / ASCIIoverRTU protocols, on a single PC, this simulator can use the help of the com0com Windows program to provide virtual serial port pairs.
- Additional TextBox allows manual input of the serial port (intended for Linux so tty0tty virtual ports could be accessed).
- The library supports Masked Bit Write, function code 22 (0x16H).

# Build
All it takes is to:

- Download and install Visual Studio community edition (ideally 2019).
- Download and extract the zip file of this project.
- Open this as an existing project in Visual Studio and, on the menu, do:
  - Build/Build Solution (or press Ctrl-Shift-B).
  - Debug/Start Debugging (or press F5) to run the app.
- Locate created EXE file in the /bin/Debug folder and copy it over to your preferred folder or Desktop.

# License
Licensed under MIT license - see the README.txt file inside the Resources folder.

# Trademarks
Any and all trademarks, either directly or indirectly mentioned in this project, belong to their respective owners.

# Useful Resources
The forum of AdvancedHMI website, which is another open source project that has this app, its Mono version as well as its VB version:

https://www.advancedhmi.com/forum/
