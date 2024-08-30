using System.IO;

namespace GameCodersToolkit
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.OpenConfigFile)]
	internal sealed class OpenConfigFileCommand : BaseCommand<OpenConfigFileCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			string configFilePath = GameCodersToolkitPackage.DataLocationsConfig.GetConfigFilePath();
			configFilePath = Path.GetFullPath(configFilePath);
			if (!string.IsNullOrEmpty(configFilePath) && File.Exists(configFilePath))
			{
				await VS.Documents.OpenAsync(configFilePath);
			}
		}
	}
}
