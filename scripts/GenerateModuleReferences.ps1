param(
    [string]$RootPath,
    [string]$OutputDir
)

$typeRegex = '^\s*(public|internal)\s+((?:sealed|abstract|static|readonly|partial)\s+)*(class|record\s+struct|record\s+class|record|struct|enum)\s+\w+'
$methodRegex = '^\s*(public)\s+((?:static|async|virtual|override|sealed)\s+)*[A-Za-z_][\w<>\[\],\.\?]*(\s+[A-Za-z_][\w<>\[\],\.\?]*)*\s+\w+(<[^>]+>)?\s*\('
$propertyRegex = '^\s*public\s+.+\s+\w+\s*\{\s*get'
$eventRegex = '^\s*public\s+(static\s+)?event\s+.+;'

function Write-FileSummary {
    param($file, $rootPath, [System.Collections.Generic.List[string]]$output)

    $relative = $file.FullName.Substring($rootPath.Length).TrimStart('\')
    $output.Add("=== $relative ===")

    $fileLines = Get-Content -LiteralPath $file.FullName

    for ($i = 0; $i -lt $fileLines.Count; $i++) {
        $line = $fileLines[$i]

        if ($line -match $typeRegex -or $line -match $methodRegex) {
            $declaration = $line.Trim()
            $openCount = ([regex]::Matches($declaration, '\(')).Count
            $closeCount = ([regex]::Matches($declaration, '\)')).Count

            while ($openCount -gt $closeCount -and $i -lt $fileLines.Count - 1) {
                $i++
                $nextLine = $fileLines[$i].Trim()
                $declaration += " " + $nextLine
                $openCount += ([regex]::Matches($nextLine, '\(')).Count
                $closeCount += ([regex]::Matches($nextLine, '\)')).Count
            }

            $declaration = $declaration -replace '\s*\{\s*$', ''

            $indent = if ($line -match $typeRegex) { "  " } else { "    " }
            $output.Add("$indent$declaration")
        }
        elseif ($line -match $propertyRegex -or $line -match $eventRegex) {
            $output.Add("    " + $line.Trim())
        }
    }
    $output.Add("")
}

function Get-OrderedFiles {
    param($moduleRoot)

    $allFiles = Get-ChildItem -Path $moduleRoot -Recurse -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

    $order = @('Models', 'Abstractions', 'Extensions', 'Services', 'Hubs')
    $ordered = New-Object System.Collections.Generic.List[object]

    foreach ($category in $order) {
        $ordered.AddRange(@($allFiles | Where-Object { $_.FullName -match "\\$category\\" } | Sort-Object FullName))
    }

    $rest = $allFiles | Where-Object {
        $f = $_
        -not ($order | Where-Object { $f.FullName -match "\\$_\\" })
    } | Sort-Object FullName

    $ordered.AddRange(@($rest))

    return $ordered
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# ---------- Core ----------
$coreRoot = Join-Path $RootPath 'Core'
if (Test-Path $coreRoot) {
    $output = New-Object System.Collections.Generic.List[string]
    $output.Add("# Core")
    $output.Add("")

    foreach ($file in (Get-OrderedFiles -moduleRoot $coreRoot)) {
        Write-FileSummary -file $file -rootPath $RootPath -output $output
    }

    $output | Out-File -FilePath (Join-Path $OutputDir 'Core.txt') -Encoding utf8
}

# ---------- Integrations ----------
$integrationsRoot = Join-Path $RootPath 'Integrations'
if (Test-Path $integrationsRoot) {
    Get-ChildItem -Path $integrationsRoot -Directory | ForEach-Object {
        $integrationName = $_.Name
        $output = New-Object System.Collections.Generic.List[string]
        $output.Add("# Integration: $integrationName")
        $output.Add("")

        foreach ($file in (Get-OrderedFiles -moduleRoot $_.FullName)) {
            Write-FileSummary -file $file -rootPath $RootPath -output $output
        }

        $output | Out-File -FilePath (Join-Path $OutputDir "Integration.$integrationName.txt") -Encoding utf8
    }
}