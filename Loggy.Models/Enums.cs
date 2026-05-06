using System;
using System.Collections.Generic;
using System.Text;

namespace Loggy.Models
{
    public class Enums
    {
        public enum SchemaTypes
        {
            Serilog,
            NLog,
            Log4Net
        }

        public enum SortOptions
        {
            ByException,
            ByTimeStamp
        }

        public enum ModelOptions
        {
            Gemini,
            Custom
        }

    }
}
