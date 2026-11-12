ps_3_0
def c3, 1, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
texld_pp r0, v0.xy, s0
dp3 oC0.x, r0.xyz, c0.xyz
dp3 oC0.y, r0.xyz, c1.xyz
dp3 oC0.z, r0.xyz, c2.xyz
mov oC0.w, c3.x
