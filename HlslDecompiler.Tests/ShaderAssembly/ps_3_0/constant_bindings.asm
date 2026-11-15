ps_3_0
dcl_texcoord v0.xy
dcl_texcoord1 v1.xyz
dcl_2d s0
dcl_2d s3
dp3 r0.x, v1.xyz, c1.xyz
dp3 r0.y, v1.xyz, c2.xyz
dp3 r0.z, v1.xyz, c3.xyz
dp3_sat r0.x, r0.xyz, c0.xyz
mul r0.x, r0.x, c0.w
mov r1.xy, c4.xy
mad r0.yw, v0.xy, r1.xy, c5.xy
texld r1, r0.yw, s0
mul r1, r1, c6
mul r1.xyz, r0.xxx, r1.xyz
texld r2, v0.xy, s3
lrp r3, r2.x, c7, r1
mad oC0, c8, r0.z, r3
