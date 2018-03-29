using System;
using System.IO;

namespace AppFTP
{
	public sealed partial class FTPModule
	{
		private enum AuthStates
		{
			NeedUser,
			NeedPass,
			Autorized
		}

		private string logDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\Logs\\";
		private string _serverName = "Serv-U FTP server";
		private string _userCommand = "";
		private string _username;
		private string _userpass;

		private bool IsAuthorized { get; set; }
		private AuthStates AuthState { get; set; }

		private BinaryWriter fileLog = null;

		private void FTP_Init()
		{
			AuthState = AuthStates.NeedUser;
			_userCommand = "";
			_username = "";
			_userpass = "";

			if (fileLog != null)
			{
				try
				{
					fileLog.Flush();
					fileLog.Close();
				}
				catch { }

				fileLog = null;
			}
		}

		private void FTP_Welcome()
		{
			if (!Directory.Exists(logDir))
				Directory.CreateDirectory(logDir);

			fileLog = new BinaryWriter(File.Open(logDir + ((System.Net.IPEndPoint)base.ClientEndPoint).Address.ToString() + "_" + ConnectionID.ToString() + ".txt", FileMode.Create));

			buffWriter.Clear();
			buffWriter.WriteString("220 " + _serverName + "\r\n", RawServer.BuffConverter.StringEncoding.ASCII);

			base.Send(buffWriter.ToByteArray());
		}

		private bool FTP_UserCommands(byte[] buffer, int length)
		{
			if (buffer != null)
				fileLog.Write(buffer);
			fileLog.Flush();

			int pos = 0;
			string command = "";
			string arguments = "";

			buffReader.SetBuffer(false, buffer, length);

			_userCommand += buffReader.ReadString(-1, RawServer.BuffConverter.StringEncoding.UTF8);

			if (_userCommand.Length > 1024)
			{
				return false;
			}

			if (_userCommand.Contains("\r\n") && (_userCommand.IndexOf(" ") < _userCommand.IndexOf("\r\n")))
			{
				_userCommand = _userCommand.Trim();

				pos = _userCommand.IndexOf(" ");
				if (pos != -1)
				{
					command = _userCommand.Substring(0, pos).ToUpper();
					arguments = _userCommand.Substring(pos + 1);
				}
				else
					command = _userCommand;

				switch (command)
				{
					case "USER":
						buffWriter.WriteString(User(arguments), RawServer.BuffConverter.StringEncoding.ASCII);
						break;
					case "PASS":
						buffWriter.WriteString(Password(arguments), RawServer.BuffConverter.StringEncoding.ASCII);
						break;
					case "CWD":
						buffWriter.WriteString(ChangeWorkingDirectory(arguments), RawServer.BuffConverter.StringEncoding.ASCII);
						break;
					case "CDUP":
						buffWriter.WriteString(ChangeWorkingDirectory(".."), RawServer.BuffConverter.StringEncoding.ASCII);
						break;
					case "PWD":
						buffWriter.WriteString(ChangeWorkingDirectory("257 \"/\" is current directory."), RawServer.BuffConverter.StringEncoding.ASCII);
						break;
					case "PORT":
						buffWriter.WriteString(Port(), RawServer.BuffConverter.StringEncoding.ASCII);
						break;
					case "QUIT":
						buffWriter.WriteString("221 Service closing control connection.", RawServer.BuffConverter.StringEncoding.ASCII);
						DisconnectByClient();
						break;
					default:
						buffWriter.WriteString("502 Command not implemented.", RawServer.BuffConverter.StringEncoding.ASCII);
						break;
				}

				_userCommand = "";
				buffWriter.WriteString("\r\n", RawServer.BuffConverter.StringEncoding.ASCII);
				base.Send(buffWriter.ToByteArray());
			}
			else
				base.RunReceive(0);

			return true;
		}

		private void FTP_Sended()
		{
			buffWriter.Clear();
		}

		#region FTP Commands

		private string User(string username)
		{
			string outValue = "";

			if (string.IsNullOrEmpty(username.Trim()))
			{
				outValue = "531 Username bad.";
				AuthState = AuthStates.NeedUser;
			}
			else
			{
				_username = username;
				outValue = "331 Username ok, need password.";
				AuthState = AuthStates.NeedPass;
			}

			return outValue;
		}

		private string Password(string password)
		{
			if (AuthState == AuthStates.NeedPass)
			{
				_userpass = password;
				AuthState = AuthStates.Autorized;
				return "230 User logged in.";
			}
			else
			{
				return "503 Bad sequence of commands.";
			}
		}

		private string Port()
		{
			if (AuthState == AuthStates.Autorized)
				return "200 PORT Command successful.";
			else
				return "503 Bad sequence of commands.";
		}

		private string ChangeWorkingDirectory(string pathname)
		{
			if (AuthState != AuthStates.Autorized)
				return "530 Not logged in.";
			else
				return "250 Changed to new directory.";
		}

		#endregion
	}
}
