using System.Windows;
using System.Windows.Controls;

namespace GameCodersToolkit.FileRenamer.Windows
{
	/// <summary>
	/// Interaction logic for MoveFolderWindow.xaml
	/// </summary>
	public partial class MoveFolderWindow : Window
	{
		public MoveFolderWindow()
		{
			InitializeComponent();
		}

		private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
		{
			if (sender is ScrollViewer scrollViewer)
			{
				scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
				e.Handled = true;
			}
		}
	}
}
