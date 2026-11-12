ps_3_0
def c2, 0.5, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
texld_pp r0, v0.xy, s0
mul_pp r1, r0, c0
add r2.x, -r0.x, c2.x
mul r0, r0, c1
cmp oC0, r2.x, r0, r1
