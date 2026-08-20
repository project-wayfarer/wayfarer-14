#!/usr/bin/env pwsh

    [cmdletbinding()]

param(
    [Parameter(Mandatory=$true)]
    [DateTime]$since,

    [Nullable[DateTime]]$until);

$scriptDir = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent
. $(join-path $scriptDir contribs_shared.ps1)

# Wayfarer: Use repo list instead
$repos = @(
    "space-wizards/RobustToolbox"
    "space-wizards/space-station-14"
    "new-frontiers-14/frontier-station-14"
    "project-wayfarer/wayfarer-14"
)

$allCommits = $repos | ForEach-Object {
    & "$PSScriptRoot\dump_commits_since.ps1" -repo $_ -since $since -until $until
}

$contribs = ($allCommits) `
    | Select-Object -ExpandProperty author `
    | Select-Object -ExpandProperty login -Unique `
    | Where-Object { -not $ignore[$_] }`
    | ForEach-Object { if($replacements[$_] -eq $null){ $_ } else { $replacements[$_] }} `
    | Sort-Object `
# End Wayfarer

$contribs = $contribs -join ", "
Write-Host $contribs
Write-Host "Total commit count is $($engine.Length + $content.Length)"
