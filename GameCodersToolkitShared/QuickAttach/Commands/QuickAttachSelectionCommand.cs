using EnvDTE80;
using EnvDTE90;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace GameCodersToolkit.QuickAttach
{
	[Command(PackageGuids.QuickAttachCommandSet_GuidString, PackageIds.QuickAttachSelector)]
	internal sealed class QuickAttachSelectionComboCommand : BaseCommand<QuickAttachSelectionComboCommand>
	{
		private string outSelectedChoice = "";

		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			QuickAttachService service = await Package.GetServiceAsync<QuickAttachService, QuickAttachService>();
			if (e.OutValue != IntPtr.Zero)
			{
				if (service.SelectedProcess != null)
				{
					outSelectedChoice = service.SelectedProcess.GetFormatedEntryName();
					Marshal.GetNativeVariantForObject(outSelectedChoice, e.OutValue);
				}
				else
				{
					outSelectedChoice = "";
				}
			}
			else if (e.InValue is int index)
			{
				service.SelectProcessEntry(index);
			}
		}
	}

	[Command(PackageGuids.QuickAttachCommandSet_GuidString, PackageIds.QuickAttachSelectorListCmd)]
	internal sealed class QuickAttachSelectionComboListCommand : BaseCommand<QuickAttachSelectionComboListCommand>
	{
		string[] targetProcessNames = { };
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			QuickAttachService service = await Package.GetServiceAsync<QuickAttachService, QuickAttachService>();
			IntPtr pOutValue = e.OutValue;
			if (pOutValue != IntPtr.Zero)
			{
				service.UpdateProcessList();
				List<string> updatedEntries = new List<string>();
				foreach (QuickAttachProcessEntry processEntry in service.ProcessEntries)
				{
					updatedEntries.Add(processEntry.GetFormatedEntryName());
				}
				targetProcessNames = updatedEntries.ToArray();
				Marshal.GetNativeVariantForObject(targetProcessNames, pOutValue);
			}
		}
	}
}
