using GameCodersToolkit.FileTemplateCreator.ViewModels;
using GameCodersToolkit.FileTemplateCreator.Windows;
using System.IO;
using System.Windows;

namespace GameCodersToolkit
{
	[Command(PackageGuids.FileTemplateCreatorSet_GuidString, PackageIds.CreateFileFromTemplate)]
	internal sealed class CreateFileFromTemplate : BaseCommand<CreateFileFromTemplate>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			var createFileWindow = new CreateFileFromTemplateWindow();
			
			CFileTemplateDialogViewModel viewModel = new CFileTemplateDialogViewModel();
			createFileWindow.DataContext = viewModel;

			await createFileWindow.ShowDialogAsync();
		}
	}
}
