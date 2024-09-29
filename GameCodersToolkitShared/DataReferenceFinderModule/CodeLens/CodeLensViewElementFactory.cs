using DataReferenceCodeLensProviderShared.Communication;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Text;
using System.Windows;

namespace GameCodersToolkitShared.DataReferenceFinderModule.CodeLens
{
	[Export(typeof(IViewElementFactory))]
	[Name("View for Data Reference Code Lens entries")]
	[TypeConversion(typeof(CodeLensDataReferenceCustomData), typeof(FrameworkElement))]
	[Order]
	public class CodeLensViewElementFactory : IViewElementFactory
	{
		public TView CreateViewElement<TView>(ITextView textView, object model) where TView : class
		{
			if (model is CodeLensDataReferenceCustomData customData)
			{
				var customDataView = new CodeLensCustomDataView
				{
					DataContext = new CodeLensDataReferenceCustomViewModel(customData)
				};

				return customDataView as TView;
			}

			return null;
		}
	}
}
