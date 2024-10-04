using GameCodersToolkit;
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
		private void DataReferenceDetails_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				FrameworkElement frameworkElement = sender as FrameworkElement;
				CodeLensDataReferenceDetailsViewModel referenceDetails = frameworkElement?.DataContext as CodeLensDataReferenceDetailsViewModel;
				if (referenceDetails != null && GameCodersToolkitPackage.Package != null)
				{
					GameCodersToolkitPackage.Package.JoinableTaskFactory.RunAsync(referenceDetails.OpenInVisualStudioAsync).FireAndForget();
				}
			}
		}

		private void DataReferenceDetails_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

			if (treeViewItem != null)
			{
				treeViewItem.Focus();
				treeViewItem.IsSelected = true;
				e.Handled = true;
			}
		}

		static TreeViewItem VisualUpwardSearch(DependencyObject source)
		{
			while (source != null && !(source is TreeViewItem))
				source = VisualTreeHelper.GetParent(source);

			return source as TreeViewItem;
		}
	}
}
