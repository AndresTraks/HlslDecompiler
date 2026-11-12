ps_3_0
def c1, 2, -1, 0, 1
dcl_texcoord v0.xy
dcl_texcoord1 v1.xyz
dcl_texcoord2 v2.xyz
dcl_texcoord3 v3.xyz
dcl_2d s0
texld r0, v0.xy, s0
mad r0.xyz, r0.xyz, c1.xxx, c1.yyy
dp3 r1.x, v1.xyz, c0.xyz
dp3 r1.y, v2.xyz, c0.xyz
dp3 r1.z, v3.xyz, c0.xyz
dp3 r0.x, r0.xyz, r1.xyz
mul oC0.x, r0.x, c0.w
mov oC0.yzw, c1.zzw
