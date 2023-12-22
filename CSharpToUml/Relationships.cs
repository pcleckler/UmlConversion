using System.Collections.Generic;

namespace CSharpToUml
{
    public class Relationships
    {
        public static readonly Relationship Extends = new Relationship("Extends", "--|>");
        public static readonly Relationship ComposedOf = new Relationship("Composed Of", "--*");
        public static readonly Relationship RefersTo = new Relationship("Refers To", "-->");

        public static List<Relationship> All()
        {
            return new List<Relationship>()
            {
                Extends,
                ComposedOf,
                RefersTo,
            };
        }
    }
}
