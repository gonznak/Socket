using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppRS.RS.Instructions
{
	interface IBaseInstruction
	{
		int InstuctionNumber { get; }
		IBaseInstruction Deserialize(byte[] packet);
		byte[] Serialize();
	}
}
