using GameCodersToolkit.ReferenceFinder.ToolWindows;

namespace GameCodersToolkit.DataReferenceFinderModule
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.OpenDataExplorer)]
	internal sealed class OpenDataExplorerWindowCommand : BaseCommand<OpenDataExplorerWindowCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await DataExplorerWindow.ShowAsync();
		}
	}
}
