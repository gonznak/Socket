using Microsoft.Win32;
using RawServer;
using System.IO;
using System.Linq;
using System.Windows;

namespace AppRS
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		ServModule sModule = null;

		public MainWindow()
		{
			InitializeComponent();

			sModule = new ServModule();
			sModule.Start();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			BaseProtocol client = sModule.Clients.FirstOrDefault();
			if (client == null) return;


			System.Diagnostics.Debug.WriteLine("TotalBytesReceived : " + client.TotalBytesReceived);
			System.Diagnostics.Debug.WriteLine("TotalBytesTransmitted : " + client.TotalBytesTransmitted);
			client.DisconnectByClient();
			//client.Disconnect();

			return;
			/*
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "exe files (*.exe)|*.exe|All files (*.*)|*.*";
			openFileDialog.RestoreDirectory = true;

			if (openFileDialog.ShowDialog() != true) return;

			Stream myStream = openFileDialog.OpenFile();
			byte[] data = new byte[myStream.Length];
			myStream.Read(data, 0, data.Length);
			*/
			//client.SendFile(data);
		}
	}
}
