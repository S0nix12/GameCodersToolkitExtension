using Microsoft.VisualStudio.TextManager.Interop;
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

namespace GameCodersToolkit.FileTemplateCreator.Windows
{
	/// <summary>
	/// Interaction logic for NameDialogWindow.xaml
	/// </summary>
	public partial class NameDialogWindow : Window
	{
		public NameDialogWindow()
		{
			InitializeComponent();
		
			Owner = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
			TextBox.Focus();
		}

		public static bool ShowFileNameDialog(string title, out string fileName)
		{
			return ShowNameDialog(title, out fileName, (result) => result.IsValidFileName());
		}

		public static bool ShowNameDialog(string title, out string result, Predicate<string> predicate = null)
		{
			NameDialogWindow nameDialogWindow = new()
			{
				Title = title
			};

			bool? dialogResult = nameDialogWindow.ShowDialog();
			if (dialogResult.HasValue && dialogResult.Value)
			{
				string enteredText = nameDialogWindow.GetEnteredText();
				if (predicate == null || predicate(enteredText))
				{
					result = enteredText;
					return true;
				}
			}

			result = string.Empty;
			return false;
		}

		public string GetEnteredText()
		{
			return TextBox.Text;
		}

		private void TextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				DialogResult = true;
				Close();
			}
		}

		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				DialogResult = false;
				Close();
			}
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void ConfirmButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}
	}
}
