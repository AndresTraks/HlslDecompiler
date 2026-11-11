ps_3_0
dcl_texcoord v0.zw
dcl_2d s0
texld_pp r0, v0.zw, s0
mul_pp r1, r0, c0
mul r0.xyz, r0.www, r1.xyz
mad oC0.xyz, r1.xyz, c1.xyz, r0.xyz
mul oC0.w, r1.w, c1.w
