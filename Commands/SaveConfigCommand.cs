using DataReferenceFinder.Configuration;

namespace DataReferenceFinder
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.SaveDataReferenceFinderConfig)]
	internal sealed class SaveConfigCommand : BaseCommand<SaveConfigCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await DataReferenceFinderPackage.DataLocationsConfig.SaveConfigAsync();
		}
	}
}
