﻿using GameCodersToolkit.DataReferenceFinderModule.ViewModels;
using Microsoft.VisualStudio.Package;
using System.Runtime.InteropServices.Expando;
using System.Windows;
using System.Windows.Controls;

namespace GameCodersToolkit.ReferenceFinder.ToolWindows
{
	public partial class ReferenceResultsWindowControl : UserControl
	{
		public ReferenceResultsWindowControl(ReferenceResultsWindowMessenger messenger)
		{
			messenger.MessageReceived += OnToolbarMessageReceived;
			InitializeComponent();
		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			VS.MessageBox.Show("ToolWindow1Control", "Button clicked");
		}

		private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{

        }

		private async void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				Border borderControl = sender as Border;
				DataEntryResultViewModel lineResult = borderControl?.DataContext as DataEntryResultViewModel;
				await lineResult?.OpenInVisualStudioAsync();
			}
		}

		private void OnToolbarMessageReceived(object sender, EReferenceResultsWindowToolbarAction action)
		{
			switch (action)
			{
				case EReferenceResultsWindowToolbarAction.ExpandAll:
					ResultsTree.SetExpansion(true);
					break;
				case EReferenceResultsWindowToolbarAction.CollapseAll:
					ResultsTree.SetExpansion(false);
					break;
			}
		}
	}
}