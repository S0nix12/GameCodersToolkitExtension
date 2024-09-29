using System;
using System.Collections.Generic;
using System.Text;

namespace DataReferenceCodeLensProviderShared.Communication
{
	public class CodeLensDataReferenceDetails
	{
		public string Name { get; set; } = "Unkown";
		public string TypeName { get; set; } = "NoType";
		public string SubType { get; set; } = "NoSubType";
		public string SourceFile { get; set; }
		public int SourceLineNumber { get; set; }
		public string ParentPath { get; set; }
		public string IdentifierString { get; set; }
		public string ParentIdentifierString { get; set; }
	}
}
