using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataReferenceFinder.ToolWindows
{
	public enum EReferenceResultsWindowToolbarAction
	{
		CollapseAll,
		ExpandAll
	}

	public class ReferenceResultsWindowMessenger
	{
		public void Send(EReferenceResultsWindowToolbarAction action)
		{
			MessageReceived?.Invoke(this, action);
		}

		public event EventHandler<EReferenceResultsWindowToolbarAction> MessageReceived;
	}

	[Command(PackageGuids.ReferenceResultsToolbarCommandSet_GuidString, PackageIds.CollapseAllResults)]
	internal sealed class ReferenceResultsWindowCollapseAll : BaseCommand<ReferenceResultsWindowCollapseAll>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				ReferenceResultsWindowMessenger messenger = await Package.GetServiceAsync<ReferenceResultsWindowMessenger, ReferenceResultsWindowMessenger>();
				messenger.Send(EReferenceResultsWindowToolbarAction.CollapseAll);
			}).FireAndForget();

		}
	}

	[Command(PackageGuids.ReferenceResultsToolbarCommandSet_GuidString, PackageIds.ExpandAllResults)]
	internal sealed class ReferenceResultsWindowExpandAll : BaseCommand<ReferenceResultsWindowExpandAll>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				ReferenceResultsWindowMessenger messenger = await Package.GetServiceAsync<ReferenceResultsWindowMessenger, ReferenceResultsWindowMessenger>();
				messenger.Send(EReferenceResultsWindowToolbarAction.ExpandAll);
			}).FireAndForget();

		}
	}
}
