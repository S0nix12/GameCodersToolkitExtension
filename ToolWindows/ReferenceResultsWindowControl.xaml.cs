using DataReferenceFinder.ToolWindows;
using DataReferenceFinder.ViewModels;
using Microsoft.VisualStudio.Package;
using System.Runtime.InteropServices.Expando;
using System.Windows;
using System.Windows.Controls;

namespace DataReferenceFinder
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

		private async void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				Border borderControl = sender as Border;
				CLineResultViewModel lineResult = borderControl?.DataContext as CLineResultViewModel;
				await lineResult?.ShowEntryAsync();
			}
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
