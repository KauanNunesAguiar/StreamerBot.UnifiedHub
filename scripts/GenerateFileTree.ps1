param(
    [string]$RootPath,
    [string]$OutputPath
)

$exclude = @('bin', 'obj', '.git', '.vs', 'node_modules', 'generated')

function Get-Tree {
    param($Path, $Prefix)
    $items = Get-ChildItem -LiteralPath $Path |
        Where-Object { $exclude -notcontains $_.Name } |
        Sort-Object @{Expression = { $_.PSIsContainer }; Descending = $true }, Name

    $count = $items.Count
    for ($i = 0; $i -lt $count; $i++) {
        $item = $items[$i]
        $isLast = ($i -eq $count - 1)
        $connector = if ($isLast) { '+-- ' } else { '|-- ' }
        "$Prefix$connector$($item.Name)"
        if ($item.PSIsContainer) {
            $newPrefix = $Prefix + $(if ($isLast) { '    ' } else { '|   ' })
            Get-Tree -Path $item.FullName -Prefix $newPrefix
        }
    }
}

$lines = @((Split-Path $RootPath -Leaf))
$lines += Get-Tree -Path $RootPath -Prefix ''

New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath) | Out-Null
$lines | Out-File -FilePath $OutputPath -Encoding utf8