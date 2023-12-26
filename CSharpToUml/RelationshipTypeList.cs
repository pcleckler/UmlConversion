using System.Collections.Generic;

namespace CSharpToUml
{
    public class RelationshipTypeList
    {
        public static readonly RelationshipType ComposedOf = new RelationshipType("Composed Of", "--*");
        public static readonly RelationshipType Extends = new RelationshipType("Extends", "--|>");
        public static readonly RelationshipType References = new RelationshipType("References", "-->");

        public static List<RelationshipType> All()
        {
            return new List<RelationshipType>()
            {
                Extends,
                ComposedOf,
                References,
            };
        }
    }
}