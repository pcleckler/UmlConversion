using System.Collections.Generic;

namespace CSharpToUml
{
    internal class RelationshipTypeList
    {
        public static readonly RelationshipType ComposedOf = new RelationshipType("Composed Of", "--*");
        public static readonly RelationshipType Encloses = new RelationshipType("Encloses", "*--");
        public static readonly RelationshipType Implements = new RelationshipType("Implements", "-[dashed]->");
        public static readonly RelationshipType Inherits = new RelationshipType("Inherits", "--|>");
        public static readonly RelationshipType References = new RelationshipType("References", "-->");

        public static List<RelationshipType> All()
        {
            return new List<RelationshipType>()
            {
                ComposedOf,
                Encloses,
                Implements,
                Inherits,
                References,
            };
        }
    }
}