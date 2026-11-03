using System;
using System.Net;
using Clearinet.CaptureEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CaptureEngine.Tests
{
    [TestClass]
    public sealed class CaptureEngineRuntimeTests
    {
        [TestMethod]
        public void CaptureEngineOptions_RejectsInvalidArguments()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new CaptureEngineOptions(null, 8888));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CaptureEngineOptions(IPAddress.Loopback, -1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CaptureEngineOptions(IPAddress.Loopback, 65536));
        }

        [TestMethod]
        public void Runtime_StartStopDispose_IsIdempotent()
        {
            var listener = new FakeCaptureEngineListener();
            var runtime = new CaptureEngineRuntime(
                new CaptureEngineOptions(IPAddress.Loopback, 8888),
                () => listener);

            runtime.Start();
            runtime.Start();
            Assert.IsTrue(runtime.IsRunning);
            Assert.AreEqual(1, listener.StartCount);

            runtime.Stop();
            runtime.Stop();
            Assert.IsFalse(runtime.IsRunning);
            Assert.AreEqual(1, listener.StopCount);

            runtime.Dispose();
            runtime.Dispose();
            Assert.AreEqual(1, listener.DisposeCount);
        }

        private sealed class FakeCaptureEngineListener : ICaptureEngineListener
        {
            public int StartCount { get; private set; }
            public int StopCount { get; private set; }
            public int DisposeCount { get; private set; }
            public bool IsRunning { get; private set; }

            public void Start()
            {
                StartCount++;
                IsRunning = true;
            }

            public void Stop()
            {
                StopCount++;
                IsRunning = false;
            }

            public void Dispose()
            {
                DisposeCount++;
                IsRunning = false;
            }
        }
    }
}
