#Requires -Version 5.1
param(
	[string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Debug'),
	[string]$ExtensionPath = '',
	[ValidateRange(1, 600)][int]$TimeoutSeconds = 60,
	[ValidateRange(-1, 100)][int]$ScreenIndex = -1,
	[ValidateSet('all', 'eager', 'moved', 'roundtrip')][string]$Scenario = 'all',
	[ValidateSet(0, 10, 12)][int]$FontSize = 0,
	[switch]$RuntimeFonts,
	[switch]$Extensions,
	[switch]$InventoryOnly,
	[switch]$ComponentsOnly,
	[string]$ResultsDirectory = ''
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	$windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
	$parameters = @('-NoProfile', '-NonInteractive', '-STA', '-File', $PSCommandPath, '-AutomatedTest', '-BuildDirectory', $BuildDirectory, '-TimeoutSeconds', $TimeoutSeconds, '-ScreenIndex', $ScreenIndex, '-Scenario', $Scenario, '-FontSize', $FontSize)
	if ($ExtensionPath) { $parameters += @('-ExtensionPath', $ExtensionPath) }
	if ($ResultsDirectory) { $parameters += @('-ResultsDirectory', $ResultsDirectory) }
	if ($RuntimeFonts) { $parameters += '-RuntimeFonts' }
	if ($Extensions) { $parameters += '-Extensions' }
	if ($InventoryOnly) { $parameters += '-InventoryOnly' }
	if ($ComponentsOnly) { $parameters += '-ComponentsOnly' }
	& $windowsPowerShell @parameters
	if ($LASTEXITCODE -ne 0) { throw "DPI tests failed with exit code $LASTEXITCODE" }
	return
}
$suiteTimer = [Diagnostics.Stopwatch]::StartNew()
$setupTimer = [Diagnostics.Stopwatch]::StartNew()
$timings = New-Object 'Collections.Generic.List[object]'
function Write-DpiTiming([string]$Phase, [double]$Seconds, [string]$Outcome = 'completed') {
	$timings.Add([pscustomobject]@{ Phase = $Phase; Seconds = [Math]::Round($Seconds, 3); Outcome = $Outcome })
	Write-Host ('Timing {0}: {1:N3}s ({2})' -f $Phase, $Seconds, $Outcome)
}
function Resolve-DpiFile([string]$Path, [string]$Purpose) {
	if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
		throw "Missing $Purpose at '$Path'. Build the matching solution configuration before running DPI tests; use -BuildDirectory and -ExtensionPath for custom outputs."
	}
	return (Resolve-Path -LiteralPath $Path).Path
}
if (-not (Test-Path -LiteralPath $BuildDirectory -PathType Container)) {
	throw "Build directory '$BuildDirectory' does not exist. Build the solution first and pass its Clearinet output directory with -BuildDirectory."
}
$directory = (Resolve-Path -LiteralPath $BuildDirectory).Path
$core = Resolve-DpiFile (Join-Path $directory 'Clearinet.exe') 'Clearinet assembly'
$coreConfig = Resolve-DpiFile ($core + '.config') 'Clearinet DPI configuration'
$configuration = Split-Path $directory -Leaf
if (-not $ExtensionPath) {
	if ($configuration -notin @('Debug', 'Release')) {
		throw "Cannot infer the extension configuration from '$directory'. Pass -ExtensionPath pointing to the matching CAE-Default.dll."
	}
	$ExtensionPath = Join-Path $PSScriptRoot "..\Extensions\bin\$configuration\CAE-Default.dll"
}
$extension = Resolve-DpiFile $ExtensionPath 'default extension assembly'
$manifest = Resolve-DpiFile (Join-Path $PSScriptRoot '..\Clearinet\app.manifest') 'DPI manifest'
$sourcePath = Resolve-DpiFile (Join-Path $PSScriptRoot 'DpiScalingHarness.cs') 'DPI harness source'
$null = [Reflection.Assembly]::LoadFrom($core)
$runId = [Guid]::NewGuid().ToString('N')
$artifacts = Join-Path $directory 'DpiTests'
$temp = Join-Path $artifacts ('work-' + $runId)
if (-not $ResultsDirectory) { $ResultsDirectory = Join-Path $artifacts ('results-' + $runId) }
$null = New-Item -ItemType Directory -Path $ResultsDirectory -Force
$ResultsDirectory = (Resolve-Path -LiteralPath $ResultsDirectory).Path
$source = [IO.File]::ReadAllText($sourcePath)

function Invoke-DpiProcess([string[]]$Arguments) {
	$process = New-Object Diagnostics.Process
	$process.StartInfo.FileName = $exe
	$process.StartInfo.Arguments = (@('-AutomatedTest') + $Arguments) -join ' '
	$process.StartInfo.WorkingDirectory = $temp
	$process.StartInfo.UseShellExecute = $false
	$process.StartInfo.EnvironmentVariables['TEMP'] = $temp
	$process.StartInfo.EnvironmentVariables['TMP'] = $temp
	$process.StartInfo.RedirectStandardOutput = $true
	$process.StartInfo.RedirectStandardError = $true
	$process.StartInfo.CreateNoWindow = $true
	$timer = [Diagnostics.Stopwatch]::StartNew()
	try {
		$null = $process.Start()
		$stdout = $process.StandardOutput.ReadToEndAsync()
		$stderr = $process.StandardError.ReadToEndAsync()
		$timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
		if ($timedOut) {
			$process.Kill()
			$process.WaitForExit()
		}
		$output = $stdout.GetAwaiter().GetResult()
		$errors = $stderr.GetAwaiter().GetResult()
		$timer.Stop()
		[pscustomobject]@{
			ExitCode = $process.ExitCode
			Output = $output
			Errors = $errors
			TimedOut = $timedOut
			ElapsedSeconds = $timer.Elapsed.TotalSeconds
		}
	}
	finally { $process.Dispose() }
}

try {
	$null = New-Item -ItemType Directory -Path $temp
	Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Extension -in '.dll', '.exe' } | Copy-Item -Destination $temp
	Copy-Item -LiteralPath $extension -Destination (Join-Path $temp 'CAE-Default.dll')
	$exe = Join-Path $temp 'DpiScalingTests.exe'
	$compiler = New-Object CodeDom.Compiler.CompilerParameters
	$compiler.TempFiles = New-Object CodeDom.Compiler.TempFileCollection($temp, $false)
	$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll'))
	$compiler.GenerateExecutable = $true
	$compiler.OutputAssembly = $exe
	$compiler.CompilerOptions = '/win32manifest:"' + $manifest + '"'
	$setupTimer.Stop()
	Write-DpiTiming 'setup' $setupTimer.Elapsed.TotalSeconds
	$compileTimer = [Diagnostics.Stopwatch]::StartNew()
	$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
	$compileOutcome = 'failed'
	try {
		$result = $provider.CompileAssemblyFromSource($compiler, $source)
		if ($result.Errors.HasErrors) { throw ($result.Errors | Out-String) }
		$compileOutcome = 'completed'
	}
	finally {
		$provider.Dispose()
		$compileTimer.Stop()
		Write-DpiTiming 'compilation' $compileTimer.Elapsed.TotalSeconds $compileOutcome
	}
	$setupTimer.Restart()
	Copy-Item -LiteralPath $coreConfig -Destination ($exe + '.config')
	$setupTimer.Stop()
	Write-DpiTiming 'configuration-copy' $setupTimer.Elapsed.TotalSeconds
	if ($ComponentsOnly) {
		$result = Invoke-DpiProcess @('-ComponentsOnly')
		$outcome = if ($result.TimedOut) { 'timeout' } elseif ($result.ExitCode -ne 0) { 'failed' } else { 'passed' }
		Write-DpiTiming 'components' $result.ElapsedSeconds $outcome
		$result.Output + $result.Errors | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'components.log') -Encoding UTF8
		Write-Host $result.Output
		if ($result.TimedOut -or $result.ExitCode -ne 0) { throw "DPI component checks $outcome`: $($result.Errors)" }
		return
	}
	$screens = Invoke-DpiProcess @('-ListScreens')
	$screenOutcome = if ($screens.TimedOut) { 'timeout' } elseif ($screens.ExitCode -ne 0) { 'failed' } else { 'passed' }
	Write-DpiTiming 'enumeration' $screens.ElapsedSeconds $screenOutcome
	if ($screens.TimedOut -or $screens.ExitCode -ne 0) { throw "Unable to enumerate monitors: $($screens.Errors)" }
	$screens.Output | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'monitors.log') -Encoding UTF8
	$screens.Output -split '\r?\n' | Where-Object { $_ -match '^Screen ' } | Write-Host
	$indices = @($screens.Output -split '\r?\n' | Where-Object { $_ -match '^\d+$' })
	$physicalIndices = @($screens.Output -split '\r?\n' | Where-Object { $_ -match '^Physical \d+$' } | ForEach-Object { [int]($_ -replace '^Physical ', '') })
	if ($ScreenIndex -ge 0) { $indices = @($physicalIndices | Where-Object { $_ -eq $ScreenIndex }) }
	if ($indices.Count -eq 0) { throw "No monitors matched ScreenIndex=$ScreenIndex. Use -InventoryOnly to list the physical screen indices on this interactive desktop." }
	$fontSizes = if ($FontSize) { @($FontSize) } else { @(10, 12) }
	$scenarios = if ($Scenario -eq 'all') { @('eager', 'moved', 'roundtrip') } else { @($Scenario) }
	$failed = 0
	$total = 0
	$caseCount = $indices.Count * $fontSizes.Count * $scenarios.Count
	$physicalCaseCount = $physicalIndices.Count * $fontSizes.Count * $scenarios.Count
	Write-Host "Real-monitor DPI tests: $($physicalIndices.Count) physical monitors, $($indices.Count) selected configurations; $caseCount cases (physical matrix: $physicalCaseCount). Detailed logs: $ResultsDirectory"
	if ($InventoryOnly) {
		Write-Host "Inventory only: selected physical indices $($indices -join ', '); no test scenarios launched."
		return
	}
	foreach ($index in $indices) {
		foreach ($testFontSize in $fontSizes) {
			foreach ($testScenario in $scenarios) {
				$name = "screen-$index-font-$testFontSize-$testScenario"
				$registryPath = 'SOFTWARE\Clearinet\Tests\Dpi-' + [Guid]::NewGuid().ToString('N')
				try {
					$arguments = @('-Screen', $index, '-Scenario', $testScenario, '-FontSize', $testFontSize, '-RegistryPath', $registryPath)
					if ($RuntimeFonts) { $arguments += '-RuntimeFonts' }
					if ($Extensions) { $arguments += '-Extensions' }
					$result = Invoke-DpiProcess $arguments
					$outcome = if ($result.TimedOut) { 'timeout' } elseif ($result.ExitCode -ne 0) { 'failed' } else { 'passed' }
					Write-DpiTiming $name $result.ElapsedSeconds $outcome
					$result.Output + $result.Errors | Set-Content -LiteralPath (Join-Path $ResultsDirectory ($name + '.log')) -Encoding UTF8
					$total++
					if ($result.TimedOut -or $result.ExitCode -ne 0) {
						$failed++
						Write-Host "FAIL $name (timeout=$($result.TimedOut))"
						$result.Errors -split '\r?\n' | Where-Object { $_ } | Select-Object -First 6 | Write-Host
					}
					else { Write-Host "PASS $name" }
					$result.Output -split '\r?\n' | Where-Object { $_ -match '^(Cold start:|DPI scaling:)' } | Write-Host
				}
				finally {
					if (Test-Path -LiteralPath "HKCU:\$registryPath") { Remove-Item -LiteralPath "HKCU:\$registryPath" -Recurse -Force }
				}
			}
		}
	}
	Write-Host "$total cold-start scenarios completed; $failed failed. Logs: $ResultsDirectory"
	if ($failed) { throw "$failed DPI scenarios failed; see $ResultsDirectory" }
}
finally {
	try {
		if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
	}
	finally {
		$suiteTimer.Stop()
		Write-DpiTiming 'total' $suiteTimer.Elapsed.TotalSeconds
		$timings | Export-Csv -LiteralPath (Join-Path $ResultsDirectory 'timings.csv') -NoTypeInformation -Encoding UTF8
	}
}
