using System.Collections.Generic;
using System.IO;

namespace RossLean.DynamicGenericBridge
{
    static class Extensions
    {
        public static void WriteJoined(this TextWriter writer, string separator, IEnumerable<string> sequence)
        {
            bool first = true;
            foreach(var value in sequence)
            {
                if(first)
                {
                    first = false;
                }
                else
                {
                    writer.Write(separator);
                }
                writer.Write(value);
            }
        }
    }
}
