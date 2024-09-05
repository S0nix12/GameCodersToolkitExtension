using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.FileTemplateCreator.MakeFileParser
{


	public interface IUberFileEntry
	{
		string GetName();
		IEnumerable<ISourceGroupEntry> GetSourceGroups();
	}

	public interface ISourceGroupEntry
	{
		string GetName();
		int GetLineNumber();
		IEnumerable<IRegularFileEntry> GetFiles();
	}

	public interface IRegularFileEntry
	{
		string GetName();
		int GetLineNumber();
	}

	public interface IMakeFile
	{
		public abstract IMakeFile AddUberFile(string previousUberFileName, string newUberFileName);
		public abstract IMakeFile AddGroup(string uberFileName, string previousGroupName, string newGroupName);
		public abstract IMakeFile AddFiles(string uberFileName, string groupName, string previousFileName, IEnumerable<string> newFileNames);

		public abstract IEnumerable<IUberFileEntry> GetUberFileEntries();
		public bool IsValid();
	}

	public interface IMakeFileParser
	{
		IMakeFile Parse(string filePath);
	}
}
