using System;

namespace CSharpSampleLibrary
{
    /// <summary>
    /// Sample class derived from <see cref="BaseClass"/>
    /// </summary>
    public class DerivedClass : BaseClass
    {
        /// <summary>
        /// Sample derived class constructor.
        /// </summary>
        public DerivedClass()
        {
        }

        /// <summary>
        /// Sample derived class constructor with arguments.
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        public DerivedClass(string arg1, bool arg2, int arg3)
        {
            this.Arg1Value = arg1;
            this.Arg2Value = arg2;
            this.Arg3Value = arg3;
        }

        private string Arg1Value { get; }
        private bool Arg2Value { get; }
        private int Arg3Value { get; }

        /// <summary>
        /// Double method for the derived class.
        /// </summary>
        /// <param name="arg1">The first double argument.</param>
        /// <param name="arg2">The second double argument.</param>
        /// <returns>A value of -2 or -3 as a sample return values.</returns>
        public double DerivedClassDoubleMethodWithArguments(int arg1, int arg2)
        {
            // This code to simply make use of properties and avoid messages from the compiler.
            if (this.Arg1Value != string.Empty && this.Arg2Value && this.Arg3Value > arg1 && this.Arg3Value > arg2)
            {
                return -3.0D;
            }

            return -2.0D;
        }

        /// <summary>
        /// String method for the derived class.
        /// </summary>
        /// <returns>An empty string value as a sample return value.</returns>
        public string DerivedClassStringMethod()
        {
            return string.Empty;
        }
    }
}