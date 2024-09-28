using DataReferenceCodeLensProviderShared.Communication;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Threading;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataReferenceCodeLensProviderShared
{
	public class CodeLensDataPoint : IAsyncCodeLensDataPoint
	{
		public CodeLensDataPoint(CodeLensDescriptor descriptor, ICodeLensCallbackService callbackService)
		{
			Descriptor = descriptor;
			m_callbackService = callbackService;
		}

		public async Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			CodeLensDataPointDescriptor descriptor = new CodeLensDataPointDescriptor();
			descriptor.TooltipText = "GameCodersToolkit: How often this Data Reference is found in the parsed Database";
			int referenceCount = 0;

			string dataIdentifier = "";
			if (descriptorContext.Properties.TryGetValue("DataReferenceIdentifier", out object identifierObject))
			{
				if (identifierObject is string stringIdentifier)
				{
					dataIdentifier = stringIdentifier;
				}
			}

			System.Diagnostics.Debug.WriteLine("Code Lens Data Point tries to call RPC function: " + nameof(ICodeLensDataService.GetReferenceCount));

			if (!string.IsNullOrEmpty(dataIdentifier))
			{
				referenceCount = await m_callbackService.InvokeAsync<int>(
					this, 
					nameof(ICodeLensDataService.GetReferenceCount), 
					new[] { dataIdentifier }, 
					token);
			}

			descriptor.IntValue = referenceCount;
			descriptor.Description = $"{referenceCount} Data References";
			return descriptor;
		}

		public Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			CodeLensDetailsDescriptor descriptor = new CodeLensDetailsDescriptor();
			CodeLensDetailEntryDescriptor entryDescriptor = new CodeLensDetailEntryDescriptor();
			
			return Task.FromResult<CodeLensDetailsDescriptor>(null);
		}

		public CodeLensDescriptor Descriptor { get; }
		public event AsyncEventHandler InvalidatedAsync;

		private ICodeLensCallbackService m_callbackService;
	}

}
