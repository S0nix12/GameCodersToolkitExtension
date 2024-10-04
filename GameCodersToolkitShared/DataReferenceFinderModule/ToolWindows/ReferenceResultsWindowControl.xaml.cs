using GameCodersToolkit.DataReferenceFinderModule.ViewModels;
using Microsoft.VisualStudio.Package;
using System.Runtime.InteropServices.Expando;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GameCodersToolkit.ReferenceFinder.ToolWindows
{
	public partial class ReferenceResultsWindowControl : UserControl
	{
		public ReferenceResultsWindowControl(ReferenceResultsWindowMessenger messenger)
		{
			messenger.MessageReceived += OnToolbarMessageReceived;
			InitializeComponent();
		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			VS.MessageBox.Show("ToolWindow1Control", "Button clicked");
		}

		private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{

		}

		private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				Border borderControl = sender as Border;
				DataEntryResultViewModel lineResult = borderControl?.DataContext as DataEntryResultViewModel;
				if (lineResult != null && GameCodersToolkitPackage.Package != null)
				{
					GameCodersToolkitPackage.Package.JoinableTaskFactory.RunAsync(lineResult.OpenInVisualStudioAsync).FireAndForget();
				}
			}
		}
		private void Border_PreviewRightMouseDown(object sender, MouseButtonEventArgs e)
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

		private void OnToolbarMessageReceived(object sender, EReferenceResultsWindowToolbarAction action)
		{
			switch (action)
			{
				case EReferenceResultsWindowToolbarAction.ExpandAll:
					ResultsTree.SetExpansion(true);
					break;
				case EReferenceResultsWindowToolbarAction.CollapseAll:
					ResultsTree.SetExpansion(false);
					break;
			}
		}
	}
}
