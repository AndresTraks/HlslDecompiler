ps_3_0
def c1, 1, 0, 0, 0
dcl_texcoord v0.xyz
mov_sat r0.xyz, v0.xyz
mul_pp r0.xyz, r0.xyz, c0.xxx
mad_pp r1.xyz, r0.xyz, c0.yyy, r0.xyz
mul oC0.xyz, r0.xyz, r1.xyz
mov oC0.w, c1.x
