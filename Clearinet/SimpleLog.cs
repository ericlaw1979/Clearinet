using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Clearinet
{
    public class LogEventArgs : EventArgs
    {
        internal LogEventArgs(string sLog)
        {
            LogString = sLog;
        }

        public string LogString
        {
            get;
            private set;
        }
    }

    public class SimpleLog
    {
        public event EventHandler<LogEventArgs> OnLog;

        /// <summary>
        /// List of messages logged before a listener is attached.
        /// </summary>
        private List<string> slQueuedMessages;

        public SimpleLog()
        {
            slQueuedMessages = new List<string>();
        }

        // TODO: Rather than having someone manually tell us when it's time to
        // spew out our stored messages, should we instead just accumulate until
        // the first event handler subscribes to OnLog or the first LOG call that
        // happens after a listener is available?
        internal void FlushQueue()
        {
            Debug.Assert(slQueuedMessages != null);
            EventHandler<LogEventArgs> ehSubscribers = OnLog;
            if (null != ehSubscribers)
            {
                List<string> slToReplay = slQueuedMessages;
                slQueuedMessages = null;
                foreach (string sLog in slToReplay)
                {
                    LogEventArgs olsEA = new LogEventArgs(sLog);
                    ehSubscribers(this, olsEA);
                }
            }
        }

        public void LogFormat(string sFormat, params object[] arrArgs)
        {
            Log(String.Format(sFormat, arrArgs));
        }
        public void Log(string sLog)
        {
            if (!sLog.HasText()) return;

            Trace.WriteLine(sLog);

            // If we're still accumulating queued messages, add to
            // that list and bail out.
            if (null != slQueuedMessages)
            {
                lock (slQueuedMessages)
                {
                    slQueuedMessages.Add(sLog);
                }
                return;
            }

            // Notify any listeners.
            EventHandler<LogEventArgs> ehSubscribers = OnLog;
            if (null != ehSubscribers)
            {
                LogEventArgs olsEA = new LogEventArgs(sLog);
                ehSubscribers(this, olsEA);
            }
        }
    }
}
