# Clean dist folder
$distPath = Join-Path (Split-Path -Parent $PSCommandPath) "dist"

Write-Host "Cleaning dist folder..." -ForegroundColor Yellow

Get-ChildItem -Path $distPath -File | Where-Object { $_.Name -ne '安装程序.exe' } | ForEach-Object {
    Write-Host "  Removing: $($_.Name)" -ForegroundColor Gray
    Remove-Item $_.FullName -Force
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Write-Host ""
Write-Host "dist folder contents:" -ForegroundColor Cyan
Get-ChildItem -Path $distPath | ForEach-Object {
    if ($_.PSIsContainer) {
        Write-Host "  [DIR]  $($_.Name)" -ForegroundColor Yellow
    } else {
        $sizeMB = [math]::Round($_.Length / 1MB, 2)
        Write-Host "  [FILE] $($_.Name) ($sizeMB MB)" -ForegroundColor White
    }
}
