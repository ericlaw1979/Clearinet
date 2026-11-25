using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Clearinet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace Clearinet.Tests
{
    [TestClass]
    public sealed class ScriptingTests
    {
#if TEST_MSIX
        private static readonly bool ExpectMsixBuild = true;
#else
        private static readonly bool ExpectMsixBuild = false;
#endif
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

            if (ExpectMsixBuild) Assert.IsNull(CApp.scriptRules);
            else Assert.IsNotNull(CApp.scriptRules);
            Assert.IsFalse(CApp.RunExecAction("sample-command"));
            Assert.AreEqual("class Handlers {}", File.ReadAllText(path));
        }

#if TEST_MSIX
        [TestMethod]
        [DataRow("", DisplayName = "Empty script path")]
        [DataRow(" ", DisplayName = "Whitespace script path")]
        public void CreateScriptEngine_EmptyPath_DoesNotCreateEngineOrFiles(string path)
        {
            CApp.Prefs.SetStringPref("app.paths.rules.script", path);

            CApp.CreateScriptEngine();

            Assert.IsNull(CApp.scriptRules);
            Assert.IsFalse(Directory.Exists(_testDirectory));
            Assert.IsFalse(CApp.RunExecAction("unknown-script-command"));
        }
#endif

        [STATestMethod]
        [DataRow(true, DisplayName = "Delayed rules creation")]
        [DataRow(false, DisplayName = "Immediate rules creation")]
        public void CreateScriptEngine_MissingUserScript_UsesSampleOnlyWhenUnpackaged(bool delayCreate)
        {
            Directory.CreateDirectory(_testDirectory);
            string scriptPath = Path.Combine(_testDirectory, "UserRules.js");
            string samplePath = Path.Combine(_testDirectory, "SampleRules.js");
            File.WriteAllText(samplePath, SampleScript);
            CApp.Prefs.SetStringPref("app.paths.rules.script", scriptPath);
            CApp.Prefs.SetStringPref("app.paths.rules.sample_script", samplePath);
            CApp.Prefs.SetBoolPref("scripting.delay_create", delayCreate);

            CApp.CreateScriptEngine();

            Assert.AreEqual(!ExpectMsixBuild, CApp.RunExecAction("sample-command"));
            if (!ExpectMsixBuild && !delayCreate)
                Assert.AreEqual(SampleScript, File.ReadAllText(scriptPath));
            else
                Assert.IsFalse(File.Exists(scriptPath));
        }

        [STATestMethod]
        public void CustomizeRules_MissingUserScript_OpensEditorAndCopiesSampleOnlyWhenUnpackaged()
        {
            Directory.CreateDirectory(_testDirectory);
            string scriptPath = Path.Combine(_testDirectory, "My Rules.js");
            string samplePath = Path.Combine(_testDirectory, "SampleRules.js");
            File.WriteAllText(samplePath, SampleScript);
            CApp.Prefs.SetStringPref("app.paths.rules.script", scriptPath);
            CApp.Prefs.SetStringPref("app.paths.rules.sample_script", samplePath);
            CApp.Prefs.SetBoolPref("scripting.delay_create", true);
            CApp.Prefs.SetStringPref("user.paths.editors.code", "test-editor.exe");
            CApp.CreateScriptEngine();
            Assert.IsFalse(File.Exists(scriptPath));

            int launches = 0;
            frmViewer.CustomizeRules((editor, arguments) =>
            {
                launches++;
                Assert.AreEqual("test-editor.exe", editor);
                Assert.AreEqual($"\"{scriptPath}\"", arguments);
                if (ExpectMsixBuild) Assert.IsFalse(File.Exists(scriptPath));
                else Assert.AreEqual(SampleScript, File.ReadAllText(scriptPath));
            });

            Assert.AreEqual(1, launches);
        }

#if TEST_MSIX
        [TestMethod]
        [DataRow(false, DisplayName = "Missing script directory")]
        [DataRow(true, DisplayName = "Missing script file")]
        public void CreateScriptEngine_MissingScript_DoesNotCreateEngineOrScript(bool directoryExists)
        {
            if (directoryExists) Directory.CreateDirectory(_testDirectory);
            string path = Path.Combine(_testDirectory, "CustomRules.js");
            CApp.Prefs.SetStringPref("app.paths.rules.script", path);

            CApp.CreateScriptEngine();

            Assert.IsNull(CApp.scriptRules);
            Assert.IsFalse(File.Exists(path));
            Assert.AreEqual(directoryExists, Directory.Exists(_testDirectory));
            Assert.IsFalse(CApp.RunExecAction("unknown-script-command"));
        }
#endif

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

#if TEST_MSIX
        [TestMethod]
        public void CreateScriptEngine_InvalidScript_ReportsCompilationFailure()
        {
            Directory.CreateDirectory(_testDirectory);
            string path = Path.Combine(_testDirectory, "InvalidRules.js");
            File.WriteAllText(path, "this is not valid JScript.NET");
            CApp.Prefs.SetStringPref("app.paths.rules.script", path);

            var error = Assert.ThrowsException<InvalidOperationException>(() => CApp.CreateScriptEngine());

            StringAssert.Contains(error.Message, "JScript Compilation Errors:");
            Assert.IsNull(CApp.scriptRules);
            Assert.AreEqual("this is not valid JScript.NET", File.ReadAllText(path));
        }

        [TestMethod]
        public void CreateScriptEngine_UnreadableScript_DoesNotTreatItAsMissing()
        {
            Directory.CreateDirectory(_testDirectory);
            string path = Path.Combine(_testDirectory, "LockedRules.js");
            File.WriteAllText(path, "class Handlers {}");
            CApp.Prefs.SetStringPref("app.paths.rules.script", path);
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.ThrowsException<IOException>(() => CApp.CreateScriptEngine());
            }
            Assert.IsNull(CApp.scriptRules);
        }
#endif

        [STATestMethod]
        public void ReloadScript_DeletedUserScript_UsesSampleOnlyWhenUnpackaged()
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
            Assert.AreEqual(!ExpectMsixBuild, CApp.RunExecAction("sample-command"));
            Assert.IsFalse(File.Exists(path));
            CollectionAssert.AreEqual(new[] { samplePath }, Directory.GetFiles(_testDirectory));
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
