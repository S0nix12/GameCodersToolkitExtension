﻿using CommunityToolkit.Mvvm.ComponentModel;
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

	public partial class CMakeFileUberFileViewModel : ObservableObject
	{
		public string Name { get; set; }
		public ObservableCollection<object> Children { get; set; } = new ObservableCollection<object>();

		private bool m_isExpanded;
		public bool IsExpanded { get => m_isExpanded; set => SetProperty(ref m_isExpanded, value); }

		private bool m_isFocusable = false;
		public bool IsFocusable { get => m_isFocusable; set => SetProperty(ref m_isFocusable, value); }
	}

	public partial class CMakeFileGroupViewModel : ObservableObject
	{
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

				foreach (string path in template.Paths)
				{
					vm.Contents.Add(System.IO.File.ReadAllText(path));
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

		private void OnTemplateSelected()
		{
			CurrentMakeFile = null;
			MakeFileContent.Clear();

			if (SelectedTemplate is CFileTemplateViewModel vm)
			{
				string makeFilePath = GameCodersToolkitPackage.FileTemplateCreatorConfig.GetMakeFilePathByID(vm.MakeFileID);
				if (System.IO.File.Exists(makeFilePath))
				{
					CurrentTemplate = GameCodersToolkitPackage.FileTemplateCreatorConfig.GetTemplateByName(vm.FullName);
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

		private void CreateMakeFileContent()
		{
			MakeFileContent.Clear();

			if (CurrentMakeFile == null)
				return;

			foreach (IUberFileNode uberFile in CurrentMakeFile.GetUberFiles())
			{
				CMakeFileUberFileViewModel uberFileVm = new CMakeFileUberFileViewModel();
				uberFileVm.Name = uberFile.GetName();

				foreach (IGroupNode sourceGroup in uberFile.GetGroups())
				{
					CMakeFileGroupViewModel sourceGroupVm = new CMakeFileGroupViewModel();
					sourceGroupVm.Name = sourceGroup.GetName();

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
		}

		private async Task AddUberFileAsync(CMakeFileUberFileViewModel previousVm)
		{
			if (NameDialogWindow.ShowFileNameDialog("Enter Uber File name", out string newUberFileName))
			{
				CurrentMakeFile = CurrentMakeFile.AddUberFile(previousVm != null ? previousVm.Name : string.Empty, newUberFileName);
			}
		}

		private async Task AddGroupToUberFileAsync(CMakeFileUberFileViewModel uberFileVm, CMakeFileGroupViewModel previousVm)
		{
			if (NameDialogWindow.ShowNameDialog("Enter Group name", out string newGroupName))
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

				SaveFileDialog saveDialog = new SaveFileDialog();
				saveDialog.InitialDirectory = Path.GetDirectoryName(CurrentMakeFile.GetOriginalFilePath());
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

				bool? result = saveDialog.ShowDialog();

				if (result.HasValue && result.Value)
				{
					string originalMakeFilePath = CurrentMakeFile.GetOriginalFilePath();
					await PerforceConnection.TryCheckoutFilesAsync([originalMakeFilePath]);
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
				}
			}
		}

		private string ApplyTemplateArguments(string originalTemplate, string fileName)
		{
			string result = originalTemplate;
			result = result.Replace("##FILENAME", fileName);
			result = result.Replace("##YEAR", DateTime.UtcNow.Year.ToString());
			return result;
		}

		private Dictionary<string, List<string>> GetExpandedElementsList()
		{
			Dictionary<string, List<string>> currentlyOpenElements = new Dictionary<string, List<string>>();

			foreach (CMakeFileUberFileViewModel uberFileVm in MakeFileContent.OfType<CMakeFileUberFileViewModel>())
			{
				if (uberFileVm != null && uberFileVm.IsExpanded)
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

		private CTemplateEntry m_currentTemplate = null;
		public CTemplateEntry CurrentTemplate { get => m_currentTemplate; set => m_currentTemplate = value; }


		private object m_selectedTemplate = null;
		public object SelectedTemplate { get => m_selectedTemplate; set { SetProperty(ref m_selectedTemplate, value); OnTemplateSelected(); } }


		private ObservableCollection<object> m_templates = new ObservableCollection<object>();
		public ObservableCollection<object> Templates { get => m_templates; set => SetProperty(ref m_templates, value); }


		private ObservableCollection<object> m_makeFileContent = new ObservableCollection<object>();
		public ObservableCollection<object> MakeFileContent { get => m_makeFileContent; set => SetProperty(ref m_makeFileContent, value); }

		public event EventHandler OnRequestClose;
	}
}