ps_3_0
def c1, 1, -1, 0, 0
dcl_texcoord v0.xy
dcl vFace
dcl_2d s0
cmp r0.x, vFace, c1.x, c1.y
if_lt -r0.x, c1.z
mul r0.xyz, c1.xxz, v0.xyx
mov r0.w, c0.x
texldl oC0, r0, s0
else
add r0.xy, v0.xy, v0.xy
dsx r0.zw, v0.xy
dsy r1.xy, v0.xy
texldd r0, r0.xy, s0, r0.zw, r1.xy
mov oC0, r0.zyxw
endif
