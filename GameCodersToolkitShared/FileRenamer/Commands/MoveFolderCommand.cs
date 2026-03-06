using GameCodersToolkit.FileRenamer.ViewModels;
using GameCodersToolkit.FileRenamer.Windows;
using System.Windows;

namespace GameCodersToolkit
{
	[Command(PackageGuids.FileToolsSet_GuidString, PackageIds.MoveFolder)]
	internal sealed class MoveFolderCommand : BaseCommand<MoveFolderCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			CMoveFolderDialogViewModel viewModel = new CMoveFolderDialogViewModel();

			var window = new MoveFolderWindow();
			window.DataContext = viewModel;

			viewModel.OnRequestClose += (s, args) => window.Close();
			await window.ShowDialogAsync();
		}
	}
}
