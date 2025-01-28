using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.FileTemplateCreator.ViewModels
{
	public partial class VariableViewModel : ObservableObject
	{
		public string Name { get; set; }
		public string Value { get; set; }
	}

	public partial class ConfigurationViewModel : ObservableObject
	{
		public ConfigurationViewModel()
		{
			ReadConfigValues();
		}

		private void ReadConfigValues()
		{
			PropertyInfo[] properties = typeof(CFileTemplateCreatorUserConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance);
			foreach (PropertyInfo property in properties)
			{
				EditableAttribute attribute = property.GetCustomAttribute<EditableAttribute>();
				if (attribute != null)
				{
					if (attribute.AllowEdit)
					{
						string currentValue = property.GetValue(GameCodersToolkitPackage.FileTemplateCreatorConfig.UserConfig) as string;
						
						VariableViewModel variable = new VariableViewModel();
						variable.Name = property.Name;
						variable.Value = currentValue ?? "";

						Variables.Add(variable);
					}
				}
			}
		}

		private void ApplyConfigValues()
		{
			Type configType = typeof(CFileTemplateCreatorUserConfig);

			foreach (VariableViewModel vm in Variables)
			{
				PropertyInfo propertyInfo = configType.GetProperty(vm.Name);
				propertyInfo.SetValue(GameCodersToolkitPackage.FileTemplateCreatorConfig.UserConfig, vm.Value);
			}
		}

		private bool IsConfigValid()
		{
			return true;
		}

		[RelayCommand]
		public async Task SaveAsync()
		{
			if (IsConfigValid())
			{
				ApplyConfigValues();
				await GameCodersToolkitPackage.FileTemplateCreatorConfig.SaveConfigAsync();
			}
			else
			{
				MessageBox errorMessageBox = new MessageBox();
				await errorMessageBox.ShowErrorAsync("Unable to save due to invalid configuration");
			}
		}

		private ObservableCollection<VariableViewModel> m_variables = new ObservableCollection<VariableViewModel>();
		public ObservableCollection<VariableViewModel> Variables { get => m_variables; set => SetProperty(ref m_variables, value); }
	}
}
