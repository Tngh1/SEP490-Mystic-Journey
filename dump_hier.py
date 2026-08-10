import re, sys, collections

path = sys.argv[1]
root_name = sys.argv[2] if len(sys.argv) > 2 else None

txt = open(path, encoding='utf-8', errors='replace').read()
# split into documents
docs = re.split(r'\n--- !u!', txt)
objs = {}   # fileID -> (classid, body)
for d in docs[1:]:
    m = re.match(r'(\d+) &(\d+)', d)
    if not m: continue
    classid, fid = m.group(1), m.group(2)
    objs[fid] = (classid, d)

go_name = {}
go_transform = {}
tr_children = {}
tr_parent = {}
tr_go = {}
go_components = collections.defaultdict(list)
mono_script = {}
go_active = {}

for fid, (cid, body) in objs.items():
    if cid == '1':  # GameObject
        nm = re.search(r'^  m_Name: (.*)$', body, re.M)
        go_name[fid] = nm.group(1).strip() if nm else '?'
        act = re.search(r'^  m_IsActive: (\d)', body, re.M)
        go_active[fid] = act.group(1) if act else '1'
        for c in re.finditer(r'component: \{fileID: (\d+)\}', body):
            go_components[fid].append(c.group(1))
    elif cid in ('4', '224'):  # Transform / RectTransform
        g = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
        if g:
            tr_go[fid] = g.group(1)
            go_transform[g.group(1)] = fid
        p = re.search(r'm_Father: \{fileID: (\d+)\}', body)
        tr_parent[fid] = p.group(1) if p else '0'
        kids = re.findall(r'- \{fileID: (\d+)\}', body.split('m_Children:')[1].split('m_Father:')[0]) if 'm_Children:' in body else []
        tr_children[fid] = kids
    elif cid == '114':  # MonoBehaviour
        s = re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]+)', body)
        mono_script[fid] = s.group(1) if s else '?'

# map guid -> script name
import os, glob
guid_name = {}
for meta in glob.glob('Assets/Scripts/**/*.cs.meta', recursive=True) + glob.glob('Assets/**/*.cs.meta', recursive=True):
    t = open(meta, encoding='utf-8', errors='replace').read()
    m = re.search(r'guid: ([0-9a-f]+)', t)
    if m: guid_name[m.group(1)] = os.path.basename(meta)[:-8]
# unity builtin guids
guid_name.setdefault('fe87c0e1cc204ed48ad3b37840f39efc', 'Image')
guid_name.setdefault('f4688fdb7df04437aeb418b961361dc5', 'TextMeshProUGUI')
guid_name.setdefault('4e29b1a8efbd4b44bb3f3716e73f07ff', 'Button')

CLASS = {'1':'GameObject','4':'Transform','224':'RectTransform','222':'CanvasRenderer','225':'CanvasGroup',
         '114':'MonoBehaviour','223':'Canvas','1953259897':'?'}

def comps(gid):
    out = []
    for c in go_components[gid]:
        if c not in objs: continue
        cid = objs[c][0]
        if cid in ('4','224','222'): continue
        if cid == '114':
            out.append(guid_name.get(mono_script.get(c,''), 'Mono:'+mono_script.get(c,'')[:8]))
        else:
            out.append(CLASS.get(cid, 'cls'+cid))
    return out

def dump(tr, depth=0, maxd=30):
    if depth > maxd: return
    gid = tr_go.get(tr)
    if gid is None: return
    cs = comps(gid)
    act = '' if go_active.get(gid) == '1' else ' [INACTIVE]'
    print('  '*depth + f'{go_name.get(gid,"?")}{act}  <{",".join(cs)}>  #{tr}')
    for k in tr_children.get(tr, []):
        dump(k, depth+1, maxd)

if root_name:
    for gid, nm in go_name.items():
        if nm == root_name:
            dump(go_transform[gid])
            print('===')
else:
    # roots
    for tr, p in tr_parent.items():
        if p == '0':
            dump(tr)
