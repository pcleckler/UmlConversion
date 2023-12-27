namespace CSharpSampleLibrary
{
    /// <summary>
    /// Sample base interface.
    /// </summary>
    public interface IBaseInterface
    {
        /// <summary>
        /// Gets or sets the sample BaseInterfaceProperty value.
        /// </summary>
        string BaseInterfaceProperty { get; set; }

        /// <summary>
        /// Sample boolean method for the base interface.
        /// </summary>
        /// <param name="arg1">First argument string.</param>
        /// <param name="arg2">Second argument boolean.</param>
        /// <param name="arg3">Third argument integer.</param>
        /// <returns></returns>
        bool BaseInterfaceBooleanMethod(string arg1, bool arg2, int arg3);
    }
}