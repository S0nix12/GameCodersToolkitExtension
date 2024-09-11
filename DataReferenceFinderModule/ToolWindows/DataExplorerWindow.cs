using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GameCodersToolkit.DataReferenceFinderModule
{
	public class DataExplorerWindow : BaseToolWindow<DataExplorerWindow>
	{
		public override string GetTitle(int toolWindowId) => "DataExplorer";

		public override Type PaneType => typeof(Pane);

		public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
		{
			return Task.FromResult<FrameworkElement>(new DataExplorerWindowControl());
		}

		[Guid("8894caf4-e84a-41dd-be84-b46285243707")]
		internal class Pane : ToolkitToolWindowPane
		{
			public Pane()
			{
				BitmapImageMoniker = KnownMonikers.DatabaseFile;
			}
		}
	}
}
