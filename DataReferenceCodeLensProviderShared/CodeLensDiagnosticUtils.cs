using DataReferenceCodeLensProviderShared.Communication;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataReferenceCodeLensProviderShared
{
	public static class CodeLensDiagnosticUtils
	{
		public static async Task ReportExceptionToVSAsync(ICodeLensCallbackService callbackServie, IAsyncCodeLensDataPoint dataPoint, Exception ex, CancellationToken token)
		{
			try
			{
				await callbackServie.InvokeAsync(
					dataPoint,
					nameof(ICodeLensDataService.ReportErrorMessage),
					new[] { ex.ToString() },
					token);
			}
			catch (Exception sendException)
			{
				Console.WriteLine("Error trying to send Exception to VS process");
				Console.WriteLine(sendException.ToString());
			}
		}
	}
}
