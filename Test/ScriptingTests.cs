using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Clearinet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace Clearinet.Tests
{
    [TestClass]
    public sealed class ScriptingTests
    {
        private const string SampleScript = @"import System;
class Handlers {
    public static function OnExecAction(args: String[]): boolean { return args[0] == ""sample-command""; }
}";

        private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Clearinet.Tests", Guid.NewGuid().ToString("N"));
        private readonly PreferenceBag _originalPreferences;
        private readonly bool _originalScriptDisabled;
        private readonly CONFIG.classPolicies _originalPolicies;

        public ScriptingTests()
        {
            _originalPreferences = CONFIG.pbPrefs;
            _originalScriptDisabled = CONFIG.OVERRIDES.DisableScript;
            CONFIG.OVERRIDES.DisableScript = false;
            _originalPolicies = CONFIG.POLICIES;
            CONFIG.POLICIES = new CONFIG.classPolicies();
            typeof(CONFIG).GetProperty("pbPrefs", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, new PreferenceBag(null));
            CApp.scriptRules = null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            CApp.scriptRules?.Dispose();
            CApp.scriptRules = null;
            CApp._frmMain?.Dispose();
            CApp._frmMain = null;
            CONFIG.OVERRIDES.DisableScript = _originalScriptDisabled;
            CONFIG.POLICIES = _originalPolicies;
            typeof(CONFIG).GetProperty("pbPrefs", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, _originalPreferences);
            if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
        }

        [TestMethod]
        public void RulesScript_DefaultPath_UsesOptionalUserFile()
        {
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                                           "Clearinet", "CustomRules_0_1_0_0.js");
            Assert.AreEqual(expected, CONFIG.GetPath("RulesScript"));
        }

        [TestMethod]
        public void RulesScript_ConfiguredPreference_UsesExplicitUserPath()
        {
            string path = Path.Combine(_testDirectory, "UserRules.js");
            CApp.Prefs.SetStringPref("app.paths.rules.script", path);
            Assert.AreEqual(path, CONFIG.GetPath("RulesScript"));
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Command-line scripting restriction")]
        [DataRow(true, DisplayName = "System-policy scripting restriction")]
        public void CreateScriptEngine_Disabled_DoesNotReadScript(bool byPolicy)
        {
            Directory.CreateDirectory(_testDirectory);
            string path = Path.Combine(_testDirectory, "LockedRules.js");
            File.WriteAllText(path, "class Handlers {}");
            CApp.Prefs.SetStringPref("app.paths.rules.script", path);
            CONFIG.OVERRIDES.DisableScript = !byPolicy;
            CONFIG.POLICIES.DisableScript = byPolicy;

            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                CApp.CreateScriptEngine();
            }

            Assert.IsFalse(CApp.RunExecAction("sample-command"));
            Assert.AreEqual("class Handlers {}", File.ReadAllText(path));
        }

        [STATestMethod]
        [DataRow(true, DisplayName = "Delayed rules creation")]
        [DataRow(false, DisplayName = "Immediate rules creation")]
        public void CreateScriptEngine_CleanProfile_LoadsSampleAndHonorsDelayedCreation(bool delayCreate)
        {
            Directory.CreateDirectory(_testDirectory);
            string scriptDirectory = Path.Combine(_testDirectory, "User Profile");
            string scriptPath = Path.Combine(scriptDirectory, "UserRules.js");
            string samplePath = Path.Combine(_testDirectory, "SampleRules.js");
            File.WriteAllText(samplePath, SampleScript);
            CApp.Prefs.SetStringPref("app.paths.rules.script", scriptPath);
            CApp.Prefs.SetStringPref("app.paths.rules.sample_script", samplePath);
            CApp.Prefs.SetBoolPref("scripting.delay_create", delayCreate);

            CApp.CreateScriptEngine();

            Assert.IsTrue(CApp.RunExecAction("sample-command"));
            Assert.IsTrue(Directory.Exists(scriptDirectory));
            if (delayCreate) Assert.IsFalse(File.Exists(scriptPath));
            else Assert.AreEqual(SampleScript, File.ReadAllText(scriptPath));
        }

        [STATestMethod]
        [DataRow(false, DisplayName = "Missing user rules")]
        [DataRow(true, DisplayName = "Existing user rules")]
        public void CustomizeRules_OpensEditorAndNeverOverwritesUserScript(bool hasUserScript)
        {
            Directory.CreateDirectory(_testDirectory);
            string scriptDirectory = Path.Combine(_testDirectory, "User Profile");
            string scriptPath = Path.Combine(scriptDirectory, "My Rules.js");
            string samplePath = Path.Combine(_testDirectory, "SampleRules.js");
            File.WriteAllText(samplePath, SampleScript);
            const string userScript = "class Handlers {}";
            if (hasUserScript)
            {
                Directory.CreateDirectory(scriptDirectory);
                File.WriteAllText(scriptPath, userScript);
            }
            CApp.Prefs.SetStringPref("app.paths.rules.script", scriptPath);
            CApp.Prefs.SetStringPref("app.paths.rules.sample_script", samplePath);
            CApp.Prefs.SetBoolPref("scripting.delay_create", true);
            CApp.Prefs.SetStringPref("user.paths.editors.code", "test-editor.exe");
            CApp.CreateScriptEngine();
            Assert.AreEqual(hasUserScript, File.Exists(scriptPath));

            int launches = 0;
            frmViewer.CustomizeRules((editor, arguments) =>
            {
                launches++;
                Assert.AreEqual("test-editor.exe", editor);
                Assert.AreEqual($"\"{scriptPath}\"", arguments);
                Assert.AreEqual(hasUserScript ? userScript : SampleScript, File.ReadAllText(scriptPath));
            });

            Assert.AreEqual(1, launches);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Shutdown allowed by subscriber")]
        [DataRow(true, DisplayName = "Shutdown cancelled by subscriber")]
        public void OnBeforeShutdown_NoScript_PreservesSubscriberDecision(bool cancel)
        {
            int calls = 0;
            CancelEventHandler handler = (sender, args) => { calls++; args.Cancel = cancel; };
            CApp.BeforeAppShutdown += handler;
            try
            {
                Assert.AreEqual(!cancel, CApp.OnBeforeShutdown());
                Assert.AreEqual(1, calls);
            }
            finally
            {
                CApp.BeforeAppShutdown -= handler;
            }
        }

        [TestMethod]
        public void OnBeforeShutdown_NoScriptOrSubscribers_AllowsShutdown()
        {
            Assert.IsTrue(CApp.OnBeforeShutdown());
        }

        [TestMethod]
        public void ProxyCallbacks_NoScript_PreserveLifecycleEvents()
        {
            int attaches = 0;
            int detaches = 0;
            Action onAttach = () => attaches++;
            Action onDetach = () => detaches++;
            CApp.ProxyAttach += onAttach;
            CApp.ProxyDetach += onDetach;
            try
            {
                CApp.OnProxyAttach();
                CApp.OnProxyDetach();
                Assert.AreEqual(1, attaches);
                Assert.AreEqual(1, detaches);
            }
            finally
            {
                CApp.ProxyAttach -= onAttach;
                CApp.ProxyDetach -= onDetach;
            }
        }

        [STATestMethod]
        public void CreateScriptEngine_UserScript_PreservesCommandsAndShutdownVeto()
        {
            CApp._frmMain = new frmViewer();
            Directory.CreateDirectory(_testDirectory);
            string path = Path.Combine(_testDirectory, "UserRules.js");
            const string script = @"import System;
class Handlers {
    public static function OnExecAction(args: String[]): boolean { return args[0] == ""script-test""; }
    public static function OnBeforeShutdown(): boolean { return false; }
}";
            File.WriteAllText(path, script);
            CApp.Prefs.SetStringPref("app.paths.rules.script", path);

            CApp.CreateScriptEngine();

            Assert.IsNotNull(CApp.scriptRules);
            Assert.IsTrue(CApp.RunExecAction("script-test"));
            Assert.IsFalse(CApp.RunExecAction("other-command"));
            Assert.IsFalse(CApp.OnBeforeShutdown());
            Assert.AreEqual(script, File.ReadAllText(path));
            CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(_testDirectory));
        }

        [STATestMethod]
        public void ReloadScript_DeletedUserScript_LoadsSampleWithoutRecreatingUserFile()
        {
            Directory.CreateDirectory(_testDirectory);
            string path = Path.Combine(_testDirectory, "UserRules.js");
            string samplePath = Path.Combine(_testDirectory, "SampleRules.js");
            const string script = @"import System;
class Handlers {
    public static function OnExecAction(args: String[]): boolean { return args[0] == ""script-test""; }
}";
            File.WriteAllText(path, script);
            File.WriteAllText(samplePath, SampleScript);
            CApp.Prefs.SetStringPref("app.paths.rules.script", path);
            CApp.Prefs.SetStringPref("app.paths.rules.sample_script", samplePath);
            CApp.CreateScriptEngine();
            Assert.IsTrue(CApp.RunExecAction("script-test"));

            File.Delete(path);
            CApp.scriptRules.ReloadScript();

            Assert.IsFalse(CApp.RunExecAction("script-test"));
            Assert.IsTrue(CApp.RunExecAction("sample-command"));
            Assert.IsFalse(File.Exists(path));
            CollectionAssert.AreEqual(new[] { samplePath }, Directory.GetFiles(_testDirectory));
        }

        [STATestMethod]
        public void RulesWatcher_FirstSaveAfterCustomize_ReloadsUserScript()
        {
            CApp._frmMain = new frmViewer();
            _ = CApp._frmMain.Handle;
            Directory.CreateDirectory(_testDirectory);
            string path = Path.Combine(_testDirectory, "User Profile", "UserRules.js");
            string samplePath = Path.Combine(_testDirectory, "SampleRules.js");
            File.WriteAllText(samplePath, SampleScript);
            CApp.Prefs.SetStringPref("app.paths.rules.script", path);
            CApp.Prefs.SetStringPref("app.paths.rules.sample_script", samplePath);
            CApp.Prefs.SetInt32Pref("scripting.msReloadDebounce", 0);
            CApp.CreateScriptEngine();
            Assert.IsTrue(CApp.RunExecAction("sample-command"));
            Assert.IsFalse(File.Exists(path));

            frmViewer.CustomizeRules((_, __) =>
                File.WriteAllText(path, SampleScript.Replace("sample-command", "saved-command")));

            var elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < TimeSpan.FromSeconds(10) && !CApp.RunExecAction("saved-command"))
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            Assert.IsTrue(CApp.RunExecAction("saved-command"), "Saving the customized script did not reload it.");
            Assert.IsFalse(CApp.RunExecAction("sample-command"));
        }

        [STATestMethod]
        public void TextWizard_NoScript_PreservesBuiltInTransformations()
        {
            using (var wizard = new frmTextWizard("hello"))
            {
                var transforms = (ComboBox)wizard.Controls.Find("cbxTransforms", true)[0];
                var output = wizard.Controls.Find("txtOutput", true)[0];

                transforms.SelectedIndex = 0;

                Assert.AreEqual("aGVsbG8=", output.Text);
                Assert.IsNull(CApp.scriptRules);
            }
        }
    }
}
