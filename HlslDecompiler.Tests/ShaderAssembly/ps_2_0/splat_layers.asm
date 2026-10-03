ps_2_0
def c4, 1, 0, 0, 0
dcl t0.xy
dcl t1.xyz
dcl t2.xyz
dcl t3.x
dcl_2d s0
dcl_2d s1
dcl_2d s2
dcl_2d s3
mul r0.xy, t0.xy, c2.xy
texld r1, r0.xy, s1
texld r2, r0.xy, s2
texld r0, r0.xy, s3
texld r3, t0.xy, s0
mul r2.xyz, r2.xyz, r3.yyy
mad r1.xyz, r1.xyz, r3.xxx, r2.xyz
mad r0.xyz, r0.xyz, r3.zzz, r1.xyz
nrm r1.xyz, t2.xyz
nrm r2.xyz, t1.xyz
dp3_sat r0.w, r2.xyz, r1.xyz
dp3_sat r1.x, r2.xyz, -c1.xyz
add r0.w, -r0.w, c4.x
pow r1.y, r0.w, c3.x
mad r0.xyz, r0.xyz, r1.xxx, r1.yyy
lrp r1.xyz, t3.xxx, r0.xyz, c0.xyz
mov r1.w, c4.x
mov oC0, r1
