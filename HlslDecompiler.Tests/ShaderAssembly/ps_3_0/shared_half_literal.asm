ps_3_0
def c1, 2, -1, -2, 0
dcl_texcoord v0.xy
dcl_2d s0
texld_pp r0, v0.xy, s0
mad_pp r0.xyz, r0.xyz, c1.xxx, c1.yyz
mad_pp r0.xyz, r0.www, r0.xyz, -c1.wwy
dp3 oC0.xyz, r0.xyz, c0.xyz
mov_pp oC0.w, -c1.y
