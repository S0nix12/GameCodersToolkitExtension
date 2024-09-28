using DataReferenceCodeLensProviderShared.Communication;
using GameCodersToolkit;
using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace GameCodersToolkitShared.DataReferenceFinderModule.CodeLens
{
	// Code Lens Data Point Provider and Code Lens Tagger allow to specific content types like "code" or "text" to be more generic
	// The ICodeLensCallbackListener seems to not be created when using a generic content type though so specific ones need to be used
	[Export(typeof(ICodeLensCallbackListener))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	[ContentType("CSharp")]
	[ContentType("C/C++")]
	[ContentType("JavaScript")]
	[ContentType("TypeScript")]
	[ContentType("XML")]
	public class CodeLensDataService : ICodeLensCallbackListener, ICodeLensDataService
	{
		private GenericDataIdentifier CreateDataIdentifier(string identifierString)
		{
			GenericDataIdentifier dataIdentifier;
			if (Guid.TryParse(identifierString, out Guid referenceGuid))
			{
				dataIdentifier = new GenericDataIdentifier(referenceGuid);
			}
			else
			{
				dataIdentifier = new GenericDataIdentifier(identifierString);
			}
			return dataIdentifier;
		}

		public int GetReferenceCount(string identifier)
		{
			if (GameCodersToolkitPackage.ReferenceDatabase != null)
			{
				GenericDataIdentifier dataIdentifier = CreateDataIdentifier(identifier);
				if (GameCodersToolkitPackage.ReferenceDatabase.ReferencedByEntries.TryGetValue(dataIdentifier, out var entries))
				{
					return entries.Count;
				}
			}

			return 0;
		}

		public List<CodeLensDataReferenceDetails> GetReferenceDetails(string identifier)
		{
			List<CodeLensDataReferenceDetails> outDetails = new List<CodeLensDataReferenceDetails>();

			if (GameCodersToolkitPackage.ReferenceDatabase != null)
			{
				GenericDataIdentifier dataIdentifier = CreateDataIdentifier(identifier);
				if (GameCodersToolkitPackage.ReferenceDatabase.ReferencedByEntries.TryGetValue(dataIdentifier, out var entries))
				{
					foreach (DataEntry entry in entries)
					{
						outDetails.Add(new CodeLensDataReferenceDetails
						{
							Name = entry.Name,
							SubType = entry.SubType,
							SourceFile = entry.SourceFile,
							SourceLineNumber = entry.SourceLineNumber,
							ParentPath = ReferenceDatabaseUtils.CreateDataEntryPathString(entry)
						});
					}
				}
			}

			return outDetails;
		}
	}
}
