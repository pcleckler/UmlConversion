using System;

namespace CSharpSampleLibrary
{
    /// <summary>
    /// Sample base class.
    /// </summary>
    public class BaseClass : IDerivedInterface
    {
        /// <summary>
        /// Sample base class constructor.
        /// </summary>
        public BaseClass()
        {
        }

        /// <summary>
        /// Sample base class constructor with arguments.
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        public BaseClass(string arg1, bool arg2, int arg3)
        {
        }

        /// <summary>
        /// A sample event.
        /// </summary>
        public event SampleEventHandler EventOccurred;

        /// <inheritdoc/>
        public string BaseInterfaceProperty { get; set; }

        /// <inheritdoc/>
        public string DerivedInterfaceProperty { get; set; }

        /// <summary>
        /// Sample enum usage.
        /// </summary>
        public SampleEnum SampleEnum { get; set; }

        /// <summary>
        /// Sample struct usage.
        /// </summary>
        public SampleStruct SampleStruct { get; set; }

        /// <summary>
        /// Boolean method for the base class.
        /// </summary>
        /// <returns>A false value as a sample return value.</returns>
        public bool BaseClassBooleanMethod()
        {
            return false;
        }

        /// <summary>
        /// Integer method for the base class.
        /// </summary>
        /// <param name="arg1">The first integer argument.</param>
        /// <param name="arg2">The second integer argument.</param>
        /// <returns>A value of negative one (-1) as a sample return value.</returns>
        public int BaseClassIntegerMethodWithArguments(int arg1, int arg2)
        {
            return -1;
        }

        /// <inheritdoc/>
        public bool BaseInterfaceBooleanMethod(string arg1, bool arg2, int arg3)
        {
            return false;
        }

        /// <inheritdoc/>
        public bool DerivedInterfaceBooleanMethod(string arg1, bool arg2, int arg3)
        {
            return false;
        }

        /// <summary>
        /// A sample event invocator.
        /// </summary>
        /// <param name="e">Arguments for the event.</param>
        protected virtual void OnEventOccurred(EventArgs e)
        {
            this.EventOccurred?.Invoke(this, e);
        }
    }
}