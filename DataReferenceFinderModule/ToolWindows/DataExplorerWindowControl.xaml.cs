using GameCodersToolkit.DataReferenceFinderModule.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GameCodersToolkit.DataReferenceFinderModule
{
	public partial class DataExplorerWindowControl : UserControl
	{
		public DataExplorerWindowControl()
		{
			InitializeComponent();
		}

		private void DataEntryVM_Border_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

			if (treeViewItem != null)
			{
				treeViewItem.Focus();
				treeViewItem.IsSelected = true;
				e.Handled = true;
			}
		}

		private async void DataEntryVM_Border_LeftMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				Border borderControl = sender as Border;
				DataEntryViewModel dataEntryVM = borderControl?.DataContext as DataEntryViewModel;
				if (dataEntryVM != null)
				{
					await dataEntryVM.OpenInVisualStudioAsync();
				}
			}
		}

		static TreeViewItem VisualUpwardSearch(DependencyObject source)
		{
			while (source != null && !(source is TreeViewItem))
				source = VisualTreeHelper.GetParent(source);

			return source as TreeViewItem;
		}

		private void SearchFilterField_GotFocus(object sender, RoutedEventArgs e)
		{
			if (SearchFilterField.Text == "Search...")
			{
				SearchFilterField.Text = string.Empty;
				SearchFilterField.GotFocus -= SearchFilterField_GotFocus;
			}
		}
	}
}
