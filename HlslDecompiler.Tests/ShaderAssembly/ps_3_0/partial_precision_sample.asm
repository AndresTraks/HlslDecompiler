ps_3_0
dcl_texcoord v0.xy
dcl_2d s0
texld r0, v0.xy, s0
mul_pp r1, r0, c0.x
mul oC0, r0, r1
