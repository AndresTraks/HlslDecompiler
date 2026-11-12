ps_3_0
def c1, 0, 0, 1, 0
defi i0, 4, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
mov r0, c1.y
mov r1.x, c1.y
rep i0
mad r1.yz, r1.xx, c0.xy, v0.xy
texld r2, r1.yz, s0
mad_pp r0, r2, c0.z, r0
add r1.x, r1.x, c1.z
endrep
mov oC0, r0
