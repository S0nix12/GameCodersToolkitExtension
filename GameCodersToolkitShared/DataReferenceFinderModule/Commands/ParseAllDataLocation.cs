using System.Diagnostics;

namespace GameCodersToolkit.DataReferenceFinderModule
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.ParseAllDataLocation)]
	internal sealed class ParseAllDataLocation : BaseCommand<ParseAllDataLocation>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			await GameCodersToolkitPackage.DataParsingEngine.StartDataParseAsync();
			stopwatch.Stop();
			await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync("Finished Data Parsing. Took: " + stopwatch.ElapsedMilliseconds + "ms");
		}
	}
}
