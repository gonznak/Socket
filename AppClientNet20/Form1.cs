using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using RawClient.Net20.Common;
using RawClient.Net20;

namespace AppClientNet20
{
	public partial class Form1 : Form
	{
		RawClient.Net20.Client ClientModule;

		public Form1()
		{
			InitializeComponent();

			textBox1.KeyDown += new KeyEventHandler(textBox1_KeyDown);
			this.Load += new EventHandler(Form1_Load);

			ClientModule = new RawClient.Net20.Client();
			ClientModule.ReceiveBufferSize = 65535;
			ClientModule.ServerReceiveCommand += new OnServerReceiveCommand(ClientModule_ServerReceiveCommand);
		}

		void Form1_Load(object sender, EventArgs e)
		{
			ClientModule.Connect(new System.Net.IPEndPoint(new System.Net.IPAddress(new byte[] { 192, 168, 0, 204 }), 5555));
		}

		void textBox1_KeyDown(object sender, KeyEventArgs e)
		{
			try
			{
				if (e.KeyCode == Keys.Enter)
				{
					switch (textBox1.Text)
					{
						case "disc":
							ClientModule.SendCommand(new ToClientCommand() { Command = ClientCommands.Disconnect });
							break;
						default:
							ClientModule.SendCommand(new ToClientCommand() { Command = ClientCommands.Send, ReceiveBuffer = Encoding.UTF8.GetBytes(textBox1.Text) });
							break;
					}
				}
			}
			catch
			{ }
		}

		void ClientModule_ServerReceiveCommand(RawClient.Net20.Common.FromServerCommand Command)
		{
			if (Command.ReceiveBuffer == null)
			{
				richTextBox1.AppendText(DateTime.Now.ToString("HH:mm:ss.f") + " Message: " + Command.Action + "\n");
			}
			else
			{
				richTextBox1.AppendText(DateTime.Now.ToString("HH:mm:ss.f") + " Message: " + Command.Action + ", Buffer: " + Encoding.UTF8.GetString(Command.ReceiveBuffer, 0, Command.ReceiveBufferLength) + "\n");
			}
			richTextBox1.ScrollToCaret();
		}
	}
}
