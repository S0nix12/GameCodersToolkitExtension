using GameCodersToolkit.FileTemplateCreator.ViewModels;
using GameCodersToolkit.FileTemplateCreator.Windows;
using System.IO;
using System.Windows;

namespace GameCodersToolkit
{
	[Command(PackageGuids.FileTemplateCreatorSet_GuidString, PackageIds.OpenTemplateFileCreatorConfig)]
	internal sealed class OpenConfigurationCommand : BaseCommand<OpenConfigurationCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			ConfigurationWindow window = new ConfigurationWindow();

			window.DataContext = new ConfigurationViewModel();
			await window.ShowDialogAsync();
		}
	}
}
