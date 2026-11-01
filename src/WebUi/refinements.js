// Review 3: source-specific pages, compact controls, and persistent custom colors.
const routingBeforeRefinement=routing;
routing=function(){const template=document.createElement('template');template.innerHTML=routingBeforeRefinement();const root=template.content;
 const source=root.querySelector('[data-combo="source"]')?.closest('.r-field'),backup=root.querySelector('.r-backup [data-combo="backupConfig"]');
 if(source&&backup){const holder=document.createElement('template');holder.innerHTML=field('Backup VPN',backup.outerHTML);source.replaceWith(holder.content.firstElementChild);}
 const enabled=root.querySelector('[data-check="backupEnabled"]');if(enabled){enabled.closest('.r-check').remove();const grid=root.querySelector('.rx-settings-grid');grid.insertAdjacentHTML('beforeend',check('Backup VPN','backupEnabled',state.backupEnabled));}
 root.querySelector('.rx-connection-foot')?.remove();return template.innerHTML;
};renderers.routing=routing;
const subscriptionsBeforeRefinement=subscriptions;
subscriptions=function(){const template=document.createElement('template');template.innerHTML=subscriptionsBeforeRefinement();const root=template.content;
 if(state.serverColumns!=='all'){root.querySelector('thead tr')?.lastElementChild?.remove();root.querySelectorAll('tbody tr[data-server-row]').forEach(row=>row.lastElementChild.remove());}
 root.querySelectorAll('tbody tr[data-server-row]').forEach(row=>{row.classList.remove('selected');row.classList.toggle('r-server-active',host.subscriptionActive&&row.dataset.serverRow===host.settings.ActiveServer);row.classList.toggle('r-server-focused',row.dataset.serverRow===state.selectedServer);});
 const core=root.querySelector('.r-core-label');if(core){core.remove();root.querySelector('.r-subs-toolbar').append(core);}
 return template.innerHTML;
};renderers.subscriptions=subscriptions;
app.addEventListener('click',event=>{const row=event.target.closest('[data-server-row]');if(row&&!event.target.closest('button')){event.stopImmediatePropagation();state.selectedServer=row.dataset.serverRow;render();}
 const reset=event.target.closest('[data-action="reset-palette"]');if(reset){event.stopImmediatePropagation();state.customPalette={...palettes.dark};state.theme='custom';render();saveDesktopSetting('resetPalette',true).catch(()=>{});}
},true);
const appearanceBeforeRefinement=appearance;
appearance=function(){const template=document.createElement('template');template.innerHTML=appearanceBeforeRefinement();for(const panel of template.content.querySelectorAll('section.r-panel')){const title=panel.querySelector('h3')?.textContent;if(title==='Text')panel.remove();if(title==='Custom palette')panel.querySelector('.r-toolbar').insertAdjacentHTML('beforeend',button('Reset palette','reset-palette','rotate-ccw'));}return template.innerHTML;};
const windowBeforeRefinement=windowSettings;
windowSettings=function(){const template=document.createElement('template');template.innerHTML=windowBeforeRefinement();const panel=[...template.content.querySelectorAll('section.r-panel')].find(e=>e.querySelector('h3')?.textContent==='Sidebar indicators');if(panel){panel.querySelector('.r-checks').outerHTML=Object.keys(state.visible).map(key=>`<div class="r-setting"><strong>${metricNames[key]}</strong>${check('','visible.'+key,state.visible[key])}</div>`).join('');}return template.innerHTML;};
const engineBeforeRefinement=engineSettings;
engineSettings=function(){const template=document.createElement('template');template.innerHTML=engineBeforeRefinement();const sources=[...template.content.querySelectorAll('section.r-panel')].find(e=>e.querySelector('h3')?.textContent==='Sources');if(sources)sources.insertAdjacentHTML('beforeend',checkedRow('Speed monitor','speedOn'));return template.innerHTML;};
const logsBeforeRefinement=logsPage;
logsPage=function(){const template=document.createElement('template');template.innerHTML=logsBeforeRefinement();const panel=[...template.content.querySelectorAll('section.r-panel')].find(e=>e.querySelector('h3')?.textContent==='Polling frequency');if(panel)panel.innerHTML=`<h3>Polling frequency</h3><div class="r-poll-control"><label for="poll-frequency">VPN polling rate</label><output data-poll-output>${Number(state.pollRate).toFixed(1)} polls/s</output><input id="poll-frequency" type="range" min="0.1" max="0.7" step="0.1" value="${Math.min(.7,state.pollRate)}" data-setting="pollRate" aria-label="VPN polls per second"></div><p class="r-small">Maximum 0.7 polls/s per channel. Direct uses its own interval.</p>`;return template.innerHTML;};renderers.logs=logsPage;
app.addEventListener('input',event=>{const input=event.target;if(input.dataset.setting==='pollRate'){event.stopImmediatePropagation();state.pollRate=Math.max(.1,Math.min(.7,Number(input.value)));app.querySelectorAll('[data-poll-output]').forEach(e=>e.textContent=state.pollRate.toFixed(1)+' polls/s');}
 if(input.dataset.palette){paletteEditUntil=Date.now()+1200;event.stopImmediatePropagation();state.customPalette[input.dataset.palette]=input.value;state.theme='custom';applyTheme();app.querySelectorAll('[data-theme-pick]').forEach(e=>e.setAttribute('aria-pressed',String(e.dataset.themePick==='custom')));}
},true);
const refinementStyle=document.createElement('style');refinementStyle.textContent=`
.r-subs-toolbar>.r-core-label{margin-left:12px;white-space:nowrap}.r-server-focused{outline:1px solid var(--rx-line);outline-offset:-1px}.r-server-active{background:color-mix(in srgb,var(--rx-accent) 14%,var(--rx-panel));box-shadow:inset 2px 0 var(--rx-accent)}
.r-poll-control{display:grid;grid-template-columns:1fr auto;gap:18px;align-items:center;margin:22px 0}.r-poll-control input{grid-column:1/-1;width:100%;accent-color:var(--rx-accent);cursor:pointer}.r-poll-control output{font-variant-numeric:tabular-nums;color:var(--rx-text)}
@keyframes rx-interface-enter{from{opacity:0;transform:translateY(7px)}to{opacity:1;transform:translateY(0)}}
.rx-window.rx-enter{animation:rx-interface-enter .28s ease-out both}@media(prefers-reduced-motion:reduce){.rx-window.rx-enter{animation:none}}
`;document.head.append(refinementStyle);
const appSettingsLayout=windowSettings;
windowSettings=function(){const template=document.createElement('template');template.innerHTML=appSettingsLayout();const startup=document.createElement('section');startup.className='r-panel';startup.dataset.appStartup='';startup.innerHTML='<h3>App startup</h3>';
 for(const key of ['autorun','startMin']){const row=template.content.querySelector(`[data-check="${key}"]`)?.closest('.r-setting');if(row){row.querySelector('strong').textContent=key==='autorun'?'Launch RouteX when Windows starts':'Start minimized to tray';startup.append(row);}}
 startup.insertAdjacentHTML('beforeend','<p class="r-small r-muted">The interface preloads in the tray during Windows startup.</p>');template.content.prepend(startup);const widget=template.content.querySelector('[data-check="widget"]')?.closest('.r-setting')?.querySelector('strong');if(widget)widget.textContent='Desktop widget';return template.innerHTML;};
const labeledSettingsPage=settingsPage;
settingsPage=function(){const template=document.createElement('template');template.innerHTML=labeledSettingsPage();const tab=template.content.querySelector('[data-settings-tab="window"]');if(tab)tab.textContent='App';template.content.querySelectorAll('[data-check="speedOn"]').forEach(input=>{const label=input.closest('.r-setting')?.querySelector('strong');if(label)label.textContent='Speed monitoring';});return template.innerHTML;};renderers.settings=settingsPage;
const startupOverlay=document.createElement('div');startupOverlay.id='rx-startup-overlay';startupOverlay.innerHTML=`<div class="rx-loading-mark"><img src="${logo}" alt=""><strong>RouteX</strong><span>Loading</span><i></i></div>`;document.body.append(startupOverlay);
const startupStyle=document.createElement('style');startupStyle.textContent=`
#rx-startup-overlay{position:fixed;inset:0;z-index:100000;background:var(--rx-bg,#0d0f12);display:grid;place-items:center;transition:opacity .38s ease,visibility .38s;pointer-events:auto;color:var(--rx-text,#e6edf5);font-family:'Segoe UI',sans-serif}
.rx-loading-mark{display:flex;flex-direction:column;align-items:center;gap:12px;transform:translateY(-5px);transition:transform .38s cubic-bezier(.2,.8,.2,1),opacity .3s ease}
.rx-loading-mark img{width:56px;height:56px;margin-bottom:10px}.rx-loading-mark strong{font-size:25px;font-weight:600}.rx-loading-mark span{font-size:12px;color:var(--rx-muted,#8c99ac)}
.rx-loading-mark i{width:20px;height:20px;margin-top:20px;border:3px solid color-mix(in srgb,var(--rx-accent,#4a86ff) 15%,transparent);border-top-color:var(--rx-accent,#4a86ff);border-radius:50%;animation:rx-load-spin 1.1s linear infinite}
@keyframes rx-load-spin{to{transform:rotate(360deg)}}
.rx-revealed #rx-startup-overlay{opacity:0;visibility:hidden;pointer-events:none}.rx-revealed .rx-loading-mark{opacity:0;transform:translateY(-15px) scale(.98)}
.rx-window.rx-enter{animation:rx-interface-enter .42s cubic-bezier(.2,.8,.2,1) both}
@media(prefers-reduced-motion:reduce){#rx-startup-overlay,.rx-loading-mark{transition:none}.rx-loading-mark i,.rx-window.rx-enter{animation:none}}
`;document.head.append(startupStyle);
let revealed=false,welcomeOffered=false;
window.routexPrepareReveal=async function(background=false){
 const pending=[...document.images].map(image=>image.decode?.().catch(()=>{}));if(document.fonts)pending.push(document.fonts.ready);
 if(state.background){const image=new Image();image.src=state.background;pending.push(image.decode().catch(()=>{}));}
 await Promise.race([Promise.all(pending),new Promise(resolve=>setTimeout(resolve,2000))]);
 if(!background)await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
 if(queuedSnapshot){const snapshot=queuedSnapshot;queuedSnapshot=null;applyingSnapshot=true;absorb(snapshot);structuralSnapshot=snapshotStructure(snapshot);render();applyingSnapshot=false;if(!background)await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));}
 window.chrome.webview.postMessage({command:'reveal-ready'});
};
window.routexBeginReveal=function(background=false){if(revealed)return;revealed=true;if(background){startupOverlay.remove();window.routexStartupSettled=true;return;}const colors=getComputedStyle(app);for(const key of ['--rx-bg','--rx-text','--rx-muted','--rx-accent'])startupOverlay.style.setProperty(key,colors.getPropertyValue(key));
 requestAnimationFrame(()=>requestAnimationFrame(()=>{const reduced=matchMedia('(prefers-reduced-motion: reduce)').matches;
 const options={duration:reduced?450:1100,easing:'cubic-bezier(.4,0,.2,1)',fill:'both'};
 window.routexRevealStartedAt=performance.now();window.routexRevealDuration=options.duration;
 const fade=startupOverlay.animate([{opacity:1},{opacity:0}],options);
 fade.finished.then(()=>{startupOverlay.remove();window.routexStartupSettled=true;flushSnapshot();setTimeout(offerReleaseReview,700);});}));
};
function offerReleaseReview(){if(welcomeOffered||!host.whatsNew)return;welcomeOffered=true;showReleaseReview();}
window.routexActivate=function(){if(window.routexStartupSettled)offerReleaseReview();};
const widgetWithoutClose=renderWidget;
renderWidget=function(){widgetWithoutClose();app.querySelectorAll('.r-widget [data-action="hide-widget"]').forEach(button=>button.remove());};
const releaseRender=render;
render=function(reset=false){releaseRender(reset);app.querySelectorAll('[data-action="show-widget"]').forEach(button=>button.remove());};
const finalPolish=document.createElement('style');finalPolish.textContent=`
#rx-startup-overlay{will-change:opacity;transition:none}.rx-loading-mark[hidden]{display:none}.rx-window.rx-enter{animation:none!important}
#routex-routing-concept dialog.r-release-dialog{width:min(590px,calc(100vw - 40px));border-radius:14px;overflow:hidden}
#routex-routing-concept dialog.r-release-dialog::backdrop{backdrop-filter:none;background:#0008}
@keyframes rx-welcome-fade{from{opacity:0}to{opacity:1}}dialog.r-release-dialog[open],dialog.r-release-dialog[open]::backdrop{animation:rx-welcome-fade .4s ease-in-out both}
#routex-routing-concept dialog.r-release-dialog .r-dialoghead{border:0;padding:10px 16px 0;justify-content:flex-end}.r-release-dialog .r-dialoghead h3{position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%)}
#routex-routing-concept dialog.r-release-dialog .r-dialogbody{padding:0 32px 30px}.r-release-brand{display:flex;align-items:center;justify-content:center;gap:17px;margin:5px 0 23px}.r-release-brand img{width:66px;height:66px}.r-release-brand div{display:grid;gap:2px}.r-release-brand span{font-size:13px;color:var(--rx-muted)}.r-release-brand strong{font-size:44px;line-height:1;font-weight:600;letter-spacing:-1.5px}
.r-release-hero{text-align:center}.r-release-hero h2{font-size:25px;font-weight:600;margin:0 0 12px;letter-spacing:-.5px}.r-release-hero p{font-size:13px;line-height:1.6;color:var(--rx-muted);margin:0 auto 27px;max-width:450px}
.r-release-grid{display:grid;grid-template-columns:1fr 1fr;gap:23px 24px}.r-release-item{display:grid;grid-template-columns:22px 1fr;align-items:start;gap:10px}.r-release-item>svg{width:19px;height:19px;color:var(--rx-accent);margin-top:1px}.r-release-item strong{font-size:12px;font-weight:600;display:block;margin-bottom:6px}.r-release-item p{font-size:11px;line-height:1.65;color:var(--rx-muted);margin:0}
.r-release-dialog .r-dialogfoot{padding:16px 32px;border-top:1px solid var(--rx-line)}.r-release-dialog .r-dialogfoot .primary{min-width:120px}
@media(max-width:480px){.r-release-grid{grid-template-columns:1fr}.r-release-dialog .r-dialogbody{padding:0 20px 22px}}
@media(prefers-reduced-motion:reduce){dialog.r-release-dialog[open],dialog.r-release-dialog[open]::backdrop{animation:none}}
`;document.head.append(finalPolish);
function makeWelcomeNonSubmitting(dialog){
 const form=dialog.querySelector('form'),panel=document.createElement('div');panel.className='r-welcome-content';while(form.firstChild)panel.append(form.firstChild);form.replaceWith(panel);
 const done=dialog.querySelector('.r-dialogfoot .primary');done.type='button';done.dataset.dismissWelcome='';done.onclick=event=>{event.preventDefault();event.stopPropagation();dialog.close();};done.focus({preventScroll:true});
}
function showReleaseReview(){if(!host.whatsNew)return;openModal('Welcome to RouteX '+host.version,`<div class="r-release-hero"><div class="r-release-brand"><img src="${logo}" alt="RouteX"><div><span>RouteX</span><strong>${esc(host.version)}</strong></div></div><h2>Welcome to RouteX ${esc(host.version)}</h2><p>A complete interface redesign, from Routing and Subscriptions to Settings and monitoring.</p></div><div class="r-release-grid"><div class="r-release-item">${ico('layout-dashboard')}<div><strong>Rebuilt interface</strong><p>Reorganized pages and consistent controls across the app.</p></div></div><div class="r-release-item">${ico('sliders-horizontal')}<div><strong>App & widgets</strong><p>Startup options, sidebar indicators, and widget settings in one place.</p></div></div><div class="r-release-item">${ico('palette')}<div><strong>Custom themes</strong><p>Custom colors stay saved when switching between themes.</p></div></div><div class="r-release-item">${ico('activity')}<div><strong>Controlled monitoring</strong><p>Polling is limited to 0.7 requests per second per channel.</p></div></div></div>`,()=>{},'Continue');const dialog=app.querySelector('dialog');makeWelcomeNonSubmitting(dialog);dialog.classList.add('r-release-dialog');dialog.setAttribute('aria-label','Welcome to RouteX '+host.version);dialog.querySelector('.r-dialogfoot [data-action="close-dialog"]')?.remove();dialog.addEventListener('close',()=>dialog.classList.remove('r-release-dialog'),{once:true});dialog.querySelector('button[type="submit"]')?.focus({preventScroll:true});hostCall('whats-new-shown').catch(()=>{});}
