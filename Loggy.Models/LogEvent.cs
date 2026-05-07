using System;
using System.Collections.Generic;
using System.Text;

namespace Loggy.Models
{
    public class LogEvent
    {
        public int Id { get; set; }
        public Dictionary<string, string> Schema { get; set; }
    }
}
