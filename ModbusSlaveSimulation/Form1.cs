// * This program is just another implementation of Modbus protocol in the form of a standalone Modbus Slave application
// * supporting RTU/TCP/UDP/ASCIIoverRTU protocols.
// *
// * It is using modified nModbus .NET 3.5 libraries, Copyright (c) 2006 Scott Alexander ( https://code.google.com/p/nmodbus/ ),
// * licensed under MIT license ( http://opensource.org/licenses/mit-license.php ) and included in the Project.
// * See the README.txt file in the "Resources" folder.
// *
// * DataStore can be used to set values even before the connection is established.
// *
// * The slave is set to listen and communicate on a separate background thread in order to prevent GUI from freezing up.
// *
// * Register values are read and written to by the master as signed integer range -32768 to 32767.
// * DataGridView is storing those same values as unsigned integer 0 to 65535 values.
// *
// * The program appears to be fully functional but provided AS IS (feel free to modify it but preserve all these comments).
// *
// * This particular version was coded in C# and designed for use in Windows and will not work as such in Mono.
// * RTU/ASCIIoverRTU Modes could be used together with com0com Windows program to provide virtual serial port pairs.
// *
// * Since the DataGridView controls, used to display register values within the simulator window, seem to require
// * rather fast computer, all instances were limited to initially have only 20 rows thus limiting the number of visible
// * addresses (which should still be sufficient for simulation but can be changed via the combo box).
// *
// * There is also a TextBox added to allow manual input of the serial port to be used (generally for Linux usage).
// *
// * Currently, the simulator will service requests addressed to any slave.
// * All requests will still be accessing the same data store.

using Modbus.Data;
using Modbus.Device;
using System;
using System.Drawing;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Windows.Forms;

namespace ModbusSlaveSimulation
{
    public partial class Form1 : Form
    {
		private SerialPort SerPort;
		private TcpListener ListenerTCP;
		private UdpClient ClientUDP;
		private ModbusSlave slave;
		private readonly DataStore dataStore;
		private readonly byte unitID = 1;
		private readonly Form formEditValue = new Form();     //<-|
		private readonly TextBox formTextbox = new TextBox(); //<-|  Used to edit InputRegisters and HoldingRegisters values
		private readonly Button formButtonOK = new Button();  //<-|
		private int cellRowIndex;
		private int cellColIndex;
		private bool enableMessages = true;
		private bool dgvCDSet;
		private bool dgvIDSet;
		private bool dgvIRSet;
		private bool dgvHRSet;
		private Thread bckgndThread;
		private readonly ToolTip AllToolTip = new ToolTip();

		private delegate void ListMasterRequestsCallback(string strRequest);
		private delegate void UpdateCoilDiscretes(int slave, int index, string value);
		private delegate void UpdateInputDiscretes(int slave, int index, string value);
		private delegate void UpdateInputRegisters(int slave, int index, string value);
		private delegate void UpdateHoldingRegisters(int slave, int index, string value);

		private static bool IsNumeric(string value) => int.TryParse(value, out _);

		#region "Constructor"

		public Form1()
		{
			InitializeComponent();

			SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.ContainerControl | ControlStyles.SupportsTransparentBackColor, true);

			FormClosing += Form1_FormClosing;
			Load += Form1_Load;

			btnCloseRTUASCII.BackColor = Color.Gainsboro;
			btnCloseTCPUDP.BackColor = Color.Gainsboro;

			ModbusSlave.ModbusSlaveMessageReceived += ModbusMessageReceived;

			dataStore = DataStoreFactory.CreateDefaultDataStore();
			dataStore.DataStoreReadFrom += DataStoreReadFrom;
			dataStore.DataStoreWrittenTo += DataStoreWriteTo;

			cbRowCount.SelectedIndex = 0;
			cbCommMode.SelectedIndex = 0;

			dgvCD.ColumnCount = 17;
			dgvID.ColumnCount = 17;
			dgvHR.ColumnCount = 11;
			dgvIR.ColumnCount = 11;

			formButtonOK.DialogResult = DialogResult.OK;
			formButtonOK.Text = "OK";
			formEditValue.Name = "Set Value";
			formEditValue.Text = "Set Value";
			formEditValue.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
			formEditValue.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			formEditValue.AcceptButton = formButtonOK;
			formEditValue.BackgroundImage = ModbusSlaveSimulation.Properties.Resources.DarkBlue;
			formEditValue.Icon = ModbusSlaveSimulation.Properties.Resources.ModbusRTUTCP;
			formEditValue.BackgroundImageLayout = ImageLayout.Stretch;
			formEditValue.Size = new Size(180, 100);
			formEditValue.MinimumSize = new Size(180, 100);
			formEditValue.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			formEditValue.MinimizeBox = false;
			formEditValue.MaximizeBox = false;
			formEditValue.Controls.Add(formTextbox);
			formEditValue.Controls.Add(formButtonOK);
			formTextbox.Size = new Size(60, 25);
			formTextbox.MaxLength = 5;
			formTextbox.Location = new Point(25, 25);
			formButtonOK.Size = new Size(52, 25);
			formButtonOK.Location = new Point(formTextbox.Width + 35, 23);
			formButtonOK.FlatStyle = FlatStyle.Standard;
			formButtonOK.UseVisualStyleBackColor = false;
			formButtonOK.BackColor = Color.Gainsboro;
			formButtonOK.BringToFront();

			formEditValue.Load += FormEditValue_Load;
			formButtonOK.Click += FormButtonOK_Click;
			formButtonOK.MouseHover += FormButtonOK_MouseHover;
		}

		#endregion

		#region "Requests / Updates"

		private void MasterRequests(object sender, ModbusSlaveRequestEventArgs e)
		{
			if (enableMessages)
				ListMasterRequests(e.Message.ToString());
		}

		private void ListMasterRequests(string str)
		{
			if (InvokeRequired)
				Invoke(new ListMasterRequestsCallback(ListMasterRequests), new object[] { str });
			else
			{
				ListBox1.Items.Add(str); // Add and show requests in the application window

				if (ListBox1.Items.Count > 2048)
					ListBox1.Items.RemoveAt(0);

				ListBox1.SelectedIndex = ListBox1.Items.Count - 1;
			}
		}

		private void UpdateCD(int slave, int index, string value) // Update CoilDiscretes values
		{
			if (index < 65535)
			{
				if (InvokeRequired)
				{
					Invoke(new UpdateCoilDiscretes(UpdateCD), new object[] { slave, index, value });
				}
				else
				{
					if (value == "0")
						dataStore.CoilDiscretes[index + 1] = false;
					else
						dataStore.CoilDiscretes[index + 1] = true;

					int div = Math.DivRem(index, 16, out int reminder);

					if (!(div >= dgvCD.Rows.Count))
						dgvCD.Rows[div].Cells[reminder + 1].Value = Convert.ToInt32(value);
				}
			}
		}

		private void UpdateID(int slave, int index, string value) // Update InputDiscretes values
		{
			if (index < 65535)
			{
				if (InvokeRequired)
				{
					Invoke(new UpdateInputDiscretes(UpdateID), new object[] { slave, index, value });
				}
				else
				{
					if (value == "0")
						dataStore.InputDiscretes[index + 1] = false;
					else
						dataStore.InputDiscretes[index + 1] = true;

					int div = Math.DivRem(index, 16, out int reminder);

					if (!(div >= dgvID.Rows.Count))
						dgvID.Rows[div].Cells[reminder + 1].Value = Convert.ToInt32(value);
				}
			}
		}

		private void UpdateIR(int slave, int index, string value) // Update InputRegisters values
		{
			if (index < 65535)
			{
				if (InvokeRequired)
				{
					Invoke(new UpdateInputRegisters(UpdateIR), new object[] { slave, index, value });
				}
				else
				{
					dataStore.InputRegisters[index + 1] = Convert.ToUInt16(value);

					int div = Math.DivRem(index, 10, out int reminder);

					if (!(div >= dgvIR.Rows.Count))
						dgvIR.Rows[div].Cells[reminder + 1].Value = value;
				}
			}
		}

		private void UpdateHR(int slave, int index, string value) // Update HoldingRegisters values
		{
			if (index < 65535)
			{
				if (InvokeRequired)
				{
					Invoke(new UpdateHoldingRegisters(UpdateHR), new object[] { slave, index, value });
				}
				else
				{
					dataStore.HoldingRegisters[index + 1] = Convert.ToUInt16(value);

					int div = Math.DivRem(index, 10, out int reminder);

					if (!(div >= dgvHR.Rows.Count))
						dgvHR.Rows[div].Cells[reminder + 1].Value = value;
				}
			}
		}

		#endregion

		#region "DataStore"

		private void DataStoreReadFrom(object sender, DataStoreEventArgs e)
		{
			switch (e.ModbusDataType)
			{
				case ModbusDataType.Coil:
					// Read CoilDiscretes values
					// add code here
					break;
				case ModbusDataType.Input:
					// Read InputDiscretes values
					// add code here
					break;
				case ModbusDataType.InputRegister:
					// Read InputRegisters values
					// add code here
					break;
				case ModbusDataType.HoldingRegister:
					// Read HoldingRegisters values
					// add code here
					break;
			}

			if (lblMessage.Text != "Comms Okay")
				lblMessage.Invoke((MethodInvoker)delegate {	lblMessage.Text = "Comms Okay";	});
		}

		private void DataStoreWriteTo(object sender, DataStoreEventArgs e)
		{
			int address = e.StartAddress;

			switch (e.ModbusDataType)
			{
				case ModbusDataType.Coil:
					// Write CoilDiscretes values
					for (int i = 0; i <= e.Data.A.Count - 1; i++)
					{
						if (e.Data.A[i])
							UpdateCD(1, address, "1");
                        else
							UpdateCD(1, address, "0");

						address += 1;
					}

					break;
				case ModbusDataType.HoldingRegister:
					// Write HoldingRegisters values
					for (int i = 0; i <= e.Data.B.Count - 1; i++)
					{
						UpdateHR(1, address, e.Data.B[i].ToString());
						address += 1;
					}

					break;
			}

			if (lblMessage.Text != "Comms Okay")
				lblMessage.Invoke((MethodInvoker)delegate {	lblMessage.Text = "Comms Okay";	});
		}

		#endregion

		#region "DataGridView"

		//CoilDiscretes (Digital Outputs - I/O Address Range 000000)
		private void DataGridViewCD_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex == -1 || e.ColumnIndex == 0)
				return;

			lblCellNumber.Text = (Convert.ToInt32(dgvCD.Rows[e.RowIndex].Cells[0].Value.ToString().Substring(0, 6)) + e.ColumnIndex - 1).ToString().PadLeft(6, '0');
		}

		private void DataGridViewCD_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex == -1 || e.ColumnIndex == 0)
				return;

			dgvCD.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = Convert.ToInt32(!Convert.ToBoolean(dgvCD.Rows[e.RowIndex].Cells[e.ColumnIndex].Value));
			UpdateCD(1, Convert.ToInt32(16 * e.RowIndex + e.ColumnIndex - 1), dgvCD.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString());
		}

		//InputDiscretes (Digital Inputs - I/O Address Range 100000)
		private void DataGridViewID_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex == -1 || e.ColumnIndex == 0)
				return;

			lblCellNumber.Text = (Convert.ToInt32(dgvID.Rows[e.RowIndex].Cells[0].Value.ToString().Substring(0, 6)) + e.ColumnIndex - 1).ToString();
		}

		private void DataGridViewID_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex == -1 || e.ColumnIndex == 0)
				return;

			dgvID.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = Convert.ToInt32(!Convert.ToBoolean(dgvID.Rows[e.RowIndex].Cells[e.ColumnIndex].Value));
			UpdateID(1, Convert.ToInt32(16 * e.RowIndex + e.ColumnIndex - 1), dgvID.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString());
		}

		//InputRegisters (Analog Inputs - I/O Address range 300000)
		private void DataGridViewIR_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex == -1 || e.ColumnIndex == 0 || (e.RowIndex == 6553 && e.ColumnIndex > 6))
				return;

			lblCellNumber.Text = (Convert.ToInt32(dgvIR.Rows[e.RowIndex].Cells[0].Value.ToString().Substring(0, 6)) + e.ColumnIndex - 1).ToString();
		}

		private void DataGridViewIR_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex == -1 || e.ColumnIndex == 0 || (e.RowIndex == 6553 && e.ColumnIndex > 6))
				return;

			cellRowIndex = e.RowIndex;
			cellColIndex = e.ColumnIndex;

			DialogResult dr = formEditValue.ShowDialog();

			if (dr == DialogResult.OK)
			{
				if (IsNumeric(formTextbox.Text))
				{
					if (Convert.ToInt32(formTextbox.Text) < 0 || Convert.ToInt32(formTextbox.Text) > 65535)
					{
						MessageBox.Show("The value must be integer number 0 to 65535!");
						formTextbox.Text = "";
						return;
					}

					dgvIR.Rows[cellRowIndex].Cells[cellColIndex].Value = formTextbox.Text;
					UpdateIR(1, 10 * cellRowIndex + cellColIndex - 1, dgvIR.Rows[cellRowIndex].Cells[cellColIndex].Value.ToString());
				}
				else
					MessageBox.Show("The value must be integer number 0 to 65535!");

				formTextbox.Text = "";
				formEditValue.Hide();
			}
		}

		//Holding Registers (Analog Outputs - I/O Address Range 400000)
		private void DataGridViewHR_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex == -1 || e.ColumnIndex == 0 || (e.RowIndex == 6553 && e.ColumnIndex > 6))
				return;

			lblCellNumber.Text = (Convert.ToInt32(dgvHR.Rows[e.RowIndex].Cells[0].Value.ToString().Substring(0, 6)) + e.ColumnIndex - 1).ToString();
		}

		private void DataGridViewHR_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex == -1 || e.ColumnIndex == 0 || (e.RowIndex == 6553 && e.ColumnIndex > 6))
				return;

			cellRowIndex = e.RowIndex;
			cellColIndex = e.ColumnIndex;
			
			DialogResult dr = formEditValue.ShowDialog();
			
			if (dr == DialogResult.OK)
			{
				if (IsNumeric(formTextbox.Text))
				{
					if (Convert.ToInt32(formTextbox.Text) < 0 || Convert.ToInt32(formTextbox.Text) > 65535)
					{
						MessageBox.Show("The value must be integer number 0 to 65535!");
						return;
					}
					dgvHR.Rows[cellRowIndex].Cells[cellColIndex].Value = formTextbox.Text;
					UpdateHR(1, 10 * cellRowIndex + cellColIndex - 1, dgvHR.Rows[cellRowIndex].Cells[cellColIndex].Value.ToString());
				}
				else
					MessageBox.Show("The value must be integer number 0 to 65535!");

				formTextbox.Text = "";
				formEditValue.Hide();
			}
		}

		#endregion

		#region "Private Methods"

		private void AddRowsColumnsdgvCD(int RowCount)
		{
			if (!dgvCDSet)
			{
				// Set address column labels and set all valid cell values to dataStore values
				for (int i = 0; i <= RowCount - 1; i++)
				{
					if (i < 17)
						dgvCD.Columns[i].HeaderCell.Style.BackColor = Color.LightSeaGreen;

					for (int j = 0; j <= 16; j++)
					{
						if (j == 0)
						{
							dgvCD.Rows[i].Cells[j].Value = "0" + (16 * i + 1).ToString().PadLeft(5, '0') + "-" + "0" + (16 * i + 16).ToString().PadLeft(5, '0');
						}
						else
						{
							if (dataStore.CoilDiscretes[i * 16 + j - 1])
								dgvCD.Rows[i].Cells[j].Value = 1;
							else
								dgvCD.Rows[i].Cells[j].Value = 0;
						}
					}
				}
				dgvCDSet = true;
			}
		}

		private void AddRowsColumnsdgvID(int RowCount)
		{
			if (!dgvIDSet)
			{
				// Set address column labels and set all valid cell values to dataStore values
				for (int i = 0; i <= RowCount - 1; i++)
				{
					if (i < 17)
						dgvID.Columns[i].HeaderCell.Style.BackColor = Color.SeaGreen;

					for (int j = 0; j <= 16; j++)
					{
						if (j == 0)
						{
							dgvID.Rows[i].Cells[j].Value = "1" + (16 * i + 1).ToString().PadLeft(5, '0') + "-" + "1" + (16 * i + 16).ToString().PadLeft(5, '0');
						}
						else
						{
							if (dataStore.InputDiscretes[i * 16 + j - 1])
								dgvID.Rows[i].Cells[j].Value = 1;
							else
								dgvID.Rows[i].Cells[j].Value = 0;
						}
					}
				}
				dgvIDSet = true;
			}
		}

		private void AddRowsColumnsdgvIR(int RowCount)
		{
			if (!dgvIRSet)
			{
				// Set address column labels and set all valid cell values to dataStore values
				for (int i = 0; i <= RowCount - 1; i++)
				{
					if (i < 11)
						dgvIR.Columns[i].HeaderCell.Style.BackColor = Color.SteelBlue;

					for (int j = 0; j <= 10; j++)
					{
						if (i == 6553 && j > 6)
							break;

						if (j == 0)
						{
							//Show address range up to 365536
							if (i == 6553)
								dgvIR.Rows[i].Cells[j].Value = "3" + (10 * i + 1).ToString().PadLeft(5, '0') + "-" + "3" + (10 * i + 6).ToString().PadLeft(5, '0');
							else
								dgvIR.Rows[i].Cells[j].Value = "3" + (10 * i + 1).ToString().PadLeft(5, '0') + "-" + "3" + (10 * i + 10).ToString().PadLeft(5, '0');
						}
						else
							dgvIR.Rows[i].Cells[j].Value = dataStore.InputRegisters[i * 10 + j - 1];
					}
				}
				dgvIRSet = true;
			}
		}

		private void AddRowsColumnsdgvHR(int RowCount)
		{
			if (!dgvHRSet)
			{
				// Set address column labels and set all valid cell values to dataStore values
				for (int i = 0; i <= RowCount - 1; i++)
				{
					if (i < 11)
						dgvHR.Columns[i].HeaderCell.Style.BackColor = Color.LightSteelBlue;

					for (int j = 0; j <= 10; j++)
					{
						if (i == 6553 && j > 6)
							break;

						if (j == 0)
						{
							//Show address range up to 465536
							if (i == 6553)
								dgvHR.Rows[i].Cells[j].Value = "4" + (10 * i + 1).ToString().PadLeft(5, '0') + "-" + "4" + (10 * i + 6).ToString().PadLeft(5, '0');
							else
								dgvHR.Rows[i].Cells[j].Value = "4" + (10 * i + 1).ToString().PadLeft(5, '0') + "-" + "4" + (10 * i + 10).ToString().PadLeft(5, '0');
						}
						else
							dgvHR.Rows[i].Cells[j].Value = dataStore.HoldingRegisters[i * 10 + j - 1];
					}
				}
				dgvHRSet = true;
			}
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			cbBaud.SelectedIndex = 15;
			cbDataBits.SelectedIndex = 3;
			cbParity.SelectedIndex = 0;
			cbStopBits.SelectedIndex = 0;

			GetSerialPorts();

			btnCloseRTUASCII.Enabled = false;

			cbIO.SelectedIndex = 0;

			dgvCD.RowCount = Convert.ToInt32(cbRowCount.SelectedItem);
			dgvID.RowCount = Convert.ToInt32(cbRowCount.SelectedItem);
			dgvHR.RowCount = Convert.ToInt32(cbRowCount.SelectedItem);
			dgvIR.RowCount = Convert.ToInt32(cbRowCount.SelectedItem);
			AddRowsColumnsdgvCD(Convert.ToInt32(cbRowCount.SelectedItem));
			AddRowsColumnsdgvID(Convert.ToInt32(cbRowCount.SelectedItem));
			AddRowsColumnsdgvIR(Convert.ToInt32(cbRowCount.SelectedItem));
			AddRowsColumnsdgvHR(Convert.ToInt32(cbRowCount.SelectedItem));

			Focus();
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (bckgndThread != null)
			{
				if (bckgndThread.IsAlive)
					bckgndThread.Join(10);

				bckgndThread = null;
			}

			if (SerPort != null)
			{
				if (SerPort.IsOpen)
				{
					SerPort.DiscardInBuffer();
					SerPort.DiscardOutBuffer();
					SerPort.Close();
				}

				SerPort.Dispose();
			}

			if (formEditValue != null)
			{
				formEditValue.Close();
				formEditValue.Dispose();
			}
		}

		private void FormEditValue_Load(object sender, EventArgs e)
		{
			formTextbox.Focus();
		}

		private void FormButtonOK_Click(object sender, EventArgs e)
		{
			formEditValue.DialogResult = DialogResult.OK;
		}

		private void FormButtonOK_MouseHover(object sender, EventArgs e)
		{
			formTextbox.Focus();
		}

		private void ButtonOpenRTUASCII_Click(object sender, EventArgs e)
		{
			SerPort = new SerialPort();

			if (!string.IsNullOrWhiteSpace(tbManualCOM.Text))
				SerPort.PortName = tbManualCOM.Text;
			else
				SerPort.PortName = cbPort.SelectedItem.ToString();

			SerPort.BaudRate = Convert.ToInt32(cbBaud.SelectedItem.ToString());
			SerPort.Parity = (Parity)cbParity.SelectedIndex;
			SerPort.DataBits = Convert.ToInt32(cbDataBits.SelectedItem.ToString());
			SerPort.StopBits = (StopBits)cbStopBits.SelectedIndex + 1;
			SerPort.Handshake = Handshake.None;
			SerPort.DtrEnable = true;
			SerPort.RtsEnable = true;

			try
			{
				SerPort.Open();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
				return;
			}

			if (cbCommMode.SelectedIndex == 0) //RTU
				slave = ModbusSerialSlave.CreateRtu(unitID, SerPort);
			else if (cbCommMode.SelectedIndex == 3) //ASCIIoverRTU
				slave = ModbusSerialSlave.CreateAscii(unitID, SerPort);

			slave.ModbusSlaveRequestReceived += MasterRequests;
			slave.DataStore = dataStore;

			btnOpenRTUASCII.Enabled = false;
			btnOpenRTUASCII.BackColor = Color.Gainsboro;
			btnCloseRTUASCII.Enabled = true;
			btnCloseRTUASCII.BackColor = Color.LightSteelBlue;
			btnRefresh.Enabled = false;
			btnRefresh.BackColor = Color.Gainsboro;

			cbPort.Enabled = false;
			cbBaud.Enabled = false;
			cbDataBits.Enabled = false;
			cbParity.Enabled = false;
			cbStopBits.Enabled = false;
			cbCommMode.Enabled = false;
			cbRowCount.Enabled = false;
			tbManualCOM.Enabled = false;

			bckgndThread = new Thread(BckgndThreadTask) { IsBackground = true };
			bckgndThread.Start();
		}

		private void ButtonOpenTCPUDP_Click(object sender, EventArgs e)
		{
			int tcpPort = int.Parse(tbPort.Text);
			IPAddress ipAddr = new IPAddress(0);
			IPHostEntry IPHE = null;
			try
			{
				if (IPAddress.TryParse(tbIP.Text, out ipAddr))
				{
					if (cbCommMode.SelectedIndex == 1) //TCP
						ListenerTCP = new TcpListener(ipAddr, tcpPort);
					else if (cbCommMode.SelectedIndex == 2) //UDP
						ClientUDP = new UdpClient(new IPEndPoint(ipAddr, tcpPort));
				}
				else
				{
					IPHE = Dns.GetHostEntry(tbIP.Text);

					if (cbCommMode.SelectedIndex == 1) //TCP
						ListenerTCP = new TcpListener(IPHE.AddressList[1], tcpPort);
					else if (cbCommMode.SelectedIndex == 2) //UDP
						ClientUDP = new UdpClient(new IPEndPoint(IPHE.AddressList[1], tcpPort));
				}
			}
			catch (Exception)
			{
				MessageBox.Show("Invalid IP address or hostname!");
				return;
			}

			if (cbCommMode.SelectedIndex == 1) //TCP
				slave = ModbusTcpSlave.CreateTcp(unitID, ListenerTCP);
			else if (cbCommMode.SelectedIndex == 2) //UDP
				slave = ModbusUdpSlave.CreateUdp(unitID, ClientUDP);

			slave.ModbusSlaveRequestReceived += MasterRequests;
			slave.DataStore = dataStore;

			btnOpenTCPUDP.Enabled = false;
			btnOpenTCPUDP.BackColor = Color.Gainsboro;

			btnCloseTCPUDP.Enabled = true;
			btnCloseTCPUDP.BackColor = Color.LightSteelBlue;

			tbIP.Enabled = false;
			tbPort.Enabled = false;
			cbCommMode.Enabled = false;
			cbRowCount.Enabled = false;

			bckgndThread = new Thread(BckgndThreadTask) { IsBackground = true };
			bckgndThread.Start();
		}

		private void ButtonCloseRTUASCII_Click(object sender, EventArgs e)
		{
			if (SerPort != null)
			{
				if (SerPort.IsOpen)
				{
					SerPort.DiscardInBuffer();
					SerPort.DiscardOutBuffer();
					SerPort.Close();
				}

				SerPort.Dispose();
			}

			if (slave != null)
			{
				slave.Transport.Dispose();
				slave.StopListen();
				slave.Dispose();
			}

			btnOpenRTUASCII.Enabled = true;
			btnOpenRTUASCII.BackColor = Color.LightSteelBlue;

			btnCloseRTUASCII.Enabled = false;
			btnCloseRTUASCII.BackColor = Color.Gainsboro;

			btnRefresh.Enabled = true;
			btnRefresh.BackColor = Color.LightSteelBlue;

			cbPort.Enabled = true;
			cbBaud.Enabled = true;
			cbDataBits.Enabled = true;
			cbParity.Enabled = true;
			cbStopBits.Enabled = true;
			cbCommMode.Enabled = true;
			cbRowCount.Enabled = true;
			tbManualCOM.Enabled = true;
		}

		private void ButtonCloseTCPUDP_Click(object sender, EventArgs e)
		{
			if (slave != null)
			{
				slave.Transport.Dispose();
				slave.StopListen();
				slave.Dispose();
			}

			btnOpenTCPUDP.Enabled = true;
			btnOpenTCPUDP.BackColor = Color.LightSteelBlue;
			btnCloseTCPUDP.Enabled = false;
			btnCloseTCPUDP.BackColor = Color.Gainsboro;

			tbIP.Enabled = true;
			tbPort.Enabled = true;
			cbCommMode.Enabled = true;
			cbRowCount.Enabled = true;
		}

		private void BckgndThreadTask()
		{
			try
			{
				slave.Listen();
			}
			catch (Exception)
			{
			}

			bckgndThread = null;
		}

		private void ButtonRefresh_Click(object sender, EventArgs e)
		{
			GetSerialPorts();
		}

		private void GetSerialPorts()
		{
			string[] portnames = SerialPort.GetPortNames();
			cbPort.Items.Clear();

			if (portnames.Length == 0)
			{
				cbPort.Items.Add("none found");
				btnOpenRTUASCII.Enabled = false;
			}
			else
			{
				foreach (string sPort in portnames)
				{
					cbPort.Items.Add(sPort);
				}

				btnOpenRTUASCII.Enabled = true;
			}

			cbPort.Sorted = true;
			cbPort.SelectedIndex = 0;
		}

		private void ComboBoxIO_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (cbIO.SelectedIndex == 0)
				dgvCD.BringToFront();
			else if (cbIO.SelectedIndex == 1)
				dgvID.BringToFront();
			else if (cbIO.SelectedIndex == 2)
				dgvIR.BringToFront();
			else
				dgvHR.BringToFront();

			lblCellNumber.Text = "";
			Invalidate();
		}

		private void ComboBoxRowCount_SelectedIndexChanged(object sender, EventArgs e)
		{
			dgvCDSet = false;
			dgvIDSet = false;
			dgvIRSet = false;
			dgvHRSet = false;

			if (cbRowCount.SelectedItem.ToString() == "MAX")
			{
				dgvCD.RowCount = 4096;
				dgvID.RowCount = 4096;
				dgvHR.RowCount = 6554;
				dgvIR.RowCount = 6554;
				AddRowsColumnsdgvCD(4096);
				AddRowsColumnsdgvID(4096);
				AddRowsColumnsdgvIR(6554);
				AddRowsColumnsdgvHR(6554);
			}
			else
			{
				dgvCD.RowCount = Convert.ToInt32(cbRowCount.SelectedItem);
				dgvID.RowCount = Convert.ToInt32(cbRowCount.SelectedItem);
				dgvHR.RowCount = Convert.ToInt32(cbRowCount.SelectedItem);
				dgvIR.RowCount = Convert.ToInt32(cbRowCount.SelectedItem);
				AddRowsColumnsdgvCD(Convert.ToInt32(cbRowCount.SelectedItem));
				AddRowsColumnsdgvID(Convert.ToInt32(cbRowCount.SelectedItem));
				AddRowsColumnsdgvIR(Convert.ToInt32(cbRowCount.SelectedItem));
				AddRowsColumnsdgvHR(Convert.ToInt32(cbRowCount.SelectedItem));
			}

			Invalidate();
		}

		private void ComboBoxCommMode_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (cbCommMode.SelectedIndex == 0 || cbCommMode.SelectedIndex == 3) //RTU or ASCIIoverRTU
			{
				gbRTU.Enabled = true;
				gbRTU.BringToFront();
				gbTCP.Enabled = false;
				gbTCP.SendToBack();
			}
			else //TCP or UDP
			{
				gbTCP.Enabled = true;
				gbTCP.BringToFront();
				gbRTU.Enabled = false;
				gbRTU.SendToBack();
			}
		}

		private void ListBox1_DoubleClick(object sender, EventArgs e)
		{
			enableMessages = !enableMessages;
		}

		private void CheckBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (CheckBox1.Checked)
				ListBox1.Show();
			else
			{
				ListBox1.Hide();
				ListBox1.Items.Clear();
			}
		}

		private void TextBoxManualCOM_TextChanged(object sender, EventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(tbManualCOM.Text))
				btnOpenRTUASCII.Enabled = true;
			else
				btnOpenRTUASCII.Enabled = false;
		}

		private void ModbusMessageReceived(object sender, Modbus.ModbusMessagesEventArgs e)
		{
			if (e.Message == "Master closed Socket connection.")
			{
				slave.StopListen();
				slave.Listen();
			}

			if (lblMessage.InvokeRequired)
				lblMessage.Invoke((MethodInvoker)delegate {	lblMessage.Text = e.Message; });
			else
				lblMessage.Text = e.Message;
		}

		#endregion

		#region "ToolTips"

		private void ButtonCloseRTUASCII_MouseHover(object sender, EventArgs e)
		{
			if (btnCloseRTUASCII.Enabled)
				AllToolTip.SetToolTip(btnCloseRTUASCII, "Close serial port.");
		}

		private void ButtonOpenRTUASCII_MouseHover(object sender, EventArgs e)
		{
			if (btnOpenRTUASCII.Enabled)
				AllToolTip.SetToolTip(btnOpenRTUASCII, "Open serial port with currently selected parameters.");
		}

		private void ButtonRefresh_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(btnRefresh, "Refresh serial ports list.");
		}

		private void LabelManual_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(lblManual, "Set COM port manually.");
		}

		private void LabelRowCount_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(lblRowCount, "Number of rows visible in the DataGridView.");
		}

		private void CheckBox1_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(CheckBox1, "Show/Hide messages window.");
		}

		private void GroupBoxFC_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(gbFC, "Function Code 15 - Write Multiple Coils" + Environment.NewLine + "Function Code 16 - Write Multiple Registers" + Environment.NewLine + "Function Code 22 - Masked Bit Write" + Environment.NewLine + "Function Code 23 - ReadWrite Multiple Registers");
		}

		private void ListBox1_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(ListBox1, "Double click to pause showing incoming master request messages.");
		}

		private void PictureBox1_MouseHover(object sender, EventArgs e)
		{
			AllToolTip.SetToolTip(PictureBox1, "https://code.google.com/p/nmodbus/");
		}

		#endregion
	}
}
