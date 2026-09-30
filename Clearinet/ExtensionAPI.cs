// This file describes the Extension API for Clearinet. The API aims to enable
// simple porting from extensions based on the legacy Fiddler API.
using System;
using System.Collections.Generic;

namespace Clearinet
{
    /// <summary>
    /// Extension assemblies designed to load into Clearinet should be marked with this attribute, 
    /// specifying the minimum supported version of the app. If the app's version is lower than
    /// the specified version, the Assembly will be ignored.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    public sealed class RequiredVersionAttribute : Attribute
    {
        /// <summary>
        /// Minimum app version required to load the extension.
        /// </summary>
        /// <param name="sVersion">The minimum version as a four-integer dotted string (e.g. "1.0.1.0")</param>
        public RequiredVersionAttribute(string sVersion)
        {
            RequiredVersion = sVersion;
        }

        public string RequiredVersion
        {
            get; private set;
        }
    }

    /// <summary>
    /// The most basic extension interface. 
    /// Public types implementing this interface will be instantiated by the app. 
    /// In most cases, `OnLoad` will attach event handlers to the app's events.
    /// </summary>
    public interface IAppExtension
    {
        void OnLoad();
        void OnBeforeUnload();
    }

    /// <summary>
    /// Interface for Exchange-tampering extensions. 
    /// Technically, there's nothing you can do here that you can't do without
    /// hooking the application events, but this is a legacy approach.
    /// </summary>
    public interface IAutoTamper : IAppExtension
    {
        void OnPeekAtRequestHeaders(Exchange oExchange);
        void AutoTamperRequestBefore(Exchange oExchange);
        void AutoTamperRequestAfter(Exchange oExchange);
        void OnPeekAtResponseHeaders(Exchange oExchange);
        void AutoTamperResponseBefore(Exchange oExchange);
        void AutoTamperResponseAfter(Exchange oExchange);
        void OnBeforeReturningGeneratedError(Exchange oExchange);
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class OfferFormatAttribute : Attribute
    {
        private string _sFilenameExtensions;

        /// <summary>
        /// Marks a class supporting the specified Import/Export Format.
        /// </summary>
        /// <param name="sFormatName">Name of the Format (e.g. "Netlog JSON")</param>
        /// <param name="sDescription">Longer text description of the format</param>
        public OfferFormatAttribute(string sShortName, string sDescription)
                : this(sShortName, sDescription, null) { }

        /// <summary>
        /// Marks a class supporting the specified Import/Export Format.
        /// </summary>
        /// <param name="sFormatName">Name of the Format (e.g. "Netlog JSON")</param>
        /// <param name="sDescription">Longer text description of the format</param>
        /// <param name="sExtensions">Semicolon-delimited list of dot-prefixed filename extensions (e.g. ".json;.json.gz")</param>
        public OfferFormatAttribute(string sShortName, string sDescription, string sExtensions)
        {
            FormatName = sShortName;
            FormatDescription = sDescription;
            _sFilenameExtensions = sExtensions;
        }

        /// <summary>
        /// A short name for the format (e.g. "Netlog JSON")
        /// </summary>
        public string FormatName { get; private set; }

        /// <summary>
        /// Descriptive text explaining the format (e.g. what tools generate it, documentation urls, etc)
        /// </summary>
        public string FormatDescription { get; private set; }

        /// <summary>
        /// Array of filename extensions (dot-prefixed) that this format supports. e.g. [".json", "json.gz"]
        /// </summary>
        public string[] getFilenameExtensions()
        {
            if (String.IsNullOrEmpty(_sFilenameExtensions)) return new string[0];
            return _sFilenameExtensions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

    // Link the transcoder's object type to the metadata specified by its OfferFormatAttribute.
    public class TranscoderTuple
    {
        public Type typeTranscoder;
        private OfferFormatAttribute _ofa;

        internal TranscoderTuple(OfferFormatAttribute ofa, Type type)
        {
            _ofa = ofa;
            typeTranscoder = type;
        }

        // Does this Transcoder handle a given filename extension?
        internal bool HandlesFileExtension(string sDottedExt)
        {
            foreach (string s in _ofa.getFilenameExtensions())
            {
                if (sDottedExt.OICEquals(s)) return true;
            }
            return false;
        }

        public string FormatName
        {
            get { return _ofa.FormatName; }
        }
        public string FormatDescription
        {
            get { return _ofa.FormatDescription; }
        }
    }

    public interface IExchangeImporter : IDisposable
    {
        Exchange[] ImportExchanges(string sFormat,
                                    Dictionary<string, object> dictOptions,
                                    EventHandler<ProgressEventArgs> evtProgress);
    }

    public interface IExchangeExporter : IDisposable
    {
        bool ExportExchanges(string sFormat,
            Exchange[] arrExchanges,
            Dictionary<string, object> dictOptions, EventHandler<ProgressEventArgs> evtProgress);
    }

    public class ProgressEventArgs : EventArgs
    {
        private readonly int _CompletionPercentage;
        public bool CancellationRequested { get; set; }

        /// <summary>
        /// Info passed to the progress event.
        /// </summary>
        /// <param name="flCompletionRatio">Completion ratio, 0.0 to 1.0. Set to 0 if unknown.</param>
        /// <param name="sProgressText">Short explanation of current activity</param>
        public ProgressEventArgs(float flCompletion, string sCurrentStatus)
        {
            _CompletionPercentage = (int)Math.Truncate(100 * Math.Max(0, Math.Min(flCompletion, 1)));
            CurrentStatus = sCurrentStatus ?? string.Empty;
        }

        public string CurrentStatus { get; internal set; }

        public int CompletionPercentage
        {
            get
            {
                return _CompletionPercentage;
            }
        }
    }
}