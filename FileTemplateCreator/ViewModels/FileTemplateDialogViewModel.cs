using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.Configuration;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using GameCodersToolkit.ReferenceFinder;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;

namespace GameCodersToolkit.FileTemplateCreator.ViewModels
{
	public class CFileTemplateCategoryViewModel : ObservableObject
	{
		public string Name { get; set; }
		public ObservableCollection<object> Children { get; set; } = new ObservableCollection<object>();
	}

	public partial class CFileTemplateViewModel : ObservableObject
	{
		public string Name { get; set; }
		public string FullName { get; set; }
		public ObservableCollection<string> Contents { get; set; } = new ObservableCollection<string>();
		public string MakeFileID { get; set; }

		public event Action<CFileTemplateViewModel> OnSelect;

		[RelayCommand]
		private void Select()
		{
			OnSelect?.Invoke(this);
		}
	}

	public class CMakeFileUberFileViewModel
	{
		public string Name { get; set; }
		public ObservableCollection<CMakeFileGroupViewModel> Groups { get; set; } = new ObservableCollection<CMakeFileGroupViewModel>();
	}

	public class CMakeFileGroupViewModel
	{
		public string Name { get; set; }
		public ObservableCollection<CMakeFileFileViewModel> Files { get; set; } = new ObservableCollection<CMakeFileFileViewModel>();
	}

	public class CMakeFileFileViewModel
	{
		public string Name { get; set; }
	}

	public partial class CFileTemplateDialogViewModel : ObservableObject
	{
		public CFileTemplateDialogViewModel()
		{
			foreach (CTemplateEntry template in GameCodersToolkitPackage.FileTemplateCreatorConfig.CreatorConfig.FileTemplateEntries)
			{
				if (template.Paths.Count == 0)
				{ continue; }

				List<string> categories = template.Name.Split('/').ToList();
				CFileTemplateCategoryViewModel rootModel = null;
				CFileTemplateCategoryViewModel currentModel = null;

				while (categories.Count != 1)
				{
					CFileTemplateCategoryViewModel newVm = new CFileTemplateCategoryViewModel();
					newVm.Name = categories.First();

					if (rootModel == null)
					{
						rootModel = currentModel;
					}

					if (currentModel != null)
					{
						currentModel.Children.Add(newVm);
					}

					currentModel = newVm;
					categories.RemoveAt(0);
				}

				CFileTemplateViewModel vm = new CFileTemplateViewModel();
				vm.FullName = template.Name;
				vm.Name = categories.First();
				vm.MakeFileID = template.MakeFileID;
				vm.OnSelect += OnTemplateSelected;

				foreach (string path in template.Paths)
				{
					vm.Contents.Add(File.ReadAllText(path));
				}

				if (currentModel != null)
				{
					currentModel.Children.Add(vm);
					Templates.Add(rootModel);
				}
				else
				{
					Templates.Add(vm);
				}
			}
		}

		private void OnTemplateSelected(CFileTemplateViewModel vm)
		{
			CurrentMakeFile = null;
			MakeFileContent.Clear();

			string makeFilePath = GameCodersToolkitPackage.FileTemplateCreatorConfig.GetMakeFilePathByID(vm.MakeFileID);
			if (File.Exists(makeFilePath))
			{
				IMakeFileParser parser = GameCodersToolkitPackage.FileTemplateCreatorConfig.CreateParser();
				if (parser != null)
				{
					CurrentMakeFile = parser.Parse(makeFilePath);

					foreach (IUberFileEntry uberFile in CurrentMakeFile.GetUberFileEntries())
					{
						CMakeFileUberFileViewModel uberFileVm = new CMakeFileUberFileViewModel();
						uberFileVm.Name = uberFile.GetName();

						foreach (ISourceGroupEntry sourceGroup in uberFile.GetSourceGroups())
						{
							CMakeFileGroupViewModel sourceGroupVm = new CMakeFileGroupViewModel();
							sourceGroupVm.Name = sourceGroup.GetName();

							foreach (IRegularFileEntry regularFileEntry in sourceGroup.GetFiles())
							{
								CMakeFileFileViewModel regularFileVm = new CMakeFileFileViewModel();
								regularFileVm.Name = regularFileEntry.GetName();

								sourceGroupVm.Files.Add(regularFileVm);
							}

							uberFileVm.Groups.Add(sourceGroupVm);
						}

						MakeFileContent.Add(uberFileVm);
					}

					//IEnumerable<IUberFileEntry> uberFileEntries = makeFile.GetUberFileEntries().ToList();
					//if (uberFileEntries.Count() > 0 && uberFileEntries.First().GetSourceGroups().Count() > 0)
					//{
					//	makeFile.AddFilesAndSave(
					//		uberFileEntries.ElementAt(1).GetName(),
					//		DateTime.Now.ToString(),
					//		"",
					//		DateTime.Now.ToString(),
					//		uberFileEntries.First().GetSourceGroups().First().GetFiles().Last().GetName(),
					//		new string[] {
					//					DateTime.Now.ToString() + ".cpp",
					//					DateTime.Now.ToString() + ".h",
					//		}
					//		);
					//}
				}
			}
		}

		private IMakeFile CurrentMakeFile { get; set; }

		private ObservableCollection<object> m_templates = new ObservableCollection<object>();
		public ObservableCollection<object> Templates { get => m_templates; set => SetProperty(ref m_templates, value); }


		private ObservableCollection<object> m_makeFileContent = new ObservableCollection<object>();
		public ObservableCollection<object> MakeFileContent { get => m_makeFileContent; set => SetProperty(ref m_makeFileContent, value); }
	}
}
