using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GameCodersToolkit.Utils
{
	internal static class ModernFolderPicker
	{
		public static string ShowDialog(string initialFolder = null, string title = null)
		{
			IFileOpenDialog dialog = (IFileOpenDialog)new FileOpenDialogRCW();
			try
			{
				dialog.GetOptions(out uint options);
				dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);

				if (!string.IsNullOrEmpty(title))
				{
					dialog.SetTitle(title);
				}

				if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
				{
					Guid riid = typeof(IShellItem).GUID;
					if (SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, ref riid, out IShellItem folder) == 0)
					{
						dialog.SetFolder(folder);
					}
				}

				int hr = dialog.Show(IntPtr.Zero);
				if (hr != 0)
				{
					return null; // User cancelled or error
				}

				dialog.GetResult(out IShellItem result);
				result.GetDisplayName(SIGDN_FILESYSPATH, out string path);
				return path;
			}
			finally
			{
				Marshal.ReleaseComObject(dialog);
			}
		}

		private const uint FOS_PICKFOLDERS = 0x00000020;
		private const uint FOS_FORCEFILESYSTEM = 0x00000040;
		private const uint SIGDN_FILESYSPATH = 0x80058000;

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
		private static extern int SHCreateItemFromParsingName(
			[MarshalAs(UnmanagedType.LPWStr)] string pszPath,
			IntPtr pbc,
			ref Guid riid,
			out IShellItem ppv);

		// COM class for FileOpenDialog
		[ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
		private class FileOpenDialogRCW { }

		[ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		private interface IFileOpenDialog
		{
			[PreserveSig]
			int Show([In] IntPtr hwndOwner);
			void SetFileTypes();
			void SetFileTypeIndex([In] uint iFileType);
			void GetFileTypeIndex(out uint piFileType);
			void Advise();
			void Unadvise();
			void SetOptions([In] uint fos);
			void GetOptions(out uint pfos);
			void SetDefaultFolder([In] IShellItem psi);
			void SetFolder([In] IShellItem psi);
			void GetFolder(out IShellItem ppsi);
			void GetCurrentSelection(out IShellItem ppsi);
			void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
			void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
			void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
			void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
			void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
			void GetResult(out IShellItem ppsi);
			void AddPlace([In] IShellItem psi, int fdap);
			void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
			void Close([MarshalAs(UnmanagedType.Error)] int hr);
			void SetClientGuid([In] ref Guid guid);
			void ClearClientData();
			void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
			void GetResults(out IntPtr ppenum); // IShellItemArray
			void GetSelectedItems(out IntPtr ppsai); // IShellItemArray
		}

		[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		private interface IShellItem
		{
			void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
			void GetParent(out IShellItem ppsi);
			void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
			void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
			void Compare(IShellItem psi, uint hint, out int piOrder);
		}
	}
}
