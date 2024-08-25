﻿using DataReferenceFinder.Configuration;
using DataReferenceFinder.ReferenceFinder;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataReferenceFinder.Commands
{
	public abstract class FindGuidReferencesCommandBase<T> : BaseCommand<T>
		where T : class, new()
	{
		protected async Task ExecuteFindReferencesAsync(List<CDataLocationEntry> dataLocationEntries)
		{
			var textWriter = await DataReferenceFinderPackage.ExtensionOutput.CreateOutputPaneTextWriterAsync();

			string searchText = await TextUtilFunctions.FindGuidUnderCaretAsync();

			if (string.IsNullOrEmpty(searchText))
			{
				await DataReferenceFinderPackage.ExtensionOutput.ActivateAsync();
				await textWriter.WriteLineAsync("No Guid selection found");
				return;
			}
			try
			{
				CFileReferenceScanner scanner = new CFileReferenceScanner(dataLocationEntries, searchText);
				List<Task> taskList = new List<Task>();
				Task scanTask = Task.Run(scanner.ScanAsync);
				Task progressUpdateTask = ShowScannerProgressAsync(scanner);

				await ReferenceResultsWindow.ShowAsync();
				await progressUpdateTask;
				await textWriter.WriteLineAsync("Getting Files to Scan took " + scanner.GetFilesDuration.TotalMilliseconds + "ms");
			}
			catch (Exception ex)
			{
				await DataReferenceFinderPackage.ExtensionOutput.ActivateAsync();
				await textWriter.WriteLineAsync("Exeception occured:");
				await textWriter.WriteLineAsync(ex.Message);
				await textWriter.WriteLineAsync(ex.StackTrace);
				await textWriter.WriteLineAsync(ex.ToString());
			}
		}

		async Task ShowScannerProgressAsync(CFileReferenceScanner scanner)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			int progress = 0;
			int total = 0;

			do
			{
				scanner.GetProgress(out progress, out total);
				string progressText = string.Format("Searching Files: {0}/{1}", progress, total);
				await VS.StatusBar.ShowProgressAsync(progressText, progress + 1, total + 1);
				if (total != 1)
				{
					await Task.Delay(100);
				}
				else
				{
					await Task.Delay(5);
				}
			}
			while (progress < total);
		}
	}

	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.FindGuidReferences_AllLocations)]
	internal sealed class FindGuidReferencesAllLocationsCommand : FindGuidReferencesCommandBase<FindGuidReferencesAllLocationsCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await ExecuteFindReferencesAsync(DataReferenceFinderPackage.DataLocationsConfig.GetLocationEntries());
		}

		protected override void BeforeQueryStatus(EventArgs e)
		{
			string guidText = ThreadHelper.JoinableTaskFactory.Run(TextUtilFunctions.FindGuidUnderCaretAsync);
			Command.Enabled = !string.IsNullOrEmpty(guidText);
		}
	}

	internal abstract class FindGuidReferencesLocationSpecificCommand<T> : FindGuidReferencesCommandBase<T>
		where T : class, new()
	{
		protected FindGuidReferencesLocationSpecificCommand(int specificLocationIndex)
		{
			SpecificLocationIndex = specificLocationIndex;
		}

		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			List<CDataLocationEntry> dataLocationEntries = DataReferenceFinderPackage.DataLocationsConfig.GetLocationEntries();
			if (dataLocationEntries.Count > SpecificLocationIndex)
			{
				await ExecuteFindReferencesAsync(new List<CDataLocationEntry> { dataLocationEntries[SpecificLocationIndex] });
			}
		}

		protected override void BeforeQueryStatus(EventArgs e)
		{
			string guidText = ThreadHelper.JoinableTaskFactory.Run(TextUtilFunctions.FindGuidUnderCaretAsync);
			Command.Enabled = !string.IsNullOrEmpty(guidText);

			List<CDataLocationEntry> dataLocationEntries = DataReferenceFinderPackage.DataLocationsConfig.GetLocationEntries();
			bool hasEnoughEntries = dataLocationEntries.Count > SpecificLocationIndex;
			Command.Visible = hasEnoughEntries;
			if (hasEnoughEntries)
			{
				Command.Text = "Find Guid References | " + dataLocationEntries[SpecificLocationIndex].Name;
			}
		}

		protected int SpecificLocationIndex { get; set; } = 0;
	}

	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.FindGuidReferences_Location1)]
	internal sealed class FindGuidReferencesLocation1Command : FindGuidReferencesLocationSpecificCommand<FindGuidReferencesLocation1Command>
	{
		public FindGuidReferencesLocation1Command()
			: base(0)
		{ }
	}

	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.FindGuidReferences_Location2)]
	internal sealed class FindGuidReferencesLocation2Command : FindGuidReferencesLocationSpecificCommand<FindGuidReferencesLocation2Command>
	{
		public FindGuidReferencesLocation2Command()
			: base(1)
		{ }
	}

	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.FindGuidReferences_Location3)]
	internal sealed class FindGuidReferencesLocation3Command : FindGuidReferencesLocationSpecificCommand<FindGuidReferencesLocation3Command>
	{
		public FindGuidReferencesLocation3Command()
			: base(2)
		{ }
	}

	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.FindGuidReferences_Location4)]
	internal sealed class FindGuidReferencesLocation4Command : FindGuidReferencesLocationSpecificCommand<FindGuidReferencesLocation4Command>
	{
		public FindGuidReferencesLocation4Command()
			: base(3)
		{ }
	}

	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.FindGuidReferences_Location5)]
	internal sealed class FindGuidReferencesLocation5Command : FindGuidReferencesLocationSpecificCommand<FindGuidReferencesLocation5Command>
	{
		public FindGuidReferencesLocation5Command()
			: base(4)
		{ }
	}
}
