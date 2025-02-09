using System.Windows.Controls;
using System.Windows;

namespace GameCodersToolkitShared.Utils
{
	public class VariableEditorTemplateSelector : DataTemplateSelector
	{
		public DataTemplate StringTemplate { get; set; }
		public DataTemplate BoolTemplate { get; set; }

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			VariableViewModel vm = (VariableViewModel)item;
			if (vm.Value is string)
			{
				return StringTemplate;
			}
			else if (vm.Value is bool)
			{
				return BoolTemplate;
			}

			return base.SelectTemplate(item, container); // Default template if needed
		}
	}
}
