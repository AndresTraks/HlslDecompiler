ps_3_0
def c2, 1, 0, 0, 0
dcl_texcoord1_pp v0.xyz
dcl_texcoord2_pp v1.xyz
dp3_pp r0.x, v0.xyz, c0.xyz
mad_pp oC0.xyz, v1.xyz, r0.xxx, c1.xyz
mov oC0.w, c2.x
