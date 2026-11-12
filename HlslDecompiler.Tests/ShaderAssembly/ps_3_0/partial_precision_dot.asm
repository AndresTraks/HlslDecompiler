ps_3_0
dcl_texcoord v0.xy
dcl_2d s0
texld_pp r0, v0.xy, s0
dp3_pp r0.x, r0.xyz, c0.xyz
mul oC0, r0.x, c1
