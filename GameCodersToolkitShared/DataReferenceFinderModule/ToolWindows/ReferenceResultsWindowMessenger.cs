using GameCodersToolkit.QuickAttach;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.ReferenceFinder.ToolWindows
{
	public enum EReferenceResultsWindowToolbarAction
	{
		CollapseAll,
		ExpandAll
	}

	public class ReferenceResultsWindowMessenger
	{
		public delegate string SelectedFilterProvider();
		public void Send(EReferenceResultsWindowToolbarAction action)
		{
			MessageReceived?.Invoke(this, action);
		}

		public void UpdateFilterString(string newFilter)
		{
			FilterUpdated?.Invoke(this, newFilter);
		}

		public event EventHandler<EReferenceResultsWindowToolbarAction> MessageReceived;
		public event EventHandler<string> FilterUpdated;
		public SelectedFilterProvider FilterProvider { get; set; }

	}

	[Command(PackageGuids.ReferenceResultsToolbarCommandSet_GuidString, PackageIds.CollapseAllResults)]
	internal sealed class ReferenceResultsWindowCollapseAll : BaseCommand<ReferenceResultsWindowCollapseAll>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			ReferenceResultsWindowMessenger messenger = await Package.GetServiceAsync<ReferenceResultsWindowMessenger, ReferenceResultsWindowMessenger>();
			messenger.Send(EReferenceResultsWindowToolbarAction.CollapseAll);
		}
	}

	[Command(PackageGuids.ReferenceResultsToolbarCommandSet_GuidString, PackageIds.ExpandAllResults)]
	internal sealed class ReferenceResultsWindowExpandAll : BaseCommand<ReferenceResultsWindowExpandAll>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			ReferenceResultsWindowMessenger messenger = await Package.GetServiceAsync<ReferenceResultsWindowMessenger, ReferenceResultsWindowMessenger>();
			messenger.Send(EReferenceResultsWindowToolbarAction.ExpandAll);
		}
	}

	[Command(PackageGuids.ReferenceResultsToolbarCommandSet_GuidString, PackageIds.FilterResults)]
	internal sealed class ReferenceResultsWindowFilterComboCommand : BaseCommand<ReferenceResultsWindowFilterComboCommand>
	{
		private string outSelectedChoice = "";

		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			ReferenceResultsWindowMessenger messenger = await Package.GetServiceAsync<ReferenceResultsWindowMessenger, ReferenceResultsWindowMessenger>();
			if (e.OutValue != IntPtr.Zero)
			{
				outSelectedChoice = messenger.FilterProvider != null ? messenger.FilterProvider() : "";
				Marshal.GetNativeVariantForObject(outSelectedChoice, e.OutValue);
			}
			else if (e.InValue is string filterString)
			{
				messenger.UpdateFilterString(filterString);
			}
		}
	}
}
