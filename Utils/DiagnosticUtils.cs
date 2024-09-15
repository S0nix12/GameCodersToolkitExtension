using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.Utils
{
	public static class DiagnosticUtils
	{
		public static async Task ReportExceptionFromExtensionAsync(string errorMessage, Exception exception)
		{
			await ReportExceptionFromExtensionAsync(errorMessage, exception, GameCodersToolkitPackage.ExtensionOutput);
		}

		public static async Task ReportExceptionFromExtensionAsync(string errorMessage, Exception exception, OutputWindowPane outputPane)
		{
			await outputPane.WriteLineAsync(errorMessage);
			await outputPane.WriteLineAsync(exception.Message);
			await outputPane.WriteLineAsync(exception.StackTrace);
			System.Diagnostics.Debug.WriteLine(exception.Message);
			System.Diagnostics.Debug.WriteLine(exception.StackTrace);
		}
	}
}
