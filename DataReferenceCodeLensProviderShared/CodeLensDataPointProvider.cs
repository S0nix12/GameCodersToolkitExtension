using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using DataReferenceCodeLensProvider.Properties;

namespace DataReferenceCodeLensProviderShared
{
	[Export(typeof(IAsyncCodeLensDataPointProvider))]
	[Name(Id)]
	[ContentType("code")]
	[LocalizedName(typeof(Resources), Id)]
	[Priority(200)]
	public class CodeLensDataPointProvider : IAsyncCodeLensDataPointProvider
	{
		[ImportingConstructor]
		public CodeLensDataPointProvider(Lazy<ICodeLensCallbackService> callbackService)
		{
			m_callbackService = callbackService;
		}

		public Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext context, CancellationToken token)
		{
			System.Diagnostics.Debug.WriteLine($"Possible Code Lens Point. Kind {descriptor.Kind}, Desc: {descriptor.ElementDescription}");
			return Task.FromResult((int)descriptor.Kind == 1 << 24);
		}

		public Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			return Task.FromResult<IAsyncCodeLensDataPoint>(new CodeLensDataPoint(descriptor, m_callbackService.Value));
		}

		private readonly Lazy<ICodeLensCallbackService> m_callbackService;
		internal const string Id = "DataReferenceCodeLensProvider";
	}
}
