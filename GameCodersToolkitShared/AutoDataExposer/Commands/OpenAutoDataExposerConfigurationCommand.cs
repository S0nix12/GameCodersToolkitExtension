using GameCodersToolkit.Configuration;
using GameCodersToolkitShared.Utils;
using System.Windows;

namespace GameCodersToolkit
{
	[Command(PackageGuids.AutoDataExposerSet_GuidString, PackageIds.OpenAutoDataExposerConfig)]
	internal sealed class OpenAutoDataExposerConfigurationCommand : BaseCommand<OpenAutoDataExposerConfigurationCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			ConfigurationWindow window = new ConfigurationWindow();

			ConfigurationViewModel vm = new ConfigurationViewModel("AutoDataExposer - Configuration", GameCodersToolkitPackage.AutoDataExposerConfig.UserConfig);
			vm.OnSaveRequested += (s, e) => GameCodersToolkitPackage.AutoDataExposerConfig.SaveConfig<CAutoDataExposerUserConfig>();
			vm.OnReloadRequested += (s, e) => GameCodersToolkitPackage.AutoDataExposerConfig.Reload();

			window.DataContext = vm;
			await window.ShowDialogAsync();
		}
	}
}
