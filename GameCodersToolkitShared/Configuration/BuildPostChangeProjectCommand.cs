using GameCodersToolkit.Configuration;

namespace GameCodersToolkit
{
	[Command(PackageGuids.ConfigurationSet_GuidString, PackageIds.BuildPostChangeProject)]
	internal sealed class BuildPostChangeProjectCommand : BaseCommand<BuildPostChangeProjectCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			CSharedConfiguration sharedConfig = GameCodersToolkitPackage.SharedConfig;
			if (sharedConfig?.UserConfig == null)
			{
				await VS.MessageBox.ShowAsync("Game Coders Toolkit", "Shared configuration is not loaded.");
				return;
			}

			if (string.IsNullOrWhiteSpace(sharedConfig.UserConfig.PostChangeProjectToBuild))
			{
				await VS.MessageBox.ShowAsync("Game Coders Toolkit", "No PostChangeProjectToBuild is configured. Set it in the Configuration.");
				return;
			}

			await sharedConfig.BuildPostChangeProjectAsync();
		}
	}
}
