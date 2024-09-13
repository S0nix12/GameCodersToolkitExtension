using System.Windows;
using System.Windows.Controls;

namespace GameCodersToolkit.DataReferenceFinderModule
{
	public partial class DataExplorerWindowControl : UserControl
	{
		public DataExplorerWindowControl()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			VS.MessageBox.Show("DataExplorerWindowControl", "Button clicked");
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
