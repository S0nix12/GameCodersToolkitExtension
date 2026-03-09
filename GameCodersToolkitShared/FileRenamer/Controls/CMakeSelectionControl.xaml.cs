using GameCodersToolkit.FileRenamer.ViewModels;
using GameCodersToolkit.FileTemplateCreator.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace GameCodersToolkit.FileRenamer.Controls
{
	/// <summary>
	/// Interaction logic for CMakeSelectionControl.xaml
	/// Reusable control for CMake file/uber/group selection with click-to-select groups.
	/// </summary>
	public partial class CMakeSelectionControl : UserControl
	{
		public CMakeSelectionControl()
		{
			InitializeComponent();
		}

		private void MakeFileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (DataContext is CCMakeSelectionHelper helper)
			{
				if (e.NewValue is CMakeFileGroupViewModel groupVm)
				{
					helper.ToggleGroupSelection(groupVm);
				}
				else if (e.NewValue is CMakeFileUberFileViewModel uberFileVm)
				{
					helper.SelectUberFile(uberFileVm);
				}
			}
		}
	}
}
