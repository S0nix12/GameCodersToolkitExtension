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
			m_identifier = guidId;
		}

		public GenericDataIdentifier(string nameId)
		{
			m_identifier = nameId;
		}

		public override bool Equals(object obj)
		{
			return obj is GenericDataIdentifier other ? this.Equals(other) : false;
		}

		public override int GetHashCode()
		{
			return m_identifier.GetHashCode();
		}

		public static bool operator ==(GenericDataIdentifier lhs, GenericDataIdentifier rhs)
		{
			if (lhs is null || rhs is null)
			{
				return lhs is null && rhs is null;
			}

			return lhs.Equals(rhs);
		}

		public static bool operator !=(GenericDataIdentifier lhs, GenericDataIdentifier rhs) { return !(lhs == rhs); }

		public override string ToString()
		{
			return m_identifier.ToString();
		}

		public bool Equals(GenericDataIdentifier other)
		{
			if (m_identifier is Guid thisGuid && other.m_identifier is Guid otherGuid)
			{
				return thisGuid.Equals(otherGuid);
			}
			else if (m_identifier is string thisNameId && other.m_identifier is string otherNameId)
			{
				return thisNameId.Equals(otherNameId);
			}

			return false;
		}

		private readonly object m_identifier = null;
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

		public string Name { get; set; } = "Unkown";
		public string BaseType { get; set; }
		public string SubType { get; set; } = "NoSubType";
		public string SourceFile { get; set; }
		public int SourceLineNumber { get; set; }


		public DataEntry Parent { get; set; }
		public string ParentName { get; set; }
		public List<GenericDataIdentifier> References { get; set; } = new List<GenericDataIdentifier>();
	}
}
