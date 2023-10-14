ps_3_0
def c0, 1, -1, 0, 0.300000012
def c1, -123456, 0, 0, 0
dcl vFace
dcl vPos.xy
cmp oC0.w, vFace, c0.x, c0.y
mad oC0.xyz, vPos.xxy, c0.zxx, c0.wzz
mov oDepth, c1.x
