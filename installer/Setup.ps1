param([string]$GameDir='')
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
. "$PSScriptRoot\Core.ps1"
[Windows.Forms.Application]::EnableVisualStyles()
$form = New-Object Windows.Forms.Form
$form.Text = 'Gunfire Reborn Character Sheet'; $form.Size = New-Object Drawing.Size(820,690)
$form.StartPosition = 'CenterScreen'; $form.MinimumSize=$form.Size
$pathLabel=New-Object Windows.Forms.Label; $pathLabel.Text='Gunfire Reborn folder';$pathLabel.SetBounds(20,16,760,24)
$path=New-Object Windows.Forms.ComboBox; $path.SetBounds(20,42,640,28)
foreach($candidate in @(Find-GameFolders | Select-Object -Unique)){[void]$path.Items.Add($candidate)}
if($GameDir){$path.Text=$GameDir}elseif($path.Items.Count){$path.SelectedIndex=0}
$browse=New-Object Windows.Forms.Button;$browse.Text='Browse...';$browse.SetBounds(672,40,108,30)
$browse.Add_Click({$dialog=New-Object Windows.Forms.FolderBrowserDialog;if($dialog.ShowDialog() -eq 'OK'){$path.Text=$dialog.SelectedPath}})
$check=New-Object Windows.Forms.Button;$check.Text='Check installation';$check.SetBounds(20,82,160,32)
$grid=New-Object Windows.Forms.DataGridView;$grid.SetBounds(20,126,760,138);$grid.ReadOnly=$true;$grid.AllowUserToAddRows=$false;$grid.AllowUserToDeleteRows=$false;$grid.RowHeadersVisible=$false;$grid.AutoSizeColumnsMode='Fill';$grid.SelectionMode='FullRowSelect';$grid.MultiSelect=$false
foreach($column in @('Prerequisite','Tested version','Status')){[void]$grid.Columns.Add($column,$column)}
$link=New-Object Windows.Forms.Button;$link.Text='Open selected prerequisite page';$link.SetBounds(20,276,260,32)
$link.Add_Click({if($grid.SelectedRows.Count){$row=$grid.SelectedRows[0];Start-Process ([string]$row.Tag)}})
$help=New-Object Windows.Forms.TextBox;$help.Multiline=$true;$help.ReadOnly=$true;$help.ScrollBars='Vertical';$help.SetBounds(20,318,760,112)
$help.Text="The installer does not download or install prerequisites. Select a component and open its official page for download instructions. Install it manually, then check again.`r`nWindows Steam x64 is the supported edition. Close the game before installing or uninstalling."
$unverified=New-Object Windows.Forms.CheckBox;$unverified.Text='Allow an unverified game/prerequisite version (untested combination)';$unverified.SetBounds(20,438,760,26)
$original=New-Object Windows.Forms.TextBox;$original.SetBounds(20,498,610,25)
$originalLabel=New-Object Windows.Forms.Label;$originalLabel.Text='Optional original vanilla metadata backup (enables restoration on full uninstall)';$originalLabel.SetBounds(20,474,760,22)
$backupBrowse=New-Object Windows.Forms.Button;$backupBrowse.Text='Choose backup';$backupBrowse.SetBounds(640,496,140,28)
$backupBrowse.Add_Click({$dialog=New-Object Windows.Forms.OpenFileDialog;$dialog.Filter='Metadata backup (*.dat)|*.dat|All files (*.*)|*.*';if($dialog.ShowDialog() -eq 'OK'){$original.Text=$dialog.FileName}})
$install=New-Object Windows.Forms.Button;$install.Text='Install / update mod';$install.SetBounds(20,538,210,38)
$remove=New-Object Windows.Forms.Button;$remove.Text='Uninstall mod only';$remove.SetBounds(242,538,210,38)
$removeAll=New-Object Windows.Forms.Button;$removeAll.Text='Uninstall mod + prerequisites';$removeAll.SetBounds(464,538,316,38)
$verify=New-Object Windows.Forms.LinkLabel;$verify.Text='Verify game files through Steam';$verify.SetBounds(20,590,360,26)
$verify.Add_LinkClicked({Start-Process 'steam://validate/1217060'})
$status=New-Object Windows.Forms.Label;$status.SetBounds(20,618,760,24);$status.Text='Select your game folder, then check the installation.'
function Show-Result([scriptblock]$Action){
    try {$status.Text='Working...';$form.Refresh();$result=& $Action;$help.Text=[string]$result;$status.Text='Done.'}
    catch {$status.Text='No operation completed.';$help.Text=$_.Exception.Message;[void][Windows.Forms.MessageBox]::Show($_.Exception.Message,'Character Sheet','OK','Warning')}
}
$check.Add_Click({Show-Result {
    $root=Get-GameRoot $path.Text
    $compat=Get-Content -LiteralPath "$PSScriptRoot\compatibility.json" -Raw | ConvertFrom-Json
    $grid.Rows.Clear();$messages=@()
    foreach($component in @(Get-PrerequisiteStatus $root $compat)){
        $index=$grid.Rows.Add($component.Name,$component.Version,$component.Status);$grid.Rows[$index].Tag=$component.Url
        $messages += "$($component.Name): $($component.Instructions)"
    }
    $build=if((Get-Sha256 (Join-Path $root 'GameAssembly.dll')) -eq $compat.game.gameAssemblySha256){'Verified game build.'}else{'Game build differs from the tested build.'}
    "$build`r`n`r`n"+($messages -join "`r`n`r`n")
}})
$install.Add_Click({Show-Result {Install-CharacterSheet -GameRoot $path.Text -PackageRoot $PSScriptRoot -AllowUnverified:$unverified.Checked -OriginalMetadata $original.Text}})
$remove.Add_Click({if([Windows.Forms.MessageBox]::Show('Remove Character Sheet and keep all prerequisites?','Uninstall','YesNo','Question') -eq 'Yes'){Show-Result {Uninstall-CharacterSheet -GameRoot $path.Text -PackageRoot $PSScriptRoot}}})
$removeAll.Add_Click({if([Windows.Forms.MessageBox]::Show('Remove Character Sheet and verified prerequisite files? Other plugins block this operation. Changed files are preserved. Steam verification may be needed to restore vanilla metadata. Settings and caches are kept.','Restore vanilla','YesNo','Warning') -eq 'Yes'){Show-Result {Uninstall-CharacterSheet -GameRoot $path.Text -PackageRoot $PSScriptRoot -Prerequisites}}})
$form.Controls.AddRange(@($pathLabel,$path,$browse,$check,$grid,$link,$help,$unverified,$originalLabel,$original,$backupBrowse,$install,$remove,$removeAll,$verify,$status))
[void]$form.ShowDialog()
