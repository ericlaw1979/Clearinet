#Requires -Version 5.1
param(
    [switch]$AutomatedTest,
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release')
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run with powershell.exe -NoProfile -NonInteractive -File .\Test\SuggestedFilename.Tests.ps1 -AutomatedTest'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$tests = @'
using System;
using System.IO;
using System.Reflection;
using Clearinet;

public static class SuggestedFilenameTests
{
    private static int assertions;
    private static void Equal(string expected, string actual, string scenario, bool validateFilename = true)
    {
        ++assertions;
        if (expected != actual)
            throw new Exception(scenario + ": expected [" + expected + "], got [" + actual + "].");
        if (validateFilename && (actual.Length == 0 || actual.Length > 96 || actual.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            actual.EndsWith(".", StringComparison.Ordinal) || actual.EndsWith(" ", StringComparison.Ordinal)))
            throw new Exception(scenario + ": filename is not Windows-safe.");
    }
    private static Exchange Create(string path, string disposition, string contentType)
    {
        Exchange exchange = new Exchange(new HTTPRequestHeaders(), new byte[0]);
        exchange.fullUrl = "https://example.test" + path;
        typeof(Exchange).GetMethod("EnsureID", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(exchange, null);
        exchange.ResponseHeaders = new HTTPResponseHeaders();
        if (disposition != null) exchange.ResponseHeaders["Content-Disposition"] = disposition;
        if (contentType != null) exchange.ResponseHeaders["Content-Type"] = contentType;
        return exchange;
    }
    private static void Header(string value, string expected)
    {
        Exchange exchange = Create("/url.txt", value, "application/pdf");
        Equal(expected, exchange.SuggestedFilename, value);
        Equal(value, exchange.ResponseHeaders["Content-Disposition"], "Header unchanged", false);
        Equal("https://example.test/url.txt", exchange.fullUrl, "URL unchanged", false);
    }
    public static void Run()
    {
        Exchange pending = Create("/url.pdf", null, "application/pdf");
        pending.ResponseHeaders = null;
        Equal(pending.id + ".txt", pending.SuggestedFilename, "No response overrides URL");
        Exchange empty = new Exchange(null, null);
        Equal("0.txt", empty.SuggestedFilename, "No request or response");
        Header("attachment; filename=plain.txt", "plain.txt");
        Header("inline; FILENAME=\"quoted name.txt\"", "quoted name.txt");
        Header("attachment; filename='singlequoted.txt'", "singlequoted.txt");
        Header("attachment; filename=\"'quoted name.txt'\"", "quoted name.txt");
        Header("attachment; filename='O'Brien.txt'", "O'Brien.txt");
        Header("attachment; filename=O'Brien.txt", "O'Brien.txt");
        Header("attachment; filename='unmatched.txt", "'unmatched.txt");
        Header("attachment; filename=unmatched.txt'", "unmatched.txt'");
        Header("attachment; filename=''", "url.txt");
        Header("attachment; filename='plain.txt'; filename*=UTF-8''preferred.txt", "preferred.txt");
        Header("attachment; filename=\"semi;colon.txt\"", "semi;colon.txt");
        Header("attachment; filename=\"a\\\"b.txt\"", "a_b.txt");
        Header("attachment; filename=\"a\\\\b.txt\"", "a_b.txt");
        Header("attachment; filename=plain.txt; filename*=UTF-8''caf%C3%A9%20report.txt", "caf\u00e9 report.txt");
        Header("attachment; filename*=UTF-8'en'%E2%82%AC.txt; filename=plain.txt", "\u20ac.txt");
        Header("attachment; filename*=iso-8859-1''caf%E9.txt", "caf\u00e9.txt");
        Header("attachment; filename*=UTF-8''a+b%2520.txt", "a+b%20.txt");
        Header("attachment; filename*=unknown''bad.txt; filename=plain.txt", "plain.txt");
        Header("attachment; filename*=UTF-8''; filename=plain.txt", "plain.txt");
        Header("attachment; filename=\"\"", "url.txt");
        Header("not a valid disposition", "url.txt");
        Header("attachment", "url.txt");
        Header("attachment; filename*=UTF-8''%3C%3E%3A%22%2F%5C%7C%3F%2A%00%1F.txt", new string('_', 11) + ".txt");
        Header("attachment; filename=\"trailing. \"", "trailing__");
        Header("attachment; filename=\"..\"", "__");
        Header("attachment; filename=\" \"", "_");
        foreach (string device in new[] { "CON", "prn", "AUX", "nul", "COM1", "COM9", "LPT1", "lpt9", "COM\u00b9", "LPT\u00b2", "CONIN$", "CONOUT$" })
            Header("attachment; filename=\"" + device + ".txt\"", "_" + device + ".txt");
        Header("attachment; filename=\"CON .txt\"", "_CON .txt");
        Header("attachment; filename=COM0.txt", "COM0.txt");
        Header("attachment; filename=LPT10.txt", "LPT10.txt");
        Header("attachment; filename=\"" + new string('x', 92) + ".txt\"", new string('x', 92) + ".txt");
        Header("attachment; filename=\"" + new string('x', 93) + ".txt\"", new string('x', 92) + ".txt");
        Header("attachment; filename=\"" + new string('x', 150) + ".json\"", new string('x', 91) + ".json");
        Header("attachment; filename=\"" + new string('x', 97) + "\"", new string('x', 96));
        Header("attachment; filename=\"x." + new string('y', 120) + "\"", "x." + new string('y', 94));
        Header("attachment; filename=\"" + new string('x', 95) + ".tail.\"", new string('x', 95) + "_");
        Header("attachment; filename*=UTF-8''" + new string('x', 91) + "%F0%9F%98%80.txt", new string('x', 91) + ".txt");
        foreach (string path in new[] { "/dir/report%20one.pdf?download=other.txt#fragment", "/dir/report%20one.pdf#fragment" })
            Equal("report one.pdf", Create(path, null, "text/plain").SuggestedFilename, "URL ignores query and fragment");
        Equal("download", Create("/dir/download?q=1", null, "application/pdf").SuggestedFilename, "Extensionless URL");
        Equal("a_b_c.txt", Create("/dir/a%2Fb%5Cc.txt", null, null).SuggestedFilename, "Decoded URL separators");
        Equal("a+b.txt", Create("/a+b.txt", null, null).SuggestedFilename, "URL plus is literal");
        foreach (string[] mapping in new[] {
            new[] { "", ".txt" }, new[] { "text/plain", ".txt" }, new[] { "text/html", ".html" },
            new[] { " IMAGE/PNG ; charset=binary", ".png" }, new[] { "image/jpeg", ".jpg" },
            new[] { "image/svg+xml", ".svg" }, new[] { "application/pdf", ".pdf" },
            new[] { "application/json", ".json" }, new[] { "application/problem+json", ".json" },
            new[] { "application/atom+xml", ".xml" }, new[] { "application/zip", ".zip" },
            new[] { "text/javascript", ".js" }, new[] { "application/wasm", ".wasm" },
            new[] { "audio/mpeg", ".mp3" }, new[] { "video/mp4", ".mp4" }, new[] { "font/woff2", ".woff2" },
            new[] { "text/unknown", ".txt" }, new[] { "application/octet-stream", ".bin" },
            new[] { "application/unknown", ".bin" } })
        {
            Exchange exchange = Create("/dir/?filename=query.txt", null, mapping[0]);
            Equal(exchange.id + mapping[1], exchange.SuggestedFilename, "Media type " + mapping[0]);
        }
        Exchange missingType = Create("/", null, null);
        Equal(missingType.id + ".txt", missingType.SuggestedFilename, "Missing Content-Type");
        missingType.MediaType = "application/pdf";
        Equal(missingType.id + ".pdf", missingType.SuggestedFilename, "Getter reflects changed headers");
        Exchange data = Create("/", null, "image/png");
        data.fullUrl = "data:image/png;base64,AAAA";
        Equal(data.id + ".png", data.SuggestedFilename, "Data URL has no filename");
        Console.WriteLine("Passed {0} suggested filename assertions.", assertions);
    }
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[SuggestedFilenameTests]::Run()
