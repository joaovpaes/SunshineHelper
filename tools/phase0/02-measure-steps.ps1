<#
Teste 2: mede o tempo real de cada passo, sem os "timeout" fixos dos .bat.
Rode FORA de uma transmissao, em um PowerShell como administrador, na pasta das automacoes.
Muda resolucao, HDR e reinicia o RTSS 3 vezes e VOLTA tudo ao estado normal no fim.

  powershell -ExecutionPolicy Bypass -File .\02-measure-steps.ps1 -ToolsDir "C:\caminho\da\pasta\Automacoes"
#>
param(
  [Parameter(Mandatory)] [string] $ToolsDir,
  [int] $Runs = 3,
  [string] $RtssDir = "C:\Program Files (x86)\RivaTuner Statistics Server",
  [int] $StreamW = 3840, [int] $StreamH = 2160,
  [int] $NormalW = 2560, [int] $NormalH = 1440,
  [string] $RtssProfileStream = "Global_60",
  [string] $RtssProfileNormal = "Global_171"
)
$ErrorActionPreference = 'Stop'
$qres = Join-Path $ToolsDir 'QRes.exe'
$hdr  = Join-Path $ToolsDir 'HDRSwitch.exe'
foreach ($f in @($qres, $hdr)) { if (-not (Test-Path $f)) { throw "Nao encontrei $f" } }
$out = Join-Path $PSScriptRoot 'measure-steps.log'
function Log($m) { $l = "[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}" -f (Get-Date), $m; Write-Host $l; Add-Content -Path $out -Value $l }

function Get-Mode {
  $s = & $qres /S 2>&1 | Out-String
  if ($s -match '(\d{3,5})\s*x\s*(\d{3,5})') { return @([int]$Matches[1], [int]$Matches[2]) }
  return @(0, 0)
}
function Measure-Step($name, [scriptblock] $action, [scriptblock] $ready) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  & $action | Out-Null
  $call = $sw.ElapsedMilliseconds
  while (-not (& $ready) -and $sw.ElapsedMilliseconds -lt 30000) { Start-Sleep -Milliseconds 50 }
  $ok = (& $ready)
  Log ("{0}: chamada={1} ms, pronto={2} ms, ok={3}" -f $name, $call, $sw.ElapsedMilliseconds, $ok)
}
function Set-Rtss($profile) {
  $p = Join-Path $RtssDir 'Profiles'
  Copy-Item (Join-Path $p $profile) (Join-Path $p 'Global') -Force
  Get-Process RTSS -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Process (Join-Path $RtssDir 'RTSS.exe')
}

Log "=== INICIO medicao ($Runs rodadas) ==="
Log ("Modo atual: {0}x{1}" -f (Get-Mode)[0], (Get-Mode)[1])
try {
  for ($i = 1; $i -le $Runs; $i++) {
    Log "--- rodada $i ---"
    Measure-Step 'HDR ligar'      { & $hdr --hdr_on -m 0 }  { $true }
    Measure-Step "Resolucao ${StreamW}x${StreamH}" { & $qres /x $StreamW /y $StreamH } { $m = Get-Mode; $m[0] -eq $StreamW -and $m[1] -eq $StreamH }
    Measure-Step 'RTSS (limite stream)' { Set-Rtss $RtssProfileStream } { [bool](Get-Process RTSS -ErrorAction SilentlyContinue) }
    Start-Sleep -Seconds 2
    Measure-Step 'HDR desligar'   { & $hdr --hdr_off -m 0 } { $true }
    Measure-Step "Resolucao ${NormalW}x${NormalH}" { & $qres /x $NormalW /y $NormalH } { $m = Get-Mode; $m[0] -eq $NormalW -and $m[1] -eq $NormalH }
    Measure-Step 'RTSS (limite normal)' { Set-Rtss $RtssProfileNormal } { [bool](Get-Process RTSS -ErrorAction SilentlyContinue) }
    Start-Sleep -Seconds 2
  }
} finally {
  & $hdr --hdr_off -m 0 | Out-Null
  & $qres /x $NormalW /y $NormalH | Out-Null
  Set-Rtss $RtssProfileNormal
  Log "=== FIM: estado normal restaurado ==="
}
