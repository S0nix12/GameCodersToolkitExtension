using GameCodersToolkit.FileRenamer.ViewModels;
using GameCodersToolkit.FileRenamer.Windows;
using System.Windows;

namespace GameCodersToolkit
{
	[Command(PackageGuids.FileToolsSet_GuidString, PackageIds.MoveFiles)]
	internal sealed class MoveFilesCommand : BaseCommand<MoveFilesCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			CMoveFilesDialogViewModel viewModel = new CMoveFilesDialogViewModel();
			viewModel.InitializeCMakeFileList();

			var window = new MoveFilesWindow();
			window.DataContext = viewModel;

			viewModel.OnRequestClose += (s, args) => window.Close();
			await window.ShowDialogAsync();
		}
	}
}
