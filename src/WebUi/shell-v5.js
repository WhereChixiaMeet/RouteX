// RouteX 5.0 shell: the window has no system caption any more; the interface draws its own title bar (back / forward,
// "RouteX" in the middle, minimise / maximise / close) and fills the whole window (no frame inside the frame).
// Also: compact settings and sidebar, Monitor-style connection info on Routing, the log in the accent color,
// neutral grey buttons on dark themes, no "No servers found" row.
const shellStyle=document.createElement('style');shellStyle.textContent=`
html,body{margin:0!important;padding:0!important;height:100%;overflow:hidden}
#routex-routing-concept .rx-window{height:100vh!important;min-height:0!important;border:0!important;border-radius:0!important;box-shadow:none!important}
#routex-routing-concept .rx-titlebar{flex:none;height:32px;display:flex;align-items:stretch;position:relative;background:var(--rx-side);border-bottom:1px solid var(--rx-line);user-select:none;-webkit-user-select:none;z-index:20}
#routex-routing-concept .rx-tb-nav{display:flex;align-items:center;gap:2px;padding-left:8px}
#routex-routing-concept .rx-tb-btn{width:30px;height:24px;border:0;border-radius:5px;background:transparent;color:var(--rx-muted);display:inline-flex;align-items:center;justify-content:center;padding:0;cursor:default}
#routex-routing-concept .rx-tb-btn:hover:not(:disabled){background:var(--rx-btn-hover,var(--rx-hover));color:var(--rx-text)}
#routex-routing-concept .rx-tb-btn:disabled{opacity:.32}
#routex-routing-concept .rx-tb-title{position:absolute;left:50%;top:0;height:100%;transform:translateX(-50%);display:flex;align-items:center;font-size:12px;font-weight:600;letter-spacing:.03em;color:var(--rx-text);pointer-events:none}
#routex-routing-concept .rx-tb-win{margin-left:auto;display:flex}
#routex-routing-concept .rx-tb-ctl{width:46px;height:100%;border:0;border-radius:0;background:transparent;color:var(--rx-text);display:inline-flex;align-items:center;justify-content:center;padding:0;cursor:default}
#routex-routing-concept .rx-tb-ctl:hover{background:var(--rx-btn-hover,var(--rx-hover))}
#routex-routing-concept .rx-tb-close:hover{background:#e81123;color:#fff}
#routex-routing-concept .rx-tb-ctl svg{width:10px;height:10px;flex:none}
#routex-routing-concept .rx-tb-btn svg{width:16px;height:16px;flex:none}
#routex-routing-concept .rx-shell{flex:1;min-height:0}

#routex-routing-concept .rx-nav-gap{display:none}

#routex-routing-concept .rx-window[data-page=settings] .r-maincontent{gap:10px}
#routex-routing-concept .rx-window[data-page=settings] .r-panel{padding:14px 18px}
#routex-routing-concept .rx-window[data-page=settings] .r-panel h3{margin-bottom:8px}
#routex-routing-concept .rx-window[data-page=settings] .r-setting{padding:6px 0;justify-content:flex-start;gap:16px}
#routex-routing-concept .rx-window[data-page=settings] .r-setting>:first-child{flex:0 0 220px}
#routex-routing-concept .rx-window[data-page=settings] .r-setting>input:not([type=checkbox]),#routex-routing-concept .rx-window[data-page=settings] .r-setting>.r-combo{width:min(340px,100%);flex:0 1 340px}
#routex-routing-concept .rx-window[data-page=settings] .r-setting input:not([type=checkbox]):not([type=range]),#routex-routing-concept .rx-window[data-page=settings] .r-setting .r-combo-trigger{min-height:32px;padding-top:6px;padding-bottom:6px}
#routex-routing-concept .rx-window[data-page=settings] .r-setting>label.r-check{margin-left:0}
#routex-routing-concept .rx-window[data-page=settings] .r-toolbar{margin:6px 0 2px}
#routex-routing-concept .rx-window[data-page=settings] .r-font-row{padding:6px 0}
#routex-routing-concept .rx-window[data-page=settings] .r-font-row .r-combo-trigger,#routex-routing-concept .rx-window[data-page=settings] .r-font-row input{min-height:32px;padding-top:6px;padding-bottom:6px}
#routex-routing-concept .rx-window[data-page=settings] .r-panelhead{margin-bottom:4px}
#routex-routing-concept .rx-window[data-page=settings] .r-settings-tabs{margin-bottom:0}

#routex-routing-concept .rx-connection .rx-route-top{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:16px;align-items:start}
#routex-routing-concept .rx-connection .rx-route-left{min-width:0}
#routex-routing-concept .rx-connection .rx-tag,#routex-routing-concept .rx-connection .r-connection-details{display:none}
#routex-routing-concept .rx-route-chips{display:flex;flex-wrap:nowrap;gap:5px;margin-top:9px;overflow:hidden;-webkit-mask-image:linear-gradient(90deg,#000 calc(100% - 24px),transparent);mask-image:linear-gradient(90deg,#000 calc(100% - 24px),transparent)}
#routex-routing-concept .rx-chip{flex:none}
#routex-routing-concept .rx-chip{font-size:calc(11px * var(--rx-font-scale,1));line-height:18px;color:var(--rx-muted);background:var(--rx-btn,var(--rx-soft));border:1px solid var(--rx-btn-line,var(--rx-line));border-radius:999px;padding:0 8px;white-space:nowrap;max-width:100%;overflow:hidden;text-overflow:ellipsis}
#routex-routing-concept .rx-chip:first-child{color:var(--rx-text)}
#routex-routing-concept .rx-route-tiles{display:grid;grid-template-columns:repeat(2,160px);gap:10px}
#routex-routing-concept .rx-connection .rx-route-top{margin-bottom:var(--rx-route-gap,8px)}
#routex-routing-concept .rx-tile{background:var(--rx-field);border:1px solid var(--rx-line);border-radius:8px;padding:10px 12px 11px;display:grid;gap:3px;min-width:0}
#routex-routing-concept .rx-tile-head{display:flex;align-items:center;gap:6px;font-size:calc(11px * var(--rx-font-scale,1));color:var(--rx-muted)}
#routex-routing-concept .rx-tile-head .rx-state-dot{width:7px;height:7px;flex:none}
#routex-routing-concept .rx-spark{margin-left:auto;width:52px;height:16px;overflow:visible}
#routex-routing-concept .rx-spark polyline{fill:none;stroke-width:1.4;stroke-linejoin:round;stroke-linecap:round}
#routex-routing-concept .rx-spark.vpn polyline{stroke:var(--rx-vpn)}
#routex-routing-concept .rx-spark.direct polyline{stroke:var(--rx-direct)}
#routex-routing-concept .rx-tile-value{display:flex;align-items:baseline;gap:4px;line-height:1.15}
#routex-routing-concept .rx-tile-value strong{font-size:calc(24px * var(--rx-font-scale,1));font-weight:400;color:var(--rx-text)}
#routex-routing-concept .rx-tile-value small{font-size:calc(11px * var(--rx-font-scale,1));color:var(--rx-muted)}
#routex-routing-concept .rx-tile-ip{font-size:calc(10.5px * var(--rx-font-scale,1));color:var(--rx-muted);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
@container (max-width:720px){#routex-routing-concept .rx-route-tiles{grid-template-columns:repeat(2,116px);gap:8px}#routex-routing-concept .rx-tile{padding:9px 10px 10px}#routex-routing-concept .rx-spark{width:36px}}
@container (max-width:500px){#routex-routing-concept .rx-connection .rx-route-top{grid-template-columns:1fr}#routex-routing-concept .rx-route-tiles{grid-template-columns:repeat(2,minmax(0,1fr))}}

#routex-routing-concept .r-log{background:color-mix(in srgb,var(--rx-accent) 5%,var(--rx-field));border-color:color-mix(in srgb,var(--rx-accent) 45%,var(--rx-line));scrollbar-color:color-mix(in srgb,var(--rx-accent) 55%,transparent) transparent}
#routex-routing-concept .r-logrow>span:first-child{color:color-mix(in srgb,var(--rx-accent) 65%,var(--rx-muted))}
#routex-routing-concept .r-logrow.route strong{color:var(--rx-accent)}

#routex-routing-concept .rx-window[data-tone=dark]{--rx-btn:color-mix(in srgb,var(--rx-soft) 25%,#262626);--rx-btn-hover:color-mix(in srgb,var(--rx-hover) 25%,#313131);--rx-btn-line:color-mix(in srgb,var(--rx-line) 25%,#383838)}
#routex-routing-concept .rx-window[data-tone=dark] .r-button:not(.primary){background:var(--rx-btn);border-color:var(--rx-btn-line)}
#routex-routing-concept .rx-window[data-tone=dark] .r-button:not(.primary):hover{background:var(--rx-btn-hover)}
#routex-routing-concept .rx-window[data-tone=dark] .rx-power:not([aria-checked=true]){background:var(--rx-btn);border-color:var(--rx-btn-line)}
#routex-routing-concept .rx-window[data-tone=dark] .r-tab[aria-selected=true],#routex-routing-concept .rx-window[data-tone=dark] .rx-navitem.rx-active,#routex-routing-concept .rx-window[data-tone=dark] .rx-navitem:hover{background:var(--rx-btn)}
#routex-routing-concept .rx-window[data-tone=dark] .r-iconbutton:hover{background:var(--rx-btn-hover)}
`;document.head.append(shellStyle);

const shellSvg={
 back:'<svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10 3.5 5.5 8l4.5 4.5"/></svg>',
 forward:'<svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 3.5 10.5 8 6 12.5"/></svg>',
 min:'<svg viewBox="0 0 10 10" width="10" height="10" aria-hidden="true"><path d="M0 5.5h10" stroke="currentColor" stroke-width="1"/></svg>',
 max:'<svg viewBox="0 0 10 10" width="10" height="10" fill="none" stroke="currentColor" stroke-width="1" aria-hidden="true"><rect x=".5" y=".5" width="9" height="9"/></svg>',
 restore:'<svg viewBox="0 0 10 10" width="10" height="10" fill="none" stroke="currentColor" stroke-width="1" aria-hidden="true"><rect x=".5" y="2.5" width="7" height="7"/><path d="M2.5 2.5V.5h7v7h-2"/></svg>',
 close:'<svg viewBox="0 0 10 10" width="10" height="10" stroke="currentColor" stroke-width="1" aria-hidden="true"><path d="M.5.5l9 9M9.5.5l-9 9"/></svg>'
};
function shellWindow(op){try{window.chrome.webview.postMessage({command:'window',args:{op}});}catch{}}

// back / forward between pages (buttons, Alt+← / Alt+→, the mouse's back / forward buttons)
let shellHistory=[],shellIndex=-1;
function shellTrack(){const page=state.page;if(!page||shellHistory[shellIndex]===page)return;shellHistory=shellHistory.slice(0,shellIndex+1);shellHistory.push(page);if(shellHistory.length>50)shellHistory.shift();shellIndex=shellHistory.length-1;}
function shellGo(step){const i=shellIndex+step;if(i<0||i>=shellHistory.length)return;shellIndex=i;const target=app.querySelector(`[data-nav="${shellHistory[i]}"]`);if(target)target.click();shellTitlebar();}
document.addEventListener('keydown',e=>{if(e.altKey&&!e.ctrlKey&&!e.shiftKey&&(e.key==='ArrowLeft'||e.key==='ArrowRight')){e.preventDefault();shellGo(e.key==='ArrowLeft'?-1:1);}},true);
window.addEventListener('mouseup',e=>{if(e.button===3||e.button===4){e.preventDefault();shellGo(e.button===3?-1:1);}},true);

function shellTitlebar(){
 let bar=app.querySelector(':scope>.rx-titlebar');
 if(!bar){
  bar=document.createElement('div');bar.className='rx-titlebar';
  bar.innerHTML=`<div class="rx-tb-nav"><button type="button" class="rx-tb-btn" data-tb="back" aria-label="Back" title="Back (Alt+←)">${shellSvg.back}</button><button type="button" class="rx-tb-btn" data-tb="forward" aria-label="Forward" title="Forward (Alt+→)">${shellSvg.forward}</button></div><div class="rx-tb-title">RouteX</div><div class="rx-tb-win"><button type="button" class="rx-tb-ctl" data-tb="min" aria-label="Minimize" title="Minimize">${shellSvg.min}</button><button type="button" class="rx-tb-ctl" data-tb="max" aria-label="Maximize" title="Maximize">${shellSvg.max}</button><button type="button" class="rx-tb-ctl rx-tb-close" data-tb="close" aria-label="Close" title="Close">${shellSvg.close}</button></div>`;
  app.prepend(bar);
  // drag the window by the empty part of the bar: started on the first move, so a double click still maximises
  let press=null;
  bar.addEventListener('mousedown',e=>{press=e.button===0&&!e.target.closest('button')?{x:e.screenX,y:e.screenY}:null;if(press)e.preventDefault();});
  bar.addEventListener('mousemove',e=>{if(press&&(e.buttons&1)&&Math.abs(e.screenX-press.x)+Math.abs(e.screenY-press.y)>3){press=null;shellWindow('drag');}});
  bar.addEventListener('mouseup',()=>{press=null;});
  bar.addEventListener('dblclick',e=>{if(!e.target.closest('button'))shellWindow('max');});
  bar.addEventListener('click',e=>{const b=e.target.closest('[data-tb]');if(!b)return;e.stopPropagation();const op=b.dataset.tb;if(op==='back')shellGo(-1);else if(op==='forward')shellGo(1);else shellWindow(op);});
 }
 const maximized=!!(host&&host.maximized),mb=bar.querySelector('[data-tb=max]');
 if(mb.dataset.state!==String(maximized)){mb.dataset.state=String(maximized);mb.innerHTML=maximized?shellSvg.restore:shellSvg.max;mb.title=maximized?'Restore':'Maximize';mb.setAttribute('aria-label',mb.title);}
 bar.querySelector('[data-tb=back]').disabled=shellIndex<=0;
 bar.querySelector('[data-tb=forward]').disabled=shellIndex>=shellHistory.length-1;
}

// dark or light by the actual background color, so a dark custom palette gets the grey buttons too
function shellTone(){
 const v=getComputedStyle(app).getPropertyValue('--rx-bg').trim();let r=14,g=16,b=20,m;
 if((m=v.match(/^#([0-9a-f]{6})$/i))){const n=parseInt(m[1],16);r=n>>16;g=n>>8&255;b=n&255;}
 else if((m=v.match(/^#([0-9a-f]{3})$/i))){r=parseInt(m[1][0]+m[1][0],16);g=parseInt(m[1][1]+m[1][1],16);b=parseInt(m[1][2]+m[1][2],16);}
 else if((m=v.match(/rgba?\((\d+)[ ,]+(\d+)[ ,]+(\d+)/))){r=+m[1];g=+m[2];b=+m[3];}
 app.dataset.tone=r*.299+g*.587+b*.114<128?'dark':'light';
}
const applyThemeBeforeShell=applyTheme;applyTheme=function(...a){const result=applyThemeBeforeShell.apply(this,a);try{shellTone();}catch{}return result;};

// Routing: the connection info as on the Monitor page — status, chips, VPN / Direct tiles with a sparkline
const shellLevelColor=['var(--rx-muted)','var(--rx-muted)','var(--rx-green)','#eab308','#f97316','#ef4444'];
function shellSpark(values,cls){
 const v=(values||[]).filter(x=>Number.isFinite(x)&&x>=0).slice(-40);
 if(v.length<2)return `<svg class="rx-spark ${cls}" viewBox="0 0 64 18"></svg>`;
 const hi=Math.max(...v),lo=Math.min(...v),range=Math.max(hi-lo,20,hi*.5),min=lo-(range-(hi-lo))/2;
 return `<svg class="rx-spark ${cls}" viewBox="0 0 64 18" preserveAspectRatio="none"><polyline points="${v.map((y,i)=>`${(i*64/(v.length-1)).toFixed(1)},${(16.5-(y-min)/range*15).toFixed(1)}`).join(' ')}" vector-effect="non-scaling-stroke"/></svg>`;
}
function shellTile(key,label){
 const c=(host&&host.channels&&host.channels[key])||{},value=String(c.value??'—'),ip=(String(c.line1||'').match(/IP\s+([^\s·]+)/)||[])[1]||'';
 return `<div class="rx-tile"><div class="rx-tile-head"><i class="rx-state-dot" style="background:${shellLevelColor[c.level|0]||'var(--rx-muted)'}"></i><span>${label}</span>${shellSpark(host&&host.chart&&host.chart[key],key)}</div><div class="rx-tile-value"><strong>${esc(value)}</strong><small>${value==='—'?'':esc(c.unit||'')}</small></div><small class="rx-tile-ip">${ip?'IP '+esc(ip):'&nbsp;'}</small></div>`;
}
function shellChips(){
 const parts=String(host&&host.routeDetail||'').split(' · ').concat(String(host&&host.routeDetail2||'').split(' · ').filter(x=>!/\d\s*ms\b/i.test(x)));
 return parts.map(s=>s.trim()).filter(s=>s&&!/^winws\s*[—-]?$/i.test(s)).map(s=>`<span class="rx-chip" title="${esc(s)}">${esc(s)}</span>`).join('');
}
function shellRouting(){
 if(state.page!=='routing')return;
 const card=app.querySelector('.rx-connection');if(!card)return;
 let top=card.querySelector(':scope>.rx-route-top');
 if(!top){
  const stateRow=card.querySelector(':scope>.rx-state-row'),details=card.querySelector(':scope>.r-connection-details');if(!stateRow)return;
  top=document.createElement('div');top.className='rx-route-top';
  const left=document.createElement('div');left.className='rx-route-left';left.append(stateRow);
  const chips=document.createElement('div');chips.className='rx-route-chips';left.append(chips);if(details)left.append(details);
  const tiles=document.createElement('div');tiles.className='rx-route-tiles';
  top.append(left,tiles);card.prepend(top);
 }
 top.querySelector('.rx-route-chips').innerHTML=shellChips();
 top.querySelector('.rx-route-tiles').innerHTML=shellTile('vpn','VPN')+shellTile('direct','Direct');
}

// Subscriptions: an empty list stays empty, without a "No servers found" row
function shellEmptyServers(){
 if(state.page!=='subscriptions')return;
 app.querySelectorAll('table tbody').forEach(body=>{const rows=body.rows;if(rows.length===1&&rows[0].cells.length===1&&rows[0].cells[0].colSpan>=5&&!rows[0].hasAttribute('data-server-row'))rows[0].remove();});
}

function shellAfterRender(){shellTrack();shellTitlebar();app.dataset.page=state.page||'';shellTone();shellRouting();shellEmptyServers();}
const renderBeforeShell=render;render=function(...a){const result=renderBeforeShell.apply(this,a);try{shellAfterRender();}catch(e){window.ROUTEX_UI_ERROR=String(e&&e.message||e);}return result;};
const refreshBeforeShell=refreshMeasurements;refreshMeasurements=function(...a){const result=refreshBeforeShell.apply(this,a);try{shellTitlebar();shellRouting();}catch(e){window.ROUTEX_UI_ERROR=String(e&&e.message||e);}return result;};
