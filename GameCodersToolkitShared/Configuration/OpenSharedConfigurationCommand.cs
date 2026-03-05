using GameCodersToolkit.Configuration;
using GameCodersToolkitShared.Utils;
using System.Windows;

namespace GameCodersToolkit
{
	[Command(PackageGuids.ConfigurationSet_GuidString, PackageIds.OpenSharedConfig)]
	internal sealed class OpenSharedConfigurationCommand : BaseCommand<OpenSharedConfigurationCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			ConfigurationWindow window = new ConfigurationWindow();

			ConfigurationViewModel vm = new ConfigurationViewModel("Game Coders Toolkit - Configuration", GameCodersToolkitPackage.SharedConfig.UserConfig);
			vm.OnSaveRequested += (s, e) => GameCodersToolkitPackage.SharedConfig.SaveConfig<CSharedUserConfig>();
			vm.OnReloadRequested += (s, e) => GameCodersToolkitPackage.SharedConfig.Reload();

			window.DataContext = vm;
			await window.ShowDialogAsync();
		}
	}
}
