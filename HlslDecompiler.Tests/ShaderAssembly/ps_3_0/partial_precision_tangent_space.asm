ps_3_0
def c0, 2, -1, 0, 1
dcl_texcoord v0.xy
dcl_texcoord1 v1.xyz
dcl_texcoord2 v2.xyz
dcl_texcoord3 v3.xyz
dcl_2d s0
dcl_2d s1
texld r0, v0.xy, s0
mad_pp r0.xyz, r0.xyz, c0.xxx, c0.yyy
texld r1, v0.xy, s1
dp3_pp r2.x, v1.xyz, r1.xyz
dp3_pp r2.y, v2.xyz, r1.xyz
dp3_pp r2.z, v3.xyz, r1.xyz
dp3 oC0.x, r0.xyz, r2.xyz
mov oC0.yzw, c0.zzw
