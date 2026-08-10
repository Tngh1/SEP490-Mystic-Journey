import re, sys

path = sys.argv[1]
txt = open(path, encoding='utf-8', errors='replace').read()
docs = re.split(r'\n--- !u!', txt)
objs = {}
for d in docs[1:]:
    m = re.match(r'(\d+) &(\d+)', d)
    if not m: continue
    objs[m.group(2)] = (m.group(1), d)

go_name = {}
for fid, (cid, body) in objs.items():
    if cid == '1':
        nm = re.search(r'^  m_Name: (.*)$', body, re.M)
        go_name[fid] = nm.group(1).strip() if nm else '?'

for fid, (cid, body) in objs.items():
    if cid != '114': continue
    g = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
    owner = go_name.get(g.group(1), '?') if g else '?'
    t = re.search(r'^  m_text: (.*)$', body, re.M)
    if t:
        sz = re.search(r'm_fontSize: ([\d.]+)', body)
        print(f'[{owner}] text={t.group(1)!r} fontSize={sz.group(1) if sz else "?"}')
    # image sprite + fill
    sp = re.search(r'm_Sprite: \{fileID: (-?\d+), guid: ([0-9a-f]+)', body)
    ft = re.search(r'm_FillAmount: ([\d.]+)', body)
    ty = re.search(r'm_Type: (\d+)', body)
    if sp and ft:
        print(f'[{owner}] IMG sprite_guid={sp.group(2)[:8]} type={ty.group(1) if ty else "?"} fill={ft.group(1)}')
