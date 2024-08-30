using GameCodersToolkit.Configuration;

namespace GameCodersToolkit
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.SaveDataReferenceFinderConfig)]
	internal sealed class SaveConfigCommand : BaseCommand<SaveConfigCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await GameCodersToolkitPackage.DataLocationsConfig.SaveConfigAsync();
		}
	}
}
