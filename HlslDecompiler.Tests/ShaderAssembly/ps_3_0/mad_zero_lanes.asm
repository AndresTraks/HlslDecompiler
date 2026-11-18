ps_3_0
def c2, 2, -1, -2, 0
dcl_texcoord v0.xy
dcl_2d s0
texld_pp r0, v0.xy, s0
mad r0.xyz, r0.xyz, c2.xxx, c2.yyz
mov r1.yw, c2.yw
mad_pp r0.xyz, c0.xxx, r0.xyz, -r1.wwy
dp3 oC0.xyz, r0.xyz, c1.xyz
mov oC0.w, -c2.y
