using GameCodersToolkit.AutoDataExposerModule.ViewModels;
using GameCodersToolkit.AutoDataExposerModule.Windows;
using GameCodersToolkit.FileTemplateCreator.ViewModels;
using GameCodersToolkit.FileTemplateCreator.Windows;
using System.IO;
using System.Windows;

namespace GameCodersToolkit
{
	[Command(PackageGuids.AutoDataExposerSet_GuidString, PackageIds.OpenAutoDataExposerConfig)]
	internal sealed class OpenAutoDataExposerConfigurationCommand : BaseCommand<OpenAutoDataExposerConfigurationCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			AutoDataExposerConfigurationWindow window = new AutoDataExposerConfigurationWindow();

			window.DataContext = new AutoDataExposerConfigurationViewModel();
			await window.ShowDialogAsync();
		}
	}
}
