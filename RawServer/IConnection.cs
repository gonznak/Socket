namespace RawServer
{
	interface IConnection
	{
		bool Send(byte[] data);
		void Close();
		void CleanUp();
	}
}
