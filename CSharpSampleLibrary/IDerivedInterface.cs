namespace CSharpSampleLibrary
{
    /// <summary>
    /// Sample derived interface from <see cref="IBaseInterface"/>.
    /// </summary>
    public interface IDerivedInterface : IBaseInterface
    {
        /// <summary>
        /// Gets or sets the sample DerivedInterfaceProperty value.
        /// </summary>
        string DerivedInterfaceProperty { get; set; }

        /// <summary>
        /// Sample boolean method for the derived interface.
        /// </summary>
        /// <param name="arg1">First argument string.</param>
        /// <param name="arg2">Second argument boolean.</param>
        /// <param name="arg3">Third argument integer.</param>
        /// <returns></returns>
        bool DerivedInterfaceBooleanMethod(string arg1, bool arg2, int arg3);
    }
}