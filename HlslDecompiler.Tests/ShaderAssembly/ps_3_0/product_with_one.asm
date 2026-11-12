ps_3_0
def c2, 1, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
mov r0.w, c2.x
texld_pp r1, v0.xy, s0
rsq_pp r0.y, r1.x
rcp_pp r0.x, r1.x
mul r0.z, r0.x, c1.x
mul oC0, r0, c0
