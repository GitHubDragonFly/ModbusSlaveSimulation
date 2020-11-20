using System;
using System.Windows.Forms;

namespace ModbusSlaveSimulation
{
    static class Program
    {
        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name == null)
            {
                throw new NullReferenceException("Item name is null.");
            }
            else
            {
                if (!args.Name.StartsWith("ModbusSlave") && (args.Name.StartsWith("Modbus") || args.Name.StartsWith("log4net") || args.Name.StartsWith("Unme.Common")))
                {
                    if (args.Name.Substring(0, args.Name.IndexOf(',')) == "Modbus")
                        return System.Reflection.Assembly.Load(Properties.Resources.Modbus);
                    if (args.Name.Substring(0, args.Name.IndexOf(',')) == "log4net")
                        return System.Reflection.Assembly.Load(Properties.Resources.log4net);
                    if (args.Name.Substring(0, args.Name.IndexOf(',')) == "Unme.Common")
                        return System.Reflection.Assembly.Load(Properties.Resources.Unme_Common);
                }
                return null;
            }
        }
    }
}
