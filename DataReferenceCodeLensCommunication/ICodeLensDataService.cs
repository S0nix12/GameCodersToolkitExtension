using System;
using System.Collections.Generic;
using System.Text;

namespace DataReferenceCodeLensCommunication
{
	public interface ICodeLensDataService
	{
		int GetReferenceCount(string identifier);
	}
}
