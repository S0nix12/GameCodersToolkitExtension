using GameCodersToolkit.FileTemplateCreator.ViewModels;
using GameCodersToolkit.FileTemplateCreator.Windows;
using System.Windows;

namespace GameCodersToolkit
{
	[Command(PackageGuids.FileTemplateCreatorSet_GuidString, PackageIds.CreateFileFromTemplate)]
	internal sealed class CreateFileFromTemplateCommand : BaseCommand<CreateFileFromTemplateCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			CFileTemplateDialogViewModel viewModel = new CFileTemplateDialogViewModel();
			
			var window = new CreateFileFromTemplateWindow();
			window.DataContext = viewModel;

			viewModel.OnRequestClose += (s, e) => window.Close();
			viewModel.OnSaveFileDialogCreated += (s, dialog) => { return dialog.ShowDialog(window); };
			await window.ShowDialogAsync();
		}
	}
}
