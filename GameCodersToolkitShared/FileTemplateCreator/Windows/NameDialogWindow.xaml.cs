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

			Closing += (s, e) =>
			{
				if (!TestEnteredText.HasValue)
					TestEnteredText = false;

				if (TestEnteredText.Value && Predicate != null && !Predicate(TextBox.Text))
				{
					ErrorAction?.Invoke(TextBox.Text);
					e.Cancel = true;
				}
				else
				{
					DialogResult = TestEnteredText;
				}

				TestEnteredText = null;
            };
		}

		public static bool ShowNameDialog(string title, out string result, Predicate<string> predicate, Action<string> errorAction, string content = "")
		{
			NameDialogWindow nameDialogWindow = new()
			{
				Title = title,
				ErrorAction = errorAction,
				Predicate = predicate
			};

			nameDialogWindow.TextBox.Text = content != null ? content : "";
			nameDialogWindow.TextBox.CaretIndex = int.MaxValue;

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
                TestEnteredText = true;
                Close();
			}
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            TestEnteredText = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
            {
                TestEnteredText = false;
                Close();
			}
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            TestEnteredText = false;
            Close();
		}

		bool? TestEnteredText {  get; set; }
		Action<string> ErrorAction { get; set; } = null;
		Predicate<string> Predicate { get; set; } = null;
	}
}
