using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.Configuration;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace GameCodersToolkitShared.Utils
{
	public partial class VariableViewModel : ObservableObject
	{
		public string Name { get; set; }
		public object Value { get; set; }
	}

	public partial class ConfigurationViewModel : ObservableObject
	{
		public ConfigurationViewModel(string windowTitle, object targetObject)
		{
			WindowTitle = windowTitle;
			TargetObject = targetObject;
			TargetType = TargetObject.GetType();

			ReadConfigValues();
		}

		private void ReadConfigValues()
		{
			PropertyInfo[] properties = TargetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
			foreach (PropertyInfo property in properties)
			{
				EditableAttribute attribute = property.GetCustomAttribute<EditableAttribute>();
				if (attribute != null)
				{
					if (attribute.AllowEdit)
					{
						object currentValue = property.GetValue(TargetObject);

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
				propertyInfo.SetValue(TargetObject, vm.Value);
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
				OnSaveRequested?.Invoke(this, EventArgs.Empty);
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
			OnReloadRequested?.Invoke(this, EventArgs.Empty);
		}

		private ObservableCollection<VariableViewModel> m_variables = new ObservableCollection<VariableViewModel>();
		public ObservableCollection<VariableViewModel> Variables { get => m_variables; set => SetProperty(ref m_variables, value); }

		public string WindowTitle { get; set; }

		public event EventHandler OnSaveRequested;
		public event EventHandler OnReloadRequested;

		private Type TargetType { get; set; }
		private object TargetObject { get; set; }
	}
}
