$ErrorActionPreference = 'Stop'
$project = 'C:/Users/JUNGLE/Documents/GitHub/POC_06'
$evidence = Join-Path $project 'production/qa/evidence/story-001-ring-track-train-movement'
$lockPath = 'C:/Users/JUNGLE/AppData/Local/Temp/POC_06-unity-cli.lock'
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor/Unity.exe'
Set-Location -LiteralPath $project
$ownerPath = Join-Path $lockPath 'story-001-owner.txt'
$ownerToken = [guid]::NewGuid().ToString()
function Enter-UnityLock {
    while ($true) {
        if (Get-Process Unity -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 15; continue }
        try {
            New-Item -ItemType Directory -Path $lockPath -ErrorAction Stop | Out-Null
            $ownerToken | Set-Content -LiteralPath $ownerPath
            break
        }
        catch { Start-Sleep -Seconds 15 }
    }
}
function Exit-UnityLock {
    if ((Test-Path -LiteralPath $ownerPath) -and (Get-Content -LiteralPath $ownerPath) -eq $ownerToken) {
        Remove-Item -LiteralPath $ownerPath
        Remove-Item -LiteralPath $lockPath
    }
}
Enter-UnityLock
try {
    $deadline = (Get-Date).AddMinutes(10)
    while (Get-Process Unity -ErrorAction SilentlyContinue) {
        if ((Get-Date) -ge $deadline) {
            'NOT VERIFIED - editor open; no Unity CLI invocation launched.' | Set-Content -LiteralPath (Join-Path $evidence 'verification-status.txt')
            exit 3
        }
        Write-Output 'Waiting for the user-owned Unity editor to close.'
        Start-Sleep -Seconds 30
    }
    $testProcess = Start-Process -FilePath $editor -ArgumentList @('-batchmode', '-runTests', '-projectPath', '.', '-testPlatform', 'EditMode', '-testFilter', 'IronRing.Track', '-testResults', 'test-results/editmode.xml', '-logFile', "$evidence/editmode.log") -WindowStyle Hidden -PassThru
    $testProcess.WaitForExit()
    $testExit = $testProcess.ExitCode
    if (Test-Path test-results/editmode.xml) { Copy-Item -LiteralPath test-results/editmode.xml -Destination "$evidence/editmode.xml" }
    "EditMode process exit: $testExit" | Set-Content -LiteralPath "$evidence/verification-status.txt"
    if ($testExit -ne 0) { exit $testExit }
    [xml]$results = Get-Content -LiteralPath test-results/editmode.xml
    $trackTests = $results.SelectNodes("//test-case[contains(@fullname, 'IronRing.Track.Tests')]")
    if ($trackTests.Count -eq 0 -or @($trackTests | Where-Object result -ne 'Passed').Count -gt 0) { throw 'Track tests did not all pass or were not discovered.' }
    Exit-UnityLock
    Enter-UnityLock
    if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Unity editor reopened before build; no build launched.' }
    $buildProcess = Start-Process -FilePath $editor -ArgumentList @('-batchmode', '-quit', '-projectPath', '.', '-buildWindows64Player', 'Builds/Demo/Story001/Game.exe', '-logFile', "$evidence/build.log") -WindowStyle Hidden -PassThru
    $buildProcess.WaitForExit()
    if ($buildProcess.ExitCode -ne 0) { throw 'Player build failed.' }
    Exit-UnityLock
    $player = Start-Process -FilePath "$project/Builds/Demo/Story001/Game.exe" -ArgumentList @('-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0', '--track-demo', 'true', '--track-screenshot', "$evidence/01-train-between-nodes.png", '-logFile', "$evidence/player.log") -WindowStyle Hidden -PassThru
    if (-not $player.WaitForExit(60000)) { throw 'Evidence player did not finish within 60 seconds; inspect the running player.' }
    if (-not (Test-Path "$evidence/01-train-between-nodes.png")) { throw 'Player did not create screenshot.' }
    'Track tests passed; player built and screenshot retained. Image inspection still required.' | Add-Content -LiteralPath "$evidence/verification-status.txt"
}
finally {
    Exit-UnityLock
}
