using GameCodersToolkit.DataReferenceFinderModule.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.Utils
{
	public interface ISearchableViewModel
	{
		abstract public string GetSearchField();
	}

	public interface INestedSearchableViewModel : ISearchableViewModel
	{
		Predicate<object> AdditionalFilter { get; }
		string[] SearchTokens { get; set; }
		IEnumerable ChildEntries { get; }
		ICollectionView FilteredView { get; }
	}

	public static class SearchEntryUtils
	{
		public static bool MatchesSearchTokens(ISearchableViewModel searchable, string[] searchTokens)
		{
			string searchField = searchable.GetSearchField();
			return searchTokens.Length == 0 || searchTokens.All(token => searchField.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
		}

		public static void SetSearchTokens(INestedSearchableViewModel searchable, string[] searchTokens)
		{
			string searchField = searchable.GetSearchField();
			
			// Remove all tokens that match this level before passing it to the next. This way we display also entries that match the tokens mixed
			string[] childFilterTokens = searchTokens.Where(token => searchField.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0).ToArray();
			searchable.SearchTokens = childFilterTokens;

			// All tokens match this entry if we removed all Tokens
			bool selfMatch = childFilterTokens.Length == 0;

			foreach (object child in searchable.ChildEntries)
			{
				if (child is INestedSearchableViewModel nestedChild)
				{
					SetSearchTokens(nestedChild, childFilterTokens);
				}
			}

			Predicate<object> additionalFilter = searchable.AdditionalFilter;

			if (selfMatch)
			{
				searchable.FilteredView.Filter = additionalFilter;
			}
			else if (searchable.FilteredView.Filter == additionalFilter)
			{
				searchable.FilteredView.Filter = entry =>
				{
					if (additionalFilter != null && !additionalFilter(entry))
						return false;

					if (entry is ISearchableViewModel searchableEntry)
					{
						return FilterChild(searchableEntry, searchable.SearchTokens);
					}
					return false;
				};
			}
			else
			{
				searchable.FilteredView.Refresh();
			}
		}

		public static bool FilterChild(ISearchableViewModel childEntry, string[] searchTokens)
		{
			return searchTokens.Length == 0
			|| (childEntry is INestedSearchableViewModel nestedEntry && !nestedEntry.FilteredView.IsEmpty)
			|| MatchesSearchTokens(childEntry, searchTokens);
		}
	}
}
