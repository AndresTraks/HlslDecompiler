ps_3_0
def c1, 1, 0, 0, 0
dcl_texcoord v0
dcl_texcoord1 v1.xy
dcl_2d s0
dsx r0.xy, v1.xy
dsy r0.zw, v1.xy
mul r0, r0, c0.yyzz
texldd r0, v1.xy, s0, r0.xy, r0.zw
mul r1.xyz, c1.xxy, v1.xyx
mov r1.w, c0.x
texldb r1, r1, s0
texldp r2, v0, s0
mad r1, r2, c0.w, r1
add oC0, r0, r1
