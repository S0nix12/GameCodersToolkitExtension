using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;

namespace GameCodersToolkit.DataReferenceFinderModule.DataEditorCommunication
{
	public class OpenDataEntryMessage
	{
		public OpenDataEntryMessage() { }
		public OpenDataEntryMessage(DataEntry dataEntry)
		{
			Name = dataEntry.Name;
			TypeName = dataEntry.BaseType;
			SubTypeName = dataEntry.SubType;
			SourceFile = dataEntry.SourceFile;
			SourceLineNumber = dataEntry.SourceLineNumber;
			IdentifierString = dataEntry.Identifier.ToString();
			
			if (dataEntry.Parent != null)
			{
				ParentIdentifierString = dataEntry.Parent.Identifier.ToString();
			}
		}

		public string Name { get; set; }
		public string TypeName { get; set; }
		public string SubTypeName { get; set; }
		public string SourceFile { get; set; }
		public int SourceLineNumber { get; set; }
		public string IdentifierString { get; set; }
		public string ParentIdentifierString { get; set; }
	}
}
