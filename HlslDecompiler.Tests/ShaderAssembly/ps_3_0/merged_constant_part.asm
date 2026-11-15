ps_3_0
dcl_texcoord v0.xy
dcl_texcoord1 v1.xyz
dcl_2d s0
texld r0, v0.xy, s0
mul oC0.xyz, r0.xxx, v1.xyz
mul oC0.w, r0.x, c0.w
