using GameCodersToolkit.FileRenamer.ViewModels;
using GameCodersToolkit.FileTemplateCreator.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace GameCodersToolkit.FileRenamer.Windows
{
	/// <summary>
	/// Interaction logic for MoveFilesWindow.xaml
	/// </summary>
	public partial class MoveFilesWindow : Window
	{
		public MoveFilesWindow()
		{
			InitializeComponent();
		}

		private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
		{
			if (sender is ScrollViewer scrollViewer)
			{
				scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
				e.Handled = true;
			}
		}

		private void MakeFileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (DataContext is CMoveFilesDialogViewModel vm)
			{
				if (e.NewValue is CMakeFileUberFileViewModel uberFileVm)
				{
					vm.SelectedUberFile = uberFileVm;
					vm.SelectedGroup = null;
				}
				else if (e.NewValue is CMakeFileGroupViewModel groupVm)
				{
					// Find the parent uber file
					foreach (var item in vm.MakeFileContent)
					{
						if (item is CMakeFileUberFileViewModel parentUber)
						{
							foreach (var child in parentUber.Children)
							{
								if (child == groupVm)
								{
									vm.SelectedUberFile = parentUber;
									vm.SelectedGroup = groupVm;
									return;
								}
							}
						}
					}
				}
			}
		}
	}
}
