$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
$compiler = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
$sdk = Join-Path $env:TEMP 'grading-webview2-1.0.2903.40'
if (!(Test-Path "$sdk/lib/net462/Microsoft.Web.WebView2.Core.dll")) {
    New-Item -ItemType Directory -Force $sdk | Out-Null
    Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.2903.40/microsoft.web.webview2.1.0.2903.40.nupkg' -OutFile "$sdk/sdk.zip"
    Expand-Archive "$sdk/sdk.zip" $sdk -Force
}
$stage = Join-Path $env:TEMP ("grading-build-" + [guid]::NewGuid())
New-Item -ItemType Directory $stage | Out-Null
Push-Location $project
try {
    & npm.cmd run build
    if ($LASTEXITCODE) { throw '界面构建失败' }
    Copy-Item dist/client "$stage/client" -Recurse
    Copy-Item "$sdk/lib/net462/Microsoft.Web.WebView2.Core.dll","$sdk/lib/net462/Microsoft.Web.WebView2.WinForms.dll","$sdk/runtimes/win-x64/native/WebView2Loader.dll" $stage
    & $compiler /nologo /target:library /platform:x64 "/out:$stage/Workbench.dll" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Net.Http.dll /r:System.Security.dll /r:System.Web.Extensions.dll "/r:$stage/Microsoft.Web.WebView2.Core.dll" "/r:$stage/Microsoft.Web.WebView2.WinForms.dll" "$PSScriptRoot\Workbench.cs" "$PSScriptRoot\ModelRequest.cs" "$PSScriptRoot\ScreenReader.cs" "$PSScriptRoot\OcrScanOverlay.cs"
    if ($LASTEXITCODE) { throw '窗口编译失败' }
    Compress-Archive "$stage/*" "$stage.zip"
    $output = Join-Path $stage '阅卷台.exe'
    & $compiler /nologo /target:winexe /platform:x64 "/out:$output" "/resource:$stage.zip,workbench.zip" /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "$PSScriptRoot\Launcher.cs"
    if ($LASTEXITCODE) { throw '桌面打包失败' }
    $check = Start-Process -FilePath $output -ArgumentList '--smoke-test' -WindowStyle Hidden -PassThru
    if (!$check.WaitForExit(20000)) { $check.Kill(); throw '新版启动检查超时' }
    if ($check.ExitCode -ne 0) { throw '新版启动检查失败' }

    $release = Join-Path (Split-Path $project) '阅卷台.exe'
    $pending = "$release.$([guid]::NewGuid()).tmp"
    try {
        [IO.File]::Copy($output, $pending)
        if ([IO.File]::Exists($release)) {
            [IO.File]::Replace($pending, $release, [NullString]::Value)
        } else {
            [IO.File]::Move($pending, $release)
        }
    } finally {
        if ([IO.File]::Exists($pending)) { [IO.File]::Delete($pending) }
    }
    Get-ChildItem -LiteralPath (Split-Path $release) -File |
        Where-Object Name -match '^阅卷台\.exe~RF[0-9a-f]+\.TMP$' |
        Remove-Item -ErrorAction Continue
} finally {
    Pop-Location
    $tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')
    foreach ($path in @($stage, "$stage.zip")) {
        $absolute = [IO.Path]::GetFullPath($path)
        if ((Split-Path $absolute) -eq $tempRoot -and
            (Split-Path $absolute -Leaf) -match '^grading-build-[0-9a-f-]{36}(\.zip)?$' -and
            (Test-Path -LiteralPath $absolute)) {
            Remove-Item -LiteralPath $absolute -Recurse -Force -ErrorAction Continue
        }
    }
}





