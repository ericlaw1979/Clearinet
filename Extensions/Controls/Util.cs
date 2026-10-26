using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Windows.Forms;

namespace Be.Windows.Forms
{
    static class Util
    {
        /// <summary>
        /// Initializes an instance of Util class
        /// </summary>
        static Util()
        {
        }

        public static T GetRoot<T>(Control c) where T : Control
        {
            if (c == null)
                return default(T);

            var parent = c.Parent;

            var found = parent as T;
            if (found != null && found.Parent == null)
                return found;

            return GetRoot<T>(parent);
        }
    }
}
