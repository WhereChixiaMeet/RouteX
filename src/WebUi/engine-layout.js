// Engine sources and concise connection labels; routing keys stay unchanged.
// The host already has a native title bar; retain one brand in the sidebar.
app.querySelector('.rx-chrome')?.remove();
const nativeChromeStyle=document.createElement('style');nativeChromeStyle.textContent='#routex-routing-concept .rx-window::before{inset:0}';document.head.append(nativeChromeStyle);
const powerBeforeTunnel=power;
power=function(name,on){const template=document.createElement('template');template.innerHTML=powerBeforeTunnel(name,on);if(name==='vpn'){const control=template.content.firstElementChild;control.setAttribute('aria-label','Tunnel');control.querySelector('.rx-powertext').innerHTML=`Tunnel <small>${on?'ON':'OFF'}</small>`;}return template.innerHTML;};
const configsNavigation=nav.find(item=>item&&item[0]==='configs');if(configsNavigation)configsNavigation[2]='Tunnel Configs';
descriptions.routing='Apps: Include, Strict, Exclude';
const engineWithSources=engineSettings;
engineSettings=function(){const template=document.createElement('template');template.innerHTML=engineWithSources();const root=template.content;
 const sources=[...root.querySelectorAll('section.r-panel')].find(panel=>panel.querySelector('h3')?.textContent==='Sources');if(sources)root.prepend(sources);
 root.querySelectorAll('h3').forEach(title=>{if(/^VPN.*WireSock/i.test(title.textContent))title.textContent='Wiresock';else if(/^DPI.*zapret/i.test(title.textContent))title.textContent='DPI';else if(/^Routing.*Configs/i.test(title.textContent))title.textContent='VLESS / VMess / Trojan';});
 root.querySelector('[data-action="demo-service"]')?.closest('.r-setting')?.remove();
 const folder=root.querySelector('[data-setting="zapretDir"]')?.closest('.r-setting');if(folder){folder.classList.add('r-dpi-source-setting');folder.innerHTML=`<strong>DPI Source folder</strong><div class="r-dpi-source-control"><input type="text" data-setting="zapretDir" value="${esc(host.paths.dpi)}" aria-label="DPI Source folder"><div class="r-toolbar">${button('Browse…','pick-dpi-folder','folder-open')}${button('Default','default-dpi-folder','rotate-ccw')}</div><small class="r-small r-muted">Updates are installed in this folder.</small></div>`;}
 return template.innerHTML;
};
const routingWithLabels=routing;
routing=function(){const template=document.createElement('template');template.innerHTML=routingWithLabels();const root=template.content;
 const backup=root.querySelector('[data-combo="backupConfig"]')?.closest('.r-field')?.querySelector(':scope>span');if(backup)backup.textContent='Backup config';
 const primary=root.querySelector('[data-combo="config"]')?.closest('.r-field')?.querySelector(':scope>span');if(primary)primary.textContent='Primary config';
 const enabled=root.querySelector('[data-check="backupEnabled"]')?.closest('.r-check');if(enabled)for(const node of enabled.childNodes)if(node.nodeType===Node.TEXT_NODE)node.textContent='Backup config';
 root.querySelector('.rx-connection-sub')?.remove();const tag=root.querySelector('.rx-tag'),selected=configs.find(config=>config.id===(host.activeConfig||host.settings.PrimaryTunnel));if(tag)tag.textContent=(selected?.protocol||'Config').toUpperCase();
 const lock=root.querySelector('.r-lockhint');if(lock&&host.locked)lock.innerHTML=ico('lock-keyhole')+'Disconnect to edit';
 const strict=root.querySelector('[data-list="strict"] h3');if(strict)strict.textContent='Strict';
 return template.innerHTML;
};renderers.routing=routing;
const engineStyle=document.createElement('style');engineStyle.textContent=`
.r-dpi-source-setting{align-items:start!important;gap:24px}.r-dpi-source-setting>strong{padding-top:12px}.r-dpi-source-control{width:min(65%,620px);min-width:0;display:grid;gap:9px}.r-dpi-source-control input{width:100%!important;box-sizing:border-box}.r-dpi-source-control>.r-toolbar{margin:0;justify-content:flex-start}
.rx-main{container-type:inline-size}@container(max-width:1120px){.r-subs-toolbar>.r-toolbar{margin-left:0!important;justify-content:flex-start;flex:0 1 auto}.r-subs-toolbar>.r-spacer{display:none}.r-subs-toolbar>.r-core-label{margin-left:auto!important}}
@media(max-width:820px){.r-dpi-source-setting{flex-wrap:wrap}.r-dpi-source-control{width:100%}}
`;document.head.append(engineStyle);

// Per-server actions target the row under the pointer, independently of prior selection.
function openServerMenu(row,x,y){
 closeEntryMenu();const id=row.dataset.serverRow,server=servers.find(entry=>entry.id===id);if(!server)return;
 state.selectedServer=id;app.querySelectorAll('[data-server-row]').forEach(entry=>entry.classList.toggle('r-server-focused',entry.dataset.serverRow===id));
 const menu=document.createElement('div');menu.className='r-entry-menu r-server-menu';menu.setAttribute('role','menu');menu.setAttribute('aria-label','Server actions');menu.dataset.serverId=id;
 for(const[label,action]of [['Connect','connect-selected'],['Test latency','test-server'],['Test speed','speed-server'],[server.favorite?'Remove favorite':'Add favorite','favorite-selected'],['Copy link','copy-server'],['Reset traffic','reset-server-traffic'],['Delete','delete-server']]){
  const control=document.createElement('button');control.type='button';control.textContent=label;control.dataset.serverAction=action;control.setAttribute('role','menuitem');control.disabled=action==='connect-selected'&&!!host.busy;
  control.onclick=event=>{event.stopPropagation();closeEntryMenu();if(action==='delete-server'){openModal('Delete server',`<p>Delete <strong>${esc(server.name)}</strong>?</p>`,()=>hostCall('action',{name:action,id}).catch(()=>{}),'Delete');return;}hostCall('action',{name:action,id}).catch(()=>{});};menu.append(control);
 }
 placeContextMenu(menu,x,y);
}
app.addEventListener('contextmenu',event=>{const row=event.target.closest('[data-server-row]');if(!row)return;event.preventDefault();event.stopImmediatePropagation();openServerMenu(row,event.clientX,event.clientY);},true);
