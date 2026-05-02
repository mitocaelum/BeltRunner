param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        [string]$TargetPath
    )

    $resolvedBase = (Resolve-Path -LiteralPath $BasePath).Path.TrimEnd('\') + '\'
    $resolvedTarget = (Resolve-Path -LiteralPath $TargetPath).Path
    $baseUri = New-Object System.Uri($resolvedBase)
    $targetUri = New-Object System.Uri($resolvedTarget)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()) -replace '/', '\'
}

function Get-XmlAttributeValue {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode]$Node,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if( $null -eq $Node.Attributes ) {
        return $null
    }

    $attribute = $Node.Attributes[$Name]
    if( $null -eq $attribute ) {
        return $null
    }

    return $attribute.Value
}

function Convert-InlineXmlToMarkdown {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode]$Node
    )

    $builder = New-Object System.Text.StringBuilder

    foreach( $child in $Node.ChildNodes ) {
        if( $child.NodeType -eq [System.Xml.XmlNodeType]::Text -or $child.NodeType -eq [System.Xml.XmlNodeType]::Whitespace ) {
            [void]$builder.Append($child.InnerText)
            continue
        }

        switch( $child.Name ) {
            "c" {
                [void]$builder.Append(('`{0}`' -f $child.InnerText.Trim()))
            }
            "see" {
                $cref = Get-XmlAttributeValue -Node $child -Name "cref"
                if( [string]::IsNullOrWhiteSpace($cref) ) {
                    [void]$builder.Append($child.InnerText)
                } else {
                    [void]$builder.Append(('`{0}`' -f ($cref -replace '^[A-Z]:', '')))
                }
            }
            "paramref" {
                $name = Get-XmlAttributeValue -Node $child -Name "name"
                [void]$builder.Append(('`{0}`' -f $name))
            }
            "para" {
                [void]$builder.AppendLine()
                [void]$builder.AppendLine()
                [void]$builder.Append((Convert-InlineXmlToMarkdown -Node $child).Trim())
                [void]$builder.AppendLine()
                [void]$builder.AppendLine()
            }
            default {
                [void]$builder.Append((Convert-InlineXmlToMarkdown -Node $child))
            }
        }
    }

    return $builder.ToString()
}

function Convert-XmlSectionToMarkdown {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode]$Node
    )

    if( $Node.Name -eq "list" ) {
        $items = foreach( $item in $Node.SelectNodes("./item") ) {
            $termNode = $item.SelectSingleNode("./term")
            $descriptionNode = $item.SelectSingleNode("./description")

            if( $termNode -and $descriptionNode ) {
                "- **$((Convert-InlineXmlToMarkdown -Node $termNode).Trim())**: $((Convert-InlineXmlToMarkdown -Node $descriptionNode).Trim())"
            } elseif( $descriptionNode ) {
                "- $((Convert-InlineXmlToMarkdown -Node $descriptionNode).Trim())"
            } else {
                "- $((Convert-InlineXmlToMarkdown -Node $item).Trim())"
            }
        }

        return ($items -join [Environment]::NewLine)
    }

    return (Convert-InlineXmlToMarkdown -Node $Node).Trim()
}

function Get-DocumentationRecord {
    param(
        [object[]]$Lines,

        [int]$StartIndex
    )

    if( $null -eq $Lines -or $Lines.Count -eq 0 ) {
        return $null
    }

    $docLines = New-Object System.Collections.Generic.List[string]
    $index = $StartIndex

    while( $index -lt $Lines.Length -and $Lines[$index].TrimStart().StartsWith("///", [System.StringComparison]::Ordinal) ) {
        $docLines.Add(($Lines[$index] -replace '^\s*/// ?', ''))
        $index++
    }

    while( $index -lt $Lines.Length ) {
        $trimmed = $Lines[$index].Trim()
        if( [string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("[", [System.StringComparison]::Ordinal) ) {
            $index++
            continue
        }

        break
    }

    if( $index -ge $Lines.Length ) {
        return $null
    }

    $signature = $Lines[$index].Trim()
    $xmlText = "<doc>`n$($docLines -join [Environment]::NewLine)`n</doc>"
    $xml = [xml]$xmlText

    $kind = "Member"
    $name = $signature

    $typeMatch = [regex]::Match($signature, '\b(class|interface|enum|struct|record)\s+([A-Za-z_][A-Za-z0-9_<>,]*)')
    if( $typeMatch.Success ) {
        $kind = "Type"
        $name = $typeMatch.Groups[2].Value
    } else {
        $methodMatch = [regex]::Match($signature, '([A-Za-z_][A-Za-z0-9_]*)\s*\(')
        if( $methodMatch.Success ) {
            $name = $methodMatch.Groups[1].Value
        }
    }

    [pscustomobject]@{
        EndIndex  = $index
        Signature = $signature
        Kind      = $kind
        Name      = $name
        Xml       = $xml.doc
    }
}

function Write-TestDocument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot,

        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    $lines = @(Get-Content -LiteralPath $SourcePath)
    $namespaceLine = $lines | Where-Object { $_.Trim().StartsWith("namespace ", [System.StringComparison]::Ordinal) } | Select-Object -First 1
    $namespaceName = if( $namespaceLine ) { ($namespaceLine.Trim() -replace '^namespace\s+', '').TrimEnd(';', ' ') } else { "" }
    $records = New-Object System.Collections.Generic.List[object]

    for( $i = 0; $i -lt $lines.Length; $i++ ) {
        if( $lines[$i].TrimStart().StartsWith("///", [System.StringComparison]::Ordinal) ) {
            $record = Get-DocumentationRecord -Lines $lines -StartIndex $i
            if( $null -ne $record ) {
                $records.Add($record)
                $i = $record.EndIndex
            }
        }
    }

    $relativeSource = (Get-RelativePath -BasePath $ProjectRoot -TargetPath $SourcePath) -replace '\\', '/'
    $title = [System.IO.Path]::GetFileNameWithoutExtension($SourcePath)
    $builder = New-Object System.Text.StringBuilder

    [void]$builder.AppendLine("# $title")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine(('- Source: `{0}`' -f $relativeSource))
    if( -not [string]::IsNullOrWhiteSpace($namespaceName) ) {
        [void]$builder.AppendLine(('- Namespace: `{0}`' -f $namespaceName))
    }
    [void]$builder.AppendLine("- Generated from XML documentation comments.")
    [void]$builder.AppendLine()

    foreach( $record in $records ) {
        [void]$builder.AppendLine("## $($record.Name)")
        [void]$builder.AppendLine()
        [void]$builder.AppendLine("- Kind: $($record.Kind)")
        [void]$builder.AppendLine(('- Signature: `{0}`' -f $record.Signature))
        [void]$builder.AppendLine()

        foreach( $child in $record.Xml.ChildNodes ) {
            $heading = switch( $child.Name ) {
                "summary" { "Summary" }
                "remarks" { "Remarks" }
                "returns" { "Expected Result" }
                "value" { "Value" }
                default { $null }
            }

            if( $null -ne $heading ) {
                $content = Convert-XmlSectionToMarkdown -Node $child
                if( -not [string]::IsNullOrWhiteSpace($content) ) {
                    [void]$builder.AppendLine("### $heading")
                    [void]$builder.AppendLine()
                    [void]$builder.AppendLine($content)
                    [void]$builder.AppendLine()
                }
            }
        }

        $paramNodes = @($record.Xml.SelectNodes("./param"))
        if( $paramNodes.Count -gt 0 ) {
            [void]$builder.AppendLine("### Parameters")
            [void]$builder.AppendLine()
            foreach( $param in $paramNodes ) {
                $paramName = Get-XmlAttributeValue -Node $param -Name "name"
                $paramText = Convert-XmlSectionToMarkdown -Node $param
                [void]$builder.AppendLine(('- `{0}`: {1}' -f $paramName, $paramText))
            }
            [void]$builder.AppendLine()
        }
    }

    $directory = Split-Path -Parent $OutputPath
    if( -not (Test-Path -LiteralPath $directory) ) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    Set-Content -LiteralPath $OutputPath -Value $builder.ToString() -Encoding UTF8
}

function Update-ReadmeIndex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReadmePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Entries
    )

    $content = Get-Content -LiteralPath $ReadmePath -Raw
    $startMarker = "<!-- TEST-DOC-LINKS:START -->"
    $endMarker = "<!-- TEST-DOC-LINKS:END -->"
    $replacement = $startMarker + [Environment]::NewLine + ($Entries -join [Environment]::NewLine) + [Environment]::NewLine + $endMarker
    $pattern = [regex]::Escape($startMarker) + ".*?" + [regex]::Escape($endMarker)
    $updated = [regex]::Replace($content, $pattern, $replacement, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Set-Content -LiteralPath $ReadmePath -Value $updated -Encoding UTF8
}

$projectRootPath = (Resolve-Path $ProjectRoot).Path
$readmePath = Join-Path $projectRootPath "README.md"
$docsRoot = Join-Path $projectRootPath "docs\\test-files"
$sourceFiles = Get-ChildItem -Path $projectRootPath -Recurse -File -Filter *.cs |
    Where-Object {
        $_.FullName -notmatch '\\bin\\' -and
        $_.FullName -notmatch '\\obj\\'
    } |
    Sort-Object FullName

$indexEntries = New-Object System.Collections.Generic.List[string]

foreach( $file in $sourceFiles ) {
    $relativeSource = Get-RelativePath -BasePath $projectRootPath -TargetPath $file.FullName
    $relativeMarkdown = [System.IO.Path]::ChangeExtension($relativeSource, ".md")
    $outputPath = Join-Path $docsRoot $relativeMarkdown
    Write-TestDocument -ProjectRoot $projectRootPath -SourcePath $file.FullName -OutputPath $outputPath

    $linkPath = ([System.IO.Path]::Combine("docs", "test-files", $relativeMarkdown)) -replace '\\', '/'
    $label = ($relativeSource -replace '\\', '/')
    $indexEntries.Add("- [$label]($linkPath)")
}

Update-ReadmeIndex -ReadmePath $readmePath -Entries $indexEntries

Write-Host ("Generated {0} documentation file(s)." -f $sourceFiles.Count)
foreach( $entry in $indexEntries ) {
    Write-Host $entry
}
