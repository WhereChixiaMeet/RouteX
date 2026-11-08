$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$vendor=Join-Path $repo 'vendor/webview2'
$sdk=Join-Path $vendor 'sdk'
$required=@('lib/net462/Microsoft.Web.WebView2.Core.dll','lib/net462/Microsoft.Web.WebView2.WinForms.dll','runtimes/win-x64/native/WebView2Loader.dll','runtimes/win-x86/native/WebView2Loader.dll','LICENSE.txt','NOTICE.txt')
if(@($required | Where-Object { -not(Test-Path -LiteralPath (Join-Path $sdk $_)) }).Count -gt 0){
 New-Item -ItemType Directory -Force -Path $vendor | Out-Null
 $archive=Join-Path $vendor 'sdk.zip'
 if(-not(Test-Path -LiteralPath $archive)){
  [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
  Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.3537.50/microsoft.web.webview2.1.0.3537.50.nupkg' -OutFile $archive
 }
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '5EA526BBD728ADDA0DA4D31219267E96460494A427E4894C4E09D9F320F4B9AA'){throw 'WebView2 SDK package checksum mismatch'}
 Expand-Archive -LiteralPath $archive -DestinationPath $sdk -Force
}
$web=Join-Path $PSScriptRoot 'WebUi'
function ReadWeb([string]$name){return [IO.File]::ReadAllText((Join-Path $web $name)).Replace("`r`n","`n")}
$html=ReadWeb 'v5-approved.html'
$icons=ReadWeb 'icons.js'
$html=[regex]::new('(?s)(<script[^>]*>).*?(</script>)').Replace($html,{param($m)$m.Groups[1].Value+$icons+$m.Groups[2].Value},1)
$bridge=(ReadWeb 'desktop.js')+"`n"+(ReadWeb 'refinements.js')+"`n"+(ReadWeb 'routing-controls.js')+"`n"+(ReadWeb 'engine-layout.js')+"`n"+(ReadWeb 'shell-v5.js')
$marker="render();`n})();"
if(([regex]::Matches($html,[regex]::Escape($marker))).Count -ne 1){throw 'Interface entry point is missing or ambiguous'}
$html=$html.Replace($marker,$bridge+"`nrender();rpc(`"interface-ready`");`n})();")
$html=$html.Replace('<script>(() => {','<script>window.ROUTEX_HOST_INITIAL=ROUTEX_HOST_INITIAL_JSON;</script><script>(() => {')
$html=[regex]::Replace($html,'<title>RouteX.*?</title>','<title>RouteX</title>')
if(-not $html.Contains('window.ROUTEX_HOST_INITIAL=')){throw 'Initial state placeholder is missing'}
[IO.File]::WriteAllText((Join-Path $web 'interface.html'),$html,[Text.UTF8Encoding]::new($false))
Write-Output 'Prepared the production RouteX interface and pinned WebView2 SDK.'
