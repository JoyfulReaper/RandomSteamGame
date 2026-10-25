param(
    [switch]$Push
)

$ErrorActionPreference = "Stop"

$Repository = "ghcr.io/joyfulreaper/randomsteam"
$RepoRoot = Split-Path -Parent $PSScriptRoot

function Assert-Succeeded {
    param([string]$Operation)

    if ($LASTEXITCODE -ne 0) {
        throw "$Operation failed with exit code $LASTEXITCODE"
    }
}

Push-Location $RepoRoot

try {
    $Branch = git branch --show-current
    Assert-Succeeded "Git branch check"

    if ($Branch -ne "main") {
        throw "Expected main branch, currently on '$Branch'."
    }

    $Changes = git status --porcelain
    Assert-Succeeded "Git status check"

    if ($Changes) {
        throw "Working tree contains uncommitted changes. Commit or stash them first."
    }

    $CommitSha = git rev-parse --short=12 HEAD
    Assert-Succeeded "Git commit lookup"

    $Image = "${Repository}:${CommitSha}"

    Write-Host "`nBuilding $Image" -ForegroundColor Cyan

    docker build --tag $Image .
    Assert-Succeeded "Docker build"

    if ($Push) {
        Write-Host "`nPushing $Image" -ForegroundColor Cyan

        docker push $Image
        Assert-Succeeded "Docker push"
    }

    Write-Host "`nSuccess!" -ForegroundColor Green
    Write-Host "Image: $Image"

    if (-not $Push) {
        Write-Host "Image built locally. Use -Push to publish it."
    }
}
finally {
    Pop-Location
}