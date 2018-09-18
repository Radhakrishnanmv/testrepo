using Microsoft.SharePoint;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IGEventHandlers
{
    class Log
    {
        public static void LogMessage(string Message)
        {
            SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                EventLog oLog = new EventLog();
                // oLog.Source = "SharePoint Foundation";
                oLog.Source = "IGEventHandlers";
                oLog.WriteEntry(Message, EventLogEntryType.Information);
            });
        }
    }
}
