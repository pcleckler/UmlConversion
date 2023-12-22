using System.Collections.Generic;

namespace CSharpToUml
{
    public class UmlSegments
    {
        public static readonly UmlSegment Name = new UmlSegment("Name", "{");
        public static readonly UmlSegment Summary = new UmlSegment("Summary", "..");
        public static readonly UmlSegment Events = new UmlSegment("Events", "--");
        public static readonly UmlSegment Fields = new UmlSegment("Fields", "--");
        public static readonly UmlSegment Properties = new UmlSegment("Properties", "--");
        public static readonly UmlSegment Constructors = new UmlSegment("Constructors", "--");
        public static readonly UmlSegment Methods = new UmlSegment("Methods", "--");
        public static readonly UmlSegment Relationships = new UmlSegment("Relationships", string.Empty);

        public static List<UmlSegment> ClassSegments()
        {
            return new List<UmlSegment>()
            {
                Summary,
                Events,
                Fields,
                Properties,
                Constructors,
                Methods,
            };
        }

        public static List<UmlSegment> AllSegments()
        {
            var list = ClassSegments();

            list.Insert(0, UmlSegments.Name);
                
            list.Add(Relationships);

            return list;
        }
    }
}
