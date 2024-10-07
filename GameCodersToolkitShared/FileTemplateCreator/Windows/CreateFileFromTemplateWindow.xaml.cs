using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
	/// Interaction logic for CreateFileFromTemplateWindow.xaml
	/// </summary>
	public partial class CreateFileFromTemplateWindow : Window
	{
		public CreateFileFromTemplateWindow()
		{
			InitializeComponent();
		}

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
			if (sender is ScrollViewer scrollViewer)
			{
				scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
				e.Handled = true;
			}
        }
    }
}
