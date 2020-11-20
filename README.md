# ModbusSlaveSimulation
Windows app supporting Modbus RTU, TCP, UDP and ASCIIoverRTU protocols for simulation.

Based on modified nModbus .NET 3.5 libraries, Copyright (c) 2006 Scott Alexander ( https://code.google.com/p/nmodbus/ ).

# Functionality
- The values can be set before the connection is established.
- For RTU / ASCIIoverRTU protocols, on a single PC, this simulator can use the help of the com0com Windows program to provide virtual serial port pairs.
- Additional TextBox was added to allow for manual input of the serial port to be used (intended for Linux so tty0tty virtual ports could be accessed).
- The library supports Masked Bit Write, function code 22 (0x16H).

# License
Licensed under MIT license - see the README.txt file inside the Resources folder.

# Useful Resources
The forum of AdvancedHMI website, which is another open source project that has this app, its Mono version as well as its counterpart Modbus Master:

https://www.advancedhmi.com/forum/
