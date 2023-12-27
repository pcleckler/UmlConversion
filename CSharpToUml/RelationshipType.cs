namespace CSharpToUml
{
    internal class RelationshipType
    {
        public RelationshipType(string label, string arrow)
        {
            this.Label = label;
            this.Arrow = arrow;
        }

        public string Arrow { get; set; }
        public string Label { get; set; }

        public override string ToString()
        {
            return this.Label;
        }
    }
}