$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "SDK .NET 10 non trovato. Installare .NET 10 SDK e ripetere il comando."
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot "src\Sciuscia.Foundations\Sciuscia.Foundations.csproj"
$outputFolder = Join-Path $projectRoot "dist\Fondazioni-Solo-LAB-win-x64"
$uiProjectFile = Join-Path $projectRoot "src\Sciuscia.Ui\Sciuscia.Ui.csproj"
$uiOutputFolder = Join-Path $projectRoot "dist\Gestionale-Sciuscia-Interfaccia-win-x64"

function Invoke-Step {
    param([string]$Description, [scriptblock]$Command)
    Write-Host $Description
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description non riuscito (codice $LASTEXITCODE)."
    }
}

# $ErrorActionPreference = "Stop" ferma lo script sugli errori di PowerShell,
# ma NON su un eseguibile nativo (dotnet.exe) che termina con codice diverso da zero:
# per questo ogni passaggio va verificato esplicitamente con $LASTEXITCODE.
Invoke-Step "Ripristino dipendenze" { dotnet restore $projectFile }
Invoke-Step "Autotest Fondazioni" { dotnet run --project $projectFile --configuration Release -- --self-test }
Invoke-Step "Pubblicazione eseguibile Windows autonomo" {
    dotnet publish $projectFile `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        --output $outputFolder
}
Invoke-Step "Autotest dell'eseguibile pubblicato" {
    & (Join-Path $outputFolder "Sciuscia.Foundations.exe") --self-test
}

Write-Host "Pacchetto creato in: $outputFolder"

# Interfaccia grafica: nessun autotest a riga di comando (è una finestra), si verifica solo
# che compili e si pubblichi; il collaudo visivo va fatto aprendo l'eseguibile.
Invoke-Step "Ripristino dipendenze (interfaccia grafica)" { dotnet restore $uiProjectFile }
Invoke-Step "Pubblicazione interfaccia grafica" {
    dotnet publish $uiProjectFile `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $uiOutputFolder
}

Write-Host "Interfaccia grafica creata in: $uiOutputFolder"

