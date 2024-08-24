namespace DataReferenceFinder.Commands
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.OpenReferenceResults)]
	internal sealed class OpenReferenceResultsWindow : BaseCommand<OpenReferenceResultsWindow>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await ReferenceResultsWindow.ShowAsync();
		}
	}
}
