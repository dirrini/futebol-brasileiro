[CmdletBinding()]
param(
    [switch]$BuildOnly,
    [switch]$SkipBuild,
    [switch]$CleanBuild,
    [string]$UnityPath
)

$ErrorActionPreference = 'Stop'

if ($BuildOnly -and $SkipBuild) {
    throw 'Use apenas uma das opcoes: -BuildOnly ou -SkipBuild.'
}
if ($SkipBuild -and $CleanBuild) {
    throw '-CleanBuild recompila o Unity e nao pode ser usado junto com -SkipBuild.'
}

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPrefix = $projectRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

function Assert-WorkspacePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($projectPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "O caminho deve ficar dentro deste projeto: $fullPath"
    }

    # Do not move through a junction/symlink that redirects a workspace path.
    $ancestor = $fullPath
    while (-not $ancestor.Equals($projectRoot, [StringComparison]::OrdinalIgnoreCase)) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "O caminho de build nao pode atravessar um link ou junction: $ancestor"
            }
        }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }

    return $fullPath
}

function Move-WithinWorkspace {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    # Validate both absolute targets immediately before every directory move.
    $checkedSource = Assert-WorkspacePath -Path $Source
    $checkedDestination = Assert-WorkspacePath -Path $Destination
    if (Test-Path -LiteralPath $checkedDestination) {
        throw "O destino ja existe; nenhum diretorio foi movido: $checkedDestination"
    }
    Move-Item -LiteralPath $checkedSource -Destination $checkedDestination
}

function Get-UnityEditor {
    $versionFile = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
    $versionMatch = [regex]::Match((Get-Content -LiteralPath $versionFile -Raw), '(?m)^m_EditorVersion:\s*(\S+)')
    if (-not $versionMatch.Success) {
        throw "Nao foi possivel ler a versao do Unity em $versionFile"
    }
    $version = $versionMatch.Groups[1].Value

    if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
        $editor = [IO.Path]::GetFullPath($UnityPath)
    } else {
        $candidates = @()
        foreach ($installationRoot in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA)) {
            if (-not [string]::IsNullOrWhiteSpace($installationRoot)) {
                $candidates += Join-Path $installationRoot "Unity/Hub/Editor/$version/Editor/Unity.exe"
            }
        }
        $editor = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($editor)) {
            throw "Unity $version nao encontrado no Unity Hub. Instale essa versao ou use -UnityPath 'C:\caminho\Editor\Unity.exe'."
        }
    }

    if (-not (Test-Path -LiteralPath $editor -PathType Leaf)) {
        throw "Executavel Unity nao encontrado: $editor"
    }
    $installedVersion = (Get-Item -LiteralPath $editor).VersionInfo.ProductVersion
    if ([string]::IsNullOrWhiteSpace($installedVersion) -or $installedVersion.Split('_')[0] -ne $version) {
        throw "O Editor selecionado deve ser Unity $version. Versao encontrada: $installedVersion"
    }
    $module = Join-Path (Split-Path -Parent $editor) 'Data/PlaybackEngines/WebGLSupport/UnityEditor.WebGL.Extensions.dll'
    if (-not (Test-Path -LiteralPath $module -PathType Leaf)) {
        throw "Instale o modulo WebGL Build Support para Unity $version no Unity Hub. Modulo ausente: $module"
    }

    Write-Host "Unity do projeto: $version"
    Write-Host "Editor: $editor"
    return $editor
}

function Assert-ProjectUnlocked {
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    $lockPath = Join-Path $ProjectPath 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $lockPath) {
        $lockHandle = $null
        try {
            $lockHandle = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        } catch {
            throw "A copia de build esta aberta no Unity, ou seu lock esta inacessivel: $ProjectPath. Aguarde o build dessa copia terminar."
        } finally {
            if ($null -ne $lockHandle) {
                $lockHandle.Dispose()
            }
        }
    }
}

function Assert-NoDestinationLinks {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }
    $pendingDirectories = New-Object 'System.Collections.Generic.Stack[string]'
    $pendingDirectories.Push($Path)
    while ($pendingDirectories.Count -gt 0) {
        foreach ($item in (Get-ChildItem -LiteralPath $pendingDirectories.Pop() -Force)) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "A sincronizacao nao pode remover ou atravessar links na copia de build: $($item.FullName)"
            }
            if ($item.PSIsContainer) {
                $pendingDirectories.Push($item.FullName)
            }
        }
    }
}

function Sync-UnityBuildProject {
    param([Parameter(Mandatory = $true)][string]$CopyPath)

    $checkedCopy = Assert-WorkspacePath -Path $CopyPath
    $expectedCopy = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Builds/UnityWebGLProject'))
    if (-not $checkedCopy.Equals($expectedCopy, [StringComparison]::OrdinalIgnoreCase)) {
        throw "A copia de build deve usar exclusivamente $expectedCopy"
    }
    if ($null -eq (Get-Command robocopy.exe -ErrorAction SilentlyContinue)) {
        throw 'Robocopy nao encontrado. Este script requer o robocopy do Windows.'
    }

    $copyPrefix = $checkedCopy.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($directoryName in @('Assets', 'Packages', 'ProjectSettings')) {
        # /MIR may remove destination files. Resolve and confine both full paths
        # before each invocation, and never mirror to the original project roots.
        $source = Assert-WorkspacePath -Path (Join-Path $projectRoot $directoryName)
        $destination = Assert-WorkspacePath -Path (Join-Path $checkedCopy $directoryName)
        if (-not $destination.StartsWith($copyPrefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not [IO.Path]::GetDirectoryName($destination).Equals($checkedCopy, [StringComparison]::OrdinalIgnoreCase) -or
            $source.Equals($destination, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Destino de sincronizacao inseguro: $destination"
        }
        if (-not (Test-Path -LiteralPath $source -PathType Container)) {
            throw "Diretorio de origem ausente: $source"
        }
        Assert-NoDestinationLinks -Path $destination

        Write-Host "Sincronizando arquivos salvos de $directoryName para a copia de build..."
        & robocopy.exe $source $destination /MIR /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS
        $copyExitCode = $LASTEXITCODE
        if ($copyExitCode -lt 0 -or $copyExitCode -gt 7) {
            throw "Robocopy falhou ao sincronizar $directoryName (codigo $copyExitCode)."
        }
    }
}

function Assert-DockerReady {
    if ($null -eq (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Docker nao encontrado. Instale/inicie o Docker Desktop com containers Linux, ou use -BuildOnly.'
    }

    $dockerType = & docker info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0) {
        throw 'Nao foi possivel acessar o Docker. Inicie o Docker Desktop com containers Linux.'
    }
    if (($dockerType -join '').Trim() -ne 'linux') {
        throw 'Este servidor requer containers Linux. Troque o Docker Desktop para Linux containers.'
    }
    & docker compose config --quiet
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Compose indisponivel ou compose.yaml invalido. Confira a mensagem acima.'
    }
}

$buildLockStream = $null
Push-Location -LiteralPath $projectRoot
try {
    $buildPath = Assert-WorkspacePath -Path (Join-Path $projectRoot 'Builds/WebGL')

    if ($SkipBuild -and -not (Test-Path -LiteralPath (Join-Path $buildPath 'index.html') -PathType Leaf)) {
        throw "Nao existe build para servir em $buildPath. Execute sem -SkipBuild primeiro."
    }

    $buildsDirectory = Assert-WorkspacePath -Path (Join-Path $projectRoot 'Builds')
    $buildLockPath = Assert-WorkspacePath -Path (Join-Path $buildsDirectory 'webgl-build.lock')
    New-Item -ItemType Directory -Path $buildsDirectory -Force | Out-Null
    try {
        $buildLockStream = [IO.File]::Open($buildLockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    } catch {
        throw 'Outro comando webgl.ps1 esta usando este projeto, ou o lock de build esta inacessivel. Aguarde sua conclusao e tente novamente.'
    }

    if (-not $BuildOnly) {
        Assert-DockerReady
    }

    if (-not $SkipBuild) {
        $editorPath = Get-UnityEditor
        $buildProjectPath = Assert-WorkspacePath -Path (Join-Path $projectRoot 'Builds/UnityWebGLProject')
        Assert-ProjectUnlocked -ProjectPath $buildProjectPath
        New-Item -ItemType Directory -Path $buildProjectPath -Force | Out-Null
        Write-Host 'O build usa uma copia isolada dos arquivos salvos; o projeto original pode continuar aberto no Unity.'
        Write-Host "Copia e cache WebGL: $buildProjectPath"
        Sync-UnityBuildProject -CopyPath $buildProjectPath

        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
        $stagePath = Assert-WorkspacePath -Path (Join-Path $projectRoot "Builds/WebGL-staging-$stamp")
        $backupPath = Assert-WorkspacePath -Path (Join-Path $projectRoot "Builds/WebGL-backup-$stamp")
        $logDirectory = Assert-WorkspacePath -Path (Join-Path $projectRoot 'Logs/WebGL')
        $logPath = Join-Path $logDirectory "build-$stamp.log"
        if (Test-Path -LiteralPath $stagePath) {
            throw "O diretorio temporario ja existe: $stagePath"
        }
        New-Item -ItemType Directory -Path $stagePath -Force | Out-Null
        New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

        $arguments = '-batchmode -nographics -quit -buildTarget WebGL -projectPath "{0}" -executeMethod FStudio.Build.WebGLBuild.Run -logFile "{1}"' -f $buildProjectPath, $logPath
        $previousBuildPath = [Environment]::GetEnvironmentVariable('WEBGL_BUILD_PATH', 'Process')
        $previousCleanBuild = [Environment]::GetEnvironmentVariable('WEBGL_CLEAN_BUILD', 'Process')
        Write-Host 'Compilando Addressables e jogo WebGL no Unity local. A primeira compilacao pode demorar.'
        if ($CleanBuild) {
            Write-Host 'Build limpo solicitado: o Unity vai reconstruir scripts e dados do player na copia isolada. Isso pode demorar mais.'
        }
        Write-Host "Log: $logPath"
        try {
            [Environment]::SetEnvironmentVariable('WEBGL_BUILD_PATH', $stagePath, 'Process')
            $cleanBuildValue = if ($CleanBuild) { '1' } else { '0' }
            [Environment]::SetEnvironmentVariable('WEBGL_CLEAN_BUILD', $cleanBuildValue, 'Process')
            $unityProcess = Start-Process -FilePath $editorPath -ArgumentList $arguments -WorkingDirectory $buildProjectPath -WindowStyle Hidden -PassThru
            # Start-Process -Wait also waits for descendants such as the licensing
            # client. Only this editor process determines when the build has ended.
            $unityProcess.WaitForExit()
            $unityProcess.Refresh()
        } finally {
            [Environment]::SetEnvironmentVariable('WEBGL_BUILD_PATH', $previousBuildPath, 'Process')
            [Environment]::SetEnvironmentVariable('WEBGL_CLEAN_BUILD', $previousCleanBuild, 'Process')
        }

        if ($unityProcess.ExitCode -ne 0) {
            throw "Unity encerrou com codigo $($unityProcess.ExitCode). O build anterior foi preservado. Consulte $logPath"
        }
        if (-not (Test-Path -LiteralPath (Join-Path $stagePath 'index.html') -PathType Leaf)) {
            throw "Unity nao gerou index.html. O build anterior foi preservado. Consulte $logPath"
        }

        $previousBuildMoved = $false
        if (Test-Path -LiteralPath $buildPath) {
            if (-not (Test-Path -LiteralPath $buildPath -PathType Container)) {
                throw "O destino do build existe e nao e um diretorio: $buildPath"
            }
            Move-WithinWorkspace -Source $buildPath -Destination $backupPath
            $previousBuildMoved = $true
            Write-Host "Build anterior preservado em: $backupPath"
        }

        try {
            Move-WithinWorkspace -Source $stagePath -Destination $buildPath
        } catch {
            if ($previousBuildMoved -and -not (Test-Path -LiteralPath $buildPath)) {
                Move-WithinWorkspace -Source $backupPath -Destination $buildPath
            }
            throw
        }
        Write-Host "Build concluido: $buildPath"
    }

    if (-not $BuildOnly) {
        & docker compose up --build -d --wait soccer-web
        if ($LASTEXITCODE -ne 0) {
            throw 'O servidor nao ficou pronto. Consulte: docker compose logs soccer-web'
        }
        Write-Host 'Jogo disponivel em http://localhost:8080'
    }
} finally {
    if ($null -ne $buildLockStream) {
        $buildLockStream.Dispose()
    }
    Pop-Location
}
