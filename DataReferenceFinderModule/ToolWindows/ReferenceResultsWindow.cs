using Microsoft.VisualStudio.Imaging;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GameCodersToolkit.ReferenceFinder.ToolWindows
{
	public class ReferenceResultsWindow : BaseToolWindow<ReferenceResultsWindow>
	{
		public override string GetTitle(int toolWindowId) => "DataReferenceResults";

		public override Type PaneType => typeof(Pane);

		public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
		{
			ReferenceResultsWindowMessenger messenger = await Package.GetServiceAsync<ReferenceResultsWindowMessenger, ReferenceResultsWindowMessenger>();
			return new ReferenceResultsWindowControl(messenger);
		}

		[Guid("a41b151f-a0f0-42a6-bf69-505d80cf8235")]
		internal class Pane : ToolkitToolWindowPane
		{
			public Pane()
			{
				BitmapImageMoniker = KnownMonikers.ToolWindow;
				ToolBar = new CommandID(PackageGuids.ReferenceResultsToolbarCommandSet_Guid, PackageIds.ReferenceResultsToolbar);
			}
		}
	}
}
