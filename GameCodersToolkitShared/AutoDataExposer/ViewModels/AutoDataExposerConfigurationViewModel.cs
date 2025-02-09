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

namespace GameCodersToolkit.AutoDataExposerModule.ViewModels
{
	public partial class VariableViewModel : ObservableObject
	{
		public string Name { get; set; }
		public object Value { get; set; }
		public bool UseCheckBox { get; set; }
	}

	public partial class AutoDataExposerConfigurationViewModel : ObservableObject
	{
		public CAutoDataExposerUserConfig UserConfig { get { return GameCodersToolkitPackage.AutoDataExposerConfig.GetConfig<CAutoDataExposerUserConfig>(); } }

		public AutoDataExposerConfigurationViewModel()
		{
			ReadConfigValues();
		}

		private void ReadConfigValues()
		{
			PropertyInfo[] properties = typeof(CAutoDataExposerUserConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance);
			foreach (PropertyInfo property in properties)
			{
				EditableAttribute attribute = property.GetCustomAttribute<EditableAttribute>();
				if (attribute != null)
				{
					if (attribute.AllowEdit)
					{
						object currentValue = property.GetValue(UserConfig);
						
						VariableViewModel variable = new VariableViewModel();
						variable.Name = property.Name;

						if (property.PropertyType == typeof(string))
						{
							variable.Value = currentValue ?? "";
						}
						else
						{
							variable.Value = currentValue;
						}

						Variables.Add(variable);
					}
				}
			}
		}

		private void ApplyConfigValues()
		{
			Type configType = typeof(CAutoDataExposerUserConfig);

			foreach (VariableViewModel vm in Variables)
			{
				PropertyInfo propertyInfo = configType.GetProperty(vm.Name);
				propertyInfo.SetValue(UserConfig, vm.Value);
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
				GameCodersToolkitPackage.AutoDataExposerConfig.SaveConfig<CAutoDataExposerUserConfig>();
			}
			else
			{
				MessageBox errorMessageBox = new MessageBox();
				await errorMessageBox.ShowErrorAsync("Unable to save due to invalid configuration");
			}
        }

        [RelayCommand]
        public void Reload()
        {
			GameCodersToolkitPackage.FileTemplateCreatorConfig.Reload();
        }

        private ObservableCollection<VariableViewModel> m_variables = new ObservableCollection<VariableViewModel>();
		public ObservableCollection<VariableViewModel> Variables { get => m_variables; set => SetProperty(ref m_variables, value); }
	}
}
