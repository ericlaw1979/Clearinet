# Copilot Instructions

## Project Guidelines

* User expects JScript.NET-compatible examples for Clearinet scripts; avoid C#-style delegate constructor syntax that does not compile in JScript.NET.
* When providing JScript.NET interop examples, avoid anonymous function-to-delegate assignment because it doesn't work.
* When launching or initializing Clearinet.exe for a test, always include the command line argument `-AutomatedTest` as the first argument to suppress interactive warnings.
* Keep `CaptureEngine.Tests` independent of `Extensions.csproj`; its performance inspector tests compile the relevant extension source as a linked file. Do not add a project reference to Extensions for those tests.
* Clearinet and Extensions use classic .NET Framework resource generation to avoid shipping `System.Resources.Extensions`. Build with Visual Studio or .NET Framework `MSBuild.exe`; do not enable preserialized resources or add that package to work around `dotnet build`/`dotnet test` resource errors. After a successful Visual Studio or MSBuild build, use `dotnet test --no-build` to run tests.

