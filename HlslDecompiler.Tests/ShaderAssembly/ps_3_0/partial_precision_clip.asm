ps_3_0
dcl_texcoord v0.xy
dcl_2d s0
texld_pp r0, v0.xy, s0
add r1, r0.w, -c0.x
mul oC0, r0, c1
texkill r1
