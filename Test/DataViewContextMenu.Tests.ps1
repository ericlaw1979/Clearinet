#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Extensions\bin\Debug'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	throw 'Run with powershell.exe -NoProfile -STA -File .\Test\DataViewContextMenu.Tests.ps1'
}
$directory = (Resolve-Path -LiteralPath $BuildDirectory).Path
Add-Type -AssemblyName System.Windows.Forms
$null = [Reflection.Assembly]::LoadFrom((Join-Path $directory 'Clearinet.exe'))
$null = [Reflection.Assembly]::LoadFrom((Join-Path $directory 'CAE-Default.dll'))
$instanceMembers = [Reflection.BindingFlags]'Instance, NonPublic'
$staticMembers = [Reflection.BindingFlags]'Static, NonPublic'
$script:assertions = 0
function Check([bool]$condition, [string]$message) {
	$script:assertions++
	if (-not $condition) { throw $message }
}
function InvokeHidden($target, [string]$name, [object[]]$arguments) {
	for ($i = 0; $i -lt $arguments.Length; $i++) {
		$arguments[$i] = $arguments[$i].PSObject.BaseObject
	}
	$target.GetType().GetMethod($name, $instanceMembers).Invoke($target, $arguments)
}
function OpenMenu {
	InvokeHidden $menu 'OnOpening' @((New-Object System.ComponentModel.CancelEventArgs))
}
$clipboard = [Windows.Forms.Clipboard]::GetDataObject()
$view = New-Object Extensions.DataView
$form = New-Object Windows.Forms.Form
$viewer = $null
$wizard = $null
$mainField = [Clearinet.CApp].GetField('_frmMain', $staticMembers)
$overridesField = [Clearinet.CONFIG].GetField('Overrides', $staticMembers)
$oldMain = $mainField.GetValue($null)
$oldOverrides = $overridesField.GetValue($null)
$oldEngine = [Clearinet.CApp]::scriptRules
try {
	$form.ShowInTaskbar = $false
	$form.Controls.Add($view)
	$tree = $view.GetType().GetField('tree', $instanceMembers).GetValue($view)
	$menu = $tree.ContextMenuStrip
	Check ($null -ne $menu -and $menu.Items.Count -eq 3) 'Three context actions are attached to the tree'
	Check ($menu.Items[0].Text.Replace('&', '') -ceq 'Copy') 'Copy label'
	Check ($menu.Items[1].Text.Replace('&', '') -ceq 'Copy Subtree') 'Copy Subtree label'
	Check ($menu.Items[2].Text.Replace('&', '') -ceq 'Send to Textransformer') 'Textransformer label'
	OpenMenu
	Check (@($menu.Items | Where-Object Enabled).Count -eq 0) 'All actions disabled without a selection'

	$root = $tree.Nodes.Add('root')
	$branch = $root.Nodes.Add('branch')
	$null = $branch.Nodes.Add('first')
	$nested = $branch.Nodes.Add('nested')
	$null = $nested.Nodes.Add('deep')
	$null = $branch.Nodes.Add('last')
	$sibling = $root.Nodes.Add('outside')
	InvokeHidden $view 'ShowTree' @()
	$form.Show()
	$tree.ExpandAll()
	$tree.SelectedNode = $sibling
	$bounds = $branch.Bounds
	InvokeHidden $tree 'OnMouseDown' @((New-Object Windows.Forms.MouseEventArgs([Windows.Forms.MouseButtons]::Right, 1, ($bounds.X + 2), ($bounds.Y + 2), 0)))
	Check ($tree.SelectedNode -eq $branch) 'Right-click selects the clicked node rather than the previous selection'
	$branch.Collapse()
	OpenMenu
	Check (@($menu.Items | Where-Object Enabled).Count -eq 3) 'All actions enabled with a selection'
	$menu.Items[0].PerformClick()
	Check ([Windows.Forms.Clipboard]::GetText() -ceq 'branch') 'Copy copies only the selected node text'
	$menu.Items[1].PerformClick()
	Check ([Windows.Forms.Clipboard]::GetText() -ceq "branch`r`n`tfirst`r`n`tnested`r`n`t`tdeep`r`n`tlast") 'Copy Subtree includes collapsed descendants in order, indented relative to the selection, without siblings'
	$tree.SelectedNode = $sibling
	$menu.Items[1].PerformClick()
	Check ([Windows.Forms.Clipboard]::GetText() -ceq 'outside') 'A leaf subtree contains only that node without a trailing newline'
	$key = New-Object Windows.Forms.KeyEventArgs([Windows.Forms.Keys]::Control -bor [Windows.Forms.Keys]::C)
	InvokeHidden $tree 'OnKeyDown' @($key)
	Check ($key.SuppressKeyPress -and $key.Handled -and [Windows.Forms.Clipboard]::GetText() -ceq 'outside') 'Ctrl+C preserves single-node copying'
	$empty = $root.Nodes.Add('')
	$null = $empty.Nodes.Add('value')
	$tree.SelectedNode = $empty
	$menu.Items[1].PerformClick()
	Check ([Windows.Forms.Clipboard]::GetText() -ceq "`r`n`tvalue") 'Copy Subtree preserves an empty node label as its own line'

	$overrides = $overridesField.GetValue($null)
	$overrides.GetType().GetField('DisableScript', $instanceMembers).SetValue($overrides, $true)
	$overridesField.SetValue($null, $overrides)
	[Clearinet.CApp]::scriptRules = [Activator]::CreateInstance([Clearinet.JScriptEngine], $instanceMembers, $null, @('unused'), $null)
	$viewer = New-Object Clearinet.frmViewer
	$mainField.SetValue($null, $viewer)
	$tree.SelectedNode = $branch
	$menu.Items[2].PerformClick()
	$wizard = @($viewer.OwnedForms | Where-Object { $_.GetType().Name -eq 'frmTextWizard' })[0]
	Check ($null -ne $wizard -and $wizard.Visible) 'Send to Textransformer opens the tool'
	$input = $wizard.GetType().GetField('txtInput', $instanceMembers).GetValue($wizard)
	Check ($input.Text -ceq 'branch') 'Textransformer receives only the current node text'

	InvokeHidden $tree 'OnMouseDown' @((New-Object Windows.Forms.MouseEventArgs([Windows.Forms.MouseButtons]::Right, 1, ($tree.ClientSize.Width - 2), ($tree.ClientSize.Height - 2), 0)))
	OpenMenu
	Check ($null -eq $tree.SelectedNode -and @($menu.Items | Where-Object Enabled).Count -eq 0) 'Right-clicking empty space clears selection and disables the actions'
	InvokeHidden $tree 'OnKeyDown' @((New-Object Windows.Forms.KeyEventArgs([Windows.Forms.Keys]::Space)))
	$view.Clear()
	OpenMenu
	Check (@($menu.Items | Where-Object Enabled).Count -eq 0) 'Clearing the document disables the actions'
	$view.Dispose()
	Check $menu.IsDisposed 'Disposing DataView disposes its context menu'
	Write-Output "DataView context menu assertions passed: $script:assertions"
}
finally {
	if ($null -ne $wizard) { $wizard.Dispose() }
	if ($null -ne $viewer) { $viewer.Dispose() }
	$mainField.SetValue($null, $oldMain)
	$overridesField.SetValue($null, $oldOverrides)
	[Clearinet.CApp]::scriptRules = $oldEngine
	$form.Dispose()
	$view.Dispose()
	if ($null -ne $clipboard) { [Windows.Forms.Clipboard]::SetDataObject($clipboard, $true) }
	else { [Windows.Forms.Clipboard]::Clear() }
}
