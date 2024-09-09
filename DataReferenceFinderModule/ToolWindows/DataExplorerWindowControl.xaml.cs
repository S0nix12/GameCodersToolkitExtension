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
	}
}
