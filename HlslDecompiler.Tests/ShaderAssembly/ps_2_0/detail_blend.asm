ps_2_0
def c1, 2, 0, 0, 0
dcl t0.xy
dcl_2d s0
dcl_2d s1
dcl_2d s2
mul r0.xy, t0.xy, c0.xx
texld r0, r0.xy, s1
texld r1, t0.xy, s0
texld r2, t0.xy, s2
mul r0.xyz, r0.xyz, r1.xyz
mad r0.xyz, r0.xyz, c1.xxx, -r1.xyz
mad r0.xyz, r2.xxx, r0.xyz, r1.xyz
add r0.w, -r2.x, c0.y
cmp r1.xyz, r0.www, r1.xyz, r0.xyz
mov oC0, r1
