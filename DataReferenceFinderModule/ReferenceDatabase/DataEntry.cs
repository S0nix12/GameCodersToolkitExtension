using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase
{
	public class GenericDataIdentifier
	{
		public GenericDataIdentifier(Guid guidId)
		{
			GuidId = guidId;
			NameId = null;
		}

		public GenericDataIdentifier(string nameId)
		{
			GuidId = null;
			NameId = nameId;
		}

		public override bool Equals(object obj)
		{
			return obj is GenericDataIdentifier other ? this.Equals(other) : false;
		}

		public override int GetHashCode()
		{
			return GuidId != null ? GuidId.GetHashCode() : NameId.GetHashCode();
		}

		public static bool operator==(GenericDataIdentifier lhs, GenericDataIdentifier rhs)
		{
			if (lhs is null || rhs is null)
			{
				return lhs is null && rhs is null;
			}

			return lhs.Equals(rhs);
		}

		public static bool operator!=(GenericDataIdentifier lhs, GenericDataIdentifier rhs) { return !(lhs == rhs); }

		public override string ToString()
		{
			return GuidId != null ? GuidId.ToString() : NameId;
		}

		public bool Equals(GenericDataIdentifier other)
		{
			return GuidId != null ? GuidId.Equals(other.GuidId) : NameId == other.NameId;
		}

		public Guid? GuidId { get; private set; } = null;
		public string NameId { get; private set; } = null;
	}

	public class DataEntry
	{
		public override bool Equals(object obj)
		{
			if (obj is DataEntry otherEntry)
			{
				return Identifier.Equals(otherEntry.Identifier);
			}

			if (obj is GenericDataIdentifier dataIdentifier)
			{
				return Identifier.Equals(dataIdentifier);
			}

			return false;
		}
		public static bool operator ==(DataEntry lhs, DataEntry rhs)
		{
			if (lhs is null || rhs is null)
			{
				return lhs is null && rhs is null;
			}

			return lhs.Equals(rhs);
		}
		public static bool operator !=(DataEntry lhs, DataEntry rhs) { return !(lhs == rhs); }
		public override int GetHashCode()
		{
			return Identifier.GetHashCode();
		}

		public GenericDataIdentifier Identifier { get; set; }

		public string Name { get; set; }
		public string SourceFile { get; set; }
		public int SourceLineNumber { get; set; }


		public DataEntry Parent { get; set; }
		public string ParentName { get; set; }
		public List<GenericDataIdentifier> References { get; set; } = new List<GenericDataIdentifier>();
	}
}
