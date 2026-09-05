ps_3_0
def c2, 1, -1, 0, 0
dcl vPos.xy
dcl vFace
cmp r0.x, vFace, c2.x, c2.y
mul r1, c0, vPos.x
mul r2, c1, vPos.y
cmp oC0, -r0.x, r2, r1
