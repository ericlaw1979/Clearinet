using System;

namespace Clearinet
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AssemblyBuiltDateAttribute : Attribute
    {
        public DateTime BuiltDate { get; }

        public AssemblyBuiltDateAttribute(string value)
        {
            if (DateTime.TryParse(value, out DateTime result))
            {
                BuiltDate = result;
            }
            else
            {
                BuiltDate = DateTime.MinValue;
            }
        }
    }
}