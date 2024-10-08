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
			await ReportErrorMessageFromExtensionAsync(errorMessage, exception.ToString(), outputPane);
		}

		public static async Task ReportErrorMessageFromExtensionAsync(string errorMessage, string details)
		{
			await ReportErrorMessageFromExtensionAsync(errorMessage, details, GameCodersToolkitPackage.ExtensionOutput);
		}

		public static async Task ReportErrorMessageFromExtensionAsync(string errorMessage, string details, OutputWindowPane outputPane)
		{
			await outputPane.WriteLineAsync(errorMessage);
			await outputPane.WriteLineAsync(details);
			System.Diagnostics.Debug.WriteLine(errorMessage);
			System.Diagnostics.Debug.WriteLine(details);
		}
	}
}
