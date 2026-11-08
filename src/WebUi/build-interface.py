from pathlib import Path
import re
root=Path(__file__).resolve().parents[2]
workspace=root.parents[1]
source=Path(__file__).parent/'v5-approved.html'
html=source.read_text(encoding='utf-8')
icon_resource=(Path(__file__).parent/'icons.js').read_text(encoding='utf-8')
html=re.sub(r'(<script[^>]*>).*?(</script>)',lambda m:m.group(1)+icon_resource+m.group(2),html,count=1,flags=re.S)
bridge=(Path(__file__).parent/'desktop.js').read_text(encoding='utf-8')
bridge+='\n'+(Path(__file__).parent/'refinements.js').read_text(encoding='utf-8')
bridge+='\n'+(Path(__file__).parent/'routing-controls.js').read_text(encoding='utf-8')
bridge+='\n'+(Path(__file__).parent/'engine-layout.js').read_text(encoding='utf-8')
bridge+='\n'+(Path(__file__).parent/'shell-v5.js').read_text(encoding='utf-8')
assert html.count('render();\n})();')==1
html=html.replace('render();\n})();',bridge+'\nrender();rpc("interface-ready");\n})();')
html=html.replace('<script>(() => {','<script>window.ROUTEX_HOST_INITIAL=ROUTEX_HOST_INITIAL_JSON;</script><script>(() => {',1)
assert 'window.ROUTEX_HOST_INITIAL=' in html
html=html.replace('<title>RouteX — UI prototype</title>','<title>RouteX</title>')
(Path(__file__).parent/'interface.html').write_text(html,encoding='utf-8')
print('Embedded the approved v5 interface with desktop bindings')
