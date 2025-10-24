param(
    [string]$ExeName = "MyGames.Desktop.exe",
    [string]$ShortcutName = "MyGames.lnk",
    [string]$Configuration = "Debug",
    [bool]$EnableShortcut = $true   # có thể tắt bằng cách dùng false
)

# =============================
# ⚙️ CONFIG
# =============================
$desktop = [Environment]::GetFolderPath('Desktop')
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$targetPath = Join-Path $projectDir "bin\$Configuration\net8.0-windows\$ExeName"
$linkPath = Join-Path $desktop $ShortcutName

Write-Host "[MyGames] DeployShortcut.ps1 running..."
Write-Host "[MyGames] Build configuration: $Configuration"
Write-Host "[MyGames] Target executable: $targetPath"

if (-not $EnableShortcut) {
    Write-Host "[MyGames] Shortcut auto-creation is DISABLED. Skipping."
    exit 0
}

# =============================
# 🗑️ Xóa shortcut cũ
# =============================
if (Test-Path $linkPath) {
    Remove-Item $linkPath -Force
    Write-Host "[MyGames] Old shortcut removed."
}

# =============================
# 🪄 Tạo shortcut mới
# =============================
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($linkPath)
$shortcut.TargetPath = $targetPath
$shortcut.WorkingDirectory = Split-Path $targetPath
$shortcut.IconLocation = "$targetPath,0"
$shortcut.Description = "MyGames Shortcut"
$shortcut.Save()

Write-Host "[MyGames] ✅ Shortcut created: $linkPath"
