using System.Collections.Generic;

namespace DataReferenceCodeLensProviderShared.Communication
{
	public interface ICodeLensDataService
	{
		int GetReferenceCount(string identifier);
		List<CodeLensDataReferenceDetails> GetReferenceDetails(string identifier);
		void ReportErrorMessage(string message);
	}
}
