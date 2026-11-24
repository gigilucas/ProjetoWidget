# Renders each widget in design/widget-mockups.html to docs/references/<name>.png (1024x1024)
# using headless Microsoft Edge.
$root    = Split-Path $PSScriptRoot -Parent
$page    = Join-Path $PSScriptRoot 'widget-mockups.html'
$outDir  = Join-Path $root 'docs\references'
$edge    = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$profile = Join-Path $env:TEMP 'projetowidget-render-profile'
$url     = 'file:///' + ($page -replace '\\', '/')

foreach ($name in 'clock', 'cpu-ram', 'calendar', 'weather', 'music') {
    $out = Join-Path $outDir "$name.png"
    & $edge --headless=new --disable-gpu --hide-scrollbars --user-data-dir="$profile" `
        --force-device-scale-factor=2 --window-size=512,512 --virtual-time-budget=2000 `
        --screenshot="$out" "$url#$name" 2>$null | Out-Null
    Write-Output "$name -> $out"
}
