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
		public abstract Task<IMakeFile> AddUberFileAsync(IUberFileNode prevUberFileNode, string newUberFileName);
		public abstract Task<IMakeFile> AddGroupAsync(IUberFileNode uberFileNode, IGroupNode prevGroupNode, string newGroupName);
		public abstract Task<IMakeFile> AddFilesAsync(IUberFileNode uberFileNode, IGroupNode groupNode, IFileNode prevFileNode, IEnumerable<string> newFileNames);
		public abstract Task SaveAsync();

		public abstract IEnumerable<IUberFileNode> GetUberFiles();
		public abstract string GetOriginalFilePath();
	}

	public interface IMakeFileParser
	{
		IMakeFile Parse(string filePath);
	}
}
