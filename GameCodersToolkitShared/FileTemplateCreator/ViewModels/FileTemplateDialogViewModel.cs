using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderBrowserEx;
using GameCodersToolkit.Configuration;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using GameCodersToolkit.FileTemplateCreator.Windows;
using GameCodersToolkit.ReferenceFinder;
using GameCodersToolkit.SourceControl;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Forms;

namespace GameCodersToolkit.FileTemplateCreator.ViewModels
{
	public class CFileTemplateCategoryViewModel : ObservableObject
	{
		public string Name { get; set; }
		public ObservableCollection<object> Children { get; set; } = new ObservableCollection<object>();

		private bool m_isFocusable = false;
		public bool IsFocusable { get => m_isFocusable; set => SetProperty(ref m_isFocusable, value); }
	}

	public partial class CFileTemplateViewModel : ObservableObject
	{
		public string Name { get; set; }
		public string FullName { get; set; }
		public ObservableCollection<string> Contents { get; set; } = new ObservableCollection<string>();
		public string MakeFileID { get; set; }


		private bool m_isFocusable = true;
		public bool IsFocusable { get => m_isFocusable; set => SetProperty(ref m_isFocusable, value); }


		public event Action<CFileTemplateViewModel> OnSelect;

		[RelayCommand]
		private void Select()
		{
			OnSelect?.Invoke(this);
		}
	}

	public partial class CMakeFileViewModel : ObservableObject
	{
		public string Name { get; set; }
		public string ID { get; set; }


		private bool m_isFocusable = true;
		public bool IsFocusable { get => m_isFocusable; set => SetProperty(ref m_isFocusable, value); }

		private bool m_isSelected = false;
		public bool IsSelected
		{
			get => m_isSelected;
			set
			{
				SetProperty(ref m_isSelected, value);
				if (value)
				{
					Select();
				}
			}
		}


		public event Action<CMakeFileViewModel> OnSelect;

		[RelayCommand]
		private void Select()
		{
			OnSelect?.Invoke(this);
		}
	}

	public partial class CMakeFileUberFileViewModel : ObservableObject
	{
		public string DisplayName { get; set; }
		public string Name { get; set; }
		public ObservableCollection<object> Children { get; set; } = new ObservableCollection<object>();

		private bool m_isExpanded = true;
		public bool IsExpanded { get => m_isExpanded; set => SetProperty(ref m_isExpanded, value); }

		private bool m_isFocusable = false;
		public bool IsFocusable { get => m_isFocusable; set => SetProperty(ref m_isFocusable, value); }
	}

	public partial class CMakeFileGroupViewModel : ObservableObject
	{
		public string DisplayName { get; set; }
		public string Name { get; set; }
		public ObservableCollection<object> Children { get; set; } = new ObservableCollection<object>();

		private bool m_isExpanded;
		public bool IsExpanded { get => m_isExpanded; set => SetProperty(ref m_isExpanded, value); }

		private bool m_isFocusable = false;
		public bool IsFocusable { get => m_isFocusable; set => SetProperty(ref m_isFocusable, value); }
	}

	public partial class CMakeFileFileViewModel : ObservableObject
	{
		public string Name { get; set; }

		private bool m_isFocusable = false;
		public bool IsFocusable { get => m_isFocusable; set => SetProperty(ref m_isFocusable, value); }
	}

	public partial class CSelectableEntryViewModel : ObservableObject
	{
		public string Name { get; set; }

		public delegate Task OnSelectDelegate(CSelectableEntryViewModel vm);
		public event OnSelectDelegate OnSelect;

		[RelayCommand]
		private async Task SelectAsync()
		{
			await OnSelect?.Invoke(this);
		}
	}

	public partial class CFileTemplateDialogViewModel : ObservableObject
	{
		public CFileTemplateDialogViewModel()
		{
			CreateTemplateList();
		}

        private void CreateTemplateList()
        {
            foreach (CTemplateEntry template in GameCodersToolkitPackage.FileTemplateCreatorConfig.CreatorConfig.FileTemplateEntries)
            {
                if (template.Paths.Count == 0)
                { 
					continue; 
				}

                List<string> categories = template.Name.Split('/').ToList();
                CFileTemplateCategoryViewModel rootModel = null;
                CFileTemplateCategoryViewModel currentModel = null;

                while (categories.Count != 1)
                {
					string categoryName = categories[0];
					if (currentModel != null)
					{
						CFileTemplateCategoryViewModel existingCategory = FindChildCategoryInCollection(currentModel.Children, categoryName);
						if (existingCategory != null)
						{
							currentModel = existingCategory;
							categories.RemoveAt(0);
							continue;
						}
					}

					if (rootModel == null)
					{
						CFileTemplateCategoryViewModel existingVm = FindChildCategoryInCollection(Templates, categoryName);
						if (existingVm != null)
						{
							rootModel = existingVm;
							currentModel = existingVm;
							categories.RemoveAt(0);
							continue;
						}
					}

                    CFileTemplateCategoryViewModel newVm = new CFileTemplateCategoryViewModel();
                    newVm.Name = categories.First();

                    if (rootModel == null)
                    {
                        rootModel = newVm;
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

                foreach (string path in template.Paths)
                {
                    vm.Contents.Add(System.IO.File.ReadAllText(path));
                }

                if (currentModel != null)
                {
                    currentModel.Children.Add(vm);

					if (!Templates.Contains(rootModel))
					{
						Templates.Add(rootModel);
					}
                }
                else
                {
                    Templates.Add(vm);
                }
            }

            UpdateWindowTitleIndex();
        }

		CFileTemplateCategoryViewModel FindChildCategoryInCollection(IList<object> objects, string categoryName)
        {
            foreach (object obj in objects)
            {
                if (obj is CFileTemplateCategoryViewModel existingVm)
                {
                    if (existingVm.Name == categoryName)
                    {
						return existingVm;
                    }
                }
            }

			return null;
        }

        private void OnTemplateSelected()
		{
			CurrentMakeFile = null;
			SelectedMakeFile = null;
			MakeFileContent.Clear();

			if (SelectedTemplate is CFileTemplateViewModel vm)
			{
				CreateMakeFiles(vm.MakeFileID);
            }

            UpdateWindowTitleIndex();
        }

		private void CreateMakeFiles(string defaultSelectedFileId)
		{
			CMakeFileViewModel selectedViewModel = null;

			MakeFiles.Clear();

			foreach (var fileEntry in GameCodersToolkitPackage.FileTemplateCreatorConfig.CreatorConfig.CMakeFileEntries)
			{
				CMakeFileViewModel makeFileViewModel = new CMakeFileViewModel();
				makeFileViewModel.ID = fileEntry.ID;
				makeFileViewModel.OnSelect += vm =>
				{
					foreach (var makeFileVm in MakeFiles)
					{
						if (makeFileVm != vm)
						{
							makeFileVm.IsSelected = false;
						}
					}

					SelectedMakeFile = vm;
				};
				MakeFiles.Add(makeFileViewModel);

				if (defaultSelectedFileId == fileEntry.ID)
				{
					selectedViewModel = makeFileViewModel;
				}
			}

			if (selectedViewModel != null)
			{
				selectedViewModel.IsSelected = true;
			}

            UpdateWindowTitleIndex();
		}

		private void OnMakeFileSelected()
		{
			if (SelectedMakeFile is CMakeFileViewModel makeFileVm)
			{
				if (SelectedTemplate is CFileTemplateViewModel templateVm)
				{
					string makeFilePath = GameCodersToolkitPackage.FileTemplateCreatorConfig.GetMakeFilePathByID(makeFileVm.ID);
					if (File.Exists(makeFilePath))
					{
						CurrentTemplate = GameCodersToolkitPackage.FileTemplateCreatorConfig.GetTemplateByName(templateVm.FullName);
						if (CurrentTemplate != null)
						{
							IMakeFileParser parser = GameCodersToolkitPackage.FileTemplateCreatorConfig.CreateParser();
							if (parser != null)
							{
								CurrentMakeFile = parser.Parse(makeFilePath);
							}
						}
					}
				}
            }

            UpdateWindowTitleIndex();
        }

		private void CreateMakeFileContent()
		{
			MakeFileContent.Clear();

			if (CurrentMakeFile == null)
				return;

			foreach (IUberFileNode uberFile in CurrentMakeFile.GetUberFiles())
			{
				CMakeFileUberFileViewModel uberFileVm = new CMakeFileUberFileViewModel();
				uberFileVm.Name = uberFile.GetName();
				uberFileVm.DisplayName = uberFileVm.Name + $" ({uberFile.GetGroups().Count()} Groups)";

				foreach (IGroupNode sourceGroup in uberFile.GetGroups())
				{
					CMakeFileGroupViewModel sourceGroupVm = new CMakeFileGroupViewModel();
					sourceGroupVm.Name = sourceGroup.GetName();
					sourceGroupVm.DisplayName = sourceGroup.GetName() + $" ({sourceGroup.GetFiles().Count()} Files)";

					foreach (IFileNode regularFileEntry in sourceGroup.GetFiles())
					{
						CMakeFileFileViewModel regularFileVm = new CMakeFileFileViewModel();
						regularFileVm.Name = regularFileEntry.GetName();

						sourceGroupVm.Children.Add(regularFileVm);
					}

					CMakeFileFileViewModel lastFileViewModel = sourceGroupVm.Children.LastOrDefault() as CMakeFileFileViewModel;
					CSelectableEntryViewModel selectablFileVm = new CSelectableEntryViewModel();
					selectablFileVm.Name = "Add Template Here";
					selectablFileVm.OnSelect += (vm) => AddFileToGroupAsync(uberFileVm, sourceGroupVm, lastFileViewModel);
					sourceGroupVm.Children.Add(selectablFileVm);

					uberFileVm.Children.Add(sourceGroupVm);
				}

				CMakeFileGroupViewModel lastGroupViewModel = uberFileVm.Children.LastOrDefault() as CMakeFileGroupViewModel;
				CSelectableEntryViewModel selectableGroupVm = new CSelectableEntryViewModel();
				selectableGroupVm.Name = "Add Group";
				selectableGroupVm.OnSelect += (vm) => AddGroupToUberFileAsync(uberFileVm, lastGroupViewModel);
				uberFileVm.Children.Add(selectableGroupVm);

				MakeFileContent.Add(uberFileVm);
			}

			CMakeFileUberFileViewModel lastUberFileViewModel = MakeFileContent.LastOrDefault() as CMakeFileUberFileViewModel;
			CSelectableEntryViewModel selectableUberFileVm = new CSelectableEntryViewModel();
			selectableUberFileVm.Name = "Add Uber File";
			selectableUberFileVm.OnSelect += (vm) => AddUberFileAsync(lastUberFileViewModel);
			MakeFileContent.Add(selectableUberFileVm);

            UpdateWindowTitleIndex();
        }

		private async Task AddUberFileAsync(CMakeFileUberFileViewModel previousVm)
		{
			Predicate<string> predicate = (result) =>
			{
				return result.IsValidFileName() && CurrentMakeFile.GetUberFiles().Where(e => e.GetName() == result).Count() == 0;
			};

			Action<string> errorAction = (result) =>
			{
                Community.VisualStudio.Toolkit.MessageBox errorMessageBox = new Community.VisualStudio.Toolkit.MessageBox();
                errorMessageBox.ShowError($"There already is a uber file named {result}!");
            };

            if (NameDialogWindow.ShowNameDialog("Enter Uber File name", out string newUberFileName, predicate, errorAction, previousVm?.Name))
			{
				CurrentMakeFile = CurrentMakeFile.AddUberFile(previousVm != null ? previousVm.Name : string.Empty, newUberFileName);
			}
		}

		private async Task AddGroupToUberFileAsync(CMakeFileUberFileViewModel uberFileVm, CMakeFileGroupViewModel previousVm)
        {
            Predicate<string> predicate = (result) =>
            {
                return result.IsValidFileName() && CurrentMakeFile.GetUberFiles().Where(e => e.GetName() == uberFileVm.Name).First().GetGroups().Where(e => e.GetName() == result).Count() == 0;
            };

            Action<string> errorAction = (result) =>
            {
                Community.VisualStudio.Toolkit.MessageBox errorMessageBox = new Community.VisualStudio.Toolkit.MessageBox();
                errorMessageBox.ShowError($"There already is a group named {result}!");
            };

            if (NameDialogWindow.ShowNameDialog("Enter Group name", out string newGroupName, predicate, errorAction, previousVm?.Name))
			{
				CurrentMakeFile = CurrentMakeFile.AddGroup(uberFileVm.Name, previousVm != null ? previousVm.Name : string.Empty, newGroupName);
			}
		}

		private async Task AddFileToGroupAsync(CMakeFileUberFileViewModel uberFileVm, CMakeFileGroupViewModel groupVm, CMakeFileFileViewModel previousVm)
		{
			if (uberFileVm != null && groupVm != null)
			{
				List<string> newFilePathsAbsolute = new List<string>();
				List<string> newFilePathsRelative = new List<string>();
				string newGenericFileName = string.Empty;
				string newFolderPath = string.Empty;

				string initialDirectory = Path.GetDirectoryName(CurrentMakeFile.GetOriginalFilePath());

				if (previousVm != null)
				{
					initialDirectory = Path.GetDirectoryName(Path.Combine(Path.GetDirectoryName(CurrentMakeFile.GetOriginalFilePath()), previousVm.Name));
                }

				Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog();
				saveDialog.InitialDirectory = initialDirectory;
				saveDialog.AddExtension = false;
				saveDialog.ValidateNames = true;
				saveDialog.Title = "Enter directory and name of new file(s)";
				saveDialog.OverwritePrompt = false;
				saveDialog.FileOk += (s, e) =>
				{
					newFilePathsAbsolute.Clear();
					newFilePathsRelative.Clear();

					newFolderPath = Path.GetDirectoryName(saveDialog.FileName);
					newGenericFileName = Path.GetFileNameWithoutExtension(saveDialog.FileName);

					foreach (string templateFileName in CurrentTemplate.AbsolutePaths)
					{
						string templateExtension = Path.GetExtension(templateFileName);
						string fileName = newGenericFileName + templateExtension;
						string fullPath = Path.Combine(newFolderPath, fileName);
						string relativePathToMakeFile = CurrentMakeFile.GetOriginalFilePath().MakeRelativePath(fullPath);

						newFilePathsRelative.Add(relativePathToMakeFile);
						newFilePathsAbsolute.Add(fullPath);
					}

					List<string> alreadyExistingFiles = new List<string>();
					foreach (string fileName in newFilePathsAbsolute)
					{
						if (File.Exists(fileName))
						{
							alreadyExistingFiles.Add(fileName);
						}
					}

					if (alreadyExistingFiles.Count > 0)
					{
						string errorMessage = $"The following files already exist in the given folder: \n\n";
						foreach (string fileName in alreadyExistingFiles)
						{
							errorMessage += fileName;
							errorMessage += "\n";
						}

						Community.VisualStudio.Toolkit.MessageBox errorMessageBox = new Community.VisualStudio.Toolkit.MessageBox();
						errorMessageBox.ShowError(errorMessage);
						e.Cancel = true;
					}
				};

				bool? result = OnSaveFileDialogCreated?.Invoke(this, saveDialog);

				if (result.HasValue && result.Value)
				{
					string originalMakeFilePath = CurrentMakeFile.GetOriginalFilePath();
					await PerforceConnection.TryCheckoutFilesAsync(new string[] { originalMakeFilePath });
					await PerforceConnection.TryAddFilesAsync(newFilePathsAbsolute);

					//Allow fallback in case we do not have a valid P4 connection or the file is locked etc.
					if (!originalMakeFilePath.IsFileWritable())
					{
						string message = $"The make file could not be checked out: \n\n {originalMakeFilePath} \n\n Would you like to make it overwrite it locally?";

						Community.VisualStudio.Toolkit.MessageBox messageBox = new Community.VisualStudio.Toolkit.MessageBox();
						bool makeWritable = await messageBox.ShowConfirmAsync(message);

						if (makeWritable)
						{
							originalMakeFilePath.MakeFileWritable();

							if (!originalMakeFilePath.IsFileWritable())
							{
								string errorMessage = $"Failed to make file writable: \n\n {originalMakeFilePath}";

								Community.VisualStudio.Toolkit.MessageBox errorMessageBox = new Community.VisualStudio.Toolkit.MessageBox();
								await messageBox.ShowErrorAsync(message);
								return;
							}
						}
						else
						{
							return;
						}
					}

					// Create new files and apply template content to them
					for (int i = 0; i < CurrentTemplate.AbsolutePaths.Count; i++)
					{
						string newFilePath = newFilePathsAbsolute[i];
						string templateFilePath = CurrentTemplate.AbsolutePaths[i];

						string templateContent = File.ReadAllText(templateFilePath);
						templateContent = ApplyTemplateArguments(templateContent, newGenericFileName);
						File.WriteAllText(newFilePath, templateContent);
					}

					// Add the new files to the make file and save it to disk
					CurrentMakeFile = CurrentMakeFile.AddFiles(uberFileVm.Name, groupVm.Name, previousVm != null ? previousVm.Name : string.Empty, newFilePathsRelative);
					CurrentMakeFile.Save();

					List<Task> openFileTasks = new List<Task>();
					foreach (string newFilePath in newFilePathsAbsolute)
					{
						openFileTasks.Add(VS.Documents.OpenAsync(newFilePath));
					}
					await Task.WhenAll(openFileTasks);

					//Process finished, end the dialog
					OnRequestClose(this, new EventArgs());

					GameCodersToolkitPackage.FileTemplateCreatorConfig.ExecutePostBuildScript();
                }
			}
		}

		private string ApplyTemplateArguments(string originalTemplate, string fileName)
		{
			string result = originalTemplate;
			result = result.Replace("##FILENAME##", fileName);
			result = result.Replace("##YEAR##", DateTime.UtcNow.Year.ToString());

			{
				string guidString = "##GUID##";
                int nextGuidOccurenceIndex = result.IndexOf(guidString);
				while (nextGuidOccurenceIndex > 0)
				{
					result = result.Remove(nextGuidOccurenceIndex, guidString.Length).Insert(nextGuidOccurenceIndex, Guid.NewGuid().ToString());
                    nextGuidOccurenceIndex = result.IndexOf(guidString);
                }
			}

			string authorName = GameCodersToolkitPackage.FileTemplateCreatorConfig.CreatorConfig.AuthorName;
            if (string.IsNullOrWhiteSpace(authorName))
			{
				authorName = "AUTHOR (you can add an author name in the extension settings)";
			}

			result.Replace("##AUTHOR##", authorName);

            return result;
		}

		private Dictionary<string, List<string>> GetExpandedElementsList()
		{
			Dictionary<string, List<string>> currentlyOpenElements = new Dictionary<string, List<string>>();

			foreach (CMakeFileUberFileViewModel uberFileVm in MakeFileContent.OfType<CMakeFileUberFileViewModel>())
			{
				if (uberFileVm != null && uberFileVm.IsExpanded)
				{
					if (!currentlyOpenElements.ContainsKey(uberFileVm.Name))
					{
						currentlyOpenElements.Add(uberFileVm.Name, new List<string>());

						foreach (CMakeFileGroupViewModel groupVm in uberFileVm.Children.OfType<CMakeFileGroupViewModel>())
						{
							if (groupVm != null && groupVm.IsExpanded)
							{
								currentlyOpenElements[uberFileVm.Name].Add(groupVm.Name);
							}
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
				CMakeFileUberFileViewModel uberFileVm = MakeFileContent.Where(obj =>
				{
					if (obj is CMakeFileUberFileViewModel vm)
						return vm.Name == pair.Key;

					return false;
				}
				).FirstOrDefault() as CMakeFileUberFileViewModel;

				if (uberFileVm != null)
				{
					uberFileVm.IsExpanded = true;

					foreach (string groupVmName in pair.Value)
					{
						CMakeFileGroupViewModel groupVm = uberFileVm.Children.Where(obj =>
						{
							if (obj is CMakeFileGroupViewModel vm)
								return vm.Name == groupVmName;

							return false;
						}
						).FirstOrDefault() as CMakeFileGroupViewModel;
						if (groupVm != null)
						{
							groupVm.IsExpanded = true;
						}
					}
				}
			}
		}

		private void UpdateWindowTitleIndex()
		{
			int index = 0;

			if (SelectedTemplate != null)
				index++;

			if (SelectedMakeFile != null)
				index++;

			WindowTitle = $"({index + 1}/{WindowTitles.Length}) {WindowTitles[index]}";
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

        private string[] WindowTitles { get; set; } = ["Select a template...", "Select make file...", "Select uber file and group to store new file(s)"];


		private string m_windowTitle = string.Empty;
		public string WindowTitle { get => m_windowTitle; set => SetProperty(ref m_windowTitle, value); }


		private CTemplateEntry m_currentTemplate = null;
		public CTemplateEntry CurrentTemplate { get => m_currentTemplate; set => SetProperty(ref m_currentTemplate, value); }


		private object m_selectedTemplate = null;
		public object SelectedTemplate { get => m_selectedTemplate; set { SetProperty(ref m_selectedTemplate, value); OnTemplateSelected(); } }


		private ObservableCollection<object> m_templates = new ObservableCollection<object>();
		public ObservableCollection<object> Templates { get => m_templates; set => SetProperty(ref m_templates, value); }


		private CMakeFileViewModel m_selectedMakeFile = null;
		public CMakeFileViewModel SelectedMakeFile { get => m_selectedMakeFile; set { SetProperty(ref m_selectedMakeFile, value); OnMakeFileSelected(); } }


		private ObservableCollection<CMakeFileViewModel> m_makeFiles = new ObservableCollection<CMakeFileViewModel>();
		public ObservableCollection<CMakeFileViewModel> MakeFiles { get => m_makeFiles; set => SetProperty(ref m_makeFiles, value); }


		private ObservableCollection<object> m_makeFileContent = new ObservableCollection<object>();
		public ObservableCollection<object> MakeFileContent { get => m_makeFileContent; set => SetProperty(ref m_makeFileContent, value); }

		public event EventHandler OnRequestClose;
		public event Func<object, Microsoft.Win32.SaveFileDialog, bool?> OnSaveFileDialogCreated;
	}
}
