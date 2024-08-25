using DataReferenceFinder.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace DataReferenceFinder
{
	public partial class ReferenceResultsWindowControl : UserControl
	{
		public ReferenceResultsWindowControl()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			VS.MessageBox.Show("ToolWindow1Control", "Button clicked");
		}

		private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{

        }

		private async void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				Border borderControl = sender as Border;
				CLineResultViewModel lineResult = borderControl?.DataContext as CLineResultViewModel;
				await lineResult?.ShowEntryAsync();
			}
		}
	}
}
