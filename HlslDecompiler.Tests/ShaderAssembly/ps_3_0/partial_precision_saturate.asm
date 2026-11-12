ps_3_0
dcl_texcoord v0.xy
dcl_2d s0
texld r0, v0.xy, s0
mov r1, c0
mad_sat_pp r0, r0, r1, c1
mul oC0, r0, c2
