using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Clearinet
{
    // Queue jobs and they'll happen periodically at the interval described until canceled.
    // TODO: We could avoid creating the timer if there are no gigs, but that would require a bit more complexity in the code,
    // and in Clearinet, we ALWAYS have work to do.
    internal class GigWorker
    {
        private List<gig> listGigs = new List<gig>();
        private Timer timerWork;

        internal GigWorker()
        {
            int minterval = CApp.Prefs.GetInt32Pref("app.performance.gigworker.min_interval", 250);
            // Allowed range 15ms to 5s.
            minterval = Math.Max(15, Math.Min(minterval, 5000));
            timerWork = new Timer(new TimerCallback(goWork), null, minterval, minterval);
        }

        private void goWork(object _)
        {
            if (CApp.isClosing)
            {
                timerWork.Dispose();
                listGigs = null;
                return;
            }

            gig[] gigsThisRun = getAllGigs();


            foreach (var todo in gigsThisRun)
            {
                // If we're do, do the job.
                if (PlatformAPI.GetTickCount64() > todo._ulMostRecentRun + todo._msInterval)
                {
                    todo._actGig.Invoke();
                    todo._ulMostRecentRun = PlatformAPI.GetTickCount64();  // TODO: VALIDATE!
                }
            }
        }

        // Assign work to this worker. It'll run on a background thread.
        internal gig createGig(string sName, Action actGig, uint msInterval)
        {
            var gigNew = new gig(sName, actGig, msInterval);
            lock (listGigs)
            {
                listGigs.Add(gigNew);
            }
            return gigNew;
        }


        internal void cancelGig(gig gigToCancel)
        {
            if (null == gigToCancel) { Debug.Assert(false); return; }
            lock (listGigs)
            {
                listGigs.Remove(gigToCancel);
            }
        }

        internal void cancelGigsNamed(string sName)
        {
            lock (listGigs)
            {
                listGigs.RemoveAll(g => g._name.OICEquals(sName));
            }
        }

        private gig[] getAllGigs()
        {
            gig[] gigsAll;
            lock (listGigs)
            {
                gigsAll = new gig[listGigs.Count];
                listGigs.CopyTo(gigsAll);
            }
            return gigsAll;
        }
        public override string ToString()
        {
            gig[] all = getAllGigs();
            StringBuilder sbResult = new StringBuilder();
            sbResult.AppendLine($"Gigworker has {all.Length} gigs. {(all.Length > 0 ? "They are: " : string.Empty)}");
            foreach (gig g in all) sbResult.AppendLine("\t" + g.ToString());
            return sbResult.ToString();
        }

        internal class gig
        {
            public uint _msInterval;
            public UInt64 _ulMostRecentRun;
            public string _name;

            public Action _actGig;

            // Interesting design issue: We can't make this private if we're going to hand any out to the caller,
            // because the caller needs to be able to pass it back to us when they want to cancel it. So we make it internal.
            // We could fix this by creating the notion of a "gig handle" that the caller can use to cancel,
            // but that seems like overkill.
            internal gig(string sName, Action actGig, uint msInterval)
            {
                _name = sName;
                _actGig = actGig;
                _msInterval = msInterval;
                _ulMostRecentRun = PlatformAPI.GetTickCount64();
            }

            public override string ToString()
            {
                return ($"Gig: '{_name}' runs every {_msInterval:N0}ms, and most recently ran at {_ulMostRecentRun:N0}");

            }
        }
    }
}