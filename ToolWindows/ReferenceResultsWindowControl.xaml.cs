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
	}
}
