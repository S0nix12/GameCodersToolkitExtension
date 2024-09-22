using System.Diagnostics;

namespace GameCodersToolkit.DataReferenceFinderModule
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.ParseAllDataLocation)]
	internal sealed class ParseAllDataLocation : BaseCommand<ParseAllDataLocation>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await GameCodersToolkitPackage.DataParsingEngine.ParseDataAsync();
		}
	}
}
