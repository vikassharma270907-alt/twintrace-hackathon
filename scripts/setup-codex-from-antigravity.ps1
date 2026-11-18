[CmdletBinding()]
param(
    [switch]$SkipRemoteConnectors,
    [switch]$SkipMcp
)

$ErrorActionPreference = 'Stop'

$UserHome = [Environment]::GetFolderPath('UserProfile')
$CodexHome = Join-Path $UserHome '.codex'
$AntigravityConfig = Join-Path $UserHome '.gemini\config'
$AntigravityPlugins = Join-Path $AntigravityConfig 'plugins'
$AntigravityRuntime = Join-Path $UserHome '.gemini\antigravity'
$MarketplaceRoot = Join-Path $CodexHome 'marketplaces\antigravity-migrated'
$MarketplacePlugins = Join-Path $MarketplaceRoot 'plugins'
$CodexAgents = Join-Path $CodexHome 'agents'
$CodexConfig = Join-Path $CodexHome 'config.toml'
$GlobalAgents = Join-Path $CodexHome 'AGENTS.md'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ProjectAgents = Join-Path $ProjectRoot 'AGENTS.md'

function Ensure-Directory([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function ConvertTo-TomlString([string]$Value) {
    return ($Value | ConvertTo-Json -Compress)
}

function Copy-DirectoryContents([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Missing source directory: $Source"
    }
    Ensure-Directory $Destination
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Get-PluginMetadata([string]$ExtractedRoot, [string]$FallbackDescription) {
    $metadata = $null
    foreach ($relative in @('plugin.json', '.codex-plugin\plugin.json', '.claude-plugin\plugin.json', 'package.json')) {
        $candidate = Join-Path $ExtractedRoot $relative
        if (-not (Test-Path -LiteralPath $candidate)) { continue }
        try {
            $parsed = Get-Content -LiteralPath $candidate -Raw | ConvertFrom-Json
            if ($parsed.description -or $parsed.version) {
                $metadata = $parsed
                break
            }
        } catch {
            continue
        }
    }

    $version = if ($metadata -and $metadata.version) { [string]$metadata.version } else { '1.0.0' }
    if ($version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
        $version = '1.0.0'
    }
    $description = if ($metadata -and $metadata.description) { [string]$metadata.description } else { $FallbackDescription }
    return [pscustomobject]@{ Version = $version; Description = $description }
}

function Write-PortableManifest(
    [string]$PluginRoot,
    [string]$Name,
    [string]$Version,
    [string]$Description
) {
    $manifest = [ordered]@{
        '$schema' = 'https://agent-plugins.org/schemas/1.0.0/plugin.schema.json'
        name = $Name
        version = $Version
        description = $Description
        author = [ordered]@{ name = 'Migrated from local Antigravity installation' }
    }
    $manifestPath = Join-Path $PluginRoot 'plugin.json'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
}

function Stage-GitPlugin([hashtable]$Definition, [string]$ScratchRoot) {
    $repo = Join-Path $AntigravityPlugins $Definition.Repository
    if (-not (Test-Path -LiteralPath (Join-Path $repo '.git'))) {
        throw "Git repository is unavailable for $($Definition.Name): $repo"
    }

    $target = Join-Path $MarketplacePlugins $Definition.Name
    $existingManifest = Join-Path $target 'plugin.json'
    $existingSkillCount = if (Test-Path -LiteralPath (Join-Path $target 'skills')) {
        @(Get-ChildItem -LiteralPath (Join-Path $target 'skills') -Recurse -File -Filter 'SKILL.md' -ErrorAction SilentlyContinue).Count
    } else { 0 }
    if ((Test-Path -LiteralPath $existingManifest) -and $existingSkillCount -gt 0) {
        $existingMetadata = Get-PluginMetadata $target $Definition.Description
        Write-Host "SKIP staged plugin $($Definition.Name) ($existingSkillCount skills)"
        return [pscustomobject]@{ Name = $Definition.Name; Skills = $existingSkillCount; Version = $existingMetadata.Version }
    }

    $extract = Join-Path $ScratchRoot $Definition.Name
    $archive = Join-Path $ScratchRoot ($Definition.Name + '.zip')
    Ensure-Directory $extract
    & git -C $repo archive --format=zip -o $archive HEAD
    if ($LASTEXITCODE -ne 0) { throw "git archive failed for $($Definition.Name)" }
    Expand-Archive -LiteralPath $archive -DestinationPath $extract -Force

    Ensure-Directory $target
    Ensure-Directory (Join-Path $target 'skills')
    foreach ($mapping in $Definition.Mappings) {
        $source = Join-Path $extract $mapping.Source
        $destination = Join-Path $target $mapping.Destination
        Copy-DirectoryContents $source $destination
    }

    $metadata = Get-PluginMetadata $extract $Definition.Description
    Write-PortableManifest $target $Definition.Name $metadata.Version $metadata.Description
    $skillCount = @(Get-ChildItem -LiteralPath (Join-Path $target 'skills') -Recurse -File -Filter 'SKILL.md').Count
    if ($skillCount -eq 0) { throw "No skills were staged for $($Definition.Name)" }
    return [pscustomobject]@{ Name = $Definition.Name; Skills = $skillCount; Version = $metadata.Version }
}

function Stage-LemoPlugin([string]$ScratchRoot) {
    $target = Join-Path $MarketplacePlugins 'lemo-opuscar'
    $existingManifest = Join-Path $target 'plugin.json'
    $existingSkillCount = if (Test-Path -LiteralPath (Join-Path $target 'skills')) {
        @(Get-ChildItem -LiteralPath (Join-Path $target 'skills') -Recurse -File -Filter 'SKILL.md' -ErrorAction SilentlyContinue).Count
    } else { 0 }
    if ((Test-Path -LiteralPath $existingManifest) -and $existingSkillCount -gt 0) {
        $existingMetadata = Get-PluginMetadata $target 'Film, motion graphics, and animation workflows migrated from Antigravity.'
        Write-Host "SKIP staged plugin lemo-opuscar ($existingSkillCount skills)"
        return [pscustomobject]@{ Name = 'lemo-opuscar'; Skills = $existingSkillCount; Version = $existingMetadata.Version }
    }

    $clone = Join-Path $ScratchRoot 'lemo-upstream'
    & git clone --depth 1 --filter=blob:none --sparse 'https://github.com/lemomo-ai/lemo-opuscar.git' $clone
    if ($LASTEXITCODE -ne 0) { throw 'Unable to fetch the clean Lemo-Opuscar repository.' }
    & git -C $clone sparse-checkout set 'plugin/skills'
    if ($LASTEXITCODE -ne 0) { throw 'Unable to select the Lemo-Opuscar plugin skills.' }
    Ensure-Directory $target
    Ensure-Directory (Join-Path $target 'skills')
    $sourceSkills = Join-Path $clone 'plugin\skills'
    if (Test-Path -LiteralPath $sourceSkills) {
        Copy-DirectoryContents $sourceSkills (Join-Path $target 'skills')
    }
    $metadata = Get-PluginMetadata $clone 'Film, motion graphics, and animation workflows migrated from Antigravity.'
    Write-PortableManifest $target 'lemo-opuscar' $metadata.Version $metadata.Description
    $skillCount = @(Get-ChildItem -LiteralPath (Join-Path $target 'skills') -Recurse -File -Filter 'SKILL.md' -ErrorAction SilentlyContinue).Count
    if ($skillCount -eq 0) {
        throw 'The clean Lemo-Opuscar repository did not contain installable skills.'
    }
    return [pscustomobject]@{ Name = 'lemo-opuscar'; Skills = $skillCount; Version = $metadata.Version }
}

function Stage-AntigravityRuntime {
    $sourceSkills = Join-Path $AntigravityRuntime 'skills'
    if (-not (Test-Path -LiteralPath $sourceSkills)) {
        throw "Antigravity runtime skills are unavailable: $sourceSkills"
    }
    $target = Join-Path $MarketplacePlugins 'antigravity-runtime'
    Ensure-Directory $target
    Copy-DirectoryContents $sourceSkills (Join-Path $target 'skills')
    Ensure-Directory (Join-Path $target 'runtime')
    foreach ($folder in @('get-shit-done', 'bin', 'lib')) {
        $source = Join-Path $AntigravityRuntime $folder
        if (Test-Path -LiteralPath $source) {
            Copy-DirectoryContents $source (Join-Path (Join-Path $target 'runtime') $folder)
        }
    }

    $portableRuntime = (Join-Path $target 'runtime').Replace('\', '/')
    $legacyHome = $AntigravityRuntime.Replace('\', '/')
    $textExtensions = @('.md', '.json', '.js', '.cjs', '.mjs', '.ps1', '.sh', '.toml', '.yaml', '.yml')
    Get-ChildItem -LiteralPath $target -Recurse -File | Where-Object { $textExtensions -contains $_.Extension.ToLowerInvariant() } | ForEach-Object {
        $content = [IO.File]::ReadAllText($_.FullName)
        $updated = $content.Replace('$HOME/.gemini/antigravity', $portableRuntime)
        $updated = $updated.Replace('~/.gemini/antigravity', $portableRuntime)
        $updated = $updated.Replace($legacyHome, $portableRuntime)
        if ($updated -ne $content) {
            [IO.File]::WriteAllText($_.FullName, $updated, (New-Object Text.UTF8Encoding($false)))
        }
    }

    Write-PortableManifest $target 'antigravity-runtime' '1.0.0' 'The working Antigravity GSD, Graphify, project-planning, execution, and verification skill runtime.'
    $skillCount = @(Get-ChildItem -LiteralPath (Join-Path $target 'skills') -Recurse -File -Filter 'SKILL.md').Count
    return [pscustomobject]@{ Name = 'antigravity-runtime'; Skills = $skillCount; Version = '1.0.0' }
}

function Get-FrontmatterValue([string]$Frontmatter, [string]$Key) {
    $match = [regex]::Match($Frontmatter, '(?m)^' + [regex]::Escape($Key) + ':\s*(.+?)\s*$')
    if (-not $match.Success) { return '' }
    $value = $match.Groups[1].Value.Trim()
    if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    return $value.Replace('\"', '"').Replace("''", "'")
}

function Convert-AntigravityAgents {
    Ensure-Directory $CodexAgents
    $sources = @{}
    $eccAgents = Join-Path $AntigravityPlugins 'everything-claude-code\agents'
    if (Test-Path -LiteralPath $eccAgents) {
        Get-ChildItem -LiteralPath $eccAgents -File -Filter '*.md' | ForEach-Object { $sources[$_.BaseName] = $_.FullName }
    }
    $runtimeAgents = Join-Path $AntigravityRuntime 'agents'
    if (Test-Path -LiteralPath $runtimeAgents) {
        Get-ChildItem -LiteralPath $runtimeAgents -File -Filter '*.md' | ForEach-Object { $sources[$_.BaseName] = $_.FullName }
    }
    $charretteArchitect = Join-Path $AntigravityPlugins 'charrette\agents\architect.md'
    if (Test-Path -LiteralPath $charretteArchitect) { $sources['architect'] = $charretteArchitect }

    $runtimeSupport = (Join-Path $MarketplacePlugins 'antigravity-runtime\runtime').Replace('\', '/')
    $uiUxSupport = (Join-Path $MarketplacePlugins 'ui-ux-pro-max').Replace('\', '/')
    $written = 0
    foreach ($stem in @($sources.Keys | Sort-Object)) {
        $source = $sources[$stem]
        $raw = [IO.File]::ReadAllText($source).Replace("`r`n", "`n")
        $frontmatter = ''
        $body = $raw
        $match = [regex]::Match($raw, '\A---\n(?<front>.*?)\n---\n?(?<body>.*)\z', [Text.RegularExpressions.RegexOptions]::Singleline)
        if ($match.Success) {
            $frontmatter = $match.Groups['front'].Value
            $body = $match.Groups['body'].Value
        }
        $description = Get-FrontmatterValue $frontmatter 'description'
        if ([string]::IsNullOrWhiteSpace($description)) {
            $description = "Antigravity specialist migrated from $([IO.Path]::GetFileName($source))."
        }
        $tools = Get-FrontmatterValue $frontmatter 'tools'
        if ([string]::IsNullOrWhiteSpace($tools)) { $tools = 'not declared' }

        $body = $body.Replace('$HOME/.gemini/antigravity', $runtimeSupport)
        $body = $body.Replace('~/.gemini/antigravity', $runtimeSupport)
        $body = $body.Replace('C:\Users\vikas sharma\.gemini\config\plugins\ui-ux-pro-max', $uiUxSupport.Replace('/', '\'))
        $body = $body.Replace('.claude/audits/', '.codex/audits/')

        $compatibility = @"
Codex compatibility:
- Migrated from Antigravity agent $([IO.Path]::GetFileName($source)).
- The parent session's sandbox, approval policy, MCP servers, and available tools are authoritative.
- Legacy tool names are capability intent; use the closest available Codex capability and never claim use of a missing tool.
- Legacy Task(), Agent tool, native_swarm, and slash-command wording means Codex subagent delegation or the equivalent installed skill.
- A ../skills/ reference means the corresponding installed skill in the plugin catalog, not a path relative to this TOML file.
- Original tool allowlist (advisory only): $tools.

"@
        $instructions = $compatibility + $body
        $toml = @(
            "# Migrated from Antigravity: $source",
            ('name = ' + (ConvertTo-TomlString $stem)),
            ('description = ' + (ConvertTo-TomlString $description)),
            ('developer_instructions = ' + (ConvertTo-TomlString $instructions)),
            ''
        ) -join "`n"

        $destination = Join-Path $CodexAgents ($stem + '.toml')
        if (Test-Path -LiteralPath $destination) {
            $existing = Get-Content -LiteralPath $destination -Raw
            if ($existing -notmatch '^# Migrated from Antigravity:') {
                Write-Warning "Preserving pre-existing custom agent: $destination"
                continue
            }
        }
        [IO.File]::WriteAllText($destination, $toml, (New-Object Text.UTF8Encoding($false)))
        $written++
    }
    return $written
}

function Add-GuidanceBlock([string]$Path, [string]$Block) {
    $marker = '<!-- ANTIGRAVITY-TO-CODEX -->'
    if (Test-Path -LiteralPath $Path) {
        $existing = Get-Content -LiteralPath $Path -Raw
        if ($existing.Contains($marker)) { return }
        Add-Content -LiteralPath $Path -Value ("`n`n" + $Block) -Encoding utf8
    } else {
        Set-Content -LiteralPath $Path -Value $Block -Encoding utf8
    }
}

function Ensure-AgentsConfig {
    if (-not (Test-Path -LiteralPath $CodexConfig)) { throw "Codex config is missing: $CodexConfig" }
    $configText = Get-Content -LiteralPath $CodexConfig -Raw
    if ($configText -notmatch '(?m)^\[agents\]\s*$') {
        Add-Content -LiteralPath $CodexConfig -Value @"

[agents]
enabled = true
max_concurrent_threads_per_session = 8
"@ -Encoding utf8
    }
}

function Write-Marketplace([array]$InstalledPlugins) {
    $catalogDir = Join-Path $MarketplaceRoot '.agents\plugins'
    Ensure-Directory $catalogDir
    $entries = @()
    foreach ($plugin in $InstalledPlugins | Sort-Object Name) {
        $entries += [ordered]@{
            name = $plugin.Name
            source = [ordered]@{ source = 'local'; path = ('./plugins/' + $plugin.Name) }
            policy = [ordered]@{ installation = 'AVAILABLE'; authentication = 'ON_USE' }
            category = 'Developer Tools'
        }
    }
    $catalog = [ordered]@{
        name = 'antigravity-migrated'
        interface = [ordered]@{ displayName = 'Antigravity Migrated' }
        plugins = $entries
    }
    $catalogPath = Join-Path $catalogDir 'marketplace.json'
    [IO.File]::WriteAllText($catalogPath, ($catalog | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding($false)))
}

function Invoke-Codex([string[]]$Arguments, [string]$Label) {
    $output = & codex @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "$Label failed: $($output | Out-String)"
        return $false
    }
    Write-Host "OK  $Label" -ForegroundColor Green
    return $true
}

function Install-Plugins([array]$LocalPlugins) {
    Invoke-Codex @('plugin', 'marketplace', 'add', $MarketplaceRoot, '--json') 'register Antigravity marketplace' | Out-Null
    foreach ($plugin in $LocalPlugins | Sort-Object Name) {
        Invoke-Codex @('plugin', 'add', ($plugin.Name + '@antigravity-migrated'), '--json') ("install local plugin " + $plugin.Name) | Out-Null
    }

    if (-not $SkipRemoteConnectors) {
        $remote = @(
            'agent-reach@openai-curated-remote',
            'figma@openai-curated-remote',
            'firebase@openai-curated-remote',
            'google-calendar@openai-curated-remote',
            'google-drive@openai-curated-remote',
            'laya@openai-curated-remote',
            'notion@openai-curated-remote'
        )
        foreach ($pluginId in $remote) {
            Invoke-Codex @('plugin', 'add', $pluginId, '--json') ("install connector " + $pluginId) | Out-Null
        }
    }
}

function Ensure-McpServers {
    if ($SkipMcp) { return }
    $configured = @()
    try {
        $mcpState = (& codex mcp list --json | Out-String) | ConvertFrom-Json
        $configured = @($mcpState | ForEach-Object { $_.name })
    } catch {
        $configured = @()
    }

    $commands = @(
        [pscustomobject]@{ Name = 'graphify'; Args = @('mcp', 'add', 'graphify', '--', (Join-Path $AntigravityRuntime 'bin\graphify-mcp.exe')) },
        [pscustomobject]@{ Name = 'context7'; Args = @('mcp', 'add', 'context7', '--', 'npx', '-y', '@upstash/context7-mcp') },
        [pscustomobject]@{ Name = 'skillspector'; Args = @('mcp', 'add', 'skillspector', '--env', 'PYTHONIOENCODING=utf-8', '--env', 'PYTHONUNBUFFERED=1', '--', (Join-Path $UserHome 'AppData\Roaming\uv\tools\skillspector\Scripts\python.exe'), '-m', 'skillspector.cli', 'mcp') },
        [pscustomobject]@{ Name = 'magic'; Args = @('mcp', 'add', 'magic', '--', 'node', (Join-Path $AntigravityPlugins 'magic\dist\index.js')) },
        [pscustomobject]@{ Name = 'wispr-flow'; Args = @('mcp', 'add', 'wispr-flow', '--url', 'https://api.wisprflow.ai/connect/mcp') }
    )
    foreach ($entry in $commands) {
        if ($configured -contains $entry.Name) {
            Write-Host "SKIP MCP $($entry.Name) already configured"
            continue
        }
        Invoke-Codex $entry.Args ("configure MCP " + $entry.Name) | Out-Null
    }
}

if (-not (Test-Path -LiteralPath $AntigravityPlugins)) { throw "Antigravity plugins directory not found: $AntigravityPlugins" }
if (-not (Test-Path -LiteralPath $AntigravityRuntime)) { throw "Antigravity runtime not found: $AntigravityRuntime" }
Ensure-Directory $MarketplacePlugins
Ensure-Directory $CodexAgents

$scratch = Join-Path ([IO.Path]::GetTempPath()) ('codex-antigravity-' + [guid]::NewGuid().ToString('N'))
Ensure-Directory $scratch

$definitions = @(
    @{ Name='agent-collab-skills'; Repository='agent-collab-skills'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Collaborative agent planning and reconciliation skills.' },
    @{ Name='agent-skills'; Repository='agent-skills'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Production engineering skills covering the development lifecycle.' },
    @{ Name='anthropic-skills'; Repository='anthropic-skills'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Document, presentation, design, and application workflow skills.' },
    @{ Name='devops-skills'; Repository='cc-devops-skills'; Mappings=@(@{Source='devops-skills-plugin\skills';Destination='skills'}); Description='DevOps generators and validators for infrastructure and CI.' },
    @{ Name='charrette'; Repository='charrette'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Architecture, planning, orchestration, review, and delivery skills.' },
    @{ Name='claude-code-agents'; Repository='claude-code-agents'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Audit, feature, release, and deployment workflow skills.' },
    @{ Name='coderabbit'; Repository='coderabbit'; Mappings=@(@{Source='skills';Destination='skills'}); Description='CodeRabbit review and review-comment remediation skills.' },
    @{ Name='context7'; Repository='context7'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Context7 documentation lookup skills.' },
    @{ Name='design-motion-principles'; Repository='design-motion-principles'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Purposeful, accessible interface motion guidance.' },
    @{ Name='diagram-design'; Repository='diagram-design'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Diagram design workflow.' },
    @{ Name='logo-designer-skill'; Repository='logo-designer-skill'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Logo design workflow.' },
    @{ Name='magic-workflows'; Repository='magic'; Mappings=@(@{Source='skills';Destination='skills'}); Description='21st.dev and Magic UI workflow skill.' },
    @{ Name='ponytail'; Repository='ponytail'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Minimal, YAGNI-oriented implementation and review skills.' },
    @{ Name='ralph'; Repository='ralph'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Ralph PRD and autonomous iteration skills.' },
    @{ Name='scientific-writing-skills'; Repository='scientific-writing-skills'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Scientific manuscript, proposal, outreach, and publishing skills.' },
    @{ Name='skillspector'; Repository='skillspector'; Mappings=@(@{Source='skills\skill-inspector';Destination='skills\skill-inspector'}); Description='Security inspection for agent skills. Test fixtures are excluded.' },
    @{ Name='strix'; Repository='strix'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Application security and authorized penetration-testing skills.' },
    @{ Name='styleseed'; Repository='styleseed'; Mappings=@(@{Source='skills';Destination='skills'},@{Source='extensions\learning\skills';Destination='skills'}); Description='Visual style exploration and learning workflows.' },
    @{ Name='superpowers'; Repository='superpowers'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Planning, debugging, testing, review, and execution skills.' },
    @{ Name='taste-skill'; Repository='taste-skill'; Mappings=@(@{Source='skills';Destination='skills'}); Description='High-craft visual and interface design workflow.' },
    @{ Name='terraform-skill'; Repository='terraform-skill'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Terraform workflow skill.' },
    @{ Name='ui-craft'; Repository='ui-craft'; Mappings=@(@{Source='skills';Destination='skills'}); Description='UI design and implementation skills with focused visual directions.' },
    @{ Name='ui-ux-pro-max'; Repository='ui-ux-pro-max'; Mappings=@(@{Source='.claude\skills';Destination='skills'}); Description='UI/UX design intelligence and supporting design skills.' },
    @{ Name='unslop'; Repository='unslop'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Polish AI-looking interfaces into intentional product design.' },
    @{ Name='vercel-agent-skills'; Repository='vercel-agent-skills'; Mappings=@(@{Source='skills';Destination='skills'}); Description='Vercel, React, Next.js, performance, and deployment skills.' }
)

$localPlugins = @()
try {
    $localPlugins += Stage-AntigravityRuntime
    foreach ($definition in $definitions) {
        $localPlugins += Stage-GitPlugin $definition $scratch
    }
    $localPlugins += Stage-LemoPlugin $scratch
} finally {
    if ((Test-Path -LiteralPath $scratch) -and $scratch.StartsWith([IO.Path]::GetTempPath(), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Marketplace $localPlugins
$agentCount = Convert-AntigravityAgents

$globalGuidance = @'
<!-- ANTIGRAVITY-TO-CODEX -->
# Global Codex collaboration guidance

- Use subagents for non-trivial work when exploration, implementation, tests, documentation, or review can proceed independently.
- Keep the primary agent responsible for requirements, decisions, integration, and the final result. Give each subagent a bounded task and ask for a concise evidence-backed summary.
- Avoid parallel edits to the same file or tightly coupled area. Prefer one writer per area, then run an independent verification pass when the change is risky or multi-file.
- Custom specialists live in `~/.codex/agents`. Choose a narrow specialist when its description matches; otherwise use the built-in explorer or worker.
- Inherit the active Codex model, permissions, tools, and MCP servers unless a task explicitly calls for a different supported setting. Antigravity-specific model IDs, `native_swarm`, and legacy `Task()` syntax are not Codex APIs.
- Finish implementation work by running the relevant tests, lint, type checks, or targeted runtime verification. Report anything that could not be verified.
'@
Add-GuidanceBlock $GlobalAgents $globalGuidance

$projectGuidance = @'
<!-- ANTIGRAVITY-TO-CODEX -->
# Codex project guidance

- This repository contains the reusable Antigravity-to-Codex installer at `scripts/setup-codex-from-antigravity.ps1`.
- Preserve existing Antigravity files unless the user explicitly asks to remove them. `.mcp.json` is retained for Antigravity; Codex MCP servers are configured globally.
- Use the Ralph files under `scripts/ralph/` only when the user requests the Ralph loop or supplies a PRD.
- For implementation work, delegate independent exploration and verification where useful, keep edits non-overlapping, and run checks appropriate to the files changed.
'@
Add-GuidanceBlock $ProjectAgents $projectGuidance

Ensure-AgentsConfig
Install-Plugins $localPlugins
Ensure-McpServers

$summary = [ordered]@{
    marketplace = $MarketplaceRoot
    local_plugins = $localPlugins.Count
    local_skills = ($localPlugins | Measure-Object -Property Skills -Sum).Sum
    custom_agents = $agentCount
    remote_connectors_requested = if ($SkipRemoteConnectors) { 0 } else { 7 }
    mcp_setup_requested = -not $SkipMcp
}
$summary | ConvertTo-Json -Depth 4
