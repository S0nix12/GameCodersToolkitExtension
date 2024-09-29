﻿using DataReferenceCodeLensProviderShared.Communication;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Threading;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

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

		public async Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			CodeLensDetailsDescriptor descriptor = new CodeLensDetailsDescriptor();

			string dataIdentifier = "";
			if (descriptorContext.Properties.TryGetValue("DataReferenceIdentifier", out object identifierObject))
			{
				if (identifierObject is string stringIdentifier)
				{
					dataIdentifier = stringIdentifier;
				}
			}

			List<CodeLensDataReferenceDetails> referenceDetailsList = new List<CodeLensDataReferenceDetails>();
			if (!string.IsNullOrEmpty(dataIdentifier))
			{
				referenceDetailsList = await m_callbackService.InvokeAsync<List<CodeLensDataReferenceDetails>>(
					this,
					nameof(ICodeLensDataService.GetReferenceDetails),
					new[] { dataIdentifier },
					token);
			}

			descriptor = GetCustomDataDescriptor(referenceDetailsList);

			return descriptor;
		}

		private CodeLensDetailsDescriptor GetBasicDescriptor(List<CodeLensDataReferenceDetails> referenceDetailsList)
		{
			CodeLensDetailsDescriptor descriptor = new CodeLensDetailsDescriptor();
			if (referenceDetailsList.Count > 0)
			{
				referenceDetailsList = referenceDetailsList.OrderBy(entry => entry.SourceFile).ThenBy(entry => entry.SourceLineNumber).ToList();
				descriptor.Headers = new List<CodeLensDetailHeaderDescriptor>
				{
					new CodeLensDetailHeaderDescriptor
					{
						UniqueName = "FilePath",
						DisplayName = "File",
						Width = 0.2
					},
					new CodeLensDetailHeaderDescriptor
					{
						UniqueName = "DataPath",
						DisplayName = "Data Path",
						Width = 1.0
					}
				};

				List<CodeLensDetailEntryDescriptor> detailEntries = new List<CodeLensDetailEntryDescriptor>();
				foreach (var referenceDetails in referenceDetailsList)
				{
					CodeLensDetailEntryDescriptor entryDescriptor = new CodeLensDetailEntryDescriptor();
					entryDescriptor.Tooltip = referenceDetails.Name;
					entryDescriptor.Fields = new List<CodeLensDetailEntryField>
					{
						new CodeLensDetailEntryField
						{
							Text = Path.GetFileName(referenceDetails.SourceFile)
						},
						new CodeLensDetailEntryField
						{
							Text = $"{referenceDetails.SubType}: {referenceDetails.ParentPath}"
						}
					};
					detailEntries.Add(entryDescriptor);
				}
				descriptor.Entries = detailEntries;
				descriptor.SelectionMode = CodeLensDetailEntriesSelectionMode.Single;
			}
			return descriptor;
		}

		private CodeLensDetailsDescriptor GetCustomDataDescriptor(List<CodeLensDataReferenceDetails> referenceDetailsList)
		{
			CodeLensDetailsDescriptor descriptor = new CodeLensDetailsDescriptor();

			descriptor.CustomData = new List<CodeLensDataReferenceCustomData>
			{
				new CodeLensDataReferenceCustomData
				{
					Details = referenceDetailsList
				}
			};
			descriptor.Headers = new List<CodeLensDetailHeaderDescriptor>();
			descriptor.Entries = new List<CodeLensDetailEntryDescriptor>();

			return descriptor;
		}

		public CodeLensDescriptor Descriptor { get; }
		public event AsyncEventHandler InvalidatedAsync;

		private ICodeLensCallbackService m_callbackService;
	}

}
