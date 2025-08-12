using EnvDTE80;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;

namespace GameCodersToolkit.QuickAutotest
{
    [Command(PackageGuids.QuickAutotestCommandSet_GuidString, PackageIds.QuickAutotestCommand)]
    internal sealed class QuickAutotestCommand : BaseCommand<QuickAutotestCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var quickAutotestService = await Package.GetServiceAsync<QuickAutotestService, QuickAutotestService>();

            if (!QuickAutotestOptions.Instance.RunTestsOnSelect && quickAutotestService.SelectedAutotest != null)
            {
                quickAutotestService.ExecuteAutotestEntry(quickAutotestService.SelectedAutotest);
            }
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            var quickAutotestService = Package.GetService<QuickAutotestService, QuickAutotestService>();

            Command.Visible = !QuickAutotestOptions.Instance.RunTestsOnSelect;
            Command.Enabled = Command.Visible && quickAutotestService.SelectedAutotest != null;
        }
    }
}
