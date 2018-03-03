using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace AppClientSendFile
{
	class Program
	{
		static void Main(string[] args)
		{
			Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			client.Connect(new IPEndPoint(IPAddress.Parse("192.168.0.204"), 9520));
			client.SendFile(@"d:\test.jpg");
			client.Shutdown(SocketShutdown.Both);
			client.Close();
		}
	}
}
