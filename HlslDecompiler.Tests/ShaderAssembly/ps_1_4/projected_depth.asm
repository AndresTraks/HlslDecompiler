ps_1_4
def c0, 0.5, 0.25, 2, 0
texcrd r5.xy, t0_dw
texcrd r2.xyz, t3.xyw
mul r5.xy, r5.xy, c0.xy
add r5.y, r5.y, c0.z
phase
texld r1, t1_dz
texkill r1
texdepth r5
mad r0.xyz, r2.xyz, c0.zzz, r1.xyz
+ mov r0.w, r1.w
