namespace DataReferenceCodeLensProviderShared.Communication
{
	public interface ICodeLensDataService
	{
		int GetReferenceCount(string identifier);
	}
}
