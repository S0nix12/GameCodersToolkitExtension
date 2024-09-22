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
	[ContentType("C/C++")]
	[LocalizedName(typeof(Resources), Id)]
	[Priority(210)]
	public class CodeLensDataPointProvider : IAsyncCodeLensDataPointProvider
	{
		public Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext context, CancellationToken token)
		{
			var methodsOnly = descriptor.Kind == CodeElementKinds.Method || descriptor.Kind == CodeElementKinds.Function;
			return Task.FromResult(methodsOnly);
		}

		public Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			return Task.FromResult<IAsyncCodeLensDataPoint>(new CodeLensDataPoint(descriptor));
		}


		internal const string Id = "DataReferenceCodeLensProvider";
	}

}
