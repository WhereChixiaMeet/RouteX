// Keep only the Lucide paths used by the desktop interface. Source is the approved
// offline prototype; its ISC notice is preserved in the generated resource.
const fs=require('fs'),vm=require('vm'),path=require('path');
const html=fs.readFileSync(path.join(__dirname,'v5-approved.html'),'utf8');
const scripts=[...html.matchAll(/<script[^>]*>([\s\S]*?)<\/script>/g)];
const context={};vm.runInNewContext(scripts[0][1],context);
const source=scripts.slice(1).map(s=>s[1]).join('\n')+fs.readFileSync(path.join(__dirname,'desktop.js'),'utf8')+fs.readFileSync(path.join(__dirname,'refinements.js'),'utf8')+fs.readFileSync(path.join(__dirname,'routing-controls.js'),'utf8')+fs.readFileSync(path.join(__dirname,'engine-layout.js'),'utf8');
const names=new Set([...source.matchAll(/['"`]([a-z][a-z0-9-]*)['"`]/g)].map(m=>m[1]));
const icons={};for(const key of names){const match=Object.entries(context.lucide.icons).find(([name])=>name.toLowerCase()===key.replaceAll('-',''));if(match)icons[key]=match[1];}
const renderer=`
window.lucide={createIcons(options={}){for(const target of document.querySelectorAll('i[data-lucide]')){
 const name=target.getAttribute('data-lucide'),paths=ROUTEX_ICONS[name];if(!paths)continue;
 const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');
 for(const[k,v]of Object.entries({width:24,height:24,viewBox:'0 0 24 24',fill:'none',stroke:'currentColor','stroke-width':2,'stroke-linecap':'round','stroke-linejoin':'round',...options.attrs}))svg.setAttribute(k,v);
 for(const attr of target.attributes)svg.setAttribute(attr.name,attr.value);
 svg.setAttribute('class',((target.getAttribute('class')||'')+' lucide lucide-'+name).trim());
 for(const[tag,attrs]of paths){const element=document.createElementNS('http://www.w3.org/2000/svg',tag);for(const[k,v]of Object.entries(attrs))element.setAttribute(k,v);svg.append(element);}target.replaceWith(svg);
}}};`;
fs.writeFileSync(path.join(__dirname,'icons.js'),'/* Lucide v1.8.0 — ISC. See LUCIDE-LICENSE.txt. */\nconst ROUTEX_ICONS='+JSON.stringify(icons)+';\n'+renderer);
console.log('Embedded '+Object.keys(icons).length+' used Lucide icons');
