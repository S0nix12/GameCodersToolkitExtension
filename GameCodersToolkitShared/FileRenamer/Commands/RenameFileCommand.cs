using GameCodersToolkit.FileRenamer.ViewModels;
using GameCodersToolkit.FileRenamer.Windows;
using System.Windows;

namespace GameCodersToolkit
{
	[Command(PackageGuids.FileToolsSet_GuidString, PackageIds.RenameFile)]
	internal sealed class RenameFileCommand : BaseCommand<RenameFileCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			DocumentView activeDocument = await VS.Documents.GetActiveDocumentViewAsync();
			string activeFilePath = activeDocument?.FilePath;

			if (string.IsNullOrEmpty(activeFilePath))
			{
				await VS.MessageBox.ShowErrorAsync("File Renamer", "No active document found. Please open a file first.");
				return;
			}

			CRenameFileDialogViewModel viewModel = new CRenameFileDialogViewModel(activeFilePath);

			var window = new RenameFileWindow();
			window.DataContext = viewModel;

			viewModel.OnRequestClose += (s, args) => window.Close();
			await window.ShowDialogAsync();
		}
	}
}
