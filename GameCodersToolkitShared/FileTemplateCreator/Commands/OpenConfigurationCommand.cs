using GameCodersToolkit.Configuration;
using GameCodersToolkitShared.Utils;
using System.Windows;

namespace GameCodersToolkit
{
	[Command(PackageGuids.FileTemplateCreatorSet_GuidString, PackageIds.OpenTemplateFileCreatorConfig)]
	internal sealed class OpenConfigurationCommand : BaseCommand<OpenConfigurationCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			ConfigurationWindow window = new ConfigurationWindow();

			ConfigurationViewModel vm = new ConfigurationViewModel("FileTemplateCreator - Configuration", GameCodersToolkitPackage.FileTemplateCreatorConfig.UserConfig);
			vm.OnSaveRequested += (s, e) => GameCodersToolkitPackage.FileTemplateCreatorConfig.SaveConfig<CFileTemplateCreatorUserConfig>();
			vm.OnReloadRequested += (s, e) => GameCodersToolkitPackage.FileTemplateCreatorConfig.Reload();

			window.DataContext = vm;
			await window.ShowDialogAsync();
		}
	}
}
