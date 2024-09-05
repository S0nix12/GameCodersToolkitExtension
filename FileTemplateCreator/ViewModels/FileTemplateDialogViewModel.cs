using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.Configuration;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using GameCodersToolkit.ReferenceFinder;
using Microsoft.VisualStudio.OLE.Interop;
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

	public partial class CMakeFileUberFileViewModel : ObservableObject
	{
		public string Name { get; set; }
		public ObservableCollection<CMakeFileGroupViewModel> Groups { get; set; } = new ObservableCollection<CMakeFileGroupViewModel>();

		public event Action<CMakeFileUberFileViewModel> OnSelect;

		private bool m_isExpanded;
		public bool IsExpanded { get => m_isExpanded; set => SetProperty(ref m_isExpanded, value); }

		[RelayCommand]
		private void Select()
		{
			OnSelect?.Invoke(this);
		}
	}

	public partial class CMakeFileGroupViewModel : ObservableObject
	{
		public string Name { get; set; }
		public ObservableCollection<CMakeFileFileViewModel> Files { get; set; } = new ObservableCollection<CMakeFileFileViewModel>();

		public event Action<CMakeFileGroupViewModel> OnSelect;

		private bool m_isExpanded;
		public bool IsExpanded { get => m_isExpanded; set => SetProperty(ref m_isExpanded, value); }

		[RelayCommand]
		private void Select()
		{
			OnSelect?.Invoke(this);
		}
	}

	public partial class CMakeFileFileViewModel : ObservableObject
	{
		public string Name { get; set; }
		public event Action<CMakeFileFileViewModel> OnSelect;

		[RelayCommand]
		private void Select()
		{
			OnSelect?.Invoke(this);
		}
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
			UberFiles.Clear();

			string makeFilePath = GameCodersToolkitPackage.FileTemplateCreatorConfig.GetMakeFilePathByID(vm.MakeFileID);
			if (File.Exists(makeFilePath))
			{
				IMakeFileParser parser = GameCodersToolkitPackage.FileTemplateCreatorConfig.CreateParser();
				if (parser != null)
				{
					CurrentMakeFile = parser.Parse(makeFilePath);
				}
			}
		}

		private void CreateMakeFileContent()
		{
			UberFiles.Clear();

			if (CurrentMakeFile == null)
				return;

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

					CMakeFileFileViewModel lastFileViewModel = sourceGroupVm.Files.LastOrDefault();
					CMakeFileFileViewModel newFileViewModel = new CMakeFileFileViewModel();
					newFileViewModel.Name = "Add Template Here";
					newFileViewModel.OnSelect += (vm) => AddFileToGroup(uberFileVm, sourceGroupVm, lastFileViewModel, newFileViewModel);
					sourceGroupVm.Files.Add(newFileViewModel);

					uberFileVm.Groups.Add(sourceGroupVm);
				}

				CMakeFileGroupViewModel lastGroupViewModel = uberFileVm.Groups.LastOrDefault();
				CMakeFileGroupViewModel newGroupViewModel = new CMakeFileGroupViewModel();
				newGroupViewModel.Name = "Add Group";
				newGroupViewModel.OnSelect += (vm) => AddGroupToUberFile(uberFileVm, lastGroupViewModel, newGroupViewModel);
				uberFileVm.Groups.Add(newGroupViewModel);

				UberFiles.Add(uberFileVm);
			}

			CMakeFileUberFileViewModel lastUberFileViewModel = UberFiles.LastOrDefault() as CMakeFileUberFileViewModel;
			CMakeFileUberFileViewModel newUberFileViewModel = new CMakeFileUberFileViewModel();
			newUberFileViewModel.Name = "Add Uber File";
			newUberFileViewModel.OnSelect += (vm) => AddUberFile(lastUberFileViewModel, newUberFileViewModel);
			UberFiles.Add(newUberFileViewModel);
		}

		private void AddUberFile(CMakeFileUberFileViewModel previousVm, CMakeFileUberFileViewModel vm)
		{
			CurrentMakeFile = CurrentMakeFile.AddUberFile(previousVm != null ? previousVm.Name : string.Empty, DateTime.Now.ToString());
		}

		private void AddGroupToUberFile(CMakeFileUberFileViewModel uberFileVm, CMakeFileGroupViewModel previousVm, CMakeFileGroupViewModel vm)
		{
			CurrentMakeFile = CurrentMakeFile.AddGroup(uberFileVm.Name, previousVm != null ? previousVm.Name : string.Empty, DateTime.Now.ToString());
		}

		private void AddFileToGroup(CMakeFileUberFileViewModel uberFileVm, CMakeFileGroupViewModel groupVm, CMakeFileFileViewModel previousVm, CMakeFileFileViewModel vm)
		{
			if (uberFileVm != null && groupVm != null)
			{
				CurrentMakeFile = CurrentMakeFile.AddFiles(uberFileVm.Name, groupVm.Name, previousVm != null ? previousVm.Name : string.Empty, new string[] { DateTime.Now.ToString() });
			}
		}

		private Dictionary<string, List<string>> GetExpandedElementsList()
		{
			Dictionary<string, List<string>> currentlyOpenElements = new Dictionary<string, List<string>>();

			foreach (CMakeFileUberFileViewModel uberFileVm in UberFiles)
			{
				if (uberFileVm.IsExpanded)
				{
					currentlyOpenElements.Add(uberFileVm.Name, new List<string>());

					foreach (CMakeFileGroupViewModel groupVm in uberFileVm.Groups)
					{
						if (groupVm.IsExpanded)
						{
							currentlyOpenElements[uberFileVm.Name].Add(groupVm.Name);
						}
					}
				}
			}

			return currentlyOpenElements;
		}

		private void ApplyExpandedElementsList(Dictionary<string, List<string>> currentlyOpenElements)
		{
			foreach (var pair in currentlyOpenElements)
			{
				CMakeFileUberFileViewModel uberFileVm = UberFiles.Where(vm => vm.Name == pair.Key).FirstOrDefault();
				if (uberFileVm != null)
				{
					uberFileVm.IsExpanded = true;

					foreach (string groupVmName in pair.Value)
					{
						CMakeFileGroupViewModel groupVm = uberFileVm.Groups.Where(vm => vm.Name == groupVmName).FirstOrDefault();
						if (groupVm != null)
						{ 
							groupVm.IsExpanded = true; 
						}
					}
				}
			}
		}

		private IMakeFile m_currentMakeFile = null;
		private IMakeFile CurrentMakeFile 
		{ 
			get => m_currentMakeFile; 
			set 
			{
				var expandedElementsList = GetExpandedElementsList();
				m_currentMakeFile = value; 
				CreateMakeFileContent();
				ApplyExpandedElementsList(expandedElementsList);
			} 
		}


		private ObservableCollection<object> m_templates = new ObservableCollection<object>();
		public ObservableCollection<object> Templates { get => m_templates; set => SetProperty(ref m_templates, value); }


		private ObservableCollection<CMakeFileUberFileViewModel> m_uberFiles = new ObservableCollection<CMakeFileUberFileViewModel>();
		public ObservableCollection<CMakeFileUberFileViewModel> UberFiles { get => m_uberFiles; set => SetProperty(ref m_uberFiles, value); }
	}
}
