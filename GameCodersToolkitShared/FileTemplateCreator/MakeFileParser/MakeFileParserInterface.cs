using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.FileTemplateCreator.MakeFileParser
{


	public interface IUberFileNode
	{
		string GetName();
		IEnumerable<IGroupNode> GetGroups();
	}

	public interface IGroupNode
	{
		string GetName();
		int GetLineNumber();
		IEnumerable<IFileNode> GetFiles();
	}

	public interface IFileNode
	{
		string GetName();
		int GetLineNumber();
	}

	public interface IMakeFile
	{
		public abstract IMakeFile AddUberFile(IUberFileNode prevUberFileNode, string newUberFileName);
		public abstract IMakeFile AddGroup(IUberFileNode uberFileNode, IGroupNode prevGroupNode, string newGroupName);
		public abstract IMakeFile AddFiles(IUberFileNode uberFileNode, IGroupNode groupNode, IFileNode prevFileNode, IEnumerable<string> newFileNames);
		public abstract void Save();

		public abstract IEnumerable<IUberFileNode> GetUberFiles();
		public abstract string GetOriginalFilePath();
	}

	public interface IMakeFileParser
	{
		IMakeFile Parse(string filePath);
	}
}
