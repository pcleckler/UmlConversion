namespace CSharpToUml
{
    internal class UmlSegment
    {
        public UmlSegment(string Name, string Separator)
        {
            this.Name = Name;
            this.Separator = Separator;
        }

        public string Name { get; set; }
        public string Separator { get; set; }

        public override string ToString()
        {
            return this.Name;
        }
    }
}