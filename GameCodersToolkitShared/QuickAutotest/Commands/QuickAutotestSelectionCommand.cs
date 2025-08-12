using EnvDTE80;
using EnvDTE90;
using GameCodersToolkit.Configuration;
using GameCodersToolkit.QuickAutotest;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace GameCodersToolkit.QuickAttach
{
	[Command(PackageGuids.QuickAutotestCommandSet_GuidString, PackageIds.QuickAutotestSelector)]
	internal sealed class QuickAutotestSelectionComboCommand : BaseCommand<QuickAutotestSelectionComboCommand>
	{
		private string outSelectedChoice = "";

		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            QuickAutotestService service = await Package.GetServiceAsync<QuickAutotestService, QuickAutotestService>();

			if (QuickAutotestOptions.Instance.RunTestsOnSelect)
			{
                service.SelectAutotestEntry(-1);

                if (e.OutValue != IntPtr.Zero)
                {
                    outSelectedChoice = "Select the Autotest to run...";
                    Marshal.GetNativeVariantForObject(outSelectedChoice, e.OutValue);
                }
                else if (e.InValue is int index)
                {
                    service.ExecuteAutotestEntry(index);
                }
            }
			else
			{
                if (e.OutValue != IntPtr.Zero)
                {
                    if (service.SelectedAutotest != null)
                    {
                        outSelectedChoice = service.SelectedAutotest.GetFormatedEntryName();
                        Marshal.GetNativeVariantForObject(outSelectedChoice, e.OutValue);
                    }
                    else
                    {
                        outSelectedChoice = "Select autotest...";
                    }
                }
                else if (e.InValue is int index)
                {
                    service.SelectAutotestEntry(index);
                }
            }
        }
	}

	[Command(PackageGuids.QuickAutotestCommandSet_GuidString, PackageIds.QuickAutotestSelectorListCmd)]
	internal sealed class QuickAutotestSelectionComboListCommand : BaseCommand<QuickAutotestSelectionComboListCommand>
	{
		string[] targetProcessNames = { };
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			QuickAutotestService service = await Package.GetServiceAsync<QuickAutotestService, QuickAutotestService>();
			IntPtr pOutValue = e.OutValue;
			if (pOutValue != IntPtr.Zero)
			{
				List<string> updatedEntries = new List<string>();
				foreach (QuickAutotestEntry autotestEntry in service.Autotests)
				{
					updatedEntries.Add(autotestEntry.GetFormatedEntryName());
				}
				targetProcessNames = updatedEntries.ToArray();
				Marshal.GetNativeVariantForObject(targetProcessNames, pOutValue);
			}
		}
	}
}
