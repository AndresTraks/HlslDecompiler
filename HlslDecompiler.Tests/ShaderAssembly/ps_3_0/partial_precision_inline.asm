ps_3_0
dcl_texcoord v0.zw
dcl_2d s0
texld_pp r0, v0.zw, s0
mul_pp r1, r0, c0
mad oC0.xyz, r1.xyz, c1.xyz, r0.xyz
mov_pp oC0.w, r1.w
