<#
.SYNOPSIS
    Unity プロジェクトのフォーマットチェックを実行

.DESCRIPTION
    dotnet format を使用して .editorconfig に準拠しているかチェックします。
    Unity プロジェクトでは複数の .sln/.csproj が生成されるため、
    メインの .sln ファイルを自動検出して使用します。
    デフォルトで Assets/Programs/ のみを対象とします。

.PARAMETER Fix
    -Fix を指定すると、フォーマット違反を自動修正します。

.PARAMETER All
    -All を指定すると、全ファイルを対象にします（デフォルトは Assets/Programs/ のみ）。

.EXAMPLE
    # チェックのみ（Assets/Programs/ のみ）
    .\format-check.ps1

    # 自動修正
    .\format-check.ps1 -Fix

    # 全ファイルをチェック
    .\format-check.ps1 -All
#>

param(
    [switch]$Fix,
    [switch]$All
)

$ErrorActionPreference = "Stop"

# プロジェクトパス
$unityProjectPath = Join-Path $PSScriptRoot "..\src\Game.Client"
$unityProjectPath = Resolve-Path $unityProjectPath

Write-Host "=== Unity Project Format Check ===" -ForegroundColor Cyan
Write-Host "Project: $unityProjectPath"
Write-Host ""

# .sln ファイルを検索
$slnFiles = Get-ChildItem -Path $unityProjectPath -Filter "*.sln" -File

if ($slnFiles.Count -eq 0) {
    Write-Host "No .sln file found. Please open Unity Editor to generate the solution file." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "In Unity Editor:"
    Write-Host "  Edit -> Preferences -> External Tools -> Regenerate project files"
    exit 1
}

# メインの .sln ファイルを選択（プロジェクト名.sln を優先）
$mainSln = $slnFiles | Where-Object { $_.Name -eq "Game.Client.sln" } | Select-Object -First 1
if (-not $mainSln) {
    $mainSln = $slnFiles | Select-Object -First 1
}

Write-Host "Using solution: $($mainSln.Name)" -ForegroundColor Green
Write-Host ""

# 対象パスを設定
$includePath = "Assets/Programs/"
if ($All) {
    $includeOption = @()
    Write-Host "Target: All files" -ForegroundColor Cyan
} else {
    $includeOption = @("--include", $includePath)
    Write-Host "Target: $includePath" -ForegroundColor Cyan
}
Write-Host ""

# dotnet format 実行
Push-Location $unityProjectPath
try {
    if ($Fix) {
        Write-Host "Running: dotnet format `"$($mainSln.Name)`" $($includeOption -join ' ')" -ForegroundColor Yellow
        & dotnet format $mainSln.Name @includeOption --verbosity normal

        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "Format completed successfully!" -ForegroundColor Green
        } else {
            Write-Host ""
            Write-Host "Format completed with warnings." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Running: dotnet format `"$($mainSln.Name)`" --verify-no-changes $($includeOption -join ' ')" -ForegroundColor Yellow
        & dotnet format $mainSln.Name --verify-no-changes @includeOption --verbosity normal

        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "All files are properly formatted!" -ForegroundColor Green
        } else {
            Write-Host ""
            Write-Host "Some files need formatting. Run with -Fix to auto-fix." -ForegroundColor Red
            exit 1
        }
    }
} finally {
    Pop-Location
}
