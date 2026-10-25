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
    internal class GigWorker : IDisposable
    {
        private List<gig> listGigs = new List<gig>();
        private Timer _timer;

        internal GigWorker()
        {
            int minterval = CApp.Prefs.GetInt32Pref("app.performance.gigworker.min_interval", 250);
            // Allowed range 15ms to 5s.
            minterval = Math.Max(15, Math.Min(minterval, 5000));
            _timer = new Timer(new TimerCallback(goWork), null, minterval, minterval);
        }

        private void goWork(object _)
        {
            if (CApp.isClosing)
            {
                _timer.Dispose();
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

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
            listGigs = null;
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

    // "LazyGigWorker" should be named something like "CoalescingTaskList" but my name is more fun. 
    // Queue a job by name and it'll happen before the time specified. If the named job was already on the schedule, no work is queued.
    internal class LazyGigWorker : IDisposable
    {
        private class OneOffGig
        {
            public ulong _tcDue;
            public string _name;
            public Action _actOOG;

            internal OneOffGig(string sName, Action actOneTimeGig, ulong msDueBefore)
            {
                _name = sName;
                _actOOG = actOneTimeGig;
                _tcDue = msDueBefore + PlatformAPI.GetTickCount64();
            }

            public override string ToString()
            {
                return ($"OneTimeGig: '{_name}' runs before tick {_tcDue:N0}.");
            }
        }

        private static Timer _timer = null;
        private static ReaderWriterLockSlim _rwls = new ReaderWriterLockSlim();
        private static Dictionary<string, OneOffGig> _dictGigs = new Dictionary<string, OneOffGig>();

        public static bool bookGig(string sName, uint uiDueBefore, Action actGig)
        {
            try
            {
                // If we're already booked to do the job, do it.
                // BUG: This assumes that all gigs have a same-or-greater DueBefore.
                // TODO: What we really should do is ensure that the new DueBefore is later than the existing one, otherwise, accelerate it.
                _rwls.EnterReadLock();
                if (_dictGigs.ContainsKey(sName))
                {
                    return false;
                }
            }
            finally
            {
                _rwls.ExitReadLock();
            }

            OneOffGig oGig = new OneOffGig(sName, actGig, uiDueBefore);

            try
            {
                _rwls.EnterWriteLock();

                // Revalidate inside the lock that we only have one.
                if (_dictGigs.ContainsKey(sName))
                {
                    return false;
                }
                _dictGigs.Add(sName, oGig);
                if (null == _timer)
                {
                    int minterval = CApp.Prefs.GetInt32Pref("app.performance.LazyGigWorker.min_interval", 250);
                    _timer = new Timer(new TimerCallback(goWork), null, minterval, minterval);
                }
            }
            finally
            {
                _rwls.ExitWriteLock();
            }

            return true;
        }

        public static bool cancelGig(string sGigName)
        {
            try
            {
                _rwls.EnterWriteLock();
                return _dictGigs.Remove(sGigName);
            }
            finally
            {
                _rwls.ExitWriteLock();
            }
        }

        private static void goWork(object _)
        {
            List<KeyValuePair<string, OneOffGig>> listGigsThisTick = null;
            try
            {
                _rwls.EnterUpgradeableReadLock();
                ulong tc = PlatformAPI.GetTickCount64();

                foreach (KeyValuePair<string, OneOffGig> g in _dictGigs)
                {
                    if (tc > g.Value._tcDue)
                    {
                        if (null == listGigsThisTick) listGigsThisTick = new List<KeyValuePair<string, OneOffGig>>();
                        listGigsThisTick.Add(g);
                    }
                }

                if (null == listGigsThisTick) return;

                // Grab the list of gigs to run on this tick and remove them from the schedule.
                _rwls.EnterWriteLock();
                try
                {
                    foreach (KeyValuePair<string, OneOffGig> oItem in listGigsThisTick) _dictGigs.Remove(oItem.Key);
                    if ((_dictGigs.Count < 1) && (_timer != null))
                    {
                        _timer.Dispose();
                        _timer = null;
                    }
                }
                finally
                {
                    _rwls.ExitWriteLock();
                }
            }
            finally
            {
                _rwls.ExitUpgradeableReadLock();
            }

            // execute the gigs that are due. 
            foreach (KeyValuePair<string, OneOffGig> otg in listGigsThisTick)
            {
                try
                {
                    otg.Value._actOOG.Invoke();
                }
                catch (Exception eX)
                {
                    Debug.Assert(false, eX.Message);
                }
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
            _dictGigs = null;
        }
    }
}