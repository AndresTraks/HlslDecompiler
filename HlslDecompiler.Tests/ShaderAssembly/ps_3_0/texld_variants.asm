ps_3_0
def c2, 1, 0, 0, 0
dcl_texcoord v0
dcl_texcoord1 v1
dcl_2d s0
mul r0.xyz, c2.xxy, v0.xyx
mov r0.w, c1.x
texldb r0, r0, s0
texldp r1, v0, s0
mov r2.xy, v0.xy
texldd r2, r2.xy, s0, v1.xy, v1.zw
mad r0, r0, r2, r1
mul r1.xyz, c2.xxy, v0.xyx
mov r1.w, c0.x
texldl r1, r1, s0
add oC0, r0, -r1
