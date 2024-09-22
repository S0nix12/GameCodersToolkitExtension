using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Threading;
using System.Threading;
using System.Threading.Tasks;

namespace DataReferenceCodeLensProviderShared
{
	public class CodeLensDataPoint : IAsyncCodeLensDataPoint
	{
		public CodeLensDataPoint(CodeLensDescriptor descriptor)
		{
			Descriptor = descriptor;
		}

		public Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			return Task.FromResult(new CodeLensDataPointDescriptor
			{
				Description = "Test Code Lens Description",
				TooltipText = "Test Tooltip Text"
			});
		}

		public Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			return Task.FromResult<CodeLensDetailsDescriptor>(null);
		}

		public CodeLensDescriptor Descriptor { get; }
		public event AsyncEventHandler InvalidatedAsync;
	}

}
