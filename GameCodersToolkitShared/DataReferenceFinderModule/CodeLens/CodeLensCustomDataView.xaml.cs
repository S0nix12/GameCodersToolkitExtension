using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GameCodersToolkitShared.DataReferenceFinderModule.CodeLens
{
	/// <summary>
	/// Interaction logic for CodeLensCustomDataView.xaml
	/// </summary>
	public partial class CodeLensCustomDataView : UserControl
	{
		public CodeLensCustomDataView()
		{
			InitializeComponent();
		}
		private async void DataReferenceDetails_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				Grid gridControl = sender as Grid;
				CodeLensDataReferenceDetailsViewModel referenceDetails = gridControl?.DataContext as CodeLensDataReferenceDetailsViewModel;
				if (referenceDetails != null)
				{
					await referenceDetails.OpenInVisualStudioAsync();
				}
			}
		}
	}
}
