using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace GameCodersToolkit.FileRenamer.Windows
{
	/// <summary>
	/// Interaction logic for RenameFileWindow.xaml
	/// </summary>
	public partial class RenameFileWindow : Window
	{
		public RenameFileWindow()
		{
			InitializeComponent();

			Loaded += (s, e) =>
			{
				NewNameTextBox.Focus();
				NewNameTextBox.SelectAll();
			};
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
