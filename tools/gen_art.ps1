# 코덱스 CLI 로 그림 시트 뽑기: PROMPT.md 의 코드 블록을 골라 이어붙여 codex exec 에 넘김
# 사용: powershell -File tools/gen_art.ps1 <요청 폴더> <저장 이름> <블록 제목들...>
#   블록 제목 = PROMPT.md 의 "## 제목" (첫 단어). 첫 블록 앞에 COMMON 이 있으면 자동으로 붙임
param([string]$dir, [string]$out, [string[]]$blocks)
$md = Get-Content (Join-Path $dir 'PROMPT.md') -Raw -Encoding UTF8
$sections = @{}
foreach ($m in [regex]::Matches($md, '(?ms)^## (\S+)[^\n]*\n.*?```\r?\n(.*?)```')) { $sections[$m.Groups[1].Value] = $m.Groups[2].Value.Trim() }
$text = ''
if ($sections.ContainsKey('COMMON')) { $text = $sections['COMMON'] + "`n" }
foreach ($b in $blocks) { $text += $sections[$b] + "`n" }
if ($sections.Count -eq 0) { $text = [regex]::Match($md, '(?ms)```\r?\n(.*?)```').Groups[1].Value }
$refs = Get-ChildItem $dir -Filter 'ref_*' | ForEach-Object { '-i'; $_.FullName }
$target = Join-Path (Resolve-Path $dir) $out
$prompt = "Use your built-in image generation tool to create exactly ONE image from the prompt below, using the attached reference images for style. Save the final PNG to: $target`n`nPROMPT:`n$text"
$log = Join-Path $dir ("codex_" + [IO.Path]::GetFileNameWithoutExtension($out) + ".txt")
$prompt | codex exec --skip-git-repo-check -s workspace-write -C $dir @refs *> $log
if (Test-Path $target) { "OK $target" } else { "FAIL $out (log: $log)" }
